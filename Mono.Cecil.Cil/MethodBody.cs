//
// Author:
//   Jb Evain (jbevain@gmail.com)
//
// Copyright (c) 2008 - 2015 Jb Evain
// Copyright (c) 2008 - 2011 Novell, Inc.
//
// Licensed under the MIT/X11 license.
//

using System;
using System.Threading;

using Mono.Collections.Generic;

namespace Mono.Cecil.Cil {

	public sealed class MethodBody {

		readonly internal MethodDefinition method;

		internal ParameterDefinition this_parameter;
		internal int max_stack_size;
		internal int code_size;
		internal bool init_locals;
		internal MetadataToken local_var_token;

		internal Collection<Instruction> instructions;
		internal Collection<ExceptionHandler> exceptions;
		internal Collection<VariableDefinition> variables;

		public MethodDefinition Method {
			get { return method; }
		}

		public int MaxStackSize {
			get { return max_stack_size; }
			set { max_stack_size = value; }
		}

		public int CodeSize {
			get { return code_size; }
		}

		public bool InitLocals {
			get { return init_locals; }
			set { init_locals = value; }
		}

		public MetadataToken LocalVarToken {
			get { return local_var_token; }
			set { local_var_token = value; }
		}

		public Collection<Instruction> Instructions {
			get {
				if (instructions == null)
					Interlocked.CompareExchange (ref instructions, new InstructionCollection (method), null);

				return instructions;
			}
		}

		public bool HasExceptionHandlers {
			get { return !exceptions.IsNullOrEmpty (); }
		}

		public Collection<ExceptionHandler> ExceptionHandlers {
			get {
				if (exceptions == null)
					Interlocked.CompareExchange (ref exceptions, new Collection<ExceptionHandler> (), null);

				return exceptions;
			}
		}

		public bool HasVariables {
			get { return !variables.IsNullOrEmpty (); }
		}

		public Collection<VariableDefinition> Variables {
			get {
				if (variables == null)
					Interlocked.CompareExchange (ref variables, new VariableDefinitionCollection (this.method), null);

				return variables;
			}
		}

		public ParameterDefinition ThisParameter {
			get {
				if (method == null || method.DeclaringType == null)
					throw new NotSupportedException ();

				if (!method.HasThis)
					return null;

				if (this_parameter == null)
					Interlocked.CompareExchange (ref this_parameter, CreateThisParameter (method), null);

				return this_parameter;
			}
		}

		static ParameterDefinition CreateThisParameter (MethodDefinition method)
		{
			var parameter_type = method.DeclaringType as TypeReference;

			if (parameter_type.HasGenericParameters) {
				var instance = new GenericInstanceType (parameter_type, parameter_type.GenericParameters.Count);
				for (int i = 0; i < parameter_type.GenericParameters.Count; i++)
					instance.GenericArguments.Add (parameter_type.GenericParameters [i]);

				parameter_type = instance;

			}

			if (parameter_type.IsValueType || parameter_type.IsPrimitive)
				parameter_type = new ByReferenceType (parameter_type);

			return new ParameterDefinition (parameter_type, method);
		}

		public MethodBody (MethodDefinition method)
		{
			this.method = method;
		}

		public ILProcessor GetILProcessor ()
		{
			return new ILProcessor (this);
		}
	}

	sealed class VariableDefinitionCollection : Collection<VariableDefinition> {

		readonly MethodDefinition method;

		internal VariableDefinitionCollection (MethodDefinition method)
		{
			this.method = method;
		}

		internal VariableDefinitionCollection (MethodDefinition method, int capacity)
			: base (capacity)
		{
			this.method = method;
		}

		protected override void OnAdd (VariableDefinition item, int index)
		{
			item.index = index;
		}

		protected override void OnInsert (VariableDefinition item, int index)
		{
			item.index = index;
			UpdateVariableIndices (index, 1);
		}

		protected override void OnSet (VariableDefinition item, int index)
		{
			item.index = index;
		}

		protected override void OnRemove (VariableDefinition item, int index)
		{
			UpdateVariableIndices (index + 1, -1, item);
			item.index = -1;
		}

		void UpdateVariableIndices (int startIndex, int offset, VariableDefinition variableToRemove = null)
		{
			for (int i = startIndex; i < size; i++)
				items [i].index = i + offset;

			var debug_info = method == null ? null : method.debug_info;
			if (debug_info == null || debug_info.Scope == null)
				return;

			foreach (var scope in debug_info.GetScopes ()) {
				if (!scope.HasVariables)
					continue;

				var variables = scope.Variables;
				int variableDebugInfoIndexToRemove = -1;
				for (int i = 0; i < variables.Count; i++) {
					var variable = variables [i];

					// If a variable is being removed detect if it has debug info counterpart, if so remove that as well.
					// Note that the debug info can be either resolved (has direct reference to the VariableDefinition)
					// or unresolved (has only the number index of the variable) - this needs to handle both cases.
					if (variableToRemove != null &&
						((variable.index.IsResolved && variable.index.ResolvedVariable == variableToRemove) ||
							(!variable.index.IsResolved && variable.Index == variableToRemove.Index))) {
						variableDebugInfoIndexToRemove = i;
						continue;
					}

					// For unresolved debug info updates indeces to keep them pointing to the same variable.
					if (!variable.index.IsResolved && variable.Index >= startIndex) {
						variable.index = new VariableIndex (variable.Index + offset);
					}
				}

				if (variableDebugInfoIndexToRemove >= 0)
					variables.RemoveAt (variableDebugInfoIndexToRemove);
			}
		}
	}

	class InstructionCollection : Collection<Instruction> {

		readonly MethodDefinition method;

		internal InstructionCollection (MethodDefinition method)
		{
			this.method = method;
		}

		internal InstructionCollection (MethodDefinition method, int capacity)
			: base (capacity)
		{
			this.method = method;
		}

		protected override void OnAdd (Instruction item, int index)
		{
			if (index == 0)
				return;

			var previous = items [index - 1];
			previous.next = item;
			item.previous = previous;
		}

		protected override void OnInsert (Instruction item, int index)
		{
			if (size == 0)
				return;

			var current = items [index];
			if (current == null) {
				var last = items [index - 1];
				last.next = item;
				item.previous = last;
				return;
			}

			var previous = current.previous;
			if (previous != null) {
				previous.next = item;
				item.previous = previous;
			}

			current.previous = item;
			item.next = current;
		}

		protected override void OnSet (Instruction item, int index)
		{
			var current = items [index];

			item.previous = current.previous;
			item.next = current.next;

			current.previous = null;
			current.next = null;
		}

		protected override void OnRemove (Instruction item, int index)
		{
			var previous = item.previous;
			if (previous != null)
				previous.next = item.next;

			var next = item.next;
			if (next != null)
				next.previous = item.previous;

			RemoveSequencePoint (item);

			item.previous = null;
			item.next = null;
		}

		void RemoveSequencePoint (Instruction instruction)
		{
			var debug_info = method.debug_info;
			if (debug_info == null || !debug_info.HasSequencePoints)
				return;

			var sequence_points = debug_info.sequence_points;
			for (int i = 0; i < sequence_points.Count; i++) {
				if (sequence_points [i].Offset == instruction.offset) {
					sequence_points.RemoveAt (i);
					return;
				}
			}
		}
	}
}

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
			int startOffset = 0;
			if (size != 0) {
				var current = items [index];
				if (current == null) {
					var last = items [index - 1];
					last.next = item;
					item.previous = last;
					return;
				}

				startOffset = current.Offset;

				var previous = current.previous;
				if (previous != null) {
					previous.next = item;
					item.previous = previous;
				}

				current.previous = item;
				item.next = current;
			}

			UpdateDebugInformation (null, null);
		}

		protected override void OnSet (Instruction item, int index)
		{
			var current = items [index];

			item.previous = current.previous;
			item.next = current.next;

			current.previous = null;
			current.next = null;

			UpdateDebugInformation (item, current);
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
			UpdateDebugInformation (item, next ?? previous);

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

		void UpdateDebugInformation (Instruction removedInstruction, Instruction existingInstruction)
		{
			// Various bits of debug information store instruction offsets (as "pointers" to the IL)
			// Instruction offset can be either resolved, in which case it
			// has a reference to Instruction, or unresolved in which case it stores numerical offset (instruction offset in the body).
			// Depending on where the InstructionOffset comes from (loaded from PE/PDB or constructed) it can be in either state.
			// Each instruction has its own offset, which is populated on load, but never updated (this would be pretty expensive to do).
			// Instructions created during the editting will typically have offset 0 (so incorrect).
			// Manipulating unresolved InstructionOffsets is pretty hard (since we can't rely on correct offsets of instructions).
			// On the other hand resolved InstructionOffsets are easy to maintain, since they point to instructions and thus inserting
			// instructions is basically a no-op and removing instructions is as easy as changing the pointer.
			// For this reason the algorithm here is:
			//  - First make sure that all instruction offsets are resolved - if not - resolve them
			//     - First time this will be relatively expensive as it will walk the entire method body to convert offsets to instruction pointers
			//       Within the same debug info, IL offsets are typically stored in the "right" order (sequentially per start offsets),
			//       so the code uses a simple one-item cache instruction<->offset to avoid walking instructions multiple times
			//       (that would only happen for scopes which are out of order).
			//     - Subsequent calls should be cheap as it will only walk all local scopes without doing anything (as it checks that they're resolved)
			//     - If there was an edit which adds some unresolved, the cost is proportional (the code will only resolve those)
			//  - Then update as necessary by manipulaitng instruction references alone

			InstructionOffsetResolver resolver = new InstructionOffsetResolver (items, removedInstruction, existingInstruction);

			if (method.debug_info != null)
				UpdateLocalScope (method.debug_info.Scope, ref resolver);

			var custom_debug_infos = method.custom_infos ?? method.debug_info?.custom_infos;
			if (custom_debug_infos != null) {
				foreach (var custom_debug_info in custom_debug_infos) {
					switch (custom_debug_info) {
					case StateMachineScopeDebugInformation state_machine_scope:
						UpdateStateMachineScope (state_machine_scope, ref resolver);
						break;

					case AsyncMethodBodyDebugInformation async_method_body:
						UpdateAsyncMethodBody (async_method_body, ref resolver);
						break;

					default:
						// No need to update the other debug info as they don't store instruction references
						break;
					}
				}
			}
		}

		void UpdateLocalScope (ScopeDebugInformation scope, ref InstructionOffsetResolver resolver)
		{
			if (scope == null)
				return;

			scope.Start = resolver.Resolve (scope.Start);

			if (scope.HasScopes) {
				foreach (var subScope in scope.Scopes)
					UpdateLocalScope (subScope, ref resolver);
			}

			scope.End = resolver.Resolve (scope.End);
		}

		void UpdateStateMachineScope (StateMachineScopeDebugInformation debugInfo, ref InstructionOffsetResolver resolver)
		{
			resolver.Restart ();
			foreach (var scope in debugInfo.Scopes) {
				scope.Start = resolver.Resolve (scope.Start);
				scope.End = resolver.Resolve (scope.End);
			}
		}

		void UpdateAsyncMethodBody (AsyncMethodBodyDebugInformation debugInfo, ref InstructionOffsetResolver resolver)
		{
			if (!debugInfo.CatchHandler.IsResolved) {
				resolver.Restart ();
				debugInfo.CatchHandler = resolver.Resolve (debugInfo.CatchHandler);
			}

			resolver.Restart ();
			for (int i = 0; i < debugInfo.Yields.Count; i++) {
				debugInfo.Yields [i] = resolver.Resolve (debugInfo.Yields [i]);
			}

			resolver.Restart ();
			for (int i = 0; i < debugInfo.Resumes.Count; i++) {
				debugInfo.Resumes [i] = resolver.Resolve (debugInfo.Resumes [i]);
			}
		}

		struct InstructionOffsetResolver {
			readonly Instruction [] items;
			readonly Instruction removed_instruction;
			readonly Instruction existing_instruction;

			int cache_offset;
			int cache_index;
			Instruction cache_instruction;

			public int LastOffset { get => cache_offset; }

			public InstructionOffsetResolver (Instruction[] instructions, Instruction removedInstruction, Instruction existingInstruction)
			{
				items = instructions;
				removed_instruction = removedInstruction;
				existing_instruction = existingInstruction;
				cache_offset = 0;
				cache_index = 0;
				cache_instruction = items [0];
			}

			public void Restart ()
			{
				cache_offset = 0;
				cache_index = 0;
				cache_instruction = items [0];
			}

			public InstructionOffset Resolve (InstructionOffset inputOffset)
			{
				var result = ResolveInstructionOffset (inputOffset);
				if (!result.IsEndOfMethod && result.ResolvedInstruction == removed_instruction)
					result = new InstructionOffset (existing_instruction);

				return result;
			}

			InstructionOffset ResolveInstructionOffset (InstructionOffset inputOffset)
			{
				if (inputOffset.IsResolved)
					return inputOffset;

				int offset = inputOffset.Offset;

				if (cache_offset == offset)
					return new InstructionOffset (cache_instruction);

				if (cache_offset > offset) {
					// This should be rare - we're resolving offset pointing to a place before the current cache position
					// resolve by walking the instructions from start and don't cache the result.
					int size = 0;
					for (int i = 0; i < items.Length; i++) {
						// The array can be larger than the actual size, in which case its padded with nulls at the end
						// so when we reach null, treat it as an end of the IL.
						if (items [i] == null)
							return new InstructionOffset (i == 0 ? items [0] : items [i - 1]);

						if (size == offset)
							return new InstructionOffset (items [i]);

						if (size > offset)
							return new InstructionOffset (i == 0 ? items [0] : items [i - 1]);

						size += items [i].GetSize ();
					}

					// Offset is larger than the size of the body - so it points after the end
					return new InstructionOffset ();
				} else {
					// The offset points after the current cache position - so continue counting and update the cache
					int size = cache_offset;
					for (int i = cache_index; i < items.Length; i++) {
						cache_index = i;
						cache_offset = size;

						var item = items [i];

						// Allow for trailing null values in the case of
						// instructions.Size < instructions.Capacity
						if (item == null)
							return new InstructionOffset (i == 0 ? items [0] : items [i - 1]);

						cache_instruction = item;

						if (cache_offset == offset)
							return new InstructionOffset (cache_instruction);

						if (cache_offset > offset)
							return new InstructionOffset (i == 0 ? items [0] : items [i - 1]);

						size += item.GetSize ();
					}

					return new InstructionOffset ();
				}
			}
		}
	}
}
msbuild /p:Configuration=net_3_5_Release /target:Clean
msbuild /p:Configuration=net_3_5_Release
msbuild /p:Configuration=net_4_0_Release /target:Clean
msbuild /p:Configuration=net_4_0_Release
dotnet clean Mono.Cecil.sln /p:Configuration=netstandard_Release
dotnet build Mono.Cecil.sln /p:Configuration=netstandard_Release

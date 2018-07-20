Installation for .NET Core 2.1+:

dotnet build

dotnet tool install --add-source nupkg -g dotnetcementrefs

Usage examples:

dotnetcementrefs - converts cement references to nuget package references (with latest applicable versions) for all solutions in current directory.

dotnetcementrefs <module directory> - converts cement references to nuget package references (with latest applicable versions) for all solutions in <module directory>.
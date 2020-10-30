Installation for .NET Core 2.1+:

dotnet build

dotnet tool update --add-source nupkg -g dotnetcementrefs

Usage examples:

dotnetcementrefs - converts cement references to nuget package references (with latest applicable versions) for all solutions in current directory.

dotnetcementrefs <module directory> - converts cement references to nuget package references (with latest applicable versions) for all solutions in <module directory>.

parameters:

--allowPrereleasePackages - allow to use last package with pre-release version, instead checking self-project version suffix by default
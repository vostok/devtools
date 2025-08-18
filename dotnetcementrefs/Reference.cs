using Microsoft.Build.Evaluation;
using NuGet.Frameworks;

namespace dotnetcementrefs;

internal sealed record Reference
{
    public Reference(ProjectItem source, string include, NuGetFramework targetFramework, string path)
    {
        Source = source;
        Include = include;
        TargetFramework = targetFramework;
        Path = path;
    }

    public ProjectItem Source { get; }
    public string Include { get; }
    public NuGetFramework TargetFramework { get; }
    public string Path { get; }
    public string? NugetPackageName { get; set; }
    public bool? NugetPackageAllowPrerelease { get; set; }
}

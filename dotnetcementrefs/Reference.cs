using Microsoft.Build.Evaluation;
using NuGet.Frameworks;

namespace dotnetcementrefs;

internal sealed record Reference
{
    public Reference(ProjectItem source, string include, NuGetFramework targetFramework)
    {
        Source = source;
        Include = include;
        TargetFramework = targetFramework;
    }

    public ProjectItem Source { get; }
    public string Include { get; }
    public NuGetFramework TargetFramework { get; }
    public string? NugetPackageName { get; set; }
    public string? NugetPackageAllowPrerelease { get; set; }
}

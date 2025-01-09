using System;
using System.Collections.Generic;
using System.Linq;

namespace dotnetcementrefs;

internal sealed class Parameters
{
    public string TargetSlnPath { get; }
    public string SolutionConfiguration { get; }
    public string[] SourceUrls { get; }
    public string[] CementReferencePrefixes { get; }
    public string[] MissingReferencesToRemove { get; }
    public string[] ReferencesToRemove { get; }
    public bool FailOnNotFoundPackage { get; }
    public bool AllowLocalProjects { get; }
    public bool AllowPrereleasePackages { get; }
    public bool EnsureMultitargeted { get; }
    public bool CopyPrivateAssetsMetadata { get; }
    public bool UseFloatingVersions { get; }

    public Parameters(string[] args)
    {
        var positionalArgs = args.Where(x => !x.StartsWith("-")).ToArray();
        TargetSlnPath = positionalArgs.Length > 0 ? positionalArgs[0] : Environment.CurrentDirectory;
        SourceUrls = GetArgsByKey(args, "--source:").ToArray();
        CementReferencePrefixes = new[] {"Vostok."}.Concat(GetArgsByKey(args, "--refPrefix:")).ToArray();
        MissingReferencesToRemove = GetArgsByKey(args, "--removeMissing:").ToArray();
        ReferencesToRemove = GetArgsByKey(args, "--remove:").ToArray();
        FailOnNotFoundPackage = !args.Contains("--ignoreMissingPackages");
        SolutionConfiguration = GetArgsByKey(args, "--solutionConfiguration:").FirstOrDefault() ?? "Release";
        AllowLocalProjects = args.Contains("--allowLocalProjects");
        AllowPrereleasePackages = args.Contains("--allowPrereleasePackages");
        EnsureMultitargeted = args.Contains("--ensureMultitargeted");
        CopyPrivateAssetsMetadata = args.Contains("--copyPrivateAssets");
        UseFloatingVersions = args.Contains("--useFloatingVersions");
    }
    
    private static IEnumerable<string> GetArgsByKey(string[] args, string key)
    {
        return args
            .Where(x => x.StartsWith(key))
            .Select(x => x.Substring(key.Length).Trim());
    }
}
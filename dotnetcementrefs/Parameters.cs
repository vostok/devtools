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

    public Parameters(string targetSlnPath, string solutionConfiguration, string[] sourceUrls, string[] cementReferencePrefixes, string[] missingReferencesToRemove, string[] referencesToRemove, bool failOnNotFoundPackage, bool allowLocalProjects, bool allowPrereleasePackages, bool ensureMultitargeted, bool copyPrivateAssetsMetadata, bool useFloatingVersions)
    {
        TargetSlnPath = targetSlnPath;
        SolutionConfiguration = solutionConfiguration;
        SourceUrls = sourceUrls;
        CementReferencePrefixes = cementReferencePrefixes;
        MissingReferencesToRemove = missingReferencesToRemove;
        ReferencesToRemove = referencesToRemove;
        FailOnNotFoundPackage = failOnNotFoundPackage;
        AllowLocalProjects = allowLocalProjects;
        AllowPrereleasePackages = allowPrereleasePackages;
        EnsureMultitargeted = ensureMultitargeted;
        CopyPrivateAssetsMetadata = copyPrivateAssetsMetadata;
        UseFloatingVersions = useFloatingVersions;
    }

    public static Parameters Parse(string[] args)
    {
        var positionalArgs = args.Where(x => !x.StartsWith("-")).ToArray();
        var targetSlnPath = positionalArgs.Length > 0 ? positionalArgs[0] : Environment.CurrentDirectory;
        var sourceUrls = GetArgsByKey(args, "--source:").ToArray();
        var cementReferencePrefixes = new[] {"Vostok."}.Concat(GetArgsByKey(args, "--refPrefix:")).ToArray();
        var missingReferencesToRemove = GetArgsByKey(args, "--removeMissing:").ToArray();
        var referencesToRemove = GetArgsByKey(args, "--remove:").ToArray();
        var failOnNotFoundPackage = !args.Contains("--ignoreMissingPackages");
        var solutionConfiguration = GetArgsByKey(args, "--solutionConfiguration:").FirstOrDefault() ?? "Release";
        var allowLocalProjects = args.Contains("--allowLocalProjects");
        var allowPrereleasePackages = args.Contains("--allowPrereleasePackages");
        var ensureMultitargeted = args.Contains("--ensureMultitargeted");
        var copyPrivateAssetsMetadata = args.Contains("--copyPrivateAssets");
        var useFloatingVersions = args.Contains("--useFloatingVersions");
        
        return new Parameters(targetSlnPath, solutionConfiguration, sourceUrls, cementReferencePrefixes,
            missingReferencesToRemove, referencesToRemove, failOnNotFoundPackage, allowLocalProjects,
            allowPrereleasePackages, ensureMultitargeted, copyPrivateAssetsMetadata, useFloatingVersions);
    }

    private static IEnumerable<string> GetArgsByKey(string[] args, string key)
    {
        return args
            .Where(x => x.StartsWith(key))
            .Select(x => x.Substring(key.Length).Trim());
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

public static class Program
{
    private static readonly Dictionary<(string package, bool includePrerelease, string[] sourceUrls), NuGetVersion> NugetCache = new Dictionary<(string, bool, string[]), NuGetVersion>();

    public static void Main(string[] args)
    {
        var parameters = new Parameters(args);

        Console.Out.WriteLine(
            $"Converting cement references to NuGet package references for all projects of solutions located in '{parameters.WorkingDirectory}'.");

        var solutionFiles = Directory.GetFiles(parameters.WorkingDirectory, "*.sln");
        if (solutionFiles.Length == 0)
        {
            Console.Out.WriteLine("No solution files found.");
            return;
        }

        Console.Out.WriteLine(
            $"Found solution files: {Environment.NewLine}\t{string.Join(Environment.NewLine + "\t", solutionFiles)}");
        Console.Out.WriteLine();

        foreach (var solutionFile in solutionFiles)
        {
            HandleSolution(solutionFile, parameters);
        }
    }

    private static IEnumerable<string> GetArgsByKey(string[] args, string key)
    {
        return args
            .Where(x => x.StartsWith(key))
            .Select(x => x.Substring(key.Length).Trim());
    }

    private static void HandleSolution(string solutionFile, Parameters parameters)
    {
        var solution = SolutionFile.Parse(solutionFile);
        var solutionName = Path.GetFileName(solutionFile);

        Console.Out.WriteLine($"Working with '{parameters.SolutionConfiguration}' solution configuration.");

        var projects = FilterProjectsByConfiguration(solution.ProjectsInOrder, parameters.SolutionConfiguration)
            .ToArray();

        if (!projects.Any())
        {
            Console.Out.WriteLine($"No projects found in solution {solutionName}.");
            return;
        }

        Console.Out.WriteLine(
            $"Found projects in solution {solutionName}: {Environment.NewLine}\t{string.Join(Environment.NewLine + "\t", projects.Select(project => project.AbsolutePath))}");
        Console.Out.WriteLine();

        var allProjectsInSolution = projects
            .Select(p => p.ProjectName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var solutionProject in projects)
        {
            HandleProject(solutionProject, allProjectsInSolution, parameters);
        }
    }

    private static IEnumerable<ProjectInSolution> FilterProjectsByConfiguration(
        IEnumerable<ProjectInSolution> projects,
        string configuration)
    {
        var keyPrefix = configuration + "|";

        foreach (var project in projects)
        {
            var configurations = project.ProjectConfigurations;
            var enabledConfigurations = configurations.Where(x => x.Value.IncludeInBuild);

            if (enabledConfigurations.Any(x => x.Key.StartsWith(keyPrefix, StringComparison.OrdinalIgnoreCase)))
                yield return project;
        }
    }

    private static void HandleProject(
        ProjectInSolution solutionProject,
        ISet<string> allProjectsInSolution,
        Parameters parameters)
    {
        if (!File.Exists(solutionProject.AbsolutePath))
        {
            Console.Out.WriteLine($"Project '{solutionProject.AbsolutePath}' doesn't exists.");
            return;
        }

        Console.Out.WriteLine($"Working with project '{solutionProject.ProjectName}'..");

        var project = Project.FromFile(solutionProject.AbsolutePath, new ProjectOptions
        {
            LoadSettings = ProjectLoadSettings.IgnoreMissingImports
        });

        if (ShouldIgnore(project))
        {
            Console.Out.WriteLine(
                $"Ignore project  '{solutionProject.ProjectName}' due to DotnetCementRefsExclude property in csproj.");
            return;
        }

        var references = FindCementReferences(project, allProjectsInSolution, parameters.CementReferencePrefixes);

        if (parameters.AllowLocalProjects)
        {
            references.AddRange(FindLocalProjectReferences(project, allProjectsInSolution));
        }

        if (parameters.EnsureMultitargeted)
        {
            var singleTargeted = references.Select(r => r.DirectMetadata.Single(m => m.Name == "HintPath").UnevaluatedValue).Where(r => !r.Contains("$(") && r.Contains("netstandard2.0")).ToArray();
            if (singleTargeted.Any())
            {
                throw new Exception("All cement references should support multitargeting and contain $(ReferencesFramework). But "
                    + string.Join(",", singleTargeted) + " don't.");
            }
        }
        
        if (!references.Any())
        {
            Console.Out.WriteLine($"No references found in project {solutionProject.ProjectName}.");
            return;
        }

        Console.Out.WriteLine(
            $"Found references in {solutionProject.ProjectName}: {Environment.NewLine}\t{string.Join(Environment.NewLine + "\t", references.Select(item => item.EvaluatedInclude))}");
        Console.Out.WriteLine();

        var allowPrereleasePackagesForAll = HasPrereleaseVersionSuffix(project, out var versionSuffix) || parameters.AllowPrereleasePackages;
        if (allowPrereleasePackagesForAll)
        {
            Console.Out.WriteLine(
                $"Will allow prerelease versions in package references due to prerelease version suffix '{versionSuffix}'.");
        }
        else
        {
            Console.Out.WriteLine(
                "Won't allow prerelease versions in package due to stable version of the project itself.");
        }

        Console.Out.WriteLine();

        var usePrereleaseForPrefixes = GetUsePrereleaseForPrefixes(project);
        if (!allowPrereleasePackagesForAll && usePrereleaseForPrefixes.Length > 0)
        {
            Console.Out.WriteLine(
                $"prerelease allowed for prefixes {string.Join(';', usePrereleaseForPrefixes)} by .csproj properties");
        }

        foreach (var reference in references)
        {
            var allowPrereleaseForThisReference = usePrereleaseForPrefixes.Any(reference.EvaluatedInclude.StartsWith);
            HandleReference(project, reference, allowPrereleasePackagesForAll || allowPrereleaseForThisReference,
                parameters);
        }

        project.Save();

        Console.Out.WriteLine();
    }

    private static string[] GetUsePrereleaseForPrefixes(Project project)
    {
        var property = project.Properties.FirstOrDefault(x => x.Name == "DotnetCementRefsUsePrereleaseForPrefixes");
        var usePrereleaseForPrefixes =
            property?.EvaluatedValue.Split(new[] {';', ',', ' '}, StringSplitOptions.RemoveEmptyEntries) ??
            new string[] { };
        return usePrereleaseForPrefixes;
    }

    private static bool HasPrereleaseVersionSuffix(Project project, out string suffix)
    {
        suffix = project.GetProperty("VersionSuffix")?.EvaluatedValue;

        return !string.IsNullOrWhiteSpace(suffix);
    }

    private static bool ShouldIgnore(Project project)
    {
        return project
            .Properties
            .Any(item => item.Name == "DotnetCementRefsExclude" &&
                         item.EvaluatedValue == "true");
    }


    private static List<ProjectItem> FindCementReferences(Project project, ISet<string> localProjects,
        string[] cementRefPrefixes)
    {
        return project.ItemsIgnoringCondition
            .Where(item => item.ItemType == "Reference")
            .Where(item => cementRefPrefixes.Any(x => item.EvaluatedInclude.StartsWith(x)))
            .Where(item => !localProjects.Contains(item.EvaluatedInclude))
            .ToList();
    }

    private static List<ProjectItem> FindLocalProjectReferences(Project project, ISet<string> localProjects)
    {
        return project.ItemsIgnoringCondition
            .Where(item => item.ItemType == "Reference")
            .Where(item => localProjects.Contains(item.EvaluatedInclude))
            .ToList();
    }

    private static void HandleReference(Project project, ProjectItem reference, bool allowPrereleasePackages,
        Parameters parameters)
    {
        var packageName = reference.EvaluatedInclude;

        if (packageName.Contains(","))
            throw new Exception($"Fix reference format for '{packageName}'.");

        if (parameters.ReferencesToRemove.Contains(packageName, StringComparer.OrdinalIgnoreCase))
        {
            Console.Out.WriteLine($"Removed cement reference to '{reference.EvaluatedInclude}'.");
            project.RemoveItem(reference);
            return;
        }

        var packageVersion = GetLatestNugetVersionWithCache(packageName, allowPrereleasePackages, parameters.SourceUrls);

        if (packageVersion == null)
        {
            if (parameters.MissingReferencesToRemove.Any(x => packageName.StartsWith(x)))
            {
                Console.Out.WriteLine($"Removed cement reference to '{reference.EvaluatedInclude}'.");
                project.RemoveItem(reference);
                return;
            }

            if (parameters.FailOnNotFoundPackage)
                throw new Exception(
                    $"No versions of package '{packageName}' were found on '{string.Join(", ", parameters.SourceUrls)}'.");
            return;
        }

        Console.Out.WriteLine($"Latest version of NuGet package '{packageName}' is '{packageVersion}'");

        project.RemoveItem(reference);

        Console.Out.WriteLine($"Removed cement reference to '{reference.EvaluatedInclude}'.");

        var metadata = ConstructMetadata(reference, parameters, packageVersion);
        project.AddItem("PackageReference", packageName, metadata);

        Console.Out.WriteLine($"Added package reference to '{packageName}' of version '{metadata.First().Value}'.");
        Console.Out.WriteLine();
    }

    private static IEnumerable<KeyValuePair<string, string>> ConstructMetadata(ProjectItem reference, Parameters parameters, NuGetVersion packageVersion)
    {
        var version = packageVersion.ToString();
        if (parameters.UseFloatingVersions)
        {
            version = $"{packageVersion.Version.Major}.{packageVersion.Version.Minor}.";
            version += packageVersion.IsPrerelease ? "*-*" : "*";
        }

        var metadata = new List<KeyValuePair<string, string>>
        {
            new("Version", version)
        };
        var privateAssets = reference.GetMetadataValue("PrivateAssets");
        if (parameters.CopyPrivateAssetsMetadata && !string.IsNullOrEmpty(privateAssets))
            metadata.Add(new KeyValuePair<string, string>("PrivateAssets", privateAssets));
        return metadata;
    }

    private static NuGetVersion GetLatestNugetVersionWithCache(string package, bool includePrerelease, string[] sourceUrls)
    {
        if (NugetCache.TryGetValue((package, includePrerelease, sourceUrls), out var value))
            return value;

        var version = GetLatestNugetVersionDirect(package, includePrerelease, sourceUrls);

        NugetCache.Add((package, includePrerelease, sourceUrls), version);

        return version;
    }

    private static NuGetVersion GetLatestNugetVersionDirect(string package, bool includePrerelease, string[] sourceUrls)
    {
        const int attempts = 3;

        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                foreach (var source in sourceUrls)
                {
                    var latestVersion = GetLatestNugetVersion(package, includePrerelease, source);
                    if (latestVersion != null)
                    {
                        return latestVersion;
                    }
                }

                return null;
            }
            catch (Exception error)
            {
                if (attempt == attempts)
                    throw;

                Console.Out.WriteLine($"Failed to fetch version of package '{package}'. Attempt = {attempt}. Error = {error}.");
            }
        }

        return null;
    }

    private static NuGetVersion GetLatestNugetVersion(string package, bool includePrerelease, string sourceUrl)
    {
        var providers = new List<Lazy<INuGetResourceProvider>>();

        providers.AddRange(Repository.Provider.GetCoreV3());

        var packageSource = new PackageSource(sourceUrl);

        var sourceRepository = new SourceRepository(packageSource, providers);

        var metadataResource = sourceRepository.GetResource<PackageMetadataResource>();

        var versions = metadataResource.GetMetadataAsync(package, includePrerelease, false, new SourceCacheContext(),
                new NullLogger(), CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            .Where(data => data.Identity.Id == package)
            .Select(data => data.Identity.Version)
            .ToArray();

        return versions.Any()
            ? versions.Max()
            : null;
    }

    private class Parameters
    {
        public string WorkingDirectory { get; }
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
            WorkingDirectory = positionalArgs.Length > 0 ? positionalArgs[0] : Environment.CurrentDirectory;
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
    }
}

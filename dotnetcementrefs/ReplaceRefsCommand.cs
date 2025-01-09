using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using NuGet.Versioning;

namespace dotnetcementrefs;

internal sealed class ReplaceRefsCommand
{
    private static readonly Dictionary<(string package, bool includePrerelease, string[] sourceUrls), NuGetVersion> NugetCache = new();

    private readonly IProjectsProvider projectsProvider;
    private readonly IPackageVersionProvider packageVersionProvider;

    public ReplaceRefsCommand(IProjectsProvider projectsProvider, IPackageVersionProvider packageVersionProvider)
    {
        this.projectsProvider = projectsProvider;
        this.packageVersionProvider = packageVersionProvider;
    }

    public async Task ExecuteAsync(Parameters parameters)
    {
        await Console.Out.WriteLineAsync($"Converting cement references to NuGet package references for all projects of solutions located in '{parameters.TargetSlnPath}'.");

        var solutionFiles = parameters.TargetSlnPath.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
            ? Directory.GetFiles(Environment.CurrentDirectory, parameters.TargetSlnPath)
            : Directory.GetFiles(parameters.TargetSlnPath, "*.sln");
        if (solutionFiles.Length == 0)
        {
            await Console.Out.WriteLineAsync("No solution files found.");
            return;
        }

        await Console.Out.WriteLineAsync($"Found solution files: {Environment.NewLine}\t{string.Join(Environment.NewLine + "\t", solutionFiles)}");
        await Console.Out.WriteLineAsync();
        foreach (var solutionFile in solutionFiles)
        {
            await HandleSolutionAsync(solutionFile, parameters).ConfigureAwait(false);
        }
    }

    private async Task HandleSolutionAsync(string solutionFile, Parameters parameters)
    {
        var solutionName = Path.GetFileName(solutionFile);
        await Console.Out.WriteLineAsync($"Working with '{parameters.SolutionConfiguration}' solution configuration.");
        var projects = projectsProvider.GetFromSolution(solutionFile, parameters.SolutionConfiguration);
        if (!projects.Any())
        {
            await Console.Out.WriteLineAsync($"No projects found in solution {solutionName}.");
            return;
        }

        var separator = Environment.NewLine + "\t";
        await Console.Out.WriteLineAsync($"Found projects in solution {solutionName}: {Environment.NewLine}\t{string.Join(separator, projects.Select(project => project.AbsolutePath))}");
        await Console.Out.WriteLineAsync();
        var allProjectsInSolution = projects
            .Select(p => p.ProjectName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var solutionProject in projects)
        {
            await HandleProjectAsync(solutionProject, allProjectsInSolution, parameters).ConfigureAwait(false);
        }
    }

    private async Task HandleProjectAsync(
        SolutionProject solutionProject,
        ISet<string> allProjectsInSolution,
        Parameters parameters)
    {
        if (!File.Exists(solutionProject.AbsolutePath))
        {
            await Console.Out.WriteLineAsync($"Project '{solutionProject.AbsolutePath}' doesn't exists.");
            return;
        }

        await Console.Out.WriteLineAsync($"Working with project '{solutionProject.ProjectName}'..");
        var project = Project.FromFile(solutionProject.AbsolutePath, new()
        {
            LoadSettings = ProjectLoadSettings.IgnoreMissingImports
        });
        if (ShouldIgnore(project))
        {
            await Console.Out.WriteLineAsync($"Ignore project  '{solutionProject.ProjectName}' due to DotnetCementRefsExclude property in csproj.");
            return;
        }
        
        var references = FindCementReferences(project, allProjectsInSolution, parameters.CementReferencePrefixes);
        if (parameters.AllowLocalProjects)
        {
            references.AddRange(FindLocalProjectReferences(project, allProjectsInSolution));
        }
        if (parameters.EnsureMultitargeted)
        {
            var singleTargeted = references
                .Select(r => r.DirectMetadata.Single(m => m.Name == WellKnownMetadata.Reference.HintPath).UnevaluatedValue)
                .Where(r => !r.Contains("$(") && r.Contains("netstandard2.0"))
                .ToArray();
            
            if (singleTargeted.Any())
            {
                throw new($"All cement references should support multitargeting and contain $(ReferencesFramework). But {string.Join(",", singleTargeted)} don't.");
            }
        }
        
        var itemsFromModuleReferences = ReplaceModuleReferences(project).ToHashSet();
        var itemsQuery = project.ItemsIgnoringCondition
            .Where(x => x.ItemType == WellKnownItems.Reference)
            .Where(x => itemsFromModuleReferences.Contains(x.EvaluatedInclude));
        
        if (!parameters.AllowLocalProjects)
        {
            itemsQuery = itemsQuery.Where(x => !allProjectsInSolution.Contains(x.EvaluatedInclude));
        }
        
        references = references
            .Concat(itemsQuery)
            .OrderBy(x => x.HasMetadata(WellKnownMetadata.Reference.CementInstallFrameworks))
            .ToList();
        
        if (!references.Any())
        {
            await Console.Out.WriteLineAsync($"No references found in project {solutionProject.ProjectName}.");
            return;
        }

        await Console.Out.WriteLineAsync($"Found references in {solutionProject.ProjectName}: {Environment.NewLine}\t{string.Join(Environment.NewLine + "\t", references.Select(item => item.EvaluatedInclude))}");
        await Console.Out.WriteLineAsync();
        var allowPrereleasePackagesForAll = HasPrereleaseVersionSuffix(project, out var versionSuffix) || parameters.AllowPrereleasePackages;
        if (allowPrereleasePackagesForAll)
        {
            await Console.Out.WriteLineAsync($"Will allow prerelease versions in package references due to prerelease version suffix '{versionSuffix}'.");
        }
        else
        {
            await Console.Out.WriteLineAsync("Won't allow prerelease versions in package due to stable version of the project itself.");
        }
        await Console.Out.WriteLineAsync();
        var usePrereleaseForPrefixes = GetUsePrereleaseForPrefixes(project);
        if (!allowPrereleasePackagesForAll && usePrereleaseForPrefixes.Length > 0)
        {
            await Console.Out.WriteLineAsync($"prerelease allowed for prefixes {string.Join(';', usePrereleaseForPrefixes)} by .csproj properties");
        }
        foreach (var reference in references)
        {
            var allowPrereleaseForThisReference = usePrereleaseForPrefixes.Any(reference.EvaluatedInclude.StartsWith);
            await HandleReferenceAsync(project, reference, allowPrereleasePackagesForAll || allowPrereleaseForThisReference, parameters).ConfigureAwait(false);
        }
        project.Save();
        await Console.Out.WriteLineAsync();
    }

    private static string[] ReplaceModuleReferences(Project project)
    {
        var resolver = new ModuleReferenceResolver();
        var references = resolver.Resolve(project);

        var helper = new ReferenceHelper();
        var items = helper.AddReferences(project, references);
        
        var moduleReferences = references.Select(x => x.Source);
        project.RemoveItems(moduleReferences);
        
        project.ReevaluateIfNecessary();

        return items;
    }

    private static string[] GetUsePrereleaseForPrefixes(Project project)
    {
        var property = project.Properties
            .FirstOrDefault(x => x.Name == WellKnownProperties.DotnetCementRefsUsePrereleaseForPrefixes);
        
        var usePrereleaseForPrefixes = property?.EvaluatedValue.Split(new[] { ';', ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                       ?? new string[] { };
        return usePrereleaseForPrefixes;
    }

    private static bool HasPrereleaseVersionSuffix(Project project, out string? suffix)
    {
        suffix = project.GetProperty(WellKnownProperties.VersionSuffix)?.EvaluatedValue;
        return !string.IsNullOrWhiteSpace(suffix);
    }

    private static bool ShouldIgnore(Project project)
    {
        return project
            .Properties
            .Any(item => item.Name == WellKnownProperties.DotnetCementRefsExclude && item.EvaluatedValue.ToLower() is "true");
    }


    private static List<ProjectItem> FindCementReferences(Project project, ISet<string> localProjects,
        string[] cementRefPrefixes)
    {
        return project.ItemsIgnoringCondition
            .Where(item => item.ItemType == WellKnownItems.Reference)
            .Where(item => cementRefPrefixes.Any(x => item.EvaluatedInclude.StartsWith(x)))
            .Where(item => !localProjects.Contains(item.EvaluatedInclude))
            .ToList();
    }

    private static List<ProjectItem> FindLocalProjectReferences(Project project, ISet<string> localProjects)
    {
        return project.ItemsIgnoringCondition
            .Where(item => item.ItemType == WellKnownItems.Reference)
            .Where(item => localProjects.Contains(item.EvaluatedInclude))
            .ToList();
    }

    private async Task HandleReferenceAsync(Project project, ProjectItem reference, bool allowPrereleasePackages, Parameters parameters)
    {
        var packageName = reference.GetMetadataValue(WellKnownMetadata.Reference.NugetPackageName);
        if (packageName is "")
            packageName = reference.EvaluatedInclude;
        if (packageName.Contains(","))
            throw new($"Fix reference format for '{packageName}' (there shouldn't be any explicit versions or architecture references).");

        if (parameters.ReferencesToRemove.Contains(packageName, StringComparer.OrdinalIgnoreCase))
        {
            await Console.Out.WriteLineAsync($"Removed cement reference to '{reference.EvaluatedInclude}'.");
            project.RemoveItem(reference);
            return;
        }

        var explicitPrereleaseFlag = reference
            .GetMetadataValue(WellKnownMetadata.Reference.NugetPackageAllowPrerelease)
            .ToLower();
        
        if (explicitPrereleaseFlag is "true" or "false")
            allowPrereleasePackages = bool.Parse(explicitPrereleaseFlag);
        var packageVersion = await GetLatestNugetVersionWithCacheAsync(packageName, allowPrereleasePackages, parameters.SourceUrls);
        if (packageVersion == null)
        {
            if (parameters.MissingReferencesToRemove.Any(x => packageName.StartsWith(x)))
            {
                await Console.Out.WriteLineAsync($"Removed cement reference to '{reference.EvaluatedInclude}'.");
                project.RemoveItem(reference);
                return;
            }

            if (parameters.FailOnNotFoundPackage)
                throw new($"No versions of package '{packageName}' were found on '{string.Join(", ", parameters.SourceUrls)}'.");
            return;
        }
        await Console.Out.WriteLineAsync($"Latest version of NuGet package '{packageName}' is '{packageVersion}'");

        var itemGroupsWithCementRef = project.Xml.ItemGroups.Where(g => g.Items.Any(r => r.ElementName == WellKnownItems.Reference && r.Include == reference.EvaluatedInclude)).ToList();
        var itemGroupsWithPackageRef = project.Xml.ItemGroups.Where(ig => ig.Items.Any(i => i.ElementName == WellKnownItems.PackageReference && i.Include == packageName)).ToList();
        var metadata = ConstructMetadata(reference, parameters, packageVersion);
        var condition = ConstructCondition(reference);
        if (itemGroupsWithCementRef.Any())
            foreach (var group in itemGroupsWithCementRef)
            {
                if (itemGroupsWithPackageRef.Any(ig => ReferenceEquals(ig, group)))
                {
                    continue;
                }

                var item = group.AddItem(WellKnownItems.PackageReference, packageName, metadata);
                if (condition != null)
                {
                    item.Condition = condition;
                }
            }
        else
        {
            if (!project.Items.Any(i => i.ItemType == WellKnownItems.PackageReference && i.EvaluatedInclude == packageName))
            {
                var item = project.AddItem(WellKnownItems.PackageReference, packageName, metadata)[0];
                if (condition != null)
                {
                    item.Xml.Condition = condition;
                }
            }
        }
        await Console.Out.WriteLineAsync($"Added package reference to '{packageName}' of version '{metadata.First().Value}'.");
        project.RemoveItem(reference);
        await Console.Out.WriteLineAsync($"Removed cement reference to '{reference.EvaluatedInclude}'.");
        await Console.Out.WriteLineAsync();
    }

    private static KeyValuePair<string, string>[] ConstructMetadata(ProjectItem reference, Parameters parameters, NuGetVersion packageVersion)
    {
        var version = packageVersion.ToString();
        if (parameters.UseFloatingVersions)
        {
            version = $"{packageVersion.Version.Major}.{packageVersion.Version.Minor}.";
            version += packageVersion.IsPrerelease ? "*-*" : "*";
        }

        var versionMetadata = new KeyValuePair<string, string>(WellKnownMetadata.PackageReference.Version, version);
        var metadata = new List<KeyValuePair<string, string>> { versionMetadata };
        var privateAssets = reference.GetMetadataValue(WellKnownMetadata.Reference.PrivateAssets);
        if (parameters.CopyPrivateAssetsMetadata && !string.IsNullOrEmpty(privateAssets))
            metadata.Add(new(WellKnownMetadata.PackageReference.PrivateAssets, privateAssets));
        return metadata.ToArray();
    }

    private static string? ConstructCondition(ProjectItem reference)
    {
        var targetFrameworks = reference.GetMetadataValue(WellKnownMetadata.Reference.CementInstallFrameworks);

        if (string.IsNullOrEmpty(targetFrameworks))
        {
            return null;
        }
        
        var frameworks = targetFrameworks.Split(';');
        var conditions = frameworks.Select(x => $"'$(TargetFramework)' == '{x}'");
        return string.Join(" or ", conditions);
    }

    private async Task<NuGetVersion?> GetLatestNugetVersionWithCacheAsync(string package, bool includePrerelease, string[] sourceUrls)
    {
        if (NugetCache.TryGetValue((package, includePrerelease, sourceUrls), out var value))
            return value;

        var version = await GetLatestNugetVersionDirectAsync(package, includePrerelease, sourceUrls).ConfigureAwait(false);
        if (version is not null)
            NugetCache.Add((package, includePrerelease, sourceUrls), version);
        return version;
    }

    private async Task<NuGetVersion?> GetLatestNugetVersionDirectAsync(string package, bool includePrerelease, string[] sourceUrls)
    {
        const int attempts = 3;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                foreach (var source in sourceUrls)
                {
                    var latestVersion = await GetLatestNugetVersionAsync(package, includePrerelease, source).ConfigureAwait(false);
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

                await Console.Out.WriteLineAsync($"Failed to fetch version of package '{package}'. Attempt = {attempt}. Error = {error}.");
            }
        }
        return null;
    }

    private async Task<NuGetVersion?> GetLatestNugetVersionAsync(string package, bool includePrerelease, string sourceUrl)
    {
        var versions = await packageVersionProvider.GetVersionsAsync(package, includePrerelease, sourceUrl);
        if (versions.Count == 0)
            return null;
        
        var maxVer = versions.Max();
        // semver doesn't sort suffix numerically, so .Max() will return the oldest prerelease version
        // there could be a better solution with proper string comparer,
        // but it'll only help if all prerelease versions have the same tag name with numbered suffix
        // additionally, if there's a release version available, it must be the most relevant one
        // even if there are later prerelease versions published after it
        var latest = versions.LastOrDefault(v => v.Version == maxVer.Version && !v.IsPrerelease)
            ?? versions.Last(v => v.Version == maxVer.Version);
        return latest;
    }
}

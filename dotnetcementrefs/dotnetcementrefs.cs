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
    public static void Main(string[] args)
    {
        var positionalArgs = args.Where(x => !x.StartsWith("-")).ToArray();
        var workingDirectory = positionalArgs.Length > 0 ? positionalArgs[0] : Environment.CurrentDirectory;
        var sourceUrls = new[] {"https://api.nuget.org/v3/index.json"}.Concat(ParseSources(args)).ToArray();

        Console.Out.WriteLine($"Converting cement references to NuGet package references for all projects of solutions located in '{workingDirectory}'.");

        var solutionFiles = Directory.GetFiles(workingDirectory, "*.sln");
        if (solutionFiles.Length == 0)
        {
            Console.Out.WriteLine("No solution files found.");
            return;
        }

        Console.Out.WriteLine($"Found solution files: {Environment.NewLine}\t{string.Join(Environment.NewLine + "\t", solutionFiles)}");
        Console.Out.WriteLine();

        foreach (var solutionFile in solutionFiles)
        {
            HandleSolution(solutionFile, sourceUrls);
        }
    }

    private static IEnumerable<string> ParseSources(string[] args)
    {
        const string arg = "--source:";

        return args
            .Where(x => x.StartsWith(arg))
            .Select(x => x.Substring(arg.Length).Trim());
    }

    private static void HandleSolution(string solutionFile, string[] sourceUrls)
    {
        var solution = SolutionFile.Parse(solutionFile);
        var solutionName = Path.GetFileName(solutionFile);

        if (!solution.ProjectsInOrder.Any())
        {
            Console.Out.WriteLine($"No projects found in solution {solutionName}.");
            return;
        }

        Console.Out.WriteLine($"Found projects in solution {solutionName}: {Environment.NewLine}\t{string.Join(Environment.NewLine + "\t", solution.ProjectsInOrder.Select(project => project.AbsolutePath))}");
        Console.Out.WriteLine();

        var allProjectsInSolution = solution.ProjectsInOrder
            .Select(p => p.ProjectName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var solutionProject in solution.ProjectsInOrder)
        {
            HandleProject(solutionProject, allProjectsInSolution, sourceUrls);
        }
    }

    private static void HandleProject(ProjectInSolution solutionProject, ISet<string> allProjectsInSolution, string[] sourceUrls)
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
            Console.Out.WriteLine($"Ignore project  '{solutionProject.ProjectName}' due to DotnetCementRefsExclude property in csproj.");
            return;
        }

        var cementReferences = FindCementReferences(project, allProjectsInSolution);
        if (!cementReferences.Any())
        {
            Console.Out.WriteLine($"No cement references found in project {solutionProject.ProjectName}.");
            return;
        }

        Console.Out.WriteLine($"Found cement references in {solutionProject.ProjectName}: {Environment.NewLine}\t{string.Join(Environment.NewLine + "\t", cementReferences.Select(item => item.EvaluatedInclude))}");
        Console.Out.WriteLine();

        var allowPrereleasePackages = HasPrereleaseVersionSuffix(project, out var versionSuffix);
        if (allowPrereleasePackages)
        {
            Console.Out.WriteLine($"Will allow prerelease versions in package references due to prerelease version suffix '{versionSuffix}'.");
        }
        else
        {
            Console.Out.WriteLine("Won't allow prerelease versions in package due to stable version of the project itself.");
        }

        Console.Out.WriteLine();

        foreach (var reference in cementReferences)
        {
            HandleReference(project, reference, allowPrereleasePackages, sourceUrls);
        }

        project.Save();

        Console.Out.WriteLine();
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


    private static ProjectItem[] FindCementReferences(Project project, ISet<string> localProjects)
    {
        return project.Items
            .Where(item => item.ItemType == "Reference")
            .Where(item => item.EvaluatedInclude.StartsWith("Vostok."))
            .Where(item => !localProjects.Contains(item.EvaluatedInclude))
            .ToArray();
    }

    private static void HandleReference(Project project, ProjectItem reference, bool allowPrereleasePackages, string[] sourceUrls)
    {
        project.RemoveItem(reference);

        Console.Out.WriteLine($"Removed cement reference to '{reference.EvaluatedInclude}'.");

        var packageName = reference.EvaluatedInclude;
        var packageVersion = GetLatestNugetVersion(packageName, allowPrereleasePackages, sourceUrls);

        Console.Out.WriteLine($"Latest version of NuGet package '{packageName}' is '{packageVersion}'");

        project.AddItem("PackageReference", packageName, new[]
        {
            new KeyValuePair<string, string>("Version", packageVersion.ToString())
        });

        Console.Out.WriteLine($"Added package reference to '{packageName}' of version '{packageVersion}'.");
        Console.Out.WriteLine();
    }

    private static NuGetVersion GetLatestNugetVersion(string package, bool includePrerelease, string[] sourceUrls)
    {
        foreach (var source in sourceUrls)
        {
            var latestVersion = GetLatestNugetVersion(package, includePrerelease, source);
            if (latestVersion != null)
                return latestVersion;
        }

        throw new Exception($"No versions of package '{package}' were found on '{string.Join(", ", sourceUrls)}'.");
    }
    private static NuGetVersion GetLatestNugetVersion(string package, bool includePrerelease, string sourceUrl)
    {
        var providers = new List<Lazy<INuGetResourceProvider>>();

        providers.AddRange(Repository.Provider.GetCoreV3());

        var packageSource = new PackageSource(sourceUrl);

        var sourceRepository = new SourceRepository(packageSource, providers);

        var metadataResource = sourceRepository.GetResource<PackageMetadataResource>();

        var versions = metadataResource.GetMetadataAsync(package, includePrerelease, false, new SourceCacheContext(), new NullLogger(), CancellationToken.None)
            .GetAwaiter()
            .GetResult()
            .Where(data => data.Identity.Id == package)
            .Select(data => data.Identity.Version)
            .ToArray();

        return versions.Any()
            ? versions.Max()
            : null;
    }
}
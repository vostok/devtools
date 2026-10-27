using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;

public static class Program
{
    public static void Main(string[] args)
    {
        var parameters = new Parameters(args);

        var workingDir = parameters.WorkingDirectory;
        Console.Out.WriteLine($"Setting version {parameters.VersionPart} '{parameters.VersionValue}' " +
                              $"for all projects of solutions located in '{workingDir}'.");

        var solutionFiles = Directory.GetFiles(workingDir, "*.sln");
        if (solutionFiles.Length == 0)
        {
            Console.Out.WriteLine("No solution files found.");
            return;
        }

        var csprojFiles = GetMatchingProjectsPaths(solutionFiles, parameters.SolutionConfiguration);
        Console.Out.WriteLine(
            $"Found {csprojFiles.Count} csproj files: {Environment.NewLine}\t{string.Join(Environment.NewLine + "\t", csprojFiles)}");

        foreach (var csprojFile in csprojFiles)
            HandleProject(csprojFile, parameters.CsProjProperty, parameters.VersionValue);
    }

    private static HashSet<string> GetMatchingProjectsPaths(
        string[] slnPaths,
        string configuration
    ) => slnPaths
        .Select(SolutionFile.Parse)
        .SelectMany(sln => sln.ProjectsInOrder)
        .Where(proj => proj.ProjectConfigurations.Any(cfg =>
            cfg.Value.IncludeInBuild &&
            cfg.Key.StartsWith(configuration + '|', StringComparison.OrdinalIgnoreCase)
        ))
        .Select(proj => proj.AbsolutePath)
        .ToHashSet();

    private static void HandleProject(string projectFile, string csprojPropertyName, string csprojPropertyValue)
    {
        if (!File.Exists(projectFile))
        {
            Console.Out.WriteLine($"Project file '{projectFile}' doesn't exists.");
            return;
        }

        Console.Out.WriteLine($"Working with project '{Path.GetFileName(projectFile)}'..");

        var project = Project.FromFile(projectFile, new ProjectOptions
        {
            LoadSettings = ProjectLoadSettings.IgnoreMissingImports
        });

        project.SetProperty(csprojPropertyName, csprojPropertyValue);
        project.Save();
    }

    private class Parameters
    {
        public string VersionPart { get; }
        public string CsProjProperty { get; }
        public string VersionValue { get; }
        public string WorkingDirectory { get; }
        public string SolutionConfiguration { get; }

        public Parameters(string[] args)
        {
            VersionPart = args.Contains("--version")
                ? "version"
                : args.Contains("--prefix")
                    ? "prefix"
                    : "suffix";

            CsProjProperty = args.Contains("--version")
                ? "Version"
                : args.Contains("--prefix")
                    ? "VersionPrefix"
                    : "VersionSuffix";

            var positionalArgs = args.Where(x => !x.StartsWith("--")).ToArray();

            if (positionalArgs.Length <= 0)
                throw new Exception($"Missing required argument: version {VersionPart}.");

            VersionValue = positionalArgs[0];
            WorkingDirectory = positionalArgs.Length > 1 ? positionalArgs[1] : Environment.CurrentDirectory;
            SolutionConfiguration =
                GetArgsByKey(args, "--solutionConfiguration:").FirstOrDefault() ?? "Release";
        }

        private static IEnumerable<string> GetArgsByKey(string[] args, string key)
        {
            return args
                .Where(x => x.StartsWith(key))
                .Select(x => x.Substring(key.Length).Trim());
        }
    }
}
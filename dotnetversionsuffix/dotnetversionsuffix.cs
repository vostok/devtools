using System;
using System.IO;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;

public static class Program
{
    public static void Main(string[] args)
    {
        var versionPart = args.Contains("--prefix") ? "prefix" : "suffix";
        var csProjProperty = args.Contains("--prefix") ? "VersionPrefix" : "VersionSuffix";

        if (args.Length < 0)
            throw new Exception($"Missing required argument: version {versionPart}.");

        var versionValue = args[0];
        var workingDirectory = args.Length > 1 ? args[1] : Environment.CurrentDirectory;

        Console.Out.WriteLine($"Setting version {versionPart} '{versionValue}' for all projects of solutions located in '{workingDirectory}'.");

        var solutionFiles = Directory.GetFiles(workingDirectory, "*.sln");
        if (solutionFiles.Length == 0)
        {
            Console.Out.WriteLine("No solution files found.");
            return;
        }

        Console.Out.WriteLine($"Found solution files: {Environment.NewLine}\t{string.Join(Environment.NewLine + "\t", solutionFiles)}");

        var projectFiles = solutionFiles
            .Select(SolutionFile.Parse)
            .SelectMany(solution => solution.ProjectsInOrder)
            .Select(project => project.AbsolutePath)
            .ToArray();

        if (projectFiles.Length == 0)
        {
            Console.Out.WriteLine("No projects found in solutions.");
            return;
        }

        Console.Out.WriteLine($"Found project files: {Environment.NewLine}\t{string.Join(Environment.NewLine + "\t", projectFiles)}");

        foreach (var projectFile in projectFiles)
        {
            if (!File.Exists(projectFile))
            {
                Console.Out.WriteLine($"Project file '{projectFile}' doesn't exists.");
                continue;
            }
            
            Console.Out.WriteLine($"Working with project '{Path.GetFileName(projectFile)}'..");

            var project = Project.FromFile(projectFile, new ProjectOptions
            {
                LoadSettings = ProjectLoadSettings.IgnoreMissingImports
            });

            project.SetProperty(csProjProperty, versionValue);
            project.Save();
        }
    }
}

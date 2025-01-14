using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;

namespace dotnetcementrefs;

internal sealed class ProjectsProvider : IProjectsProvider
{
    public IReadOnlyCollection<SolutionProject> GetFromSolution(string solutionPath, string solutionConfiguration)
    {
        var solution = SolutionFile.Parse(solutionPath);
        var projects = FilterProjectsByConfiguration(solution.ProjectsInOrder, solutionConfiguration);
        return projects.Select(x => new SolutionProject(x.AbsolutePath, x.ProjectName)).ToArray();
    }
    
    private static IEnumerable<ProjectInSolution> FilterProjectsByConfiguration(IEnumerable<ProjectInSolution> projects, string configuration)
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
}

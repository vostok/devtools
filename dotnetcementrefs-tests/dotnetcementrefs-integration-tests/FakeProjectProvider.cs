using dotnetcementrefs;

namespace DotnetCementRefs.Integration.Tests;

internal sealed class FakeProjectProvider : IProjectsProvider
{
    private readonly Dictionary<string, List<SolutionProject>> projects = new();
    
    public IReadOnlyCollection<SolutionProject> GetFromSolution(string solutionPath, string solutionConfiguration)
    {
        var key = string.Join('&', solutionPath, solutionConfiguration);
        return projects.TryGetValue(key, out var items) ? items : [];
    }

    public void AddToSolution(SolutionProject project, string solutionPath, string solutionConfiguration)
    {
        var key = string.Join('&', solutionPath, solutionConfiguration);
        if (projects.TryGetValue(key, out var items))
        {
            items.Add(project);
        }
        else
        {
            projects.Add(key, [project]);
        }
    }
}

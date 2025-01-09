using System.Collections.Generic;

namespace dotnetcementrefs;

internal interface IProjectsProvider
{
    public IReadOnlyCollection<SolutionProject> GetFromSolution(string solutionPath, string solutionConfiguration);
}

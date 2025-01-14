using System.Collections.Generic;

namespace dotnetcementrefs;

internal interface IProjectsProvider
{
    IReadOnlyCollection<SolutionProject> GetFromSolution(string solutionPath, string solutionConfiguration);
}

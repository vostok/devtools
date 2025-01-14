namespace dotnetcementrefs;

internal sealed record SolutionProject
{
    public string AbsolutePath { get; }
    public string ProjectName { get; }

    public SolutionProject(string absolutePath, string projectName)
    {
        AbsolutePath = absolutePath;
        ProjectName = projectName;
    }
}
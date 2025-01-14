using Microsoft.Build.Construction;

namespace DotnetCementRefs.Integration.Tests;

internal static class ProjectsFactory
{
    public static ProjectRootElement CreateClassLib(IReadOnlyCollection<string> targetFrameworks)
    {
        var project = ProjectRootElement.Create();
        project.Sdk = "Microsoft.NET.Sdk";

        var properties = project.AddPropertyGroup();

        if (targetFrameworks.Count > 1)
        {
            properties.AddProperty("TargetFrameworks", string.Join(separator: ';', targetFrameworks));
        }
        else
        {
            properties.AddProperty("TargetFramework", targetFrameworks.First());
        }

        return project;
    }
}
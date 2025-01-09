using dotnetcementrefs;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using NuGet.Versioning;

namespace DotnetCementRefs.Integration.Tests;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class ReplaceRefsCommandTests  : IDisposable
{
    private readonly TempWorkspace workspace;
    private readonly IProjectsProvider projectProvider;
    private readonly IPackageVersionProvider versionProvider;
    
    private readonly ReplaceRefsCommand command;

    public ReplaceRefsCommandTests()
    {
        workspace = TempWorkspace.Create();

        projectProvider = Substitute.For<IProjectsProvider>();
        versionProvider = Substitute.For<IPackageVersionProvider>();
        command = new ReplaceRefsCommand(projectProvider, versionProvider);
    }

    [Test]
    public async Task Should_replace_reference()
    {
        // arrange
        var moduleName = Guid.NewGuid().ToString();
        var modulePath = workspace.CreateModule(moduleName);

        var solutionConfiguration = Guid.NewGuid().ToString();
        var solutionPath = Path.Combine(modulePath, $"{Guid.NewGuid()}.sln");
        File.Create(solutionPath);

        var projectName = Guid.NewGuid().ToString();
        var projectPath = Path.Combine(workspace.Path, $"{projectName}.csproj");
        var project = ProjectsFactory.CreateClassLib(["net8.0"]);

        var prefix = Guid.NewGuid().ToString();
        var include = string.Join('.', prefix, Guid.NewGuid().ToString());
        project.AddItem("Reference", include);
        
        project.Save(projectPath);
        
        var solutionProject = new SolutionProject(projectPath, projectName);
        projectProvider.GetFromSolution(solutionPath, solutionConfiguration).Returns([solutionProject]);

        var sourceUrl = Guid.NewGuid().ToString();
        var includePrerelease = Arg.Any<bool>();
        var nugetVersion = new NuGetVersion("1.0.0");
        versionProvider.GetVersionsAsync(include, includePrerelease, sourceUrl).Returns([nugetVersion]);

        // act
        var parameters = CreateParameters(modulePath, solutionConfiguration, [sourceUrl], [prefix]);
        await command.ExecuteAsync(parameters);
        
        // assert
        var projectOptions = new ProjectOptions
        {
            ProjectCollection = new ProjectCollection(),
            LoadSettings = ProjectLoadSettings.IgnoreMissingImports 
        };
        
        var actual = Project.FromFile(project.FullPath, projectOptions);
        actual.GetItems("Reference").Should().BeEmpty();
        
        var packageReference = actual.GetItems("PackageReference").Single();
        packageReference.EvaluatedInclude.Should().Be(include);
        packageReference.GetMetadataValue("Version").Should().Be(nugetVersion.ToString());
    }

    private static Parameters CreateParameters(string targetSlnPath, string solutionConfiguration,
        IReadOnlyCollection<string> sourceUrls, IReadOnlyCollection<string> cementReferencePrefixes)
    {
        var missingReferencesToRemove = Array.Empty<string>();
        var referencesToRemove = Array.Empty<string>();
        var failOnNotFoundPackage = true;
        var allowLocalProjects = false;
        var allowPrereleasePackages = false;
        var ensureMultitargeted = false;
        var copyPrivateAssetsMetadata = false;
        var useFloatingVersions = false;

        return new Parameters(targetSlnPath, solutionConfiguration, sourceUrls.ToArray(),
            cementReferencePrefixes.ToArray(), missingReferencesToRemove, referencesToRemove, failOnNotFoundPackage,
            allowLocalProjects, allowPrereleasePackages, ensureMultitargeted, copyPrivateAssetsMetadata,
            useFloatingVersions);
    }

    public void Dispose()
    {
        try
        {
            workspace.Dispose();
        }
        catch
        {
            // ignored
        }
    }
}

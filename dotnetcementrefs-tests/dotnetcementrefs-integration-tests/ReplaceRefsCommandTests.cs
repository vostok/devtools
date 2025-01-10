using dotnetcementrefs;
using Microsoft.Build.Construction;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using NuGet.Versioning;

namespace DotnetCementRefs.Integration.Tests;

[FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
public sealed class ReplaceRefsCommandTests : IDisposable
{
    private readonly ReplaceRefsCommand command;
    private readonly FakeProjectProvider projectProvider;
    private readonly IPackageVersionProvider versionProvider;
    private readonly TempWorkspace workspace;

    private static ProjectOptions DefaultProjectOptions => new()
    {
        ProjectCollection = new ProjectCollection(),
        LoadSettings = ProjectLoadSettings.IgnoreMissingImports
    };

    public ReplaceRefsCommandTests()
    {
        workspace = TempWorkspace.Create();

        projectProvider = new FakeProjectProvider();
        versionProvider = Substitute.For<IPackageVersionProvider>();
        command = new ReplaceRefsCommand(projectProvider, versionProvider);
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

    [Test]
    public async Task Should_replace_reference()
    {
        // arrange
        var (modulePath, solutionPath, solutionConfiguration) = CreateModule();

        var project = ProjectsFactory.CreateClassLib(["net8.0"]);

        var prefix = Guid.NewGuid().ToString();
        var include = string.Join('.', prefix, Guid.NewGuid());
        project.AddItem(WellKnownItems.Reference, include);

        AddProjectToSolution(project, solutionPath, solutionConfiguration);

        var sourceUrl = Guid.NewGuid().ToString();
        var nugetVersion = CreateNuGetVersion();
        var includePrerelease = Arg.Any<bool>();
        versionProvider.GetVersionsAsync(include, includePrerelease, sourceUrl).Returns([nugetVersion]);

        // act
        var parameters = CreateParameters(modulePath, solutionConfiguration, [sourceUrl], [prefix]);
        await command.ExecuteAsync(parameters);

        // assert
        var actual = Project.FromFile(project.FullPath, DefaultProjectOptions);
        actual.GetItems(WellKnownItems.Reference).Should().BeEmpty();

        var packageReference = actual.GetItems(WellKnownItems.PackageReference).Single();
        packageReference.EvaluatedInclude.Should().Be(include);
        packageReference.GetMetadataValue(WellKnownMetadata.PackageReference.Version)
            .Should().Be(nugetVersion.ToString());
    }

    [Test]
    public async Task Should_replace_module_reference()
    {
        // arrange
        var (modulePath, solutionPath, solutionConfiguration) = CreateModule();

        var depName = Guid.NewGuid().ToString();
        workspace.CreateModule(depName);

        var prefix = Guid.NewGuid().ToString();
        var dllName = string.Join('.', prefix, Guid.NewGuid());
        var depYaml = $"""
                       full-build:
                         install:
                           - {dllName}.dll
                       """;

        workspace.WriteYaml(depName, depYaml);

        var project = ProjectsFactory.CreateClassLib(["net8.0"]);
        project.AddItem(WellKnownItems.ModuleReference, depName);

        AddProjectToSolution(project, solutionPath, solutionConfiguration);

        var sourceUrl = Guid.NewGuid().ToString();
        var includePrerelease = Arg.Any<bool>();
        var nugetVersion = CreateNuGetVersion();
        versionProvider.GetVersionsAsync(dllName, includePrerelease, sourceUrl).Returns([nugetVersion]);

        // act
        var parameters = CreateParameters(modulePath, solutionConfiguration, [sourceUrl], [prefix]);
        await command.ExecuteAsync(parameters);

        // assert
        var actual = Project.FromFile(project.FullPath, DefaultProjectOptions);
        actual.GetItems(WellKnownItems.Reference).Should().BeEmpty();
        actual.GetItems(WellKnownItems.ModuleReference).Should().BeEmpty();

        var packageReference = actual.GetItems(WellKnownItems.PackageReference).Single();
        packageReference.EvaluatedInclude.Should().Be(dllName);
        packageReference.GetMetadataValue(WellKnownMetadata.PackageReference.Version)
            .Should().Be(nugetVersion.ToString());
    }

    [Test]
    public async Task Should_support_install_groups()
    {
        // arrange
        var (modulePath, solutionPath, solutionConfiguration) = CreateModule();

        var depName = Guid.NewGuid().ToString();
        workspace.CreateModule(depName);

        var prefix = Guid.NewGuid().ToString();
        var dllNameNet6 = string.Join('.', prefix, Guid.NewGuid());
        var dllNameNet8 = string.Join('.', prefix, Guid.NewGuid());
        var depYaml = $"""
                       full-build:
                         install:
                           - groups:
                             - target-framework: "net6.0"
                               libraries:
                                 - {dllNameNet6}.dll
                             - target-framework: "net8.0"
                               libraries:
                                 - {dllNameNet8}.dll
                       """;

        workspace.WriteYaml(depName, depYaml);

        var project = ProjectsFactory.CreateClassLib(["net6.0", "net8.0"]);
        project.AddItem(WellKnownItems.ModuleReference, depName);

        AddProjectToSolution(project, solutionPath, solutionConfiguration);

        var sourceUrl = Guid.NewGuid().ToString();
        var includePrerelease = Arg.Any<bool>();
        var nugetVersion = CreateNuGetVersion();
        versionProvider.GetVersionsAsync(dllNameNet6, includePrerelease, sourceUrl).Returns([nugetVersion]);
        versionProvider.GetVersionsAsync(dllNameNet8, includePrerelease, sourceUrl).Returns([nugetVersion]);

        // act
        var parameters = CreateParameters(modulePath, solutionConfiguration, [sourceUrl], [prefix]);
        await command.ExecuteAsync(parameters);

        // assert
        var expected = new Dictionary<string, string>
        {
            ["net6.0"] = dllNameNet6,
            ["net8.0"] = dllNameNet8,
        };

        foreach (var (framework, dll) in expected)
        {
            var projectOptions = new ProjectOptions
            {
                ProjectCollection = new ProjectCollection(),
                LoadSettings = ProjectLoadSettings.IgnoreMissingImports,
                GlobalProperties = new Dictionary<string, string>
                {
                    [WellKnownProperties.TargetFramework] = framework
                }
            };

            var actual = Project.FromFile(project.FullPath, projectOptions);
            actual.GetItems(WellKnownItems.Reference).Should().BeEmpty();
            actual.GetItems(WellKnownItems.ModuleReference).Should().BeEmpty();

            var packageReference = actual.GetItems(WellKnownItems.PackageReference).Single();
            packageReference.EvaluatedInclude.Should().Be(dll);
            packageReference.GetMetadataValue(WellKnownMetadata.PackageReference.Version)
                .Should().Be(nugetVersion.ToString());
        }
    }

    [Test]
    public async Task Should_replace_when_reference_and_module_reference_exists()
    {
        // arrange
        var (modulePath, solutionPath, solutionConfiguration) = CreateModule();

        var depName = Guid.NewGuid().ToString();
        workspace.CreateModule(depName);

        var prefix = Guid.NewGuid().ToString();
        var dllName = string.Join('.', prefix, Guid.NewGuid());
        var depYaml = $"""
                       full-build:
                         install:
                           - groups:
                             - target-framework: "net8.0"
                               libraries:
                                 - {dllName}.dll
                       """;

        workspace.WriteYaml(depName, depYaml);

        var project = ProjectsFactory.CreateClassLib(["net8.0"]);
        project.AddItem(WellKnownItems.ModuleReference, depName);
        project.AddItem(WellKnownItems.Reference, dllName);

        AddProjectToSolution(project, solutionPath, solutionConfiguration);

        var sourceUrl = Guid.NewGuid().ToString();
        var includePrerelease = Arg.Any<bool>();
        var nugetVersion = CreateNuGetVersion();
        versionProvider.GetVersionsAsync(dllName, includePrerelease, sourceUrl).Returns([nugetVersion]);

        // act
        var parameters = CreateParameters(modulePath, solutionConfiguration, [sourceUrl], [prefix]);
        await command.ExecuteAsync(parameters);

        // assert
        var actual = Project.FromFile(project.FullPath, DefaultProjectOptions);
        actual.GetItems(WellKnownItems.Reference).Should().BeEmpty();
        actual.GetItems(WellKnownItems.ModuleReference).Should().BeEmpty();
        actual.GetItems(WellKnownItems.PackageReference).Should().ContainSingle();
    }

    [Test]
    public async Task Should_ignore_local_projects()
    {
        // arrange
        var (modulePath, solutionPath, solutionConfiguration) = CreateModule();

        var depName = Guid.NewGuid().ToString();
        workspace.CreateModule(depName);

        var prefix = Guid.NewGuid().ToString();
        var dllName = string.Join('.', prefix, Guid.NewGuid());
        var depYaml = $"""
                       full-build:
                         install:
                           - {dllName}.dll
                       """;

        workspace.WriteYaml(depName, depYaml);

        var project1 = ProjectsFactory.CreateClassLib(["net8.0"]);
        project1.AddItem(WellKnownItems.ModuleReference, depName);
        project1.AddItem(WellKnownItems.Reference, dllName);

        AddProjectToSolution(project1, solutionPath, solutionConfiguration);

        var project2 = ProjectsFactory.CreateClassLib(["net8.0"]);
        AddProjectToSolution(project2, solutionPath, solutionConfiguration, projectName: dllName);

        var sourceUrl = Guid.NewGuid().ToString();
        var nugetVersion = CreateNuGetVersion();
        var includePrerelease = Arg.Any<bool>();
        versionProvider.GetVersionsAsync(dllName, includePrerelease, sourceUrl).Returns([nugetVersion]);

        // act
        var parameters = CreateParameters(modulePath, solutionConfiguration, [sourceUrl], [prefix]);
        await command.ExecuteAsync(parameters);

        // assert
        var actual = Project.FromFile(project1.FullPath, DefaultProjectOptions);
        actual.GetItems(WellKnownItems.Reference).Should().ContainSingle();
        actual.GetItems(WellKnownItems.ModuleReference).Should().ContainSingle();
    }

    [Test]
    public async Task Should_allow_local_projects()
    {
        // arrange
        var (modulePath, solutionPath, solutionConfiguration) = CreateModule();

        var project1 = ProjectsFactory.CreateClassLib(["net8.0"]);

        var prefix = Guid.NewGuid().ToString();
        var include = string.Join('.', prefix, Guid.NewGuid());
        project1.AddItem(WellKnownItems.Reference, include);

        AddProjectToSolution(project1, solutionPath, solutionConfiguration);

        var project2 = ProjectsFactory.CreateClassLib(["net8.0"]);
        AddProjectToSolution(project2, solutionPath, solutionConfiguration, projectName: include);

        var sourceUrl = Guid.NewGuid().ToString();
        var nugetVersion = CreateNuGetVersion();
        var includePrerelease = Arg.Any<bool>();
        versionProvider.GetVersionsAsync(include, includePrerelease, sourceUrl).Returns([nugetVersion]);

        // act
        var parameters = CreateParameters(modulePath, solutionConfiguration, [sourceUrl], [prefix],
            allowLocalProjects: true);

        await command.ExecuteAsync(parameters);

        // assert
        var actual = Project.FromFile(project1.FullPath, DefaultProjectOptions);
        actual.GetItems(WellKnownItems.Reference).Should().BeEmpty();
        actual.GetItems(WellKnownItems.PackageReference).Should().ContainSingle();
    }

    [Test]
    public async Task Should_throw_when_ensuring_multi_targeting_and_hint_path_is_not_valid()
    {
        // arrange
        var (modulePath, solutionPath, solutionConfiguration) = CreateModule();

        var project = ProjectsFactory.CreateClassLib(["net8.0"]);

        var prefix = Guid.NewGuid().ToString();
        var include = string.Join('.', prefix, Guid.NewGuid());
        var reference = project.AddItem(WellKnownItems.Reference, include);
        
        var hintPath = string.Join('/', Guid.NewGuid(), "netstandard2.0", include + ".dll");
        reference.AddMetadata(WellKnownMetadata.Reference.HintPath, hintPath);

        AddProjectToSolution(project, solutionPath, solutionConfiguration);

        var sourceUrl = Guid.NewGuid().ToString();
        var nugetVersion = CreateNuGetVersion();
        var includePrerelease = Arg.Any<bool>();
        versionProvider.GetVersionsAsync(include, includePrerelease, sourceUrl).Returns([nugetVersion]);

        // act
        var parameters = CreateParameters(modulePath, solutionConfiguration, [sourceUrl], [prefix],
            ensureMultitargeted: true);

        var act = async () => await command.ExecuteAsync(parameters);

        // assert
        await act.Should().ThrowAsync<Exception>();
    }

    [Test]
    public async Task Should_ignore_module_reference_when_ensuring_multi_targeting()
    {
        // arrange
        var (modulePath, solutionPath, solutionConfiguration) = CreateModule();

        var depName = Guid.NewGuid().ToString();
        workspace.CreateModule(depName);

        var prefix = Guid.NewGuid().ToString();
        var dllName = string.Join('.', prefix, Guid.NewGuid());
        var depYaml = $"""
                       full-build:
                         install:
                           - {dllName}.dll
                       """;

        workspace.WriteYaml(depName, depYaml);

        var project = ProjectsFactory.CreateClassLib(["net8.0"]);
        project.AddItem(WellKnownItems.ModuleReference, depName);

        AddProjectToSolution(project, solutionPath, solutionConfiguration);

        var sourceUrl = Guid.NewGuid().ToString();
        var includePrerelease = Arg.Any<bool>();
        var nugetVersion = CreateNuGetVersion();
        versionProvider.GetVersionsAsync(dllName, includePrerelease, sourceUrl).Returns([nugetVersion]);

        // act
        var parameters = CreateParameters(modulePath, solutionConfiguration, [sourceUrl], [prefix],
            ensureMultitargeted: true);

        var act = async () => await command.ExecuteAsync(parameters);

        // assert
        await act.Should().NotThrowAsync();
    }

    private (string ModulePath, string SolutionPath, string SolutionConfiguration) CreateModule()
    {
        var moduleName = Guid.NewGuid().ToString();
        var modulePath = workspace.CreateModule(moduleName);

        var solutionConfiguration = Guid.NewGuid().ToString();
        var solutionPath = Path.Combine(modulePath, $"{Guid.NewGuid()}.sln");
        File.Create(solutionPath);

        return (modulePath, solutionPath, solutionConfiguration);
    }

    private void AddProjectToSolution(ProjectRootElement project, string solutionPath,
        string solutionConfiguration, string? projectName = null)
    {
        projectName ??= Guid.NewGuid().ToString();
        var projectPath = Path.Combine(workspace.Path, $"{projectName}.csproj");
        project.Save(projectPath);

        var solutionProject = new SolutionProject(projectPath, projectName);
        projectProvider.AddToSolution(solutionProject, solutionPath, solutionConfiguration);
    }

    private static NuGetVersion CreateNuGetVersion()
    {
        var major = Random.Shared.Next(0, int.MaxValue);
        var minor = Random.Shared.Next(0, int.MaxValue);
        var patch = Random.Shared.Next(0, int.MaxValue);
        return new NuGetVersion(major, minor, patch);
    }

    private static Parameters CreateParameters(string targetSlnPath, string solutionConfiguration,
        IReadOnlyCollection<string> sourceUrls, IReadOnlyCollection<string> cementReferencePrefixes,
        bool allowLocalProjects = false, bool ensureMultitargeted = false)
    {
        var missingReferencesToRemove = Array.Empty<string>();
        var referencesToRemove = Array.Empty<string>();
        var failOnNotFoundPackage = true;
        var allowPrereleasePackages = false;
        var copyPrivateAssetsMetadata = false;
        var useFloatingVersions = false;

        return new Parameters(targetSlnPath, solutionConfiguration, sourceUrls.ToArray(),
            cementReferencePrefixes.ToArray(), missingReferencesToRemove, referencesToRemove, failOnNotFoundPackage,
            allowLocalProjects, allowPrereleasePackages, ensureMultitargeted, copyPrivateAssetsMetadata,
            useFloatingVersions);
    }
}

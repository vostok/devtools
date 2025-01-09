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
    private readonly IProjectsProvider projectProvider;
    private readonly IPackageVersionProvider versionProvider;
    private readonly TempWorkspace workspace;

    public ReplaceRefsCommandTests()
    {
        workspace = TempWorkspace.Create();

        projectProvider = Substitute.For<IProjectsProvider>();
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
        var moduleName = Guid.NewGuid().ToString();
        var modulePath = workspace.CreateModule(moduleName);

        var solutionConfiguration = Guid.NewGuid().ToString();
        var solutionPath = CreateSolution(modulePath);

        var project = ProjectsFactory.CreateClassLib(["net8.0"]);

        var prefix = Guid.NewGuid().ToString();
        var include = string.Join('.', prefix, Guid.NewGuid());
        project.AddItem("Reference", include);

        AddProjectToSolution(project, solutionPath, solutionConfiguration);

        var sourceUrl = Guid.NewGuid().ToString();
        var nugetVersion = CreateNuGetVersion();
        var includePrerelease = Arg.Any<bool>();
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

    [Test]
    public async Task Should_replace_module_reference()
    {
        // arrange
        var moduleName = Guid.NewGuid().ToString();
        var modulePath = workspace.CreateModule(moduleName);

        var solutionConfiguration = Guid.NewGuid().ToString();
        var solutionPath = CreateSolution(modulePath);

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
        project.AddItem("ModuleReference", depName);

        AddProjectToSolution(project, solutionPath, solutionConfiguration);

        var sourceUrl = Guid.NewGuid().ToString();
        var includePrerelease = Arg.Any<bool>();
        var nugetVersion = CreateNuGetVersion();
        versionProvider.GetVersionsAsync(dllName, includePrerelease, sourceUrl).Returns([nugetVersion]);

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
        actual.GetItems("ModuleReference").Should().BeEmpty();

        var packageReference = actual.GetItems("PackageReference").Single();
        packageReference.EvaluatedInclude.Should().Be(dllName);
        packageReference.GetMetadataValue("Version").Should().Be(nugetVersion.ToString());
    }

    [Test]
    public async Task Should_support_install_groups()
    {
        // arrange
        var moduleName = Guid.NewGuid().ToString();
        var modulePath = workspace.CreateModule(moduleName);

        var solutionConfiguration = Guid.NewGuid().ToString();
        var solutionPath = CreateSolution(modulePath);

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
        project.AddItem("ModuleReference", depName);

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
                    ["TargetFramework"] = framework
                }
            };

            var actual = Project.FromFile(project.FullPath, projectOptions);
            actual.GetItems("Reference").Should().BeEmpty();
            actual.GetItems("ModuleReference").Should().BeEmpty();

            var packageReference = actual.GetItems("PackageReference").Single();
            packageReference.EvaluatedInclude.Should().Be(dll);
            packageReference.GetMetadataValue("Version").Should().Be(nugetVersion.ToString());
        }
    }  
    
    [Test]
    public async Task Should_replace_when_reference_and_module_reference_exists()
    {
        // arrange
        var moduleName = Guid.NewGuid().ToString();
        var modulePath = workspace.CreateModule(moduleName);

        var solutionConfiguration = Guid.NewGuid().ToString();
        var solutionPath = CreateSolution(modulePath);

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
        project.AddItem("ModuleReference", depName);
        project.AddItem("Reference", dllName);

        AddProjectToSolution(project, solutionPath, solutionConfiguration);

        var sourceUrl = Guid.NewGuid().ToString();
        var includePrerelease = Arg.Any<bool>();
        var nugetVersion = CreateNuGetVersion();
        versionProvider.GetVersionsAsync(dllName, includePrerelease, sourceUrl).Returns([nugetVersion]);

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
        actual.GetItems("ModuleReference").Should().BeEmpty();
        actual.GetItems("PackageReference").Should().ContainSingle();
    }

    [Test]
    public async Task Should_ignore_local_project()
    {
        // arrange
        var moduleName = Guid.NewGuid().ToString();
        var modulePath = workspace.CreateModule(moduleName);

        var solutionConfiguration = Guid.NewGuid().ToString();
        var solutionPath = CreateSolution(modulePath);

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
        project1.AddItem("ModuleReference", depName);
        project1.AddItem("Reference", dllName);
        
        var projectName1 = Guid.NewGuid().ToString();
        var projectPath1 = Path.Combine(workspace.Path, $"{projectName1}.csproj");
        project1.Save(projectPath1);   
        
        var project2 = ProjectsFactory.CreateClassLib(["net8.0"]);
        var projectName2 = dllName;
        var projectPath2 = Path.Combine(workspace.Path, $"{projectName2}.csproj");
        project2.Save(projectPath2);
        
        var solutionProject1 = new SolutionProject(projectPath1, projectName1);
        var solutionProject2 = new SolutionProject(projectPath2, projectName2);
        projectProvider.GetFromSolution(solutionPath, solutionConfiguration)
            .Returns([solutionProject1, solutionProject2]);

        var sourceUrl = Guid.NewGuid().ToString();
        var includePrerelease = Arg.Any<bool>();
        var nugetVersion = CreateNuGetVersion();
        versionProvider.GetVersionsAsync(dllName, includePrerelease, sourceUrl).Returns([nugetVersion]);

        // act
        var parameters = CreateParameters(modulePath, solutionConfiguration, [sourceUrl], [prefix]);
        await command.ExecuteAsync(parameters);

        // assert
        var projectOptions = new ProjectOptions
        {
            ProjectCollection = new ProjectCollection(),
            LoadSettings = ProjectLoadSettings.IgnoreMissingImports
        };

        var actual = Project.FromFile(project1.FullPath, projectOptions);
        actual.GetItems("Reference").Should().ContainSingle();
        actual.GetItems("ModuleReference").Should().ContainSingle();
    }

    private static string CreateSolution(string path)
    {
        var solutionPath = Path.Combine(path, $"{Guid.NewGuid().ToString()}.sln");
        File.Create(solutionPath);
        return solutionPath;
    }

    private void AddProjectToSolution(ProjectRootElement project, string solutionPath, string solutionConfiguration)
    {
        var projectName = Guid.NewGuid().ToString();
        var projectPath = Path.Combine(workspace.Path, $"{projectName}.csproj");
        project.Save(projectPath);

        var solutionProject = new SolutionProject(projectPath, projectName);
        projectProvider.GetFromSolution(solutionPath, solutionConfiguration).Returns([solutionProject]);
    } 
    

    private static NuGetVersion CreateNuGetVersion()
    {
        var major = Random.Shared.Next(0, int.MaxValue);
        var minor = Random.Shared.Next(0, int.MaxValue);
        var patch = Random.Shared.Next(0, int.MaxValue);
        return new NuGetVersion(major, minor, patch);
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
}

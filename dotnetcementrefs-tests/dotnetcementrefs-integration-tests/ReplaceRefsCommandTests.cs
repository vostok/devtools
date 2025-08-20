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

        var solutionProject = SaveProject(project, modulePath);
        projectProvider.AddToSolution(solutionProject, solutionPath, solutionConfiguration);

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

        var solutionProject = SaveProject(project, modulePath);
        projectProvider.AddToSolution(solutionProject, solutionPath, solutionConfiguration);

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

        var solutionProject = SaveProject(project, modulePath);
        projectProvider.AddToSolution(solutionProject, solutionPath, solutionConfiguration);

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

        var solutionProject = SaveProject(project, modulePath);
        projectProvider.AddToSolution(solutionProject, solutionPath, solutionConfiguration);

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

        var solutionProject1 = SaveProject(project1, modulePath);
        projectProvider.AddToSolution(solutionProject1, solutionPath, solutionConfiguration);

        var project2 = ProjectsFactory.CreateClassLib(["net8.0"]);
        var solutionProject2 = SaveProject(project2, modulePath, projectName: dllName);
        projectProvider.AddToSolution(solutionProject2, solutionPath, solutionConfiguration);

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

        var solutionProject1 = SaveProject(project1, modulePath);
        projectProvider.AddToSolution(solutionProject1, solutionPath, solutionConfiguration);

        var project2 = ProjectsFactory.CreateClassLib(["net8.0"]);
        var solutionProject2 = SaveProject(project2, modulePath);
        projectProvider.AddToSolution(solutionProject2, solutionPath, solutionConfiguration);

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

        var solutionProject = SaveProject(project, modulePath);
        projectProvider.AddToSolution(solutionProject, solutionPath, solutionConfiguration);

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

        var solutionProject = SaveProject(project, modulePath);
        projectProvider.AddToSolution(solutionProject, solutionPath, solutionConfiguration);

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

    [Test]
    public async Task Should_respect_conditions_for_module_reference()
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
                             - target-framework: net8.0
                               libraries:
                                 - {dllName}.dll
                       """;

        workspace.WriteYaml(depName, depYaml);

        var project = ProjectsFactory.CreateClassLib(["net6.0", "net8.0"]);
        var group = project.AddItemGroup();
        group.Condition = $" '$({WellKnownProperties.TargetFramework})' == 'net8.0' ";
        group.AddItem(WellKnownItems.ModuleReference, depName);

        var solutionProject = SaveProject(project, modulePath);
        projectProvider.AddToSolution(solutionProject, solutionPath, solutionConfiguration);

        var sourceUrl = Guid.NewGuid().ToString();
        var includePrerelease = Arg.Any<bool>();
        var nugetVersion = CreateNuGetVersion();
        versionProvider.GetVersionsAsync(dllName, includePrerelease, sourceUrl).Returns([nugetVersion]);

        // act
        var parameters = CreateParameters(modulePath, solutionConfiguration, [sourceUrl], [prefix]);
        await command.ExecuteAsync(parameters);

        // assert
        var actualNet6 = Project.FromFile(project.FullPath, new ProjectOptions
        {
            ProjectCollection = new ProjectCollection(),
            LoadSettings = ProjectLoadSettings.IgnoreMissingImports,
            GlobalProperties = new Dictionary<string, string>
            {
                [WellKnownProperties.TargetFramework] = "net6.0"
            }
        });
        
        actualNet6.GetItems(WellKnownItems.PackageReference).Should().BeEmpty();  
        
        var actualNet8 = Project.FromFile(project.FullPath, new ProjectOptions
        {
            ProjectCollection = new ProjectCollection(),
            LoadSettings = ProjectLoadSettings.IgnoreMissingImports,
            GlobalProperties = new Dictionary<string, string>
            {
                [WellKnownProperties.TargetFramework] = "net8.0"
            }
        });

        actualNet8.GetItems(WellKnownItems.PackageReference).Should().ContainSingle();
    }

    [Test]
    public async Task Should_replace_module_reference_with_nuget_package_name()
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
        var packageName = Guid.NewGuid().ToString();
        var metadata = new Dictionary<string, string>
        {
            { WellKnownMetadata.Reference.NugetPackageName, packageName }
        };

        project.AddItem(WellKnownItems.ModuleReference, depName, metadata);

        var solutionProject = SaveProject(project, modulePath);
        projectProvider.AddToSolution(solutionProject, solutionPath, solutionConfiguration);

        var sourceUrl = Guid.NewGuid().ToString();
        var includePrerelease = Arg.Any<bool>();
        var nugetVersion = CreateNuGetVersion();
        versionProvider.GetVersionsAsync(packageName, includePrerelease, sourceUrl).Returns([nugetVersion]);

        // act
        var parameters = CreateParameters(modulePath, solutionConfiguration, [sourceUrl], [prefix]);
        await command.ExecuteAsync(parameters);

        // assert
        var actual = Project.FromFile(project.FullPath, DefaultProjectOptions);
        actual.GetItems(WellKnownItems.Reference).Should().BeEmpty();
        actual.GetItems(WellKnownItems.ModuleReference).Should().BeEmpty();

        var packageReference = actual.GetItems(WellKnownItems.PackageReference).Single();
        packageReference.EvaluatedInclude.Should().Be(packageName);
    }     
    
    [Test]
    public async Task Should_replace_module_reference_with_hint_path_when_package_not_found()
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

        var solutionProject = SaveProject(project, modulePath);
        projectProvider.AddToSolution(solutionProject, solutionPath, solutionConfiguration);

        var packageName = Arg.Any<string>();
        var sourceUrl = Guid.NewGuid().ToString();
        var includePrerelease = Arg.Any<bool>();
        versionProvider.GetVersionsAsync(packageName, includePrerelease, sourceUrl).Returns([]);

        // act
        var parameters = CreateParameters(modulePath, solutionConfiguration, [sourceUrl], [prefix], failOnNotFoundPackage: false);
        await command.ExecuteAsync(parameters);

        // assert
        var actual = Project.FromFile(project.FullPath, DefaultProjectOptions);
        actual.GetItems(WellKnownItems.ModuleReference).Should().BeEmpty();
        actual.GetItems(WellKnownItems.Reference).Should().ContainSingle();
        
        var reference = actual.GetItems(WellKnownItems.Reference).Single();
        var hintPath = reference.GetMetadata(WellKnownMetadata.Reference.HintPath);
        hintPath.UnevaluatedValue.Should().Be($"$(CementDir){PathUtils.Combine(depName, dllName)}.dll");
        hintPath.Xml.Condition.Should().Be("'$(TargetFramework)' == 'net8.0'");
    }   
    
    
    [Test]
    public async Task Should_replace_multitarget_module_reference_with_hint_path_when_package_not_found()
    {
        // arrange
        var (modulePath, solutionPath, solutionConfiguration) = CreateModule();

        var depName = Guid.NewGuid().ToString();
        workspace.CreateModule(depName);

        var prefix = Guid.NewGuid().ToString();
        var dllName = Guid.NewGuid().ToString();
        var depYaml = $"""
                       full-build:
                         install:
                           - groups:
                             - target-framework: "net6.0"
                               libraries:
                                 - net6.0/{dllName}.dll
                             - target-framework: "net8.0"
                               libraries:
                                 - net8.0/{dllName}.dll
                       """;

        workspace.WriteYaml(depName, depYaml);

        var project = ProjectsFactory.CreateClassLib(["net6.0", "net8.0"]);
        project.AddItem(WellKnownItems.ModuleReference, depName);

        var solutionProject = SaveProject(project, modulePath);
        projectProvider.AddToSolution(solutionProject, solutionPath, solutionConfiguration);

        var packageName = Arg.Any<string>();
        var sourceUrl = Guid.NewGuid().ToString();
        var includePrerelease = Arg.Any<bool>();
        versionProvider.GetVersionsAsync(packageName, includePrerelease, sourceUrl).Returns([]);

        // act
        var parameters = CreateParameters(modulePath, solutionConfiguration, [sourceUrl], [prefix], failOnNotFoundPackage: false);
        await command.ExecuteAsync(parameters);

        // assert
        var actual = Project.FromFile(project.FullPath, DefaultProjectOptions);
        actual.GetItems(WellKnownItems.ModuleReference).Should().BeEmpty();
        actual.GetItems(WellKnownItems.Reference).Should().ContainSingle();

        var reference = actual.GetItems(WellKnownItems.Reference).Single();
        reference.Xml.Metadata.Should().HaveCount(2);

        var net6Metadata = reference.Xml.Metadata.FirstOrDefault(x => x.Condition == "'$(TargetFramework)' == 'net6.0'");
        var net8Metadata = reference.Xml.Metadata.FirstOrDefault(x => x.Condition == "'$(TargetFramework)' == 'net8.0'");

        net6Metadata.Should().NotBeNull();
        net8Metadata.Should().NotBeNull();

        net6Metadata!.Value.Should().Be($"$(CementDir){PathUtils.Combine(depName, "net6.0", dllName)}.dll");
        net8Metadata!.Value.Should().Be($"$(CementDir){PathUtils.Combine(depName, "net8.0", dllName)}.dll");
    }     
    
    [Test]
    public async Task Should_replace_only_direct_installs_of_module_reference_with_nuget_package_name()
    {
        // arrange
        var (modulePath, solutionPath, solutionConfiguration) = CreateModule();

        var depName = Guid.NewGuid().ToString();
        workspace.CreateModule(depName);
        var prefix = Guid.NewGuid().ToString();
        var depDllName = string.Join('.', prefix, Guid.NewGuid().ToString());
        var depYaml = $"""
                       full-build:
                         install:
                           - {depDllName}.dll
                       """;

        workspace.WriteYaml(depName, depYaml);  
        
        var rootName = Guid.NewGuid().ToString();
        workspace.CreateModule(rootName);
        var rootDllName = string.Join('.', prefix, Guid.NewGuid().ToString());
        var rootYaml = $"""
                       full-build:
                         install:
                           - {rootDllName}.dll
                           - module {depName}
                       """;

        workspace.WriteYaml(rootName, rootYaml);

        var project = ProjectsFactory.CreateClassLib(["net8.0"]);
        var packageName = Guid.NewGuid().ToString();
        var metadata = new Dictionary<string, string>
        {
            { WellKnownMetadata.Reference.NugetPackageName, packageName }
        };

        project.AddItem(WellKnownItems.ModuleReference, rootName, metadata);

        var solutionProject = SaveProject(project, modulePath);
        projectProvider.AddToSolution(solutionProject, solutionPath, solutionConfiguration);

        var sourceUrl = Guid.NewGuid().ToString();
        var includePrerelease = Arg.Any<bool>();
        var nugetVersion = CreateNuGetVersion();
        versionProvider.GetVersionsAsync(packageName, includePrerelease, sourceUrl).Returns([nugetVersion]);
        versionProvider.GetVersionsAsync(depDllName, includePrerelease, sourceUrl).Returns([nugetVersion]);

        // act
        var parameters = CreateParameters(modulePath, solutionConfiguration, [sourceUrl], [prefix]);
        await command.ExecuteAsync(parameters);

        // assert
        var actual = Project.FromFile(project.FullPath, DefaultProjectOptions);
        actual.GetItems(WellKnownItems.Reference).Should().BeEmpty();
        actual.GetItems(WellKnownItems.ModuleReference).Should().BeEmpty();
        actual.GetItems(WellKnownItems.PackageReference).Should().ContainSingle(x => x.EvaluatedInclude == packageName);
        actual.GetItems(WellKnownItems.PackageReference).Should().ContainSingle(x => x.EvaluatedInclude == depDllName);
    }

    [Test]
    public async Task Should_replace_module_reference_with_allow_prerelease()
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
        var includePrerelease = true;
        var metadata = new Dictionary<string, string>
        {
            { WellKnownMetadata.Reference.NugetPackageAllowPrerelease, includePrerelease.ToString() }
        };

        project.AddItem(WellKnownItems.ModuleReference, depName, metadata);

        var solutionProject = SaveProject(project, modulePath);
        projectProvider.AddToSolution(solutionProject, solutionPath, solutionConfiguration);

        var sourceUrl = Guid.NewGuid().ToString();
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

    private static SolutionProject SaveProject(ProjectRootElement project, string path, string? projectName = null)
    {
        projectName ??= Guid.NewGuid().ToString();
        var projectPath = Path.Combine(path, $"{projectName}.csproj");
        project.Save(projectPath);

        return new SolutionProject(projectPath, projectName);
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
        bool allowLocalProjects = false, bool ensureMultitargeted = false, bool failOnNotFoundPackage = true)
    {
        var missingReferencesToRemove = Array.Empty<string>();
        var referencesToRemove = Array.Empty<string>();
        var allowPrereleasePackages = false;
        var copyPrivateAssetsMetadata = false;
        var useFloatingVersions = false;

        return new Parameters(targetSlnPath, solutionConfiguration, sourceUrls.ToArray(),
            cementReferencePrefixes.ToArray(), missingReferencesToRemove, referencesToRemove, failOnNotFoundPackage,
            allowLocalProjects, allowPrereleasePackages, ensureMultitargeted, copyPrivateAssetsMetadata,
            useFloatingVersions);
    }
}

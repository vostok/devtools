## Vostok library development conventions
<br/>

### Table of contents
[Module granularity](#module-granularity)<br/>
[Cement module requirements](#cement-module-requirements)<br/>
[Project requirements](#project-requirements)<br/>
[Code-related practices](#code-related-practices)<br/>
[Versioning](#versioning)<br/>
[Git workflow](#git-workflow)<br/>
[NuGet publishing](#nuget-publishing)<br/>
[Publishing process](#publishing-process)<br/>
[Continuous integration](#continuous-integration)<br/>

<br/>


### Module granularity
* 1 Cement module = 1 Git repository = 1 published NuGet package

* It's generally a good idea to separate public interfaces from implementations. A good example of such separation is [logging.abstractions](https://github.com/vostok/logging.abstractions) library with core interfaces like `ILog` and implementation libraries, such as [logging.console](https://github.com/vostok/logging.console) and [logging.file](https://github.com/vostok/logging.file).

* The most general guideline is to design modules with their external dependencies in mind. We want our users to be able to consume what they wish without pulling any unnecessary dependency packages. This implies that one module should not contain subsets of code with notably different dependencies: if a developer finds himself pulling a JSON parser into an otherwise pristine netstandard library while working on a feature, he should strongly consider moving that particular thing into a module of its own.

* One important exception to the rule above is when an external dependency is only needed for internal consumption and is not exposed from public API in any way. In such cases it's acceptable to just merge this dependency into library with a tool such as ILRepack. References to merged dependencies should be marked private so that they are not listed in final package.  


<br/>

### Cement module requirements
* Every Cement module should have 2 configurations defined in `module.yaml`:
	* `notests` which does not include unit tests and is a default configuration for Cement consumers.
	* `full-build` which inherits from `notests` and includes unit tests.
* The build tool in `module.yaml` should be explicitly set to `dotnet`.
* Install section of `module.yaml` should point to a binary built in release mode.

<br/>

### Project requirements

* Library solution should contain 2 projects: the main one and a unit test project.
* The main project should be named `Vostok.<Something>`.
* The tests project should be named `Vostok.<Something>.Tests`.

<br/>

#### Main project

##### Common
* Should target `netstandard2.0`.
* Should generate xml-docs file during build.
* Should include Git commit info in assembly attributes using [git-commit-to-assembly-title](https://github.com/vostok/devtools/tree/master/git-commit-to-assembly-title) target from `vostok.devtools` module.

##### Versioning
* Version in `.csproj` should be split into `VersionPrefix` and `VersionSuffix`.
* `VersionPrefix` is the one and only true source of version. It gets bumped manually in `.csproj`.
* `VersionSuffix` is not for manual editing. CI tools are responsible for changing it on prerelease builds.
* `FileVersion`, `AssemblyVersion` and `PackageVersion` are all derived from `Version`.

##### Packaging
* Packages should not be generated automatically during build.
* Packages with symbols should be generated during packing.
* There should be no separate `.nuspec` file. All packaging metadata should be kept in `.csproj`.
* Following packaging metadata should be correctly filled and maintained in `.csproj`:
	* `PackageReleaseNotes` with release changelog
	* `Authors` = `Vostok team`
	* `Product` = `Vostok`
	* `Company` = `SKB Kontur`
	* `Copyright` = `Copyright (c) 2017-2018 SKB Kontur`
	* `Description` with a gist of what the library does
	* `Title` with a short version of description
	* `RepositoryType` = `git`
	* `RepositoryUrl` with a link to library's GitHub repository.
	* `PackageProjectUrl` with a link to library's GitHub repository.
	* `PackageLicenseUrl` with a link to license text in library's GitHub repository.
	* `PackageRequireLicenseAcceptance` = `false`
	* `PackageTags` = `vostok vostok.<library name>`

##### References
* References to other Vostok modules should only be added with `cm ref add` Cement command.
* References to other NuGet packages which are then merged with ILRepack should be marked with `<PrivateAssets>all</PrivateAssets>` tag.

<br/>

#### Tests project

##### Common
* Should target `net471` and `netcoreapp2.1`.
* Should include Git commit info in assembly attributes using [git-commit-to-assembly-title](https://github.com/vostok/devtools/tree/master/git-commit-to-assembly-title) target from `vostok.devtools` module.

##### Packaging
* Should not be able to produce packages: `<IsPackable>false</IsPackable>`

##### Testing tools
* Should use NUnit3 as a main unit testing library.
* Should use FluentAssertions for.. well, assertions.
* Should use NSubstitute for mocks.

<br/><br/>

### Code-related practices
* Should use [code style](https://github.com/vostok/devtools/tree/master/code-style-csharp) provided in `vostok.devtools` module.
* Should include [JetBrains Annotations](https://www.jetbrains.com/help/resharper/Reference__Code_Annotation_Attributes.html) source as internal classes in a single file.
* Should decorate appropriate pieces of code with following JetBrains Annotations:
	* `[CanBeNull]` and `[ItemCanBeNull]`
	* `[NotNull]` and `[ItemNotNull]`
	* `[PublicApi]`
	* `[Pure]`

* Every class or interface comprising library's public API should be supplied with xml-docs.
* Every non-trivial class should be unit-tested.
* Internal classes should be exposed to test project or another libraries with `InternalsVisibleTo` assembly attribute.

<br/>

### Versioning
* Release versions should be selected and incremented according to [SemVer 1.0](https://semver.org/spec/v1.0.0.html)
* Prerelease versions should be produced by adding a suffix of `-preXXXXXX` format to an ordinary version.
	* `XXXXXX` is a zero-padded six-digit build number which grows monotonically. 


<br/>


### Git workflow

#### Branches
* Main module's branch is `master`. Only stable and tested code should get into `master`, because that's what Cement users always consume. The only exception to this rule is early stage of development (prior to version `1.0.0`).

* All of the development happens in feature branches, which get merged into `master` upon completion, successful code review and testing.

* `master` branch of a Vostok library should only depend on `master` branches of other Vostok libraries.

* Feature branch of a Vostok library may depend on feature branches of other Vostok libraries.


#### Tags
* `release/{version}` tag on a commit in `master` branch marks a stable release for publishing. The version in the tag name is cosmetic and only serves informational purpose. It's recommended to set it same to project's version. Example: `release/1.0.4`.

* `release/{version}` tags on commits in any other branch are ignored and should not exist. 

* `prerelease/{anything}` tag on a commit in a feature branch marks an unstable prerelease for publishing. Example = `prerelease/special-for-my-bro-from-other-team`.

* `prerelease/{anything}` tags on commits in `master` branch are redundant as any commit in `master` branch qualifies as prerelease by default.


<br/>


### NuGet publishing
* Both release and prerelease packages should be published to [nuget.org](https://www.nuget.org/).


<br/>


### Publishing process
#### Unstable (prerelease)
* Every push to `master` branch of a Vostok library is considered an unstable prerelease and produces a package with `-pre..` suffix in version.

* To publish a prerelease package from a feature branch, one should put a `prerelease/..` tag on one of its commits.

#### Stable (release)

* To publish a release package, one should do the following:
	* Update project release notes with new changes.
	* Put a `release/..` tag on the master branch commit.
	* Increase the `VersionPrefix` in .csproj.


<br/>


### Continuous integration
* CI happens in [AppVeyor](https://ci.appveyor.com/projects).

* AppVeyor configuration is maintained in an [appveyor.yml](../library-ci/appveyor.yml) file in `vostok.devtools` module.

* Builds and tests are run in a variety of conditions:
	* On two different operating systems: Windows and Ubuntu
	* With two different dependency resolution mechanisms:
		* From latest sources in Cement.
		* From latest versions of NuGet packages (only when publishing a package).
	* This leads to either 2 or 4 configurations (depending on whether to publish packages).

* Tests are executed using all available runtimes:
	* .NET Core and .NET Framework on Windows
	* .NET Core on Ubuntu

* If the build process fails or some tests turn red in any of these configurations, the run is considered failed and no packages can be published.

* When publishing a *release* version of NuGet package, all cement dependencies are substituted with references to latest *stable* versions of corresponding NuGet packages before build and testing.

* When publishing a *prerelease* version of NuGet package, all cement dependencies are substituted with references to latest versions (*including unstable*) of corresponding NuGet packages before build and testing.

* A notification to a special Slack channel is sent every time a package is successfully published.

* A notification to a special Slack channel is sent every time a build on `master` branch or a tagged commit fails.

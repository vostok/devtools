## Vostok library development conventions

### Preferred development tools
* Visual Studio 2017 with latest updates
* .NET Core 2.1 SDK (both x86 and x64)
* JetBrains ReSharper
* [Cement](https://github.com/skbkontur/cement) for dependency management


### Module granularity
* 1 Cement module = 1 Git repository = 1 published NuGet package


### Cement module requirements
* Every Cement module should have 2 configurations defined in `module.yaml`:
	* `notests` which does not include unit tests and is a default configuration for Cement consumers.
	* `full-build` which inherits from `notests` and includes unit tests.
- The build tool in `module.yaml` should be explicitly set to `dotnet`.
- Install section of `module.yaml` should point to a binary built in release mode.

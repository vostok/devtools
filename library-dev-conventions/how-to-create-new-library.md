## How to create a new Vostok library

This is a step-by-step guide describing the process of bootstrapping a brand new Vostok library.

* If not already, read the development conventions for [ordinary libraries](conventions.md) and [source libraries](src-libs-conventions.md). 
Most of the requirements described in there are automated by [Launchpad](../launchpad) templates, but it's essential to know what's going on.
 
* Create a new repository in [Vostok organization](https://github.com/vostok) on GitHub. 
Note that repository name should not start with `vostok` prefix (it comes from organization name).

* Create an empty `master` branch in new repository.

* Select a name for Cement module. It should be in lowercase and start with a common `vostok` prefix (like `vostok.logging.abstractions`).

* Create a Cement module with following command: `cm module add <module-name> <repo-url> --package=vostok`. 
Always use SSH-based links from GitHub (those starting with `git@`) instead of https links.

* Fetch the Cement module: `cm get <module-name>`

* Move to the module directory: `cd <module-name>`

* Install or update [Launchpad](../launchpad) to latest version.

* Bootstrap repository contents with one of the following launchpad templates:
  * `launchpad new vostok-library` for ordinary libraries
  * `launchpad new vostok-source-library` for source libraries
  * You will be prompted for two variable values:
    * `ProjectName`: this is a name of solution, project and NuGet package (like `Vostok.Logging.Abstractions`).
    * `RepositoryUrl`: this is a link to library's GitHub repository (like https://github.com/vostok/logging.abstractions)
    
* Update cement dependencies: `cm update-deps <module-name>`

* Ensure that created project builds with a `dotnet build` command and push it to `master` branch.

* Setup CI on AppVeyor:
  * Login to [AppVeyor](https://ci.appveyor.com/projects) via GitHub (select 'vostok' from available accounts).
  * Create a new project and select corresponding GitHub repository for it.
  * Go to project settings and adjust the following ones in the General tab:
    * Set `appveyor.yml` location to https://raw.githubusercontent.com/vostok/devtools/master/library-ci/appveyor.yml
    * Enable secure variables in pull requests
    * Add comma-separated tags (like 'logging' or 'configuration')
  * Start a new build and see if it succeeds.

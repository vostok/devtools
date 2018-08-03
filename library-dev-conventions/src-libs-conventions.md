## Vostok source-only library development conventions
<br/>

### Table of contents
[What is a source-only library?](#what-is-a-source-only-library)<br/>
[Cement module requirements](#cement-module-requirements)<br/>
[Project requirements](#project-requirements)<br/>
[Code-related practices](#code-related-practices)<br/>
[Change log](#change-log)<br/>

### What is a source-only library?

Sometimes there are classes that are needed in several libraries, but should not be exposed to consumers of those libraries. Such classes are grouped into source-only libraries: library projects with a Cement configuration in which all classes are marked internal and no build is performed. Dependent library projects then add links to the required source files from a source-only library instead of referencing an assembly.

In case the code in a source-only library is considered useful by itself, which is almost always, the library is built and published to NuGet like a usual Vostok library.

Source-only libraries are developed in much the same way as the usual [Vostok libraries](https://github.com/vostok/devtools/blob/master/library-dev-conventions/conventions.md), so this document only covers things that are different for source-only libraries.

A sample project can be found [here](https://github.com/vostok/devtools/tree/master/src-library-project-sample).

### Cement module requirements
* Every Cement module should have 3 configurations defined in `module.yaml`:
	* `src` where no build is performed.
	* `notests` which does not include unit tests and is a default configuration for Cement consumers.
	* `full-build` which inherits from `notests` and includes unit tests.

### Project requirements

#### Main project

* Should define symbol `MAKE_CLASSES_PUBLIC` in Release configuration.

### Code-related practices
* Every class or interface comprising library's public API must be marked internal unless the special compiler switch is set:
```
    [PublicAPI]
#if MAKE_CLASSES_PUBLIC
    public
#else
    internal
#endif
    class SampleClass
```


### Change log


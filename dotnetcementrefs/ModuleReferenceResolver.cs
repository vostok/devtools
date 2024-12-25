using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cement.Vostok.Devtools;
using Microsoft.Build.Evaluation;
using NuGet.Frameworks;

namespace dotnetcementrefs;

internal sealed class ModuleReferenceResolver
{
    private readonly InstallAssetsResolver assetsResolver;

    public ModuleReferenceResolver()
    {
        assetsResolver = new InstallAssetsResolver();
    }
    
    public Reference[] Resolve(Project project)
    {
        var moduleReferences = project.ItemsIgnoringCondition
            .Where(item => item.ItemType == "ModuleReference")
            .ToList();

        if (moduleReferences.Count == 0)
        {
            return [];
        }

        var targetFrameworksProperty = project.GetPropertyValue("TargetFramework");
        if (string.IsNullOrWhiteSpace(targetFrameworksProperty))
        {
            targetFrameworksProperty = project.GetPropertyValue("TargetFrameworks");
        }
        
        var targetFrameworks = targetFrameworksProperty.Split(';', StringSplitOptions.RemoveEmptyEntries);

        var workspacePath = FileUtilities.GetDirectoryNameOfDirectoryAbove(project.FullPath, ".cement");
        if (workspacePath == null)
        {
            throw new($"Failed to find cement workspace for '{project.FullPath}'. " +
                      $"Make sure project is inside cement workspace.");
        }
        
        var references = new List<Reference>();

        foreach (var moduleReference in moduleReferences)
        {
            foreach (var framework in targetFrameworks)
            {
                var items = Resolve(workspacePath, moduleReference, framework);
                references.AddRange(items ?? []);
            }
        }

        return references.ToArray();
    }

    private Reference[]? Resolve(string workspacePath, ProjectItem moduleReference, string framework)
    {
        var assets = assetsResolver.Resolve(workspacePath, moduleReference.EvaluatedInclude, framework);
        if (assets == null)
        {
            return null;
        }

        var references = new List<Reference>();
        
        foreach (var file in assets.InstallFiles)
        {
            var filePath = LinuxPath.ReplaceSeparator(file);

            var include = Path.GetFileNameWithoutExtension(filePath);
            var nuGetFramework = NuGetFramework.ParseFolder(framework);

            var reference = new Reference(moduleReference, include, nuGetFramework);
            references.Add(reference);
        }

        return references.ToArray();
    }
}
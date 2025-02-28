using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kontur.IDevOps.Cement.Vostok.Devtools;
using Microsoft.Build.Definition;
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
            .Where(item => item.ItemType == WellKnownItems.ModuleReference)
            .ToList();

        if (moduleReferences.Count == 0)
        {
            return [];
        }

        var targetFrameworksProperty = project.GetPropertyValue(WellKnownProperties.TargetFramework);
        if (string.IsNullOrWhiteSpace(targetFrameworksProperty))
        {
            targetFrameworksProperty = project.GetPropertyValue(WellKnownProperties.TargetFrameworks);
        }

        var targetFrameworks = targetFrameworksProperty.Split(';', StringSplitOptions.RemoveEmptyEntries);

        var workspacePath = FileUtilities.GetDirectoryNameOfDirectoryAbove(project.FullPath, ".cement");
        if (workspacePath == null)
        {
            throw new($"Failed to find cement workspace for '{project.FullPath}'. " +
                      $"Make sure project is inside cement workspace.");
        }

        var references = new List<Reference>();

        foreach (var framework in targetFrameworks)
        {
            var frameworkSpecificReferences = GetModuleReferences(project, framework)
                .ToDictionary(x => x.EvaluatedInclude);

            foreach (var moduleReference in moduleReferences)
            {
                if (!frameworkSpecificReferences.TryGetValue(moduleReference.EvaluatedInclude, out _))
                {
                    continue;
                }

                var items = Resolve(workspacePath, moduleReference, framework);
                references.AddRange(items ?? []);
            }
        }

        return references.ToArray();
    }

    private static IReadOnlyCollection<ProjectItem> GetModuleReferences(Project project, string framework)
    {
        var projectWithFramework = Project.FromProjectRootElement(project.Xml, new ProjectOptions
        {
            ProjectCollection = new ProjectCollection(),
            LoadSettings = ProjectLoadSettings.IgnoreMissingImports,
            GlobalProperties = new Dictionary<string, string>
            {
                [WellKnownProperties.TargetFramework] = framework
            }
        });

        return projectWithFramework.GetItems(WellKnownItems.ModuleReference).ToArray();
    }

    private IReadOnlyCollection<Reference>? Resolve(string workspacePath, ProjectItem moduleReference, string framework)
    {
        var direct = assetsResolver.ResolveDirect(workspacePath, moduleReference.EvaluatedInclude, framework);
        var all = assetsResolver.ResolveRecursive(workspacePath, moduleReference.EvaluatedInclude, framework);
        if (direct == null || all == null)
        {
            return null;
        }

        var references = new List<Reference>();

        foreach (var file in all.InstallFiles)
        {
            var filePath = LinuxPath.ReplaceSeparator(file);

            var include = Path.GetFileNameWithoutExtension(filePath);
            var nuGetFramework = NuGetFramework.ParseFolder(framework);

            var reference = new Reference(moduleReference, include, nuGetFramework);

            if (moduleReference.HasMetadata(WellKnownMetadata.Reference.NugetPackageName) 
                && direct.InstallFiles.Contains(file))
            {
                reference.NugetPackageName =
                    moduleReference.GetMetadataValue(WellKnownMetadata.Reference.NugetPackageName);
            }

            if (moduleReference.HasMetadata(WellKnownMetadata.Reference.NugetPackageAllowPrerelease) 
                && direct.InstallFiles.Contains(file))
            {
                var meta = moduleReference.GetMetadataValue(WellKnownMetadata.Reference.NugetPackageAllowPrerelease);
                if (bool.TryParse(meta, out var isAllowed))
                {
                    reference.NugetPackageAllowPrerelease = isAllowed;
                }
            }

            references.Add(reference);
        }

        return references.ToArray();
    }
}

using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;

namespace dotnetcementrefs;

internal sealed class ReferenceHelper
{
    public string[] AddReferences(Project project, Reference[] references)
    {
        var items = new List<string>();
        
        var referencesByInclude = references.GroupBy(x => x.Include).ToArray();

        foreach (var group in referencesByInclude)
        {
            var include = group.Key;

            var existing = project.GetItems("Reference").FirstOrDefault(x => x.EvaluatedInclude == include);
            
            if (existing != null)
            {
                continue;
            }
            
            var frameworks = group.Select(x => x.TargetFramework.GetShortFolderName()).Distinct();
            var metadata = new Dictionary<string, string>
            {
                [WellKnownMetadata.Reference.CementInstallFrameworks] = string.Join(';', frameworks),
            };

            var itemGroup = group.Select(x => x.Source.Xml.Parent).FirstOrDefault(x => x is ProjectItemGroupElement);
            if (itemGroup != null)
            {
                ((ProjectItemGroupElement)itemGroup).AddItem("Reference", include, metadata);
            }
            else
            {
                project.AddItem("Reference", include, metadata);
            }

            items.Add(include);
        }

        return items.ToArray();
    }
}
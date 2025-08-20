using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;

namespace dotnetcementrefs;

internal static class CementMetadataSerializer
{
    private const char Separator = '=';

    public static string SerializeInstallPath(IReadOnlyCollection<(string Path, NuGetFramework Framework)> items)
    {
        var lines = items.Select(x => $"{x.Framework.GetShortFolderName()}{Separator}{x.Path}");
        return string.Join(Environment.NewLine, lines);
    }

    public static IReadOnlyCollection<(string Path, NuGetFramework Framework)> DeserializeInstallPath(string metadata)
    {
        return metadata.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).Select(x =>
        {
            var parts = x.Split(Separator, count: 2);
            var framework = parts[0];
            var path = parts[1];
            return (path, NuGetFramework.Parse(framework));
        }).ToArray();
    }
}

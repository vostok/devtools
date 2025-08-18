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
            var separator = x.IndexOf(Separator);
            var framework = x.Substring(0, separator);
            var path = x.Substring(separator + 1, x.Length - separator - 1);
            return (path, NuGetFramework.Parse(framework));
        }).ToArray();
    }
}

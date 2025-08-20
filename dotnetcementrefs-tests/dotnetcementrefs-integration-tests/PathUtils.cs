using dotnetcementrefs;

namespace DotnetCementRefs.Integration.Tests;

internal static class PathUtils
{
    public static string Combine(params string[] paths)
    {
        var path = Path.Combine(paths);
        return LinuxPath.ReplaceSeparator(path);
    }
}
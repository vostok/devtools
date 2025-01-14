using System.IO;

namespace dotnetcementrefs;

internal static class LinuxPath
{
    private const char DirectorySeparator = '/';

    public static string Combine(string path1, string path2)
    {
        var path = Path.Combine(path1, path2);
        return ReplaceSeparator(path);
    }

    public static string ReplaceSeparator(string path)
    {
        return path.Replace('\\', DirectorySeparator);
    }
}
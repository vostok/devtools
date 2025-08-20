namespace dotnetcementrefs;

internal static class LinuxPath
{
    private const char DirectorySeparator = '/';

    public static string ReplaceSeparator(string path)
    {
        return path.Replace('\\', DirectorySeparator);
    }
}

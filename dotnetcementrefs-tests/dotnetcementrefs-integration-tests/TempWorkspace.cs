namespace DotnetCementRefs.Integration.Tests;

internal sealed class TempWorkspace : IDisposable
{
    private TempWorkspace(string path)
    {
        Path = path;
    }

    public string Path { get; }

    public void Dispose()
    {
        Directory.Delete(Path, true);
    }

    public static TempWorkspace Create()
    {
        var directory = Directory.CreateTempSubdirectory();
        directory.CreateSubdirectory(".cement");
        return new TempWorkspace(directory.FullName);
    }

    public string CreateModule(string name, string yamlContent = "")
    {
        var modulePath = System.IO.Path.Combine(Path, name);
        Directory.CreateDirectory(modulePath);

        var yamlPath = System.IO.Path.Combine(modulePath, "module.yaml");
        File.WriteAllText(yamlPath, yamlContent);

        return modulePath;
    }

    public void WriteYaml(string name, string yamlContent)
    {
        var modulePath = System.IO.Path.Combine(Path, name);
        var yamlPath = System.IO.Path.Combine(modulePath, "module.yaml");
        File.WriteAllText(yamlPath, yamlContent);
    }
}
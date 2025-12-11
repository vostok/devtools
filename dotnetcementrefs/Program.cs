using System;
using System.Threading.Tasks;

namespace dotnetcementrefs;

public static class Program
{
    public static async Task Main(string[] args)
    {
        await Console.Out.WriteLineAsync("⚠️ This tool is deprecated and will be removed soon.");
        await Console.Out.WriteLineAsync("⚠️ Please migrate to 'cm ref convert-to-packages'.");
        await Console.Out.WriteLineAsync("⚠️ https://kontur.ru/s/hz7QFHzAOk");
        await Console.Out.WriteLineAsync("");

        var parameters = Parameters.Parse(args);

        var projectsProvider = new ProjectsProvider();
        var packageVersionProvider = new PackageVersionProvider();
        var command = new ReplaceRefsCommand(projectsProvider, packageVersionProvider);
        
        await command.ExecuteAsync(parameters).ConfigureAwait(false);
    }
}

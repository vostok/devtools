using System.Threading.Tasks;

namespace dotnetcementrefs;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var parameters = Parameters.Parse(args);
        
        var packageVersionProvider = new PackageVersionProvider();
        var command = new ReplaceRefsCommand(packageVersionProvider);
        
        await command.ExecuteAsync(parameters).ConfigureAwait(false);
    }
}

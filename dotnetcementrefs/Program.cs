using System.Threading.Tasks;

namespace dotnetcementrefs;

public static class Program
{
    public static async Task Main(string[] args)
    {
        var parameters = new Parameters(args);
        var command = new ReplaceRefsCommand();
        await command.ExecuteAsync(parameters).ConfigureAwait(false);
    }
}

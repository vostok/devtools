using System;
using System.IO;

namespace launchpad
{
    public static partial class EntryPoint
    {
        public static void Main(string[] args)
        {
            TestPackageFatcher();
            //PrintHelp();
        }

        private static void TestPackageFatcher()
        {
            var fetcher = new PackageFetcher();
            const string packageName = "Vostok.Launchpad.Templates.Library";
            const string nugetSourceUrl = "https://api.nuget.org/v3/index.json";
            var tempDir = $"D:/temp{Guid.NewGuid().ToString().Substring(0, 8)}";
            fetcher.FetchAsync(packageName, new[] { nugetSourceUrl }, new DirectoryInfo(tempDir)).GetAwaiter().GetResult();
        }

        private static void HandleListCommand()
        {
            var config = new LaunchpadConfigProvider().GetConfig();

            foreach (var definition in config.Definitions)
            {
                Console.Out.WriteLine($"{definition.Name} from '{definition.PackageName}' package.");
            }
        }

        private static void HandleNewCommand(string templateName)
        {
            var config = new LaunchpadConfigProvider().GetConfig();

            using (var tempDirectory = new TemporaryDirectory())
            {
                var packageFetcher = new PackageFetcher();
                var specProvider = new LaunchpadSpecProvider();
                var variablesFiller = new VariableFiller();
                var templateProcessor = new TemplateProcessor();

                packageFetcher.FetchAsync(templateName, config.NugetSources, tempDirectory.Info).GetAwaiter().GetResult();

                var templateSpec = specProvider.ProvideFrom(tempDirectory.Info);
                var variables = variablesFiller.FillVariables(templateSpec.Variables);

                templateProcessor.Process(tempDirectory.Info, variables);

                // TODO(iloktionov): move everything from temp directory to Environment.CurrentDirectory
            }

            Console.Out.WriteLine("Done.");
        }

        private static void PrintHelp()
        {
            var assembly = typeof (EntryPoint);
        }
    }
}

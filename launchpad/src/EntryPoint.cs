using System;
using System.IO;

namespace launchpad
{
    public static partial class EntryPoint
    {
        public static void Main(string[] args)
        {
            PrintHelp();
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

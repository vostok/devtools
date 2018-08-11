using System;
using System.IO;

namespace launchpad
{
    public static class EntryPoint
    {
        public static void Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "list")
            {
                HandleListCommand();
                return;
            }

            if (args.Length == 2 && args[0] == "new")
            {
                HandleNewCommand(args[1]);
                return;
            }

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

                packageFetcher.Fetch(templateName, config.NugetSources, tempDirectory.FullPath);

                var templateSpec = specProvider.ProvideFrom(tempDirectory.FullPath);
                var variables = variablesFiller.FillVariables(templateSpec.Variables);

                templateProcessor.Process(tempDirectory.FullPath, variables);

                tempDirectory.MoveContentsTo(Environment.CurrentDirectory);
            }

            Console.Out.WriteLine("Done.");
        }

        private static void PrintHelp()
        {
            var assembly = typeof (EntryPoint).Assembly;

            using (var stream = assembly.GetManifestResourceStream("launchpad.help.txt"))
            using (var reader = new StreamReader(stream))
            {
                Console.Out.WriteLine(reader.ReadToEnd());
                Console.Out.WriteLine();
            }
        }
    }
}

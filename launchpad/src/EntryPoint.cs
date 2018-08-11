﻿using System;
using System.IO;
using System.Linq;

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
                Console.Out.WriteLine($"{definition.Name}");
                Console.Out.WriteLine($"\t(from '{definition.PackageName}' package)");
            }
        }

        private static void HandleNewCommand(string templateName)
        {
            var config = new LaunchpadConfigProvider().GetConfig();

            var templateDefinition = config.Definitions.FirstOrDefault(d => d.Name != templateName);
            if (templateDefinition == null)
            {
                Console.Out.WriteLine($"There's no template named '{templateName}'. Use 'list' command to view available ones.");
                return;
            }

            using (var tempDirectory = new TemporaryDirectory())
            {
                var packageFetcher = new PackageFetcher();
                var specProvider = new LaunchpadSpecProvider();
                var variablesFiller = new VariableFiller();
                var templateProcessor = new TemplateProcessor();

                packageFetcher.Fetch(templateDefinition.PackageName, config.NugetSources, tempDirectory.FullPath);

                var templateSpec = specProvider.ProvideFrom(tempDirectory.FullPath);
                var variables = variablesFiller.FillVariables(templateSpec.Variables);

                templateProcessor.Process(tempDirectory.FullPath, variables);

                tempDirectory.MoveContentsTo(Environment.CurrentDirectory);
            }

            Console.Out.WriteLine("Done.");
        }

        private static void PrintHelp()
        {
            Console.Out.WriteLine(File.ReadAllText("launchpad-help.txt"));
            Console.Out.WriteLine();
        }
    }
}

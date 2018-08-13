using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace launchpad
{
    public static class EntryPoint
    {
        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return;
            }

            switch (args[0])
            {
                case "version":
                    HandleVersionCommand(); return;

                case "config":
                    HandleConfigCommand(); return;

                case "list":
                    HandleListCommand(); return;

                case "reset-config":
                    HandleResetConfigCommand(); return;

                case "set-config":
                    if (args.Length == 2)
                    {
                        HandleSetConfigCommand(args[1]);
                        return;
                    }
                    break;

                case "new":
                    if (args.Length == 2)
                    {
                        HandleNewCommand(args[1]);
                        return;
                    }
                    break;
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

            var templateDefinition = config.Definitions.FirstOrDefault(d => d.Name == templateName);
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

                File.Delete(Path.Combine(tempDirectory.FullPath, "launchpad.json"));

                tempDirectory.CopyContentsTo(Environment.CurrentDirectory);
            }

            Console.Out.WriteLine();
            Console.Out.WriteLine("Done!");
        }

        private static void HandleVersionCommand()
        {
            var attribute = typeof (EntryPoint).Assembly.GetCustomAttribute(typeof (AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;

            var version = attribute?.InformationalVersion;

            Console.Out.WriteLine(version);
        }

        private static void HandleConfigCommand()
        {
            Console.Out.WriteLine(JsonConvert.SerializeObject(new LaunchpadConfigProvider().GetConfig(), new JsonSerializerSettings
            {
                Formatting = Formatting.Indented
            }));
        }

        private static void HandleSetConfigCommand(string source)
        {
            new LaunchpadConfigProvider().SetupConfigSource(source);
        }

        private static void HandleResetConfigCommand()
        {
            new LaunchpadConfigProvider().ResetToDefaultConfig();
        }

        private static void PrintHelp()
        {
            Console.Out.WriteLine(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launchpad-help.txt")));
            Console.Out.WriteLine();
        }
    }
}

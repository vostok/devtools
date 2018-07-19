using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Vostok.Tools.GitCommit2AssemblyTitle
{
    public class GitCommit2AssemblyTitle : Task
    {
        public override bool Execute()
        {
            void LogMessageFunction(string str, object[] args) => Log.LogMessage(str, args);

            var assemblyTitleContent = GetAssemblyTitleContent(LogMessageFunction, AssemblyVersion);

            WriteAssemblyTitleContent(LogMessageFunction, assemblyTitleContent);

            return true;
        }

        [Required]
        public string AssemblyVersion { get; set; }

        public delegate void LogMessageFunction(string command, params object[] arguments);

        private static string GetAssemblyTitleContent(LogMessageFunction log, string assemblyVersion)
        {
            var gitMessage = GetGitCommitMessage(log)?.Trim();
            var gitCommitHash = GetGitCommitHash(log)?.Trim();

            if (string.IsNullOrEmpty(gitMessage))
                log("Git commit message is empty.");

            if (string.IsNullOrEmpty(gitCommitHash))
                log("Git commit hash is empty.");

            var titleBuilder = new StringBuilder();
            var contentBuilder = new StringBuilder();

            titleBuilder.AppendLine();
            titleBuilder.AppendLine(gitMessage);
            titleBuilder.Append($"Build date: {DateTime.Now:O}");

            var title = titleBuilder.ToString().Replace("\"", "'");

            var informationalVersion = $"{assemblyVersion}-{gitCommitHash?.Substring(0, 8)}";

            contentBuilder.AppendLine("using System.Reflection;");
            contentBuilder.AppendLine();
            contentBuilder.AppendLine($@"[assembly: AssemblyTitle(@""{title}"")]");
            contentBuilder.AppendLine();
            contentBuilder.AppendLine($@"[assembly: AssemblyInformationalVersion(""{informationalVersion}"")]");

            return contentBuilder.ToString();
        }

        private static string GetGitCommitMessage(LogMessageFunction log)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? GetCommandOutput("cmd", "/C git log --pretty=\"Commit: %H %nAuthor: %an %nDate: %ai %nRef names: %d%n\" -1", log)
                : GetCommandOutput("git", "log --pretty=\"Commit: %H %nAuthor: %an %nDate: %ai %nRef names: %d%n\" -1", log);
        }

        private static string GetGitCommitHash(LogMessageFunction log)
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? GetCommandOutput("cmd", "/C git log --pretty=\"%H\" -1", log)
                : GetCommandOutput("git", "log --pretty=\"%H\" -1", log);
        }

        private static void WriteAssemblyTitleContent(LogMessageFunction log, string newContent)
        {
            const string properties = "Properties";

            var assemblyTitleFileName = Path.Combine(properties, "AssemblyTitle.cs");

            if (!Directory.Exists(properties))
                Directory.CreateDirectory(properties);

            log("{0} updated", assemblyTitleFileName);

            File.WriteAllText(assemblyTitleFileName, newContent);
        }

        private static string GetCommandOutput(string command, string args, LogMessageFunction log)
        {
            var psi = new ProcessStartInfo
            {
                Arguments = args,
                CreateNoWindow = true,
                ErrorDialog = false,
                FileName = command,
                UseShellExecute = false,
                WorkingDirectory = Directory.GetCurrentDirectory(),
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            log(command + " " + args);
            var output = new StringBuilder();
            try
            {
                var p = Process.Start(psi);
                while (!p.HasExited)
                {
                    if (!p.StandardOutput.EndOfStream || !p.StandardError.EndOfStream)
                    {
                        AppendOutput(p, output);
                    }
                    else
                    {
                        Thread.Sleep(200);
                    }
                }
                AppendOutput(p, output);
                log("exit code:" + p.ExitCode);
                return output.ToString();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static void AppendOutput(Process p, StringBuilder output)
        {
            if (!p.StandardOutput.EndOfStream)
            {
                var str = p.StandardOutput.ReadToEnd();
                output.Append(str);
                Console.WriteLine(str);
            }
            if (!p.StandardError.EndOfStream)
            {
                var str = p.StandardError.ReadToEnd();
                Console.WriteLine(str);
            }
        }
    }
}

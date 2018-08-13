using System;
using System.IO;
using Newtonsoft.Json;

namespace launchpad
{
    internal class LaunchpadConfigProvider
    {
        private const string DefaultConfigFileName = "launchpad-config.json";

        private const string EnvironmentVariableName = "LAUNCHPAD_CONFIG_PATH";

        public void SetupConfigPath(string path)
        {
            Environment.SetEnvironmentVariable(EnvironmentVariableName, path, EnvironmentVariableTarget.User);
        }

        public LaunchpadConfig GetConfig()
        {
            var pathFromEnvironment = Environment.GetEnvironmentVariable(EnvironmentVariableName);

            string fileContent;

            if (!string.IsNullOrEmpty(pathFromEnvironment) && File.Exists(pathFromEnvironment))
            {
                fileContent = File.ReadAllText(pathFromEnvironment);
            }
            else
            {
                fileContent = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultConfigFileName));
            }

            return JsonConvert.DeserializeObject<LaunchpadConfig>(fileContent);
        }
    }
}

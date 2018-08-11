using System;
using System.IO;
using Newtonsoft.Json;

namespace launchpad
{
    internal class LaunchpadConfigProvider
    {
        public LaunchpadConfig GetConfig()
        {
            var fileContent = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launchpad-config.json"));

            return JsonConvert.DeserializeObject<LaunchpadConfig>(fileContent);
        }
    }
}

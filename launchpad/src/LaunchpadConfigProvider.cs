using System.IO;
using Newtonsoft.Json;

namespace launchpad
{
    internal class LaunchpadConfigProvider
    {
        public LaunchpadConfig GetConfig()
        {
            var fileContent = File.ReadAllText("launchpad-config.json");

            return JsonConvert.DeserializeObject<LaunchpadConfig>(fileContent);
        }
    }
}

using System;
using System.IO;
using Newtonsoft.Json;

namespace launchpad
{
    internal class LaunchpadSpecProvider
    {
        public LaunchpadSpec ProvideFrom(DirectoryInfo templateDirectory)
        {
            var specStr = File.ReadAllText($"{templateDirectory.FullName}\\launchpad.json");
            return JsonConvert.DeserializeObject<LaunchpadSpec>(specStr);
        }
    }
}

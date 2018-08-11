using System.IO;
using Newtonsoft.Json;

namespace launchpad
{
    internal class LaunchpadSpecProvider
    {
        public LaunchpadSpec ProvideFrom(string directory)
        {
            var specContent = File.ReadAllText(Path.Combine(directory, "launchpad.json"));

            return JsonConvert.DeserializeObject<LaunchpadSpec>(specContent);
        }
    }
}

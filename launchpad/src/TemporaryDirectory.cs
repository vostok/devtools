using System;
using System.IO;

namespace launchpad
{
    internal class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Info = new DirectoryInfo(Path.Combine(Path.GetTempPath(), "launchpad", Guid.NewGuid().ToString().Substring(0, 8)));
            Info.Create();
            Info.Refresh();
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(FullPath, true);
            }
            catch (IOException)
            {
            }
        }

        public DirectoryInfo Info { get; }

        public string FullPath => Info.FullName;
    }
}

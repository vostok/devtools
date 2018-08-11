using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;

namespace launchpad
{
    internal class PackageFetcher
    {
        public PackageFetcher()
        {
            factory = new Repository.RepositoryFactory();
        }

        private readonly Repository.RepositoryFactory factory;

        public async Task FetchAsync(string packageName, string[] sources, DirectoryInfo targetDirectory)
        {
            foreach (var source in sources)
            {
                var fetchResult = await DownloadPackageAsync(packageName, source, targetDirectory);
                if (fetchResult.Result)
                {
                    var packageFileName = $"{fetchResult.Id}.nupkg";
                    var packageFileDestination = Path.Combine(targetDirectory.FullName, packageFileName);
                    if (!Directory.Exists(targetDirectory.FullName))
                        Directory.CreateDirectory(targetDirectory.FullName);
                    UnpackPackage(packageFileDestination);
                    return;
                }
            }
        }

        private async Task<(bool Result, PackageIdentity Id)> DownloadPackageAsync(string packageName, string source, FileSystemInfo targetDirectory)
        {
            var repository = factory.GetCoreV3(source);
            var resource = new RemoteV3FindPackageByIdResource(repository, HttpSource.Create(repository));
            var versions = await resource.GetAllVersionsAsync(packageName, new NullSourceCacheContext(), new NullLogger(), CancellationToken.None);
            var highestVersion = versions.Max();
            var packageId = new PackageIdentity(packageName, highestVersion);
            var client = await resource.GetPackageDownloaderAsync(packageId, new SourceCacheContext(), new NullLogger(), CancellationToken.None);
            var packageFileName = $"{packageId}.nupkg";
            var packageFileDestination = Path.Combine(targetDirectory.FullName, packageFileName);
            if (!Directory.Exists(targetDirectory.FullName))
                Directory.CreateDirectory(targetDirectory.FullName);
            return (await client.CopyNupkgFileToAsync(packageFileDestination, CancellationToken.None), packageId);
        }

        private static void UnpackPackage(string packagePath)
        {
            ZipFile.ExtractToDirectory(packagePath, Path.GetDirectoryName(packagePath));
        }
    }
}

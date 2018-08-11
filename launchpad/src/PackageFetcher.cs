using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging;
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
            logger = new NullLogger();
        }

        public void Fetch(string packageName, string[] sources, string targetDirectory)
        {
            FetchAsync(packageName, sources, targetDirectory).GetAwaiter().GetResult();
        }

        public async Task FetchAsync(string packageName, string[] sources, string targetDirectory)
        {
            foreach (var source in sources)
            {
                var fetchResult = await DownloadPackageAsync(packageName, source, targetDirectory);
                if (fetchResult.IsSuccessful)
                {
                    var packageFileName = $"{fetchResult.Id}.nupkg";
                    var packageFileDestination = Path.Combine(targetDirectory, packageFileName);

                    if (!Directory.Exists(targetDirectory))
                        Directory.CreateDirectory(targetDirectory);

                    UnpackPackage(targetDirectory, packageFileDestination);
                    return;
                }
            }

            throw new Exception($"Package with name '{packageName}' not found");
        }

        private async Task<(bool IsSuccessful, PackageIdentity Id)> DownloadPackageAsync(string packageName, string source, string targetDirectory)
        {
            var repository = factory.GetCoreV3(source);
            var resource = new RemoteV3FindPackageByIdResource(repository, HttpSource.Create(repository));

            var versions = (await resource.GetAllVersionsAsync(packageName, new NullSourceCacheContext(), logger, CancellationToken.None)).ToArray();
            if (versions.Length == 0)
            {
                return (false, null);
            }

            var highestVersion = versions.Max();
            var packageId = new PackageIdentity(packageName, highestVersion);
  
            var client = await resource.GetPackageDownloaderAsync(packageId, new SourceCacheContext(), logger, CancellationToken.None);

            var packageFileDestination = Path.Combine(targetDirectory, $"{packageId}.nupkg");

            if (!Directory.Exists(targetDirectory))
                Directory.CreateDirectory(targetDirectory);

            return (await client.CopyNupkgFileToAsync(packageFileDestination, CancellationToken.None), packageId);
        }

        private void UnpackPackage(string extractDirectory, string packagePath)
        {
            using (var reader = new PackageArchiveReader(packagePath))
            { 
                var files = reader.GetFiles().Where(f => f.StartsWith(ContentFiles));

                foreach (var file in files)
                {
                    var fileDestination = Path.Combine(extractDirectory, Path.GetRelativePath(ContentFiles, file));
                    reader.ExtractFile(file, fileDestination, logger);
                }
            }

            File.Delete(packagePath);
        }

        private const string ContentFiles = "contentFiles";
        private readonly Repository.RepositoryFactory factory;
        private readonly ILogger logger;
    }
}
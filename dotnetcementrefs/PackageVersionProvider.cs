using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;

namespace dotnetcementrefs;

internal class PackageVersionProvider : IPackageVersionProvider
{
    public async Task<IReadOnlyCollection<NuGetVersion>> GetVersionsAsync(string package, bool includePrerelease,
        string sourceUrl, CancellationToken cancellationToken = default)
    {
        var providers = new List<Lazy<INuGetResourceProvider>>();
        providers.AddRange(Repository.Provider.GetCoreV3());
        var packageSource = new PackageSource(sourceUrl);
        var sourceRepository = new SourceRepository(packageSource, providers);
        var metadataResource = sourceRepository.GetResource<PackageMetadataResource>();
        var searchResult = await metadataResource.GetMetadataAsync(
            package,
            includePrerelease,
            false,
            new(),
            new NullLogger(),
            cancellationToken
        ).ConfigureAwait(false);
        var versions = searchResult 
            .Where(data => data.Identity.Id == package)
            .OrderBy(data => data.Published)
            .Select(data => data.Identity.Version)
            .ToArray();

        return versions;
    }
}

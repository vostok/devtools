using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Versioning;

namespace dotnetcementrefs;

internal interface IPackageVersionProvider
{
    Task<IReadOnlyCollection<NuGetVersion>> GetVersionsAsync(string package, bool includePrerelease, string sourceUrl,
        CancellationToken cancellationToken = default);
}

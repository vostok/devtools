using System.Collections.Generic;
using System.IO;
using Stubble.Core;

namespace launchpad.extensions
{
    static class FileSystemInfoExtension
    {
        public static void SubstituteName<TSystemInfo>(this TSystemInfo directoryInfo, StubbleVisitorRenderer stubble, Dictionary<string, string> variables) where TSystemInfo : FileSystemInfo
        {
            var substitutedPath = stubble.Render(directoryInfo.FullName, variables);
            if (substitutedPath.Equals(directoryInfo.FullName))
                return;

            (directoryInfo as DirectoryInfo)?.MoveTo(substitutedPath);
            (directoryInfo as FileInfo)?.MoveTo(substitutedPath);
        }
    }
}
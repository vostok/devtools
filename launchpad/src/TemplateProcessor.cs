using System;
using System.Collections.Generic;
using System.IO;
using Stubble.Core.Builders;

namespace launchpad
{
    internal class TemplateProcessor
    {
        public void Process(DirectoryInfo directory, Dictionary<string, string> variables)
        {
            var stubble = new StubbleBuilder().Build();

            var stack = new Stack<FileSystemInfo>();

            stack.Push(directory);
            while (stack.Count > 0)
            {
                var sysInfo = stack.Pop();
                var directoryInfo = sysInfo as DirectoryInfo;
                if (directoryInfo != null)
                {   
                    directoryInfo.MoveTo(stubble.Render(directory.Name, variables));
                    foreach (var systemInfo in directoryInfo.EnumerateFileSystemInfos())
                    {
                        stack.Push(systemInfo);
                    }
                }
                var fileInfo = sysInfo as FileInfo;
                if (fileInfo != null)
                {
                    fileInfo.MoveTo(stubble.Render(directory.Name, variables));
                    stubble.Render(File.ReadAllText(fileInfo.DirectoryName), variables);
                    
                }
            }
        }
    }
}

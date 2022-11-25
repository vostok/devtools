using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;

namespace Vostok.Tools.RefAnalyzer
{
    public class RefAnalyzer : Microsoft.Build.Utilities.Task
    {
        public override bool Execute()
        {
            var project = Project.FromFile(ProjectFullPath, new ProjectOptions
            {
                LoadSettings = ProjectLoadSettings.IgnoreMissingImports
            });

            CheckForForbiddenRefs(project);
            CheckForSolutionBoundary(project);
            return true;
        }

        private void CheckForForbiddenRefs(Project project)
        {
            if (string.IsNullOrEmpty(ForbiddingRegexp)) return;

            var regex = new Regex(ForbiddingRegexp, RegexOptions.Compiled);
            var referenceNames = project
                .GetItems("Reference")
                .Select(p => p.EvaluatedInclude)
                .ToList();

            var projectReferenceNames = project
                .GetItems("ProjectReference")
                .Select(p => Path.GetFileNameWithoutExtension(p.EvaluatedInclude))
                .ToList();

            var packageReferenceNames = project
                .GetItems("PackageReference")
                .Select(p => p.EvaluatedInclude)
                .ToList();

            var restrictedReferencePaths = referenceNames
                .Concat(projectReferenceNames)
                .Concat(packageReferenceNames)
                .Where(x => regex.IsMatch(x));

            foreach (var referenceFullPath in restrictedReferencePaths)
            {
                AddBuildError($"This project can't reference '{referenceFullPath}'. Ask {Owner}.");
            }
        }

        private void CheckForSolutionBoundary(Project project)
        {
            if (!CheckSolutionBoundary) return;

            var projectReferencePaths = project
                .GetItems("ProjectReference")
                .Select(p => Path.GetFullPath(Path.Combine(project.DirectoryPath, p.EvaluatedInclude)))
                .ToList();
            foreach (var referenceFullPath in projectReferencePaths)
            {
                if (!referenceFullPath.StartsWith(SolutionDir))
                {
                    AddBuildError($"This project can't reference '{referenceFullPath}' because it is outside of solution directory '{SolutionDir}'. Ask {Owner}.");
                }
            }
        }

        private void AddBuildError(string message)
        {
            BuildEngine.LogErrorEvent(new BuildErrorEventArgs("", "", "", 0, 0, 0, 0, message, "", ""));
        }

        [Required] public string SolutionDir { get; set; }
        [Required] public string ProjectFullPath { get; set; }
        [Required] public string Owner { get; set; }
        [Required] public bool CheckSolutionBoundary { get; set; }
        public string ForbiddingRegexp { get; set; }
    }
}
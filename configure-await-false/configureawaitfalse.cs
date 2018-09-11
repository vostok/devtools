using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public static class Program
{
    private const string ConfigureAwaitIdentifier = "ConfigureAwait";

    private static readonly CSharpParseOptions ParseOptions = new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.None);

    public static void Main(string[] args)
    {
        var failedResults = 0;

        foreach (var directory in args.Select(arg => new DirectoryInfo(arg)))
        {
            if (!directory.Exists)
                continue;

            foreach (var file in directory.EnumerateFiles("*.cs", SearchOption.AllDirectories))
            {
                foreach (var result in Check(File.ReadAllText(file.FullName)))
                {
                    if (!result.HasConfigureAwaitFalse)
                    {
                        Console.Out.WriteLine("Error: missing 'ConfigureAwait(false)' in file '{0}' at line {1}.", file.Name, result.Location.GetMappedLineSpan().StartLinePosition.Line);

                        failedResults++;
                    }
                }
            }
        }

        if (failedResults > 0)
            throw new Exception($"{failedResults} await(s) without 'ConfigureAwait(false)' were found.");
    }

    private static IEnumerable<CheckResult> Check(string sourceCode)
    {
        var tree = CSharpSyntaxTree.ParseText(sourceCode, ParseOptions);

        foreach (var item in tree.GetRoot().DescendantNodesAndTokens())
        {
            if (item.IsKind(SyntaxKind.AwaitExpression))
            {
                var awaitNode = (AwaitExpressionSyntax) item.AsNode();
                yield return CheckNode(awaitNode);
            }
        }
    }

    private static CheckResult CheckNode(AwaitExpressionSyntax awaitNode)
    {
        var possibleConfigureAwait = FindExpressionForConfigureAwait(awaitNode);
        var good = possibleConfigureAwait != null && IsConfigureAwait(possibleConfigureAwait.Expression) && HasFalseArgument(possibleConfigureAwait.ArgumentList);
        return new CheckResult(good, awaitNode.GetLocation());
    }

    private static InvocationExpressionSyntax FindExpressionForConfigureAwait(SyntaxNode node)
    {
        foreach (var item in node.ChildNodes())
        {
            if (item is InvocationExpressionSyntax syntax)
                return syntax;

            return FindExpressionForConfigureAwait(item);
        }
        return null;
    }

    private static bool IsConfigureAwait(ExpressionSyntax expression)
    {
        if (!(expression is MemberAccessExpressionSyntax memberAccess))
            return false;

        if (!memberAccess.Name.Identifier.Text.Equals(ConfigureAwaitIdentifier, StringComparison.Ordinal))
            return false;

        return true;
    }

    private static bool HasFalseArgument(ArgumentListSyntax argumentList)
    {
        if (argumentList.Arguments.Count != 1)
            return false;

        if (!argumentList.Arguments[0].Expression.IsKind(SyntaxKind.FalseLiteralExpression))
            return false;

        return true;
    }

    private class CheckResult
    {
        public CheckResult(bool hasConfigureAwaitFalse, Location location)
        {
            HasConfigureAwaitFalse = hasConfigureAwaitFalse;
            Location = location;
        }

        public bool HasConfigureAwaitFalse { get; }

        public Location Location { get; }
    }
}

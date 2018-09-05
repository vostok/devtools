using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Vostok.Tools.Publicize.Roslyn
{
    /// <summary>
    /// MsBuild task type
    /// </summary>
    public class PublicizeRoslyn : Task
    {
        /// <summary>
        /// Entry point of MsBuild task
        /// </summary>
        /// <returns></returns>
        public override bool Execute()
        {
            var rewriter = new Rewriter(PublicApiAttributes);
            var destinationAsPath = new Uri(DestinationDirectory).AbsolutePath;
            if (!destinationAsPath.EndsWith("/"))
                destinationAsPath += "/";
            var projDir = new Uri(Path.GetFullPath(ProjectDirectory));
            foreach (var file in SourceFiles)
            {
                Log.LogMessage(file);
                var relativePath = projDir.MakeRelativeUri(new Uri(Path.GetFullPath(file)));
                var destination = Path.Combine(destinationAsPath, relativePath.ToString());
                Log.LogMessage(destination);
                var text = Publicizer.Process(File.ReadAllText(file), rewriter);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                File.WriteAllText(destination, text, Encoding.UTF8);
            }

            return true;
        }
        
        /// <summary>
        /// $(ProjectDir)
        /// </summary>
        [Required] public string ProjectDirectory { get; set; }
        /// <summary>
        /// *.cs files to publicize
        /// </summary>
        [Required] public string[] SourceFiles { get; set; }
        /// <summary>
        /// $(ProjectDir)obj\
        /// </summary>
        [Required] public string DestinationDirectory { get; set; }
        /// <summary>
        /// PublicAPI
        /// </summary>
        [Required] public string[] PublicApiAttributes { get; set; }
    }

    internal static class Publicizer
    {
        public static string Process(string sourceText, Rewriter rewriter=null)
        {
            var tree = CSharpSyntaxTree.ParseText(sourceText, new CSharpParseOptions(LanguageVersion.Latest));
            var newNode = (rewriter ?? new Rewriter(new[] {"PublicAPI"})).Visit(tree.GetRoot());
            return newNode.ToString();
        }
    }

    internal class Rewriter : CSharpSyntaxRewriter
    {
        private readonly string[] publicApiAttributes;

        public Rewriter(string[] publicApiAttributes)
        {
            this.publicApiAttributes = publicApiAttributes;
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            if (!ShouldPublicize(node.AttributeLists))
                return base.VisitMethodDeclaration(node);
            return node
                .WithModifiers(MakePublic(node.Modifiers));
        }

        public override SyntaxNode VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            if (!ShouldPublicize(node.AttributeLists))
                return base.VisitConstructorDeclaration(node);
            return node
                .WithModifiers(MakePublic(node.Modifiers));
        }

        public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            if (!ShouldPublicize(node.AttributeLists))
                return base.VisitPropertyDeclaration(node);
            return node
                .WithModifiers(MakePublic(node.Modifiers));
        }

        public override SyntaxNode VisitEventDeclaration(EventDeclarationSyntax node)
        {
            if (!ShouldPublicize(node.AttributeLists))
                return base.VisitEventDeclaration(node);
            return node
                .WithModifiers(MakePublic(node.Modifiers));
        }

        public override SyntaxNode VisitDelegateDeclaration(DelegateDeclarationSyntax node)
        {
            if (!ShouldPublicize(node.AttributeLists))
                return base.VisitDelegateDeclaration(node);
            return node
                .WithModifiers(MakePublic(node.Modifiers));
        }

        public override SyntaxNode VisitConversionOperatorDeclaration(ConversionOperatorDeclarationSyntax node)
        {
            if (!ShouldPublicize(node.AttributeLists))
                return base.VisitConversionOperatorDeclaration(node);
            return node
                .WithModifiers(MakePublic(node.Modifiers));
        }

        public override SyntaxNode VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            if (!ShouldPublicize(node.AttributeLists))
                return base.VisitFieldDeclaration(node);
            return node
                .WithModifiers(MakePublic(node.Modifiers));
        }

        public override SyntaxNode VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            if (!ShouldPublicize(node.AttributeLists))
                return base.VisitClassDeclaration(node);
            return node
                .WithModifiers(MakePublic(node.Modifiers));
        }

        public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node)
        {
            if (!ShouldPublicize(node.AttributeLists))
                return base.VisitStructDeclaration(node);
            return node
                .WithModifiers(MakePublic(node.Modifiers));
        }

        public override SyntaxNode VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            if (!ShouldPublicize(node.AttributeLists))
                return base.VisitInterfaceDeclaration(node);
            return node
                .WithModifiers(MakePublic(node.Modifiers));
        }

        private SyntaxTokenList MakePublic(SyntaxTokenList list)
        {
            var firstToken = list.FirstOrDefault();

            var publicKeyword = SyntaxFactory
                .Token(SyntaxKind.PublicKeyword)
                .WithLeadingTrivia(firstToken.LeadingTrivia)
                .WithTrailingTrivia(SyntaxFactory.Space);
         
            var newList = SyntaxTokenList.Create(publicKeyword);

            foreach (var token in list)
            {
                switch (token.ValueText)
                {
                    case "internal":
                    case "protected":
                    case "private":
                    case "public":
                        continue;
                    default:
                        newList = newList.Add(token);
                        break;
                }
            }

            return newList;
        }

        private bool ShouldPublicize(SyntaxList<AttributeListSyntax> list)
        {
            foreach (var attributeListSyntax in list)
            {
                foreach (var attribute in attributeListSyntax.Attributes)
                {
                    var name = attribute.Name.ToString();
                    if (publicApiAttributes.Contains(name, StringComparer.OrdinalIgnoreCase))
                        return true;
                }
            }

            return false;
        }
    }
}
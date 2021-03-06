using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OmniSharp.Abstractions.Services;
using OmniSharp.Models;

namespace OmniSharp
{
    public class StructureComputer : CSharpSyntaxWalker
    {
        private readonly Stack<FileMemberElement> _roots = new Stack<FileMemberElement>();
        private readonly IEnumerable<ISyntaxFeaturesDiscover> _featureDiscovers;
        private string _currentProject;
        private SemanticModel _semanticModel;

        public static async Task<IEnumerable<FileMemberElement>> Compute(
            IEnumerable<Document> documents,
            IEnumerable<ISyntaxFeaturesDiscover> featureDiscovers)
        {
            var root = new FileMemberElement() { ChildNodes = new List<FileMemberElement>() };
            var visitor = new StructureComputer(root, featureDiscovers);

            foreach (var document in documents)
            {
                await visitor.Process(document);
            }

            return root.ChildNodes;
        }

        public static Task<IEnumerable<FileMemberElement>> Compute(IEnumerable<Document> documents) 
            => Compute(documents, Enumerable.Empty<ISyntaxFeaturesDiscover>());

        private StructureComputer(FileMemberElement root, IEnumerable<ISyntaxFeaturesDiscover> featureDiscovers)
        {
            _featureDiscovers = featureDiscovers ?? Enumerable.Empty<ISyntaxFeaturesDiscover>();
            _roots.Push(root);
        }

        private async Task Process(Document document)
        {
            _currentProject = document.Project.Name;

            if (_featureDiscovers.Any(dis => dis.NeedSemanticModel))
            {
                _semanticModel = await document.GetSemanticModelAsync();
            }

            var syntaxRoot = await document.GetSyntaxRootAsync();
            (syntaxRoot as CSharpSyntaxNode)?.Accept(this);
        }

        private FileMemberElement AsNode(SyntaxNode node, string text, Location location)
        {
            var ret = new FileMemberElement();
            var lineSpan = location.GetLineSpan();
            ret.Projects = new List<string>();
            ret.ChildNodes = new List<FileMemberElement>();
            ret.Kind = node.Kind().ToString();
            ret.Location = new QuickFix();
            ret.Location.Text = text;
            ret.Location.Line = lineSpan.StartLinePosition.Line;
            ret.Location.Column = lineSpan.StartLinePosition.Character;
            ret.Location.EndLine = lineSpan.EndLinePosition.Line;
            ret.Location.EndColumn = lineSpan.EndLinePosition.Character;

            foreach (var featureDiscover in _featureDiscovers)
            {
                var features = featureDiscover.Discover(node, _semanticModel);
                foreach (var feature in features)
                {
                    ret.Features.Add(feature);
                }
            }

            return ret;
        }

        private FileMemberElement AsChild(SyntaxNode node, string text, Location location)
        {
            var child = AsNode(node, text, location);
            var childNodes = ((List<FileMemberElement>)_roots.Peek().ChildNodes);

            // Prevent inserting the same node multiple times
            // but make sure to insert them at the right spot
            var idx = childNodes.BinarySearch(child);
            if (idx < 0)
            {
                ((List<string>)child.Projects).Add(_currentProject);
                childNodes.Insert(~idx, child);
                return child;
            }
            else
            {
                ((List<string>)childNodes[idx].Projects).Add(_currentProject);
                return childNodes[idx];
            }
        }

        private FileMemberElement AsParent(SyntaxNode node, string text, Action fn, Location location)
        {
            var child = AsChild(node, text, location);
            _roots.Push(child);
            fn();
            _roots.Pop();
            return child;
        }

        public override void VisitClassDeclaration(ClassDeclarationSyntax node)
        {
            AsParent(node, node.Identifier.Text, () => base.VisitClassDeclaration(node), node.Identifier.GetLocation());
        }

        public override void VisitInterfaceDeclaration(InterfaceDeclarationSyntax node)
        {
            AsParent(node, node.Identifier.Text, () => base.VisitInterfaceDeclaration(node), node.Identifier.GetLocation());
        }

        public override void VisitEnumDeclaration(EnumDeclarationSyntax node)
        {
            AsParent(node, node.Identifier.Text, () => base.VisitEnumDeclaration(node), node.Identifier.GetLocation());
        }

        public override void VisitEnumMemberDeclaration(EnumMemberDeclarationSyntax node)
        {
            AsChild(node, node.Identifier.Text, node.Identifier.GetLocation());
        }

        public override void VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            AsChild(node, node.Identifier.Text, node.Identifier.GetLocation());
        }

        public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            AsChild(node, node.Identifier.Text, node.Identifier.GetLocation());
        }

        public override void VisitFieldDeclaration(FieldDeclarationSyntax node)
        {
            AsChild(node, node.Declaration.Variables.First().Identifier.Text, node.Declaration.Variables.First().Identifier.GetLocation());
        }

        public override void VisitEventFieldDeclaration(EventFieldDeclarationSyntax node)
        {
            AsChild(node, node.Declaration.Variables.First().Identifier.Text, node.Declaration.Variables.First().Identifier.GetLocation());
        }

        public override void VisitStructDeclaration(StructDeclarationSyntax node)
        {
            AsParent(node, node.Identifier.Text, () => base.VisitStructDeclaration(node), node.Identifier.GetLocation());
        }

        public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
        {
            AsChild(node, node.Identifier.Text, node.Identifier.GetLocation());
        }
    }
}
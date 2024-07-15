using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RapidScadaParser.CodeElement;
using RapidScadaParser.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidScadaParser.CodeElementProcessor
{
    internal class DelegateElementProcessor : ICodeElementProcessor
    {
        public AbsCodeElement? Process(SyntaxNode node, SemanticModel model)
        {
            if (node is DelegateDeclarationSyntax delegateDeclaration)
            {
                var symbol = model.GetDeclaredSymbol(delegateDeclaration);
                if (symbol is INamedTypeSymbol delegateSymbol)
                {
                    var delegateElement = new DelegateElement
                    {
                        Name = delegateSymbol.Name,
                        Namespace = delegateSymbol.ContainingNamespace.ToDisplayString(),
                        // FullyQualifiedName = Utility.Utility.GetFullyQualifiedName(delegateSymbol),
                        FullyQualifiedName = delegateSymbol.ToDisplayString(),
                        // RawDeclarsion = Utility.Utility.GetRawDeclaration(node, delegateSymbol),
                        RawDeclarsion = delegateDeclaration.ToString(),
                        FileLocation = delegateDeclaration.SyntaxTree.FilePath.Replace(@"\", @"/"),
                        Accessibility = delegateSymbol.DeclaredAccessibility.ToString(),
                    };

                    CreateDeclaresRelationship(delegateDeclaration, model, delegateElement);

                    return delegateElement;
                }
            }
            return null;
        }

        private void CreateDeclaresRelationship(DelegateDeclarationSyntax delegateDeclaration, SemanticModel model, DelegateElement delegateElement)
        {
            var containingType = delegateDeclaration.Parent as TypeDeclarationSyntax;
            if (containingType != null)
            {
                var symbol = model.GetDeclaredSymbol(containingType);
                if (symbol != null)
                {
                    var fullyQualifyName = Utility.Utility.GetFullyQualifiedName(symbol);

                    var relationshipCypher = @"
MATCH (delegate:Delegate), (type)
WHERE delegate.FullyQualifiedName = $delegateFQN
AND type.FullyQualifiedName = $typeFQN
MERGE (type)-[:DECLARES_DELEGATE]->(delegate)";

                    var parameters = new Dictionary<string, object>
                    {
                        {"delegateFQN", delegateElement.FullyQualifiedName},
                        {"typeFQN", fullyQualifyName}
                    };

                    delegateElement.AddRelationshipCypher(relationshipCypher, parameters);
                }
            }
        }
    }
}
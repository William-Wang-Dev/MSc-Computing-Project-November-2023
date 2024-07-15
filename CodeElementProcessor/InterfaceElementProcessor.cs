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
    internal class InterfaceElementProcessor : ICodeElementProcessor
    {
        public AbsCodeElement? Process(SyntaxNode node, SemanticModel model)
        {
            if (node is InterfaceDeclarationSyntax interfaceDeclaration)
            {
                var interfaceType = node as TypeDeclarationSyntax;
                var symbol = model.GetDeclaredSymbol(interfaceDeclaration);
                if (symbol is INamedTypeSymbol interfaceSymbol)
                {
                    var interfaceElement = new InterfaceElement
                    {
                        Name = interfaceSymbol.Name,
                        Namespace = interfaceSymbol.ContainingNamespace.ToDisplayString(),
                        FullyQualifiedName = Utility.Utility.GetFullyQualifiedName(interfaceSymbol),
                        RawDeclarsion = Utility.Utility.GetRawDeclaration(node, interfaceSymbol),
                        FileLocation = interfaceDeclaration.SyntaxTree.FilePath.Replace(@"\", @"/"),
                        Accessibility = interfaceSymbol.DeclaredAccessibility.ToString(),
                    };

                    CreateExtendsRelationship(interfaceSymbol, interfaceElement);

                    return interfaceElement;
                }
            }

            return null;
        }

        private void CreateExtendsRelationship(INamedTypeSymbol interfaceSymbol, InterfaceElement interfaceElement)
        {
            var baseInterfaces = interfaceSymbol.Interfaces;

            foreach (var baseInterfaceSymbol in baseInterfaces)
            {
                var baseInterfaceFullyQualifiedName = Utility.Utility.GetFullyQualifiedName(baseInterfaceSymbol);

                var relationshipCypher = @"
MATCH (interface:Interface), (baseInterface:Interface)
WHERE interface.FullyQualifiedName = $interfaceFQN
AND baseInterface.FullyQualifiedName = $baseInterfaceFQN
MERGE (interface)-[:EXTENDS]->(baseInterface)";

                var parameters = new Dictionary<string, object>
                {
                    {"interfaceFQN", interfaceElement.FullyQualifiedName},
                    {"baseInterfaceFQN", baseInterfaceFullyQualifiedName}
                };

                interfaceElement.AddRelationshipCypher(relationshipCypher, parameters);
            }
        }
    }
}
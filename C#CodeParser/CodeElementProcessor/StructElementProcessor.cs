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
    internal class StructElementProcessor : ICodeElementProcessor
    {
        public AbsCodeElement? Process(SyntaxNode node, SemanticModel model)
        {
            if (node is StructDeclarationSyntax structDeclaration)
            {
                var symbol = model.GetDeclaredSymbol(structDeclaration);
                if (symbol is INamedTypeSymbol structSymbol)
                {
                    var structElement = new StructElement
                    {
                        Name = structSymbol.Name,
                        Namespace = structSymbol.ContainingNamespace.ToDisplayString(),
                        FullyQualifiedName = Utility.Utility.GetFullyQualifiedName(structSymbol),
                        RawDeclarsion = Utility.Utility.GetRawDeclaration(node, structSymbol),
                        FileLocation = structDeclaration.SyntaxTree.FilePath.Replace(@"\", @"/"),
                        Accessibility = structSymbol.DeclaredAccessibility.ToString(),
                    };

                    CreateNestedRelationship(structSymbol, model, structElement); // struct - class/struct nested relationship

                    return structElement;
                }
            }
            
            return null;
        }

        private void CreateNestedRelationship(INamedTypeSymbol symbol, SemanticModel model, StructElement structElement)
        {
            // Get the containing type of the declared symbol
            var containingSymbol = symbol.ContainingType;

            // Check if the containing type is not null and is a class or struct
            if (containingSymbol != null &&
                (containingSymbol.TypeKind == TypeKind.Class || containingSymbol.TypeKind == TypeKind.Struct))
            {
                // Return the full name of the containing type
                var containingName = Utility.Utility.GetFullyQualifiedName(containingSymbol);
                var relationshipCypher = @"
MATCH (struct:Struct), (type)
WHERE struct.FullyQualifiedName = $structFQN
AND type.FullyQualifiedName = $containingFQN
MERGE (struct)-[:NESTED_IN]->(type)";

                var parameters = new Dictionary<string, object>
                {
                    {"structFQN", structElement.FullyQualifiedName},
                    {"containingFQN", containingName}
                };


                structElement.AddRelationshipCypher(relationshipCypher, parameters);
            }
        }
    }
}
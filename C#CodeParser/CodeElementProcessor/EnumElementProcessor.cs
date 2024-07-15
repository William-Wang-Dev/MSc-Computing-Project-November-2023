using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
    internal class EnumElementProcessor : ICodeElementProcessor
    {
        public AbsCodeElement? Process(SyntaxNode node, SemanticModel model)
        {
            if (node is EnumDeclarationSyntax enumDeclaration)
            {
                var symbol = model.GetDeclaredSymbol(enumDeclaration);
                if (symbol is INamedTypeSymbol enumSymbol)
                {
                    var enumElement = new EnumElement
                    {
                        Name = enumSymbol.Name,
                        Namespace = enumSymbol.ContainingNamespace.ToDisplayString(),
                        FullyQualifiedName = Utility.Utility.GetFullyQualifiedName(enumSymbol),
                        FileLocation = enumDeclaration.SyntaxTree.FilePath.Replace(@"\", @"/"),
                        // RawDefinition = node.ToString(),
                        Accessibility = enumSymbol.DeclaredAccessibility.ToString(),
                    };

                    var enumDeclarationWithoutComments = enumDeclaration.ReplaceTrivia(
                        enumDeclaration.DescendantTrivia()
                        .Where(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                         trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                         trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                         trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)),
                         (originalTrivia, rewrittenTrivia) => SyntaxFactory.Whitespace(string.Empty)
                    );

                    enumElement.RawDefinition = enumDeclarationWithoutComments.ToString();

                    CreateNestedRelationship(enumSymbol, model, enumElement);

                    return enumElement;
                }
            }
            return null;
        }

        private void CreateNestedRelationship(INamedTypeSymbol symbol, SemanticModel model, EnumElement enumElement)
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
MATCH (enum:Enum), (type)
WHERE enum.FullyQualifiedName = $enumFQN
AND type.FullyQualifiedName = $containingFQN
MERGE (enum)-[:NESTED_IN]->(type)";

                var parameters = new Dictionary<string, object>
                {
                    {"enumFQN", enumElement.FullyQualifiedName},
                    {"containingFQN", containingName}
                };

                enumElement.AddRelationshipCypher(relationshipCypher, parameters);
            }
        }
    }
}
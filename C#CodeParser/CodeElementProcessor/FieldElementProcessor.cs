using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RapidScadaParser.CodeElement;
using RapidScadaParser.CodeElementProcessor;
using RapidScadaParser.Utility;
using System.Xml.Linq;

internal class FieldElementProcessor : ICodeElementProcessor
{
    public AbsCodeElement? Process(SyntaxNode node, SemanticModel model)
    {
        if (node is FieldDeclarationSyntax fieldDeclaration)
        {
            var variableDeclaration = fieldDeclaration.Declaration;
            var variableTypeName = variableDeclaration.Type.ToString();

            Console.WriteLine($"There has ${variableDeclaration.Variables.Count} in the field symbol.");
            foreach (var variable in variableDeclaration.Variables)
            {
                var symbol = model.GetDeclaredSymbol(variable);
                if (symbol is IFieldSymbol fieldSymbol)
                {
                    ITypeSymbol fieldType = fieldSymbol.Type;

                    var fieldElement = new FieldElement
                    {
                        Name = fieldSymbol.Name,
                        Type = fieldSymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty),
                        Namespace = fieldSymbol.ContainingNamespace.ToDisplayString(),
                        FullyQualifiedName = Utility.GetFullyQualifiedName(fieldSymbol.ContainingSymbol) + '.' + fieldSymbol.Name,
                        RawDeclarsion = fieldDeclaration.ToString(),
                        FileLocation = fieldDeclaration.SyntaxTree.FilePath,
                        Accessibility = fieldSymbol.DeclaredAccessibility.ToString()
                    };

                    CreateHasFieldRelationship(fieldDeclaration, model, fieldElement);

                    return fieldElement;
                }
            }
        }

        return null;
    }

    private void CreateHasFieldRelationship(FieldDeclarationSyntax fieldDeclaration, SemanticModel model, FieldElement fieldElement)
    {
        var containingType = fieldDeclaration.Parent as TypeDeclarationSyntax;
        if (containingType != null)
        {
            var symbol = model.GetDeclaredSymbol(containingType);
            if (symbol != null)
            {
                var fullyQualifyName = Utility.GetFullyQualifiedName(symbol);
                var relationshipCypher = @"
MATCH (field:Field), (type)
WHERE field.FullyQualifiedName = $fieldFQN
AND type.FullyQualifiedName = $typeFQN
MERGE (type)-[:HAS_FIELD]->(field)";

                var parameters = new Dictionary<string, object>
                {
                    {"fieldFQN", fieldElement.FullyQualifiedName},
                    {"typeFQN", fullyQualifyName}
                };

                fieldElement.AddRelationshipCypher(relationshipCypher, parameters);
            }
        }

    }
}
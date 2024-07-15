using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RapidScadaParser.CodeElement;
using RapidScadaParser.CodeElementProcessor;
using RapidScadaParser.Utility;

internal class PropertyElementProcessor : ICodeElementProcessor
{
    public AbsCodeElement? Process(SyntaxNode node, SemanticModel model)
    {
        if (node is PropertyDeclarationSyntax propertyDeclaration)
        {
            var symbol = model.GetDeclaredSymbol(propertyDeclaration);
            if (symbol is IPropertySymbol propertySymbol)
            {
                var propertyElement = new PropertyElement
                {
                    Name = propertySymbol.Name,
                    Type = propertySymbol.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty),
                    Namespace = propertySymbol.ContainingNamespace.ToDisplayString(),
                    FullyQualifiedName = Utility.GetFullyQualifiedName(propertySymbol.ContainingSymbol) + '.' + propertySymbol.Name,
                    RawDeclarsion = propertyDeclaration.ToString(),
                    FileLocation = propertyDeclaration.SyntaxTree.FilePath,
                    Accessibility = propertySymbol.DeclaredAccessibility.ToString()
                };

                CreateHasPropertyRelationship(propertyDeclaration, model, propertyElement);

                return propertyElement;
            }
        }

        return null;
    }

    private void CreateHasPropertyRelationship(PropertyDeclarationSyntax propertyDeclaration, SemanticModel model, PropertyElement propertyElement)
    {
        var containingType = propertyDeclaration.Parent as TypeDeclarationSyntax;
        if (containingType != null)
        {
            var symbol = model.GetDeclaredSymbol(containingType);
            if (symbol != null)
            {
                var fullyQualifyName = Utility.GetFullyQualifiedName(symbol);
                var relationshipCypher = @"
MATCH (property:Property), (type)
WHERE property.FullyQualifiedName = $propertyFQN
AND type.FullyQualifiedName = $typeFQN
MERGE (type)-[:HAS_PROPERTY]->(property)";

                var parameters = new Dictionary<string, object>
                {
                    {"propertyFQN", propertyElement.FullyQualifiedName},
                    {"typeFQN", fullyQualifyName}
                };

                propertyElement.AddRelationshipCypher(relationshipCypher, parameters);
            }
        }
    }
}
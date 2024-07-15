using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RapidScadaParser.CodeElement;
using RapidScadaParser.Utility;

namespace RapidScadaParser.CodeElementProcessor
{
    internal class EventElementProcessor : ICodeElementProcessor
    {
        public AbsCodeElement? Process(SyntaxNode node, SemanticModel model)
        {
            if (node is EventFieldDeclarationSyntax eventDeclaration)
            {
                // An EventFieldDeclaration can contain multiple variables, loop through them
                foreach (var variable in eventDeclaration.Declaration.Variables)
                {
                    var eventSymbol = model.GetDeclaredSymbol(variable) as IEventSymbol;
                    if (eventSymbol != null)
                    {
                        var eventElement = new EventElement
                        {
                            Name = eventSymbol.Name,
                            Namespace = eventSymbol.ContainingNamespace.ToDisplayString(),
                            FullyQualifiedName = $"{eventSymbol.ContainingType.ToDisplayString()}.{eventSymbol.Name}",
                            RawDeclarsion = eventDeclaration.ToString(),
                            FileLocation = eventDeclaration.SyntaxTree.FilePath.Replace(@"\", "/"), // Adjusting the path for consistency
                            Accessibility = eventSymbol.DeclaredAccessibility.ToString(),
                            EventHandlerType = eventSymbol.Type.ToDisplayString()
                        };

                        // Assuming we only deal with the first variable for simplicity.
                        // You might want to adjust this behavior depending on how you want to handle events declared together.

                        CreateHasEventRelationship(eventDeclaration,model, eventElement);
                        
                        return eventElement;
                    }
                }
            }

            return null;
        }

        private void CreateHasEventRelationship(EventFieldDeclarationSyntax eventDeclaration, SemanticModel model, EventElement eventElement)
        {
            var containingType = eventDeclaration.Parent as TypeDeclarationSyntax;
            if (containingType != null)
            {
                var symbol = model.GetDeclaredSymbol(containingType);
                if (symbol != null)
                {
                    var fullyQualifyName = Utility.Utility.GetFullyQualifiedName(symbol);
                    var relationshipCypher = @"
MATCH (event:Event), (type)
WHERE event.FullyQualifiedName = $eventFQN
AND type.FullyQualifiedName = $typeFQN
MERGE (type)-[:HAS_EVENT]->(event)";

                    var parameters = new Dictionary<string, object>
                    {
                        {"eventFQN", eventElement.FullyQualifiedName},
                        {"typeFQN", fullyQualifyName}
                    };

                    eventElement.AddRelationshipCypher(relationshipCypher, parameters);
                }
            }
        }
    }
}
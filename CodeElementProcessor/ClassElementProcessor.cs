using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using RapidScadaParser.Utility;
using RapidScadaParser.CodeElement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Security.Claims;
using System.Xml.Linq;

namespace RapidScadaParser.CodeElementProcessor
{
    internal class ClassElementProcessor : ICodeElementProcessor
    {
        public AbsCodeElement? Process(SyntaxNode node, SemanticModel model)
        {
            var classType = node as TypeDeclarationSyntax;
            if (classType == null)
            { 
                return null;
            }

            var symbol = model.GetDeclaredSymbol(classType) as INamedTypeSymbol;
            if (symbol == null || symbol.TypeKind != TypeKind.Class)
            {
                return null;
            }

            var classElement = new ClassElement
            {
                Name = getClassName(symbol),
                Namespace = symbol.ContainingNamespace.ToString() ?? "GlobalNamespace",
                FullyQualifiedName = Utility.Utility.GetFullyQualifiedName(symbol),
                RawDeclarsion = Utility.Utility.GetRawDeclaration(node, symbol),
                Accessibility = symbol.DeclaredAccessibility.ToString(),
                IsAbstract = symbol.IsAbstract,
                IsSealed = symbol.IsSealed,
                IsStatic = symbol.IsStatic,
            };

            // Aggregate file locations and determine the most descriptive RawDeclaration
            foreach (var declSyntax in symbol.DeclaringSyntaxReferences.Select(ds => ds.GetSyntax()).OfType<TypeDeclarationSyntax>())
            {
                classElement.FileLocations.Add(declSyntax.SyntaxTree.FilePath.Replace(@"\", @"/"));

                var currentDeclaration = Utility.Utility.GetRawDeclaration(declSyntax, symbol);
                if (string.IsNullOrEmpty(classElement.RawDeclarsion) || currentDeclaration.Length > classElement.RawDeclarsion.Length)
                {
                    classElement.RawDeclarsion = currentDeclaration;
                }
            }

            CreateInheritsRelationship(symbol, classElement); // Class - Class (Inheritance): "INHERITS"
            CreateImplementsRelationship(symbol, classElement); // Class - Interface(Implementation): "IMPLEMENTS"
            // CreateHasFieldRelationship(classType, model, classElement); // Class - Field(Composition): "HAS_FIELD"
            // CreateHasPropertyRelationship(classType, model, classElement); // Class - Property(Composition): "HAS_PROPERTY"
            // CreateHasMethodRelationship(classType, model, classElement); // Class - Method(Composition): "HAS_METHOD"
            // CreateHasEventRelationship(classType, model, classElement); // Class - Event(Composition): "HAS_EVENT"
            // CreateDeclaresRelationship(classType, model, classElement); // Class - Delegate(Declaration): "DECLARES_DELEGATE"
            CreateInstantiatesRelationship(node, model, classElement); // Generic Class - Concrete Class (Instantiation): "INSTANTIATES"
            CreateNestedRelationship(symbol, model, classElement); // class - class/struct nested relationship
            return classElement;
        }

        private void CreateInheritsRelationship(INamedTypeSymbol classSymbol, ClassElement classElement)
        {
            var baseType = classSymbol.BaseType;
            if (baseType != null && baseType.TypeKind == TypeKind.Class)
            {
                var baseClassSymbol = baseType;
                var baseClassFullyQualifiedName = Utility.Utility.GetFullyQualifiedName(baseClassSymbol);

                var relationshipCypher = @"
MATCH (derivedClass:Class), (baseClass:Class)
WHERE derivedClass.FullyQualifiedName = $derivedClassFQN
AND baseClass.FullyQualifiedName = $baseClassFQN
MERGE (derivedClass)-[:INHERITS]->(baseClass)";

                var parameters = new Dictionary<string, object>
                {
                    {"derivedClassFQN", classElement.FullyQualifiedName},
                    {"baseClassFQN", baseClassFullyQualifiedName}
                };

                classElement.AddRelationshipCypher(relationshipCypher, parameters);
            }
        }

        private void CreateImplementsRelationship(INamedTypeSymbol classSymbol, ClassElement classElement)
        {
            var implementedInterfaces = classSymbol.Interfaces;
            foreach (var interfaceSymbol in implementedInterfaces)
            {
                var interfaceFullyQualifiedName = Utility.Utility.GetFullyQualifiedName(interfaceSymbol);
                
                var relationshipCypher = @"
MATCH (implementingClass:Class), (interface:Interface)
WHERE implementingClass.FullyQualifiedName = $classFQN
AND interface.FullyQualifiedName = $interfaceFQN
MERGE (implementingClass)-[:IMPLEMENTS]->(interface)";

                var parameters = new Dictionary<string, object>
                {
                    {"classFQN", classElement.FullyQualifiedName},
                    {"interfaceFQN", interfaceFullyQualifiedName}
                };

                classElement.AddRelationshipCypher(relationshipCypher, parameters);
            }
        }

        private void CreateHasFieldRelationship(TypeDeclarationSyntax classDeclaration, SemanticModel model, ClassElement classElement)
        {
            var fields = classDeclaration.Members.OfType<FieldDeclarationSyntax>();
            foreach (var field in fields)
            {
                var variableDeclaration = field.Declaration;
                foreach (var variable in variableDeclaration.Variables)
                {
                    var fieldSymbol = model.GetDeclaredSymbol(variable);
                    if (fieldSymbol != null)
                    {
                        var fieldElement = new FieldElement
                        {
                            Name = fieldSymbol.Name,
                            Namespace = fieldSymbol.ContainingNamespace.ToDisplayString(),
                            FullyQualifiedName = $"{classElement.FullyQualifiedName}.{fieldSymbol.Name}",
                            RawDeclarsion = field.ToString(),
                            FileLocation = field.SyntaxTree.FilePath,
                            Accessibility = fieldSymbol.DeclaredAccessibility.ToString()
                        };

                        var relationshipCypher = @"
MATCH (class:Class), (field:Field)
WHERE class.FullyQualifiedName = $classFQN
AND field.FullyQualifiedName = $fieldFQN
MERGE (class)-[:HAS_FIELD]->(field)";

                        var parameters = new Dictionary<string, object>
                        {
                            {"classFQN", classElement.FullyQualifiedName},
                            {"fieldFQN", fieldElement.FullyQualifiedName}
                        };

                        classElement.AddRelationshipCypher(relationshipCypher, parameters);
                    }
                }
            }

        }

        private void CreateHasPropertyRelationship(TypeDeclarationSyntax classDeclaration, SemanticModel model, ClassElement classElement)
        {
            var properties = classDeclaration.Members.OfType<PropertyDeclarationSyntax>();
            foreach (var property in properties)
            {
                var propertySymbol = model.GetDeclaredSymbol(property);
                if (propertySymbol != null)
                {
                    var propertyElement = new PropertyElement
                    {
                        Name = propertySymbol.Name,
                        Namespace = propertySymbol.ContainingNamespace.ToDisplayString(),
                        FullyQualifiedName = $"{classElement.FullyQualifiedName}.{propertySymbol.Name}",
                        RawDeclarsion = property.ToString(),
                        FileLocation = property.SyntaxTree.FilePath,
                        Accessibility = propertySymbol.DeclaredAccessibility.ToString()
                    };

                    var relationshipCypher = @"
MATCH (class:Class), (property:Property)
WHERE class.FullyQualifiedName = $classFQN
AND property.FullyQualifiedName = $propertyFQN
MERGE (class)-[:HAS_PROPERTY]->(property)";

                    var parameters = new Dictionary<string, object>
                    {
                        {"classFQN", classElement.FullyQualifiedName},
                        {"propertyFQN", propertyElement.FullyQualifiedName}
                    };

                    classElement.AddRelationshipCypher(relationshipCypher, parameters);
                }
            }

        }

        private void CreateHasMethodRelationship(TypeDeclarationSyntax classDeclaration, SemanticModel model, ClassElement classElement)
        {
            var methods = classDeclaration.Members.OfType<MethodDeclarationSyntax>();
            foreach (var method in methods)
            {
                var methodSymbol = model.GetDeclaredSymbol(method) as IMethodSymbol;
                if (methodSymbol != null)
                {
                    var methodElement = new MethodElement
                    {
                        Name = methodSymbol.Name,
                        ReturnType = methodSymbol.ReturnType.ToDisplayString(),
                        Namespace = methodSymbol.ContainingNamespace.ToDisplayString(),
                        FullyQualifiedName = $"{classElement.FullyQualifiedName}.{methodSymbol.Name}",
                        RawDeclaration = method.ToString(),
                        FileLocation = method.SyntaxTree.FilePath,
                        Accessibility = methodSymbol.DeclaredAccessibility.ToString(),
                        IsConstruct = methodSymbol.MethodKind == MethodKind.Constructor,
                        IsDestructor = methodSymbol.MethodKind == MethodKind.Destructor
                    };

                    var relationshipCypher = @"
MATCH (class:Class), (method:Method)
WHERE class.FullyQualifiedName = $classFQN
AND method.FullyQualifiedName = $methodFQN
MERGE (class)-[:HAS_METHOD]->(method)";

                    var parameters = new Dictionary<string, object>
                    {
                        {"classFQN", classElement.FullyQualifiedName},
                        {"methodFQN", methodElement.FullyQualifiedName}
                    };

                    classElement.AddRelationshipCypher(relationshipCypher, parameters);
                }
            }

        }

        private void CreateHasEventRelationship(TypeDeclarationSyntax classDeclaration, SemanticModel model, ClassElement classElement)
        {
            var events = classDeclaration.Members.OfType<EventFieldDeclarationSyntax>();
            foreach (var @event in events)
            {
                var eventSymbol = model.GetDeclaredSymbol(@event) as IEventSymbol;
                if (eventSymbol != null)
                {
                    var eventElement = new EventElement
                    {
                        Name = eventSymbol.Name,
                        Namespace = eventSymbol.ContainingNamespace.ToDisplayString(),
                        FullyQualifiedName = $"{classElement.FullyQualifiedName}.{eventSymbol.Name}",
                        RawDeclarsion = @event.ToString(),
                        FileLocation = @event.SyntaxTree.FilePath,
                        Accessibility = eventSymbol.DeclaredAccessibility.ToString(),
                        EventHandlerType = eventSymbol.Type.ToDisplayString()
                    };

                    var relationshipCypher = @"
MATCH (class:Class), (event:Event)
WHERE class.FullyQualifiedName = $classFQN
AND event.FullyQualifiedName = $eventFQN
MERGE (class)-[:HAS_EVENT]->(event)";

                    var parameters = new Dictionary<string, object>
                    {
                        {"classFQN", classElement.FullyQualifiedName},
                        {"eventFQN", eventElement.FullyQualifiedName}
                    };

                    classElement.AddRelationshipCypher(relationshipCypher, parameters);
                }
            }
        }

        private void CreateDeclaresRelationship(TypeDeclarationSyntax classDeclaration, SemanticModel model, ClassElement classElement)
        {
            var delegateDeclarations = classDeclaration.Members.OfType<DelegateDeclarationSyntax>();
            foreach (var delegateDeclaration in delegateDeclarations)
            {
                var delegateSymbol = model.GetDeclaredSymbol(delegateDeclaration);
                if (delegateSymbol != null)
                {
                    var delegateElement = new DelegateElement
                    {
                        Name = delegateSymbol.Name,
                        Namespace = delegateSymbol.ContainingNamespace.ToDisplayString(),
                        FullyQualifiedName = $"{classElement.FullyQualifiedName}.{delegateSymbol.Name}",
                        RawDeclarsion = delegateDeclaration.ToString(),
                        FileLocation = delegateDeclaration.SyntaxTree.FilePath,
                        Accessibility = delegateSymbol.DeclaredAccessibility.ToString()
                    };

                    var relationshipCypher = @"
MATCH (class:Class), (delegate:Delegate)
WHERE class.FullyQualifiedName = $classFQN
AND delegate.FullyQualifiedName = $delegateFQN
MERGE (class)-[:DECLARES_DELEGATE]->(delegate)";

                    var parameters = new Dictionary<string, object>
                    {
                        {"classFQN", classElement.FullyQualifiedName},
                        {"delegateFQN", delegateElement.FullyQualifiedName}
                    };

                    classElement.AddRelationshipCypher(relationshipCypher, parameters);
                }
            }

        }

        private void CreateInstantiatesRelationship(SyntaxNode node, SemanticModel model, ClassElement classElement)
        {
            if (node is ObjectCreationExpressionSyntax objectCreationExpression)
            {
                var symbolInfo = model.GetSymbolInfo(objectCreationExpression);
                var symbolType = symbolInfo.Symbol?.ContainingType;

                if (symbolType != null && symbolType.IsGenericType)
                {
                    var genericClassElement = new ClassElement
                    {
                        Name = symbolType.Name,
                        Namespace = symbolType.ContainingNamespace.ToDisplayString(),
                        FullyQualifiedName = Utility.Utility.GetFullyQualifiedName(symbolType),
                        IsAbstract = symbolType.IsAbstract,
                        IsSealed = symbolType.IsSealed,
                        IsStatic = symbolType.IsStatic
                    };

                    var concreteClassElement = new ClassElement
                    {
                        Name = classElement.Name,
                        Namespace = classElement.Namespace,
                        FullyQualifiedName = classElement.FullyQualifiedName,
                        IsAbstract = classElement.IsAbstract,
                        IsSealed = classElement.IsSealed,
                        IsStatic = classElement.IsStatic
                    };

                    var relationshipCypher = @"
MATCH (genericClass:Class), (concreteClass:Class)
WHERE genericClass.FullyQualifiedName = $genericClassFQN
AND concreteClass.FullyQualifiedName = $concreteClassFQN
MERGE (concreteClass)-[:INSTANTIATES]->(genericClass)";

                    var parameters = new Dictionary<string, object>
                    {
                        {"genericClassFQN", genericClassElement.FullyQualifiedName},
                        {"concreteClassFQN", concreteClassElement.FullyQualifiedName}
                    };

                    classElement.AddRelationshipCypher(relationshipCypher, parameters);
                }
            }
        }

        private void CreateNestedRelationship(INamedTypeSymbol classSymbol, SemanticModel model, ClassElement classElement)
        {
            // Get the containing type of the declared symbol
            var containingSymbol = classSymbol.ContainingType;

            // Check if the containing type is not null and is a class or struct
            if (containingSymbol != null &&
                (containingSymbol.TypeKind == TypeKind.Class || containingSymbol.TypeKind == TypeKind.Struct))
            {
                // Return the full name of the containing type
                var containingName = Utility.Utility.GetFullyQualifiedName(containingSymbol);
                var relationshipCypher = @"
MATCH (class:Class), (type)
WHERE class.FullyQualifiedName = $classFQN
AND type.FullyQualifiedName = $containingFQN
MERGE (class)-[:NESTED_IN]->(type)";

                var parameters = new Dictionary<string, object>
                {
                    {"classFQN", classElement.FullyQualifiedName},
                    {"containingFQN", containingName}
                };

                classElement.AddRelationshipCypher(relationshipCypher, parameters);
            }
        }

        private static string getClassName(INamedTypeSymbol symbol)
        {
            string fullyQualifiedName = Utility.Utility.GetFullyQualifiedName(symbol);
            int lastDotIndex = fullyQualifiedName.LastIndexOf('.');
            string className = lastDotIndex != -1 ? fullyQualifiedName.Substring(lastDotIndex + 1) : fullyQualifiedName;
            return className;
        }
    }
}

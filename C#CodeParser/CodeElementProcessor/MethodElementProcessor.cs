using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json.Linq;
using RapidScadaParser.CodeElement;
using RapidScadaParser.CodeElementProcessor;
using RapidScadaParser.Utility;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Metadata;
using static System.Reflection.Metadata.Ecma335.MethodBodyStreamEncoder;

internal class MethodElementProcessor : ICodeElementProcessor
{
    public AbsCodeElement? Process(SyntaxNode node, SemanticModel model)
    {
        IMethodSymbol? symbol = null;
        bool isConstructor = false;
        bool isDestructor = false;
        string? rawDeclaration = null;

        if (node is BaseMethodDeclarationSyntax baseMethodNode)
        {
            symbol = model.GetDeclaredSymbol(baseMethodNode) as IMethodSymbol;
            if (symbol == null)
            {
                return null;
            }

            // Determine if the method is a constructor or destructor
            isConstructor = symbol.MethodKind == MethodKind.Constructor;
            isDestructor = symbol.MethodKind == MethodKind.Destructor;

            // Format the raw declaration
            // rawDeclaration = baseMethodNode.ToString().Split('\n').FirstOrDefault()?.Trim();
            string methodBody = baseMethodNode.Body?.ToString() ?? string.Empty;
            string methodBodyWithoutComments = string.Empty;
            if (baseMethodNode.Body != null)
            {
                var methodBodyWithoutCommentsNode = baseMethodNode.Body.ReplaceTrivia(
                        baseMethodNode.Body.DescendantTrivia().Where(trivia =>
                            trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) ||
                            trivia.IsKind(SyntaxKind.MultiLineCommentTrivia) ||
                            trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia) ||
                            trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)),
                        (originalTrivia, rewrittenTrivia) => SyntaxFactory.Whitespace(string.Empty));

                // Convert the rewritten body (without comments) to a string
                methodBodyWithoutComments = methodBodyWithoutCommentsNode.ToFullString();
            }
            
            if (methodBody != string.Empty)
            {
                rawDeclaration = baseMethodNode.ToString().Replace(methodBody, "");
            }
            else
            {
                rawDeclaration = baseMethodNode.ToString();
            }

            var methodElement = new MethodElement
            {
                Name = GetMethodName(symbol),
                ReturnType = (!isConstructor && !isDestructor) ? symbol.ReturnType.ToString() : "",
                Namespace = symbol.ContainingNamespace.ToString(),
                FullyQualifiedName = symbol.ToDisplayString(),
                RawDeclaration = rawDeclaration ?? string.Empty,
                FileLocation = baseMethodNode.SyntaxTree.FilePath.Replace(@"\", "/"),
                Accessibility = symbol.DeclaredAccessibility.ToString(),
                IsConstruct = isConstructor,
                IsDestructor = isDestructor,
                IsAbstract = symbol.IsAbstract,
                IsVirtual = symbol.IsVirtual,
                // IsStatic = symbol.IsStatic, // Uncomment if you need to determine if the method is static
                // CodeSnippet = $"{rawDeclaration}{methodBody}",
                CodeSnippet = $"{rawDeclaration}{methodBodyWithoutComments}",
            };

            CreateHasMethodRelationship(baseMethodNode, model, methodElement);
            CreateOverridenRelationships(symbol, model, methodElement);
            CreateImplRelationships(symbol, model, methodElement);
            CreateInvokeRelationships(node, model, methodElement);
            CreateAccessRelationships(node, model, methodElement);

            var (variableContexts, invokdedMethodContexts) = GetDependencies(baseMethodNode, model, methodElement.CodeSnippet);
            methodElement.VariableContexts = variableContexts;
            methodElement.InvokedMethodContexts = invokdedMethodContexts;
            return methodElement;
        }

        return null;
    }

    private void CreateHasMethodRelationship(BaseMethodDeclarationSyntax methodNode, SemanticModel model, MethodElement methodElement)
    {
        var containingType = methodNode.Parent as TypeDeclarationSyntax;
        if (containingType != null)
        {
            var symbol = model.GetDeclaredSymbol(containingType);
            string relationshipType = methodElement.IsAbstract ? "HAS_ABSTRACT_METHOD" : "HAS_METHOD";
            if (symbol != null)
            {
                var fullyQualifyName = Utility.GetFullyQualifiedName(symbol);
                var relationshipCypher = @"
MATCH (method:Method), (type)
WHERE method.FullyQualifiedName = $methodFQN
AND type.FullyQualifiedName = $typeFQN
MERGE (type)-[:" + relationshipType + "]->(method)";

                var parameters = new Dictionary<string, object>
                {
                    {"methodFQN", methodElement.FullyQualifiedName},
                    {"typeFQN", fullyQualifyName},
                    // Note: relationshipType cannot be directly used as a parameter in Cypher for relationship types or labels.
                };

                methodElement.AddRelationshipCypher(relationshipCypher, parameters);
            }
        }
    }

    private void CreateOverridenRelationships(IMethodSymbol methodSymbol, SemanticModel model, MethodElement methodElement)
    {
        var overriddenMethodSymbol = methodSymbol.OverriddenMethod;
        if (overriddenMethodSymbol != null)
        {
            var relationshipCypher = @"
MATCH (method:Method), (overriddenMethod:Method)
WHERE method.FullyQualifiedName = $methodFQN
AND overriddenMethod.FullyQualifiedName = $overriddenMethodFQN
MERGE (method)-[:OVERRIDES]->(overriddenMethod)";

            var parameters = new Dictionary<string, object>
            {
                {"methodFQN", methodElement.FullyQualifiedName},
                {"overriddenMethodFQN", overriddenMethodSymbol.ToDisplayString()}
            };

            methodElement.AddRelationshipCypher(relationshipCypher, parameters);
        }
    }

    private void CreateImplRelationships(IMethodSymbol methodSymbol, SemanticModel model, MethodElement methodElement)
    {
        var containingType = methodSymbol.ContainingType;
        var allInterfaces = containingType.AllInterfaces;

        foreach (var interfaceSymbol in allInterfaces)
        {
            var interfaceMethods = interfaceSymbol.GetMembers().OfType<IMethodSymbol>();
            foreach (var interfaceMethod in interfaceMethods)
            {
                // Console.WriteLine($"interfaceMethod name is {interfaceMethod.ToDisplayString()}, current method is {methodSymbol.ToDisplayString()}");
                if (methodSymbol.Name == interfaceMethod.Name &&
                    methodSymbol.Parameters.Select(p => p.Type).SequenceEqual(interfaceMethod.Parameters.Select(p => p.Type)))
                {
                    // Console.WriteLine($"interfaceMethod name is {interfaceMethod.ToDisplayString()}, current method is {methodSymbol.ToDisplayString()}");
                    var relationshipCypher = @"
MATCH (method:Method), (interfaceMethod:Method)
WHERE method.FullyQualifiedName = $methodFQN
AND interfaceMethod.FullyQualifiedName = $interfaceMethodFQN
MERGE (method)-[:IMPLEMENTS]->(interfaceMethod)";

                    var parameters = new Dictionary<string, object>
                    {
                        {"methodFQN", methodElement.FullyQualifiedName},
                        {"interfaceMethodFQN", interfaceMethod.ToDisplayString()}
                    };

                    methodElement.AddRelationshipCypher(relationshipCypher, parameters);
                    break;
                }
            }
        }        
    }

    private void CreateInvokeRelationships(SyntaxNode methodNode, SemanticModel model, MethodElement methodElement)
    {
        // List<string> invokedMethods = new List<string>();

        // Find all InvocationExpressionSyntax nodes within the methodNode
        var invocations = methodNode.DescendantNodes().OfType<InvocationExpressionSyntax>();

        foreach (var invocation in invocations)
        {
            // Get the symbol for each invocation
            var invokedSymbol = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;

            // If the symbol is resolved, add it to the list
            if (invokedSymbol != null)
            {
                // string methodSignature = $"{invokedSymbol.ContainingType.Name}.{invokedSymbol.Name}";
                // invokedMethods.Add(methodSignature);
                // var fullyQualifyName = Utility.GetFullyQualifiedName(invokedSymbol);
                var relationshipCypher = @"
MATCH (method:Method), (invokedMethod:Method)
WHERE method.FullyQualifiedName = $methodFQN
AND invokedMethod.FullyQualifiedName = $invokedMethodFQN
MERGE (method)-[:INVOKES]->(invokedMethod)";

                var parameters = new Dictionary<string, object>
                {
                    {"methodFQN", methodElement.FullyQualifiedName},
                    {"invokedMethodFQN", invokedSymbol.ToDisplayString()}
                };

                methodElement.AddRelationshipCypher(relationshipCypher, parameters);
            }
        }
    }
    
    private void CreateAccessRelationships(SyntaxNode methodNode, SemanticModel model, MethodElement methodElement)
    {
        var fieldsAndProperties = new List<string>();

        var identifierNames = methodNode.DescendantNodes().OfType<IdentifierNameSyntax>();

        foreach (var identifierName in identifierNames)
        {
            var symbolInfo = model.GetSymbolInfo(identifierName);
            var symbol = symbolInfo.Symbol;

            if (symbol is IFieldSymbol fieldSymbol)
            {
                var fieldName = Utility.GetFullyQualifiedName(symbol.ContainingSymbol) + '.' + symbol.Name;
                var relationshipCypher = @"
MATCH (method:Method), (field:Field)
WHERE method.FullyQualifiedName = $methodFQN
AND field.FullyQualifiedName = $fieldFQN
MERGE (method)-[:ACCESS]->(field)";

                var parameters = new Dictionary<string, object>
                {
                    {"methodFQN", methodElement.FullyQualifiedName},
                    {"fieldFQN", fieldName}
                };

                methodElement.AddRelationshipCypher(relationshipCypher, parameters);
            }
            else if (symbol is IPropertySymbol propertySymbol)
            {
                // var propertyName = propertySymbol.Name;
                var propertyName = Utility.GetFullyQualifiedName(symbol.ContainingSymbol) + '.' + symbol.Name;
                var relationshipCypher = @"
MATCH (method:Method), (property:Property)
WHERE method.FullyQualifiedName = $methodFQN
AND property.FullyQualifiedName = $propertyFQN
MERGE (method)-[:ACCESS]->(property)";

                var parameters = new Dictionary<string, object>
                {
                    {"methodFQN", methodElement.FullyQualifiedName},
                    {"propertyFQN", propertyName}
                };

                methodElement.AddRelationshipCypher(relationshipCypher, parameters);
            }
        }
    }

    private static string GetMethodName(IMethodSymbol symbol)
    {
        //var fullSignature = symbol.ToDisplayString();
        //// get 

        //// Determine the namespace and type prefix to remove
        //var qualifiedPrefix = $"{symbol.ContainingType.ContainingNamespace.ToDisplayString()}.{symbol.ContainingType.Name}.";

        //// Remove the qualified prefix from the full signature
        //var signatureWithoutQualifiedScope = fullSignature.StartsWith(qualifiedPrefix)
        //    ? fullSignature.Substring(qualifiedPrefix.Length)
        //    : fullSignature;

        // return signatureWithoutQualifiedScope;

        //    var methodSignatureFormat = new SymbolDisplayFormat(
        //memberOptions: SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeType,
        //parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName,
        //miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        //    return symbol.ToDisplayString(methodSignatureFormat);
        var methodSignatureFormat = new SymbolDisplayFormat(
                memberOptions: SymbolDisplayMemberOptions.IncludeParameters,
                parameterOptions: SymbolDisplayParameterOptions.IncludeType,
                miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

        var methodName = symbol.Name;
        // Handling name for constructor/destructor
        if (symbol.MethodKind == MethodKind.Constructor)
        {
            methodName = symbol.ContainingType.Name; // Use containing type name for constructors
        }
        else if (symbol.MethodKind == MethodKind.Destructor)
        {
            methodName = "~" + symbol.ContainingType.Name; // Prefix with '~' for destructors
        }
        var methodParameters = symbol.Parameters.Select(parameter => parameter.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty)).ToArray();
        var methodSignature = $"{methodName}({string.Join(", ", methodParameters)})";
        return methodSignature;
    }

    private static (List<MethodElement.VariableContext>, List<MethodElement.InvokedMethodContext>) GetDependencies(BaseMethodDeclarationSyntax baseMethodNode, SemanticModel model, string methodBody)
    {
        // var dependenciesLogPath = @"E:\Dev\rapidscada\rapidscada-analysis\methodDeps.txt";

        List<MethodElement.VariableContext> variableContexts = new List<MethodElement.VariableContext>();
        List<MethodElement.InvokedMethodContext> invokedMethodContexts = new List<MethodElement.InvokedMethodContext>();

        List<string> dependencies = new List<string> { "------" + Environment.NewLine + methodBody + Environment.NewLine };

        var analyzedIdentifiers = new HashSet<string>();
        var identifierNames = baseMethodNode.DescendantNodes().OfType<IdentifierNameSyntax>();

        foreach (var identifier in identifierNames)
        {
            if (!analyzedIdentifiers.Add(identifier.Identifier.Text))
            {
                continue;
            }

            var symbol = model.GetSymbolInfo(identifier).Symbol;
            if (symbol is ILocalSymbol localSymbol || symbol is IParameterSymbol parameterSymbol ||
                symbol is IPropertySymbol propertySymbol || symbol is IFieldSymbol fieldSymbol)
            {
                bool isClassLevel = symbol is IPropertySymbol || symbol is IFieldSymbol;
                bool isLocal = !isClassLevel;
                string type = symbol switch
                {
                    ILocalSymbol ls => ls.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty),
                    IParameterSymbol ps => ps.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty),
                    IPropertySymbol ps => ps.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty),
                    IFieldSymbol fs => fs.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty),
                    _ => "unknown"
                };

                // Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty)
                variableContexts.Add(new MethodElement.VariableContext { Name = symbol.Name, Type = type, IsLocal = isLocal });
                dependencies.Add($"{symbol.Name}: {type}" + (isClassLevel ? ", class-level" : ""));
            }
        }

        var invocationExpressions = baseMethodNode.DescendantNodes().OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocationExpressions)
        {
            var methodSymbol = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (methodSymbol != null && analyzedIdentifiers.Add(invocation.ToString()))
            {
                string fullyQualifiedSignature = methodSymbol.ToDisplayString();
                invokedMethodContexts.Add(new MethodElement.InvokedMethodContext { Invocation = invocation.ToString(), FullyQualifiedSignature = fullyQualifiedSignature });
                Console.WriteLine($"Invocation = {invocation.ToString()}");
                dependencies.Add($"{invocation}: {fullyQualifiedSignature}");
            }
        }

        // File.AppendAllLines(dependenciesLogPath, dependencies);

        return (variableContexts, invokedMethodContexts);
    }


}

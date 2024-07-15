using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidScadaParser.Utility
{
    internal static class Utility
    {
        // for class, structure, delegates, enum and interface using
        public static string GetFullyQualifiedName(in ISymbol symbol)
        {
            // get Fully Qualified name without ''global::''
            return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty);
        }

        public static string GetTypeName(in INamedTypeSymbol symbol)
        {
            var fullName = GetFullyQualifiedName(symbol);
            var lastDotIndex = fullName.LastIndexOf('.');
            var typeNmae = lastDotIndex != -1
                           ? fullName.Substring(lastDotIndex + 1)
                           : fullName;
            return typeNmae;
        }

        public static string GetRawDeclaration(in SyntaxNode node, in INamedTypeSymbol symbol)
        {
            var type = node as TypeDeclarationSyntax;
            if (type == null)
            {
                return string.Empty;
            }
            var modifiers = string.Join(" ", type.Modifiers.Select(m => m.Text));
            var classKeyword = type.Keyword.Text;
            var className = GetTypeName(symbol);
            var baseTypes = type.BaseList != null
                            ? ": " + string.Join(", ", type.BaseList.Types.Select(t => t.ToString()))
                            : string.Empty;

            var rawDeclaration = $"{modifiers} {classKeyword} {className}{baseTypes}";
            return rawDeclaration;
        }

        public static string EscapeCypherString(string input)
        {
            // Escapes single quotes and backslashes for Cypher string literals
            return input.Replace("\\", "\\\\").Replace("'", "\\'");
        }

    }
}

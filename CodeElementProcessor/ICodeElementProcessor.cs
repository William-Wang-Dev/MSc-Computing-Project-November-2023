using Microsoft.CodeAnalysis;
using RapidScadaParser.CodeElement;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidScadaParser.CodeElementProcessor
{
    internal interface ICodeElementProcessor
    {
        AbsCodeElement? Process(SyntaxNode node, SemanticModel model);
    }
}

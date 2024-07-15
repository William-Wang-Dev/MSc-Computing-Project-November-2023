using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo4j.Driver;
using RapidScadaParser.CodeElement;

namespace RapidScadaParser.CodeElementValidator
{
    public class FieldValidator
    {
        public void validation(FieldElement fieldElement, IAsyncSession session)
        {
            // to check the type of FieldElement
            // match (f:Field)
            // match (c:Class)
            // where f.Type == c.FullyQualifiedName
            var typeChecking = $"match (n:)";
        }
    }
}

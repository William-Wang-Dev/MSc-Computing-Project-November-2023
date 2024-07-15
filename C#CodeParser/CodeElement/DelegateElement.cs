using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidScadaParser.CodeElement
{
    public class DelegateElement : AbsCodeElement
    {
        // public string Type = "Delegate";
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string FullyQualifiedName { get; set; } = string.Empty;
        public string RawDeclarsion { get; set; } = string.Empty;
        public string FileLocation { get; set; } = string.Empty;
        public string Accessibility { get; set; } = string.Empty;
        // public List<(string, Dictionary<string, object>)> RelationshipCyphers { get; set; } = new List<(string, Dictionary<string, object>)>();

        override public (string CypherQuery, Dictionary<string, object> Parameters) ToCypherCreateNode()
        {
            var label = "Delegate";
            var parameters = new Dictionary<string, object>
            {
                { "paramName", Name },
                { "paramLabel", label },
                { "paramNamespace", Namespace },
                { "paramRawDeclaration", RawDeclarsion },
                { "paramFileLocation", FileLocation },
                { "paramAccessibility", Accessibility },
                { "paramFullyQualifiedName", FullyQualifiedName }
            };

            var cypherQuery = $@"
MERGE (n:{label} {{ FullyQualifiedName: $paramFullyQualifiedName }})
SET n += {{
    Name: $paramName,
    Label: $paramLabel,
    Namespace: $paramNamespace,
    RawDeclaration: $paramRawDeclaration,
    FileLocation: $paramFileLocation,
    Accessibility: $paramAccessibility
}}";

            return (CypherQuery: cypherQuery, Parameters: parameters);
        }
    }
}
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidScadaParser.CodeElement
{
    public class EnumElement : AbsCodeElement
    {
        // public string Type = "Enum";
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string FullyQualifiedName { get; set; } = string.Empty;
        public string FileLocation { get; set; } = string.Empty;
        public string RawDefinition { get; set; } = string.Empty;
        public string Accessibility { get; set; } = string.Empty;
        // public List<(string, Dictionary<string, object>)> RelationshipCyphers { get; set; } = new List<string>();

        override public (string CypherQuery, Dictionary<string, object> Parameters) ToCypherCreateNode()
        {
            var label = "Enum";
            var parameters = new Dictionary<string, object>
            {
                { "paramName", Name },
                { "paramLabel", label },
                { "paramNamespace", Namespace },
                { "paramRawDefinition", RawDefinition },
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
    RawDefinition: $paramRawDefinition,
    FileLocation: $paramFileLocation,
    Accessibility: $paramAccessibility
}}";

            return (CypherQuery: cypherQuery, Parameters: parameters);
        }
    }
}
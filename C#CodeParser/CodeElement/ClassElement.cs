using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace RapidScadaParser.CodeElement
{
    public class ClassElement : AbsCodeElement
    {
        // public string Type = "Class";
        public string Name { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string FullyQualifiedName { get; set; } = string.Empty;
        public string RawDeclarsion { get; set; } = string.Empty;
        public string Accessibility { get; set; } = string.Empty;
        public bool IsAbstract { get; set; }
        public bool IsSealed { get; set; }
        public bool IsStatic { get; set; }
        // because partial class involes multiple files, so using list to store them
        public List<string> FileLocations { get; set; } = new List<string>();

        // public List<(string, Dictionary<string, object>)> RelationshipCyphers { get; set; } = new List<(string, Dictionary<string, object>)>();


        override public (string CypherQuery, Dictionary<string, object> Parameters) ToCypherCreateNode()
        {
            var label = "Class";
            // Note: No need to pre-convert FileLocations; it will be handled as a parameter
            var parameters = new Dictionary<string, object>
            {
                { "paramName", Name },
                { "paramLabel", label }, // This seems redundant since 'label' is also 'Class'. Consider if necessary.
                { "paramNamespace", Namespace },
                { "paramRawDeclaration", RawDeclarsion },
                { "paramFileLocations", FileLocations }, // Assuming FileLocations is a collection of strings
                { "paramAccessibility", Accessibility },
                { "paramIsAbstract", IsAbstract },
                { "paramIsSealed", IsSealed },
                { "paramIsStatic", IsStatic },
                { "paramFullyQualifiedName", FullyQualifiedName }
            };

            var cypherQuery = $@"
MERGE (n:{label} {{ FullyQualifiedName: $paramFullyQualifiedName }})
SET n += {{
    Name: $paramName,
    Label: $paramLabel,
    Namespace: $paramNamespace,
    RawDeclaration: $paramRawDeclaration,
    Accessibility: $paramAccessibility,
    IsAbstract: $paramIsAbstract,
    IsSealed: $paramIsSealed,
    IsStatic: $paramIsStatic
}}
SET n.FileLocation = $paramFileLocations";

            return (CypherQuery: cypherQuery, Parameters: parameters);
        }

    }
}

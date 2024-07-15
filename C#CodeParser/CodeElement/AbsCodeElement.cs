using Neo4j.Driver;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RapidScadaParser.CodeElement
{
    abstract public class AbsCodeElement
    {
        // public string Label { get; set; } = "Unknown";
        public List<(string, Dictionary<string, object>)> RelationshipCyphers { get; set; } = new List<(string, Dictionary<string, object>)>();

        public string ToStr()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        abstract public (string CypherQuery, Dictionary<string, object> Parameters) ToCypherCreateNode();

        public List<(string, Dictionary<string, object>)> ToCypherCreateRelationships()
        {
            return RelationshipCyphers;
        }

        public void AddRelationshipCypher(string cypher, Dictionary<string, object> paras)
        {
            RelationshipCyphers.Add((cypher, paras));
        }

        virtual public void Validation(IAsyncSession session, string logFile)
        {

        }
    }
}

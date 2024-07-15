using Neo4j.Driver;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using static RapidScadaParser.CodeElement.MethodElement;

namespace RapidScadaParser.CodeElement
{
    public class MethodElement : AbsCodeElement
    {
        public static HashSet<string> contexts = new HashSet<string>();
        // public string Type = "Method";
        public string Name { get; set; } = string.Empty;
        public string ReturnType { get; set; } = string.Empty;
        public string Namespace { get; set; } = string.Empty;
        public string FullyQualifiedName { get; set; } = string.Empty;
        public string RawDeclaration { get; set; } = string.Empty;
        public string FileLocation { get; set; } = string.Empty;
        public string Accessibility { get; set; } = string.Empty;
        public string CodeSnippet { get; set; } = string.Empty;
        public bool IsConstruct { get; set; }
        public bool IsDestructor { get; set; }
        public bool IsAbstract { get; set; }
        public bool IsVirtual { get; set; }

        // JSON format
        // public string ContextJSON { get; set; } = string.Empty;

        public List<VariableContext> VariableContexts { get; set; } = new List<VariableContext>();
        public List<InvokedMethodContext> InvokedMethodContexts { get; set; } = new List<InvokedMethodContext>();

        public struct VariableContext
        {
            public string Name { set; get; }
            public string Type { set; get; }
            public bool IsLocal { set; get; }
        };

        public struct InvokedMethodContext
        {
            public string Invocation { set; get; }
            public string FullyQualifiedSignature { set; get; }
        };

        override public (string CypherQuery, Dictionary<string, object> Parameters) ToCypherCreateNode()
        {
            var label = "Method";
            // Prepare parameters for the Cypher query
            var parameters = new Dictionary<string, object>
            {
                { "paramName", Name },
                { "paramLabel", label },
                { "paramReturnType", ReturnType },
                { "paramNamespace", Namespace },
                { "paramFullyQualifiedName", FullyQualifiedName },
                { "paramRawDeclaration", RawDeclaration },
                { "paramFileLocation", FileLocation },
                { "paramAccessibility", Accessibility },
                // Assuming CodeSnippet is a string property of this class
                { "paramCodeSnippet", CodeSnippet },
                { "paramIsConstruct", IsConstruct },
                { "paramIsDestructor", IsDestructor },
                { "paramIsAbstract", IsAbstract },
            };

            // Constructing the Cypher statement using parameter placeholders
            var cypherQuery = @$"MERGE (n:{label} {{ FullyQualifiedName: $paramFullyQualifiedName }})
                        SET n += {{ 
                            Name: $paramName,
                            Label: $paramLabel,
                            ReturnType: $paramReturnType, 
                            Namespace: $paramNamespace, 
                            RawDeclaration: $paramRawDeclaration, 
                            FileLocation: $paramFileLocation, 
                            Accessibility: $paramAccessibility, 
                            CodeSnippet: $paramCodeSnippet, 
                            IsConstruct: $paramIsConstruct, 
                            IsDestructor: $paramIsDestructor, 
                            IsAbstract: $paramIsAbstract 
                        }}";

            return (CypherQuery: cypherQuery, Parameters: parameters);
        }

        public override void Validation(IAsyncSession session, string logFile)
        {
            // var logInfos = new List<string>();
            var vContextRecords = new HashSet<VariableContext>();
            var logInfos = new HashSet<string>();
            
            var mark = FileLocation + ":" + FullyQualifiedName + ": ";
            // to find out varaible type not in database
            foreach (var variableContext in VariableContexts)
            {
                string query = @"
match (v)
where v.FullyQualifiedName = $contextType
return count(v) AS nodeCount";

                var parameters = new Dictionary<string, object>
                {
                    { "contextType",  variableContext.Type },
                };

                var result = session.ExecuteReadAsync(async tx =>
                {
                    var res = await tx.RunAsync(query, parameters);
                    var record = await res.SingleAsync();
                    return record["nodeCount"].As<int>();
                }).GetAwaiter().GetResult();

                if (result == 0)
                {
                    // the varialbe cant be found in db. logging it.
                    // File.AppendAllText(logFile, mark + " variable: " + variableContext.Name + " : " + variableContext.Type);
                    // logInfos.Add("contextType: " + $"{variableContext.Type}    " + mark + " variableName: " + variableContext.Name + " : " + variableContext.Type);
                    contexts.Add(variableContext.Type);
                }

                vContextRecords.Add(variableContext);
            }

            string vSetQuery = @"
match (m:Method)
where m.FullyQualifiedName = $MFQN
set m.VariableContext = $context
";
            var vContextParas = new Dictionary<string, object>
                {
                    { "MFQN",  FullyQualifiedName },
                    { "context", JsonConvert.SerializeObject(vContextRecords, Formatting.Indented)},
                };

            

            session.RunAsync(vSetQuery, vContextParas).Wait();


            // to find out what method not in database
            var mContextRecords = new HashSet<InvokedMethodContext>();
            foreach (var invokedMethodContext in InvokedMethodContexts)
            {
                string query = @"
match (v:Method)
where v.FullyQualifiedName = $FQN
return count(v) AS nodeCount";

                var parameters = new Dictionary<string, object>
                {
                    { "FQN",  invokedMethodContext.FullyQualifiedSignature },
                };

                var result = session.ExecuteWriteAsync(async tx =>
                {
                    var res = await tx.RunAsync(query, parameters);
                    var record = await res.SingleAsync();
                    return record["nodeCount"].As<int>();
                }).GetAwaiter().GetResult();

                if (result == 0)
                {
                    // the varialbe cant be found in db. logging it.
                    // File.AppendAllText(logFile, mark + " method: " + invokedMethodContext.FullyQualifiedSignature);
                    // logInfos.Add("methodContextType: " + $"{invokedMethodContext.FullyQualifiedSignature}    " + mark + " method: " + invokedMethodContext.FullyQualifiedSignature);
                    contexts.Add(invokedMethodContext.FullyQualifiedSignature);
                    
                }

                mContextRecords.Add(invokedMethodContext);
            }

            string mSetQuery = @"
match (m:Method)
where m.FullyQualifiedName = $MFQN
set m.InvokedContext = $context
";
            var mContextParas = new Dictionary<string, object>
                {
                    { "MFQN",  FullyQualifiedName },
                    { "context", JsonConvert.SerializeObject(mContextRecords, Formatting.Indented)},
                };

            Console.WriteLine($"mContextRecords = {JsonConvert.SerializeObject(mContextRecords, Formatting.Indented)}");
            session.RunAsync(mSetQuery, mContextParas).Wait();

            // File.AppendAllLines(logFile, logInfos);
        }
    }
}

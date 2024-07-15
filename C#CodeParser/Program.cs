using CommandLine;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Neo4j.Driver;
using Newtonsoft.Json;
using RapidScadaParser;
using RapidScadaParser.CodeElement;
using System.Collections.Generic;


class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("hello, world!");

        string savedFilePath = string.Empty;
        List<string> filesToProcess = new List<string>();

        // cmd argus parsing
        Parser.Default.ParseArguments<Options>(args)
              .WithParsed(options =>
              {
                  savedFilePath = options.SavedFilePath;
                  filesToProcess = options.FilesToProcess?.ToList() ?? new List<string>();
              })
              .WithNotParsed(errors =>
              {
                  Console.WriteLine("Invalid command-line arguments:");
                  foreach (var error in errors)
                  {
                      Console.WriteLine(error.ToString());
                  }

                  // Print usage information
                  Console.WriteLine("Usage: program.exe -s <save-file> [-f <file1> <file2> ...]");
                  Console.WriteLine("Options:");
                  Console.WriteLine("  -s, --save-file    Required. Path to the saved file.");
                  Console.WriteLine("  -f, --files        Optional. Files to process.");

                  Environment.Exit(1); // Exit the program with an error code
              });

        // savedFilePath = $@"{savedFilePath}\test.txt";
        var logFile = $@"{savedFilePath}\test.txt";
        File.WriteAllText(logFile, string.Empty); // Clears the file
        Console.WriteLine($"saved path is {logFile}");
        
        /// start parsing logical ///
        var nodeCreater = new NodeCreater();
        ParseCode(nodeCreater, filesToProcess).Wait();
        var results = nodeCreater.CodeElementNodes;
        foreach (var result in results)
        {
            string str = result.ToStr() + Environment.NewLine;
            File.AppendAllText(logFile, str + Environment.NewLine);
        }

        /// write to database
        var uri = "bolt://localhost:7687";
        using (var driver = GraphDatabase.Driver(uri, AuthTokens.None))
        using (var session = driver.AsyncSession())
        {
            PurgeDataBase(session).Wait();
            GenerateNodesToDB(session, results).Wait();
            CreateRelationship(session, results).Wait();
            Validation(session, results, $@"{savedFilePath}\outsideDep.txt");
        }
        List<string> sortedList = MethodElement.contexts.ToList();
        sortedList.Sort();
        File.AppendAllLines($@"{savedFilePath}\outsideDep.txt", sortedList);
    }

    static async Task PurgeDataBase(IAsyncSession session)
    {
        var deleteAndCount = await session.ExecuteWriteAsync(async tx =>
        {
            IResultCursor result = await tx.RunAsync(
                @"MATCH (n) DETACH DELETE n RETURN count(n)");

            var record = await result.SingleAsync();
            return record["count(n)"].As<int>();
        });

        Console.WriteLine($"Deleted {deleteAndCount} nodes.");
    }

    static async Task ParseCode(NodeCreater nodeCreater, List<string> solutionFiles)
    {
        HashSet<string> ProcessedDoc = new HashSet<string>();
        
        MSBuildLocator.RegisterDefaults();
        using var workspace = MSBuildWorkspace.Create();

        foreach (var solutionFile in solutionFiles)
        {
            Console.WriteLine($"solution file is {solutionFile}");
            var solution = await workspace.OpenSolutionAsync(solutionFile);
#if DEBUG
                var diagnostics = workspace.Diagnostics;
                if (diagnostics.Any())
                {
                    foreach (var diagnostic in diagnostics)
                    {
                        Console.WriteLine($"{diagnostic.Kind}: {diagnostic.Message}");
                    }
                }
#endif
            foreach (var project in solution.Projects)
            {
                // Console.WriteLine($"project file is {project}");
                foreach (var document in project.Documents)
                {
                    if (document.FilePath.Contains(@"\obj\") || ProcessedDoc.Contains(document.FilePath))
                    {
                        continue;
                    }

                    Console.WriteLine($"document file is {document.FilePath}");
                    ProcessedDoc.Add(document.FilePath);
                    var syntaxTree = await document.GetSyntaxTreeAsync();
                    var root = syntaxTree?.GetRoot();
                    var semanticModel = await document.GetSemanticModelAsync();
                    if (root == null || semanticModel == null)
                    {
                        Console.WriteLine($"document file {document.FilePath} will not get processed.");
                        continue;
                    }
                    // Console.WriteLine("Start to create.");
                    nodeCreater.CreateNode(root, semanticModel);
                    // CTagsTesting.Testing(root, semanticModel, document.FilePath);
                }
            }
        }

    }

    static async Task GenerateNodesToDB(IAsyncSession session, List<AbsCodeElement> codeElements)
    {
        // var log = @"D:\AIModels\case\rapidscada-analysis\cypher.txt";
        // File.WriteAllText(log, string.Empty);

        foreach (var element in codeElements)
        {
            // Assuming each AbsCodeElement has a ToCypher() method that generates its MERGE Cypher command
            var (cypherCommand, paras) = element.ToCypherCreateNode();
            // File.AppendAllText(log, element.ToString() + Environment.NewLine + Environment.NewLine);
            try
            {
                // Execute the Cypher command asynchronously
                await session.ExecuteWriteAsync(async tx =>
                {
                    await tx.RunAsync(cypherCommand, paras);
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"throw exception for {cypherCommand}");
            }
        }
    }
    
    static async Task CreateRelationship(IAsyncSession session, List<AbsCodeElement> codeElements)
    {
        // var log = @"D:\AIModels\case\rapidscada-analysis\cypher.txt";
        // File.WriteAllText(log, string.Empty);

        foreach (var element in codeElements)
        {
            var cyphers = element.ToCypherCreateRelationships();
            foreach (var (cypher, paras) in cyphers)
            {
                // File.AppendAllText(log, element.ToString() + Environment.NewLine + Environment.NewLine);
                try
                {
                    await session.ExecuteWriteAsync(async tx =>
                    {
                        await tx.RunAsync(cypher, paras);
                    });
                }
                catch (Exception)
                {
                    Console.WriteLine($"Creating relationship throw exception for {cypher}");
                }

            }
        }
    }

    static void Validation(IAsyncSession session, List<AbsCodeElement> codeElements, string logPath)
    {
        foreach (var element in codeElements)
        {
            element.Validation(session, logPath);
        }
    }

    public class Options
    {
        [Option('s', "save-path", Required = true, HelpText = "Path to the saved file.")]
        public string SavedFilePath { get; set; } = string.Empty;

        [Option('f', "files", Required = true, HelpText = "Files to process.", Min = 1)]
        public IEnumerable<string>? FilesToProcess { get; set; }
    }
}

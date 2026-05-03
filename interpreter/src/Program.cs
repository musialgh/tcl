using System;
using System.IO;
using TclInterpreter.Parsing;
using TclInterpreter.Execution;
using TclInterpreter.Json;

namespace TclInterpreter
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                if (args.Length < 1)
                {
                    PrintUsage();
                    return 1;
                }

                var functionName = args[0];
                var recursionLimit = 10000;
                var inputJson = "";
                var scriptPath = Path.Combine(
                    Path.GetDirectoryName(typeof(Program).Assembly.Location) ?? "",
                    "../../functions.tcl"
                );

                // Parse command-line arguments
                for (int i = 1; i < args.Length; i++)
                {
                    if (args[i] == "--recursion-limit" && i + 1 < args.Length)
                    {
                        if (!int.TryParse(args[i + 1], out recursionLimit))
                        {
                            Console.Error.WriteLine("Error: Invalid recursion limit");
                            return 1;
                        }
                        i++;
                    }
                    else if (args[i] == "--input-file" && i + 1 < args.Length)
                    {
                        inputJson = File.ReadAllText(args[i + 1]);
                        i++;
                    }
                    else if (args[i] == "--script" && i + 1 < args.Length)
                    {
                        scriptPath = args[i + 1];
                        i++;
                    }
                }

                // If no input file, read from stdin
                if (string.IsNullOrEmpty(inputJson))
                {
                    inputJson = Console.In.ReadToEnd();
                }

                // Load and parse the TCL DSL program
                if (!File.Exists(scriptPath))
                {
                    // Try to find functions in parent directories
                    scriptPath = FindScriptFile();
                    if (scriptPath == null)
                    {
                        Console.Error.WriteLine($"Error: Script file not found");
                        return 1;
                    }
                }

                var scriptContent = File.ReadAllText(scriptPath);
                var lexer = new Lexer(scriptContent);
                var tokens = lexer.Tokenize();
                var parser = new Parser(tokens);
                var program = parser.Parse();

                // Execute the function
                var inputs = JsonBridge.DeserializeParameters(inputJson);
                var executor = new Executor(program, recursionLimit);
                var result = executor.ExecuteFunction(functionName, inputs);

                // Output the result as JSON
                var outputJson = JsonBridge.SerializeResult(result);
                Console.WriteLine(outputJson);

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: tcl <function_name> [options]");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --recursion-limit <N>     Maximum recursion depth (default: 10000)");
            Console.WriteLine("  --input-file <path>       Read input from file (default: stdin)");
            Console.WriteLine("  --script <path>           Path to TCL script (default: auto-detect)");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  tcl evaluate_dance_elimination < input.json");
            Console.WriteLine("  tcl compute_places --input-file marks.json --recursion-limit 5000");
        }

        static string? FindScriptFile()
        {
            var searchPaths = new[]
            {
                "./functions.tcl",
                "../functions.tcl",
                "../../DSL/adjudication.tcl",
                "../../DSL/cross-majority-skating.tcl"
            };

            foreach (var path in searchPaths)
            {
                if (File.Exists(path))
                    return Path.GetFullPath(path);
            }

            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
                var traceCallsEnabled = false;
                string? inputJson = null;
                var scriptPaths = new List<string>();

                // Parse command-line arguments
                for (int i = 1; i < args.Length; i++)
                {
                    switch (args[i])
                    {
                        case "--recursion-limit" when i + 1 < args.Length:
                            if (!int.TryParse(args[++i], out recursionLimit))
                            {
                                Console.Error.WriteLine("Error: Invalid recursion limit");
                                return 1;
                            }
                            break;
                        case "--trace-calls":
                            traceCallsEnabled = true;
                            break;
                        case "--input-file" when i + 1 < args.Length:
                            inputJson = File.ReadAllText(args[++i]);
                            break;
                        case "--script" when i + 1 < args.Length:
                            scriptPaths.Add(args[++i]);
                            break;
                    }
                }

                // If no input file specified, read from stdin
                if (inputJson == null)
                    inputJson = Console.In.ReadToEnd();

                // If no script paths specified, auto-detect
                if (scriptPaths.Count == 0)
                {
                    Console.Error.WriteLine("Info: Autodetecting script files...");
                    scriptPaths = FindScriptFiles();
                }

                if (scriptPaths.Count == 0)
                {
                    Console.Error.WriteLine("Error: No script files found. Use --script <path>.");
                    return 1;
                }

                // Parse all script files into a combined program
                var allDefs = new List<TopLevelDef>();
                foreach (var path in scriptPaths)
                {
                    if (!File.Exists(path))
                    {
                        Console.Error.WriteLine($"Error: Script file not found: {path}");
                        return 1;
                    }

                    var source = File.ReadAllText(path);
                    var lexer = new Lexer(source);
                    var tokens = lexer.Tokenize();
                    var parser = new Parser(tokens);
                    var program = parser.ParseProgram();
                    allDefs.AddRange(program.Definitions);
                }

                var combined = new ProgramNode(allDefs);

                // Execute
                var inputs = JsonBridge.DeserializeParameters(inputJson);
                var executor = new Executor(combined, recursionLimit, traceCallsEnabled);
                var result = executor.ExecuteFunction(functionName, inputs);

                // Output
                var outputJson = JsonBridge.SerializeResult(result);
                Console.WriteLine(outputJson);
                return 0;
            }
            catch (LexicalException ex)
            {
                Console.Error.WriteLine($"Lexical error: {ex.Message}");
                return 2;
            }
            catch (ParseException ex)
            {
                Console.Error.WriteLine($"Parse error: {ex.Message}");
                return 3;
            }
            catch (Execution.ExecutionException ex)
            {
                Console.Error.WriteLine($"Runtime error: {ex.Message}");
                return 4;
            }
            catch (JsonBridgeException ex)
            {
                Console.Error.WriteLine($"JSON error: {ex.Message}");
                return 5;
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
            Console.WriteLine("  --trace-calls             Enable tracing of function calls to stderr");
            Console.WriteLine("  --input-file <path>       Read input from file (default: stdin)");
            Console.WriteLine("  --script <path>           Path to TCL script (repeatable; default: auto-detect)");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  tcl evaluate_dance_elimination < input.json");
            Console.WriteLine("  tcl compute_places --input-file marks.json --recursion-limit 5000");
            Console.WriteLine("  tcl evaluate_round_final_skating --trace-calls --script adjudication.tcl --script cross-majority-skating.tcl < input.json");
        }

        /// <summary>
        /// Auto-detect .tcl files by searching common relative locations.
        /// </summary>
        static List<string> FindScriptFiles()
        {
            var baseDir = Directory.GetCurrentDirectory();

            var candidates = new[]
            {
                // simple root script convention (TODO import...)
                new[] { baseDir, "main.tcl" },
                // anything in cwd (you can use symlinks...)
                new[] { baseDir, "*.tcl" },
                // common relative path
                new[] { Path.Combine(baseDir, "tcl"), "*.tcl" },
            };

            foreach (var option in candidates)
            {
                var files = Directory.EnumerateFiles(
                                option[0], option[1], SearchOption.TopDirectoryOnly
                            ).ToList();

                if (files.Count > 0) return files;
            }
        
            return new List<string>();
        }
    }
}

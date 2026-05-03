# TCL DSL Interpreter

A command-line C# interpreter for the TCL (Tanzen Competition Language) DSL for adjudication algorithms.

## Project Structure

```
interpreter/
├── src/                          # Source code
│   ├── Lexer.cs                 # Tokenizer for DSL syntax
│   ├── Parser.cs                # Recursive descent parser
│   ├── AstNodes.cs              # AST node definitions
│   ├── TypeSystem.cs            # Type hierarchy and checking
│   ├── Executor.cs              # AST evaluator
│   ├── BuiltInFunctions.cs      # Built-in operations (map, filter, merge, etc.)
│   ├── JsonBridge.cs            # JSON ↔ DSL value conversion
│   └── Program.cs               # CLI entry point
├── tests/                        # Test suite
│   └── (test projects)
├── plan-tclInterpreter.prompt.md # Implementation plan
├── TclInterpreter.csproj        # Project file
└── README.md                    # This file
```

## Building

### Prerequisites
- .NET 8.0 SDK or later
- C# 12.0 or later

### Build Commands

```bash
# Restore dependencies
dotnet restore

# Build the project
dotnet build

# Build in release mode
dotnet build --configuration Release
```

### Creating an executable

```bash
# Publish as self-contained executable
dotnet publish --configuration Release --self-contained -p:PublishSingleFile=true
```

The output will be in `bin/Release/net8.0/publish/`

## Usage

```bash
# Basic usage: read JSON from stdin
tcl <function_name> < input.json

# Read from input file
tcl <function_name> --input-file input.json

# With custom recursion limit
tcl <function_name> --recursion-limit 5000 --input-file input.json

# Specify custom script path
tcl <function_name> --script /path/to/functions.tcl < input.json
```

## Examples

### Example 1: Simple elimination round evaluation

```bash
cat > input.json << 'EOF'
{
  "marks": {
    "1": { "jury": { "judge_a": true, "judge_b": false } },
    "2": { "jury": { "judge_a": true, "judge_b": true } }
  }
}
EOF

tcl evaluate_dance_elimination < input.json
```

### Example 2: Compute places from scores

```bash
cat > scores.json << 'EOF'
{
  "scores": {
    "1": 5.5,
    "2": 3.0,
    "3": 4.5
  }
}
EOF

tcl compute_places --input-file scores.json
```

## Implementation Status

### Completed
- ✅ Lexer: Full tokenization of DSL syntax
- ✅ Parser: Recursive descent parser with AST construction
- ✅ Type system: Hierarchy and narrowing rules
- ✅ Executor scaffolding: Basic execution context and framework

### In Progress
- 🔄 Built-in functions: map, filter, merge, concat, replace, etc.
- 🔄 Function call evaluation
- 🔄 Object comparisons (depth-first traversal)
- 🔄 Iterator clauses ("in" syntax)

### Not Started
- ⚠️ Template validation
- ⚠️ Type checking during execution (optional feature)
- ⚠️ Error messages with source locations
- ⚠️ Comprehensive test suite
- ⚠️ Performance optimizations

## Testing

Run tests with:

```bash
dotnet test
```

## Development Notes

### Key Design Decisions

1. **Object representation**: Uses `SortedDictionary<object, DslValue>` with custom key comparer
   - Numeric keys sort lower than string keys
   - Empty object `{}` sorts lowest

2. **Number precision**: Uses `decimal` type for `real` and `num` types
   - Preserves exact fractional arithmetic
   - 64-bit integers for `int` and `nat`

3. **Lazy evaluation**: `where` clauses compute only when needed
   - Reduces computation for expensive sub-expressions

4. **Recursion limiting**: Configurable maximum depth (default 10,000)
   - Prevents stack overflow on malformed recursion

### Error Handling

- **LexicalException**: Syntax errors during tokenization
- **ParseException**: Parsing errors
- **ExecutionException**: Runtime errors during evaluation
- **JsonBridgeException**: JSON serialization/deserialization errors

### Type Narrowing Hierarchy

```
any > num > real
any > num > int > nat > bool
any > object
```

Template signatures can only narrow types; they cannot widen.

## License

(Add license information if applicable)

## Contributing

(Add contribution guidelines if applicable)

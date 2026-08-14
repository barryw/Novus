using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for const fn (compile-time evaluated functions)
/// </summary>
public class ConstFnTests
{
    private IrModule BuildIr(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();
        var builder = new IrBuilder(skipAutoImports: true);
        return builder.BuildModule(tree);
    }

    [Fact]
    public void Parser_ConstFn_CorrectlyParsed()
    {
        var source = @"
const fn double(x: i32) -> i32 {
    return x * 2
}

pub fn main() -> i32 {
    return double(21)
}";
        // First, verify the ANTLR parsing
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        // Verify parsing produced expected structure
        Assert.Equal(2, tree.functionDeclaration().Length); // double and main
        Assert.Empty(tree.constDeclaration()); // no const declarations

        var doubleFuncDecl = tree.functionDeclaration()[0];
        Assert.NotNull(doubleFuncDecl);
        Assert.Equal("double", doubleFuncDecl.IDENTIFIER().GetText());
        Assert.NotNull(doubleFuncDecl.KW_CONST()); // Has const keyword
        Assert.NotNull(doubleFuncDecl.KW_FN()); // Has fn keyword
        Assert.NotNull(doubleFuncDecl.block()); // Has function body
    }

    [Fact]
    public void Parser_ConstFn_ParsesSuccessfully()
    {
        var source = @"
const fn double(x: i32) -> i32 {
    return x * 2
}

pub fn main() -> i32 {
    return double(21)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        // Verify the function exists and is marked as const
        var doubleFunc = module.Functions.FirstOrDefault(f => f.Name == "double");
        Assert.NotNull(doubleFunc);
        Assert.True(doubleFunc.IsConstFn);
    }

    [Fact]
    public void Parser_PublicConstFn_ParsesSuccessfully()
    {
        var source = @"
pub const fn square(x: i32) -> i32 {
    return x * x
}

pub fn main() -> i32 {
    return square(10)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var squareFunc = module.Functions.FirstOrDefault(f => f.Name == "square");
        Assert.NotNull(squareFunc);
        Assert.True(squareFunc.IsConstFn);
        Assert.Equal(Visibility.Public, squareFunc.Visibility);
    }

    [Fact]
    public void Parser_ConstFn_WithMultipleParameters()
    {
        var source = @"
const fn add(a: i32, b: i32) -> i32 {
    return a + b
}

pub fn main() -> i32 {
    return add(10, 20)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var addFunc = module.Functions.FirstOrDefault(f => f.Name == "add");
        Assert.NotNull(addFunc);
        Assert.True(addFunc.IsConstFn);
        Assert.Equal(2, addFunc.Parameters.Count);
    }

    [Fact]
    public void Parser_ConstFn_WithLocalVariable()
    {
        var source = @"
const fn complex_calc(x: i32) -> i32 {
    let doubled = x * 2
    let tripled = x * 3
    return doubled + tripled
}

pub fn main() -> i32 {
    return complex_calc(10)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var func = module.Functions.FirstOrDefault(f => f.Name == "complex_calc");
        Assert.NotNull(func);
        Assert.True(func.IsConstFn);
    }

    [Fact]
    public void Parser_ConstFn_WithConditional()
    {
        var source = @"
const fn max(a: i32, b: i32) -> i32 {
    if a > b {
        return a
    }
    return b
}

pub fn main() -> i32 {
    return max(10, 20)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var func = module.Functions.FirstOrDefault(f => f.Name == "max");
        Assert.NotNull(func);
        Assert.True(func.IsConstFn);
    }

    [Fact]
    public void Parser_RegularFn_NotConstFn()
    {
        var source = @"
fn regular_function(x: i32) -> i32 {
    return x
}

pub fn main() -> i32 {
    return regular_function(42)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var func = module.Functions.FirstOrDefault(f => f.Name == "regular_function");
        Assert.NotNull(func);
        Assert.False(func.IsConstFn);
    }

    [Fact]
    public void Parser_ConstFn_CallingOtherConstFn()
    {
        var source = @"
const fn double(x: i32) -> i32 {
    return x * 2
}

const fn quadruple(x: i32) -> i32 {
    return double(double(x))
}

pub fn main() -> i32 {
    return quadruple(10)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var doubleFunc = module.Functions.FirstOrDefault(f => f.Name == "double");
        var quadrupleFunc = module.Functions.FirstOrDefault(f => f.Name == "quadruple");

        Assert.NotNull(doubleFunc);
        Assert.NotNull(quadrupleFunc);
        Assert.True(doubleFunc.IsConstFn);
        Assert.True(quadrupleFunc.IsConstFn);
    }

    [Fact]
    public void ConstFnEvaluator_SimpleFunction_Evaluates()
    {
        var source = @"
const fn double(x: i32) -> i32 {
    return x * 2
}

pub fn main() -> i32 {
    return double(21)
}";
        var module = BuildIr(source);

        var doubleFunc = module.Functions.First(f => f.Name == "double");
        var evaluator = new ConstFnEvaluator(module);

        var result = evaluator.Evaluate("double", new List<object?> { 21L });
        Assert.True(result.Success);
        Assert.Equal(42L, result.Value);
    }

    [Fact]
    public void ConstFnEvaluator_Addition_Evaluates()
    {
        var source = @"
const fn add(a: i32, b: i32) -> i32 {
    return a + b
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        var evaluator = new ConstFnEvaluator(module);
        var result = evaluator.Evaluate("add", new List<object?> { 10L, 20L });

        Assert.True(result.Success);
        Assert.Equal(30L, result.Value);
    }

    [Fact]
    public void ConstFnEvaluator_WithLocalVariables_Evaluates()
    {
        var source = @"
const fn complex(x: i32) -> i32 {
    let doubled = x * 2
    let result = doubled + 10
    return result
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        var evaluator = new ConstFnEvaluator(module);
        var result = evaluator.Evaluate("complex", new List<object?> { 5L });

        Assert.True(result.Success);
        Assert.Equal(20L, result.Value);  // (5 * 2) + 10 = 20
    }

    [Fact]
    public void ConstFnEvaluator_NonConstFn_ReturnsError()
    {
        var source = @"
fn not_const(x: i32) -> i32 {
    return x
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        var evaluator = new ConstFnEvaluator(module);
        var result = evaluator.Evaluate("not_const", new List<object?> { 5L });

        Assert.False(result.Success);
        Assert.Contains("not a const fn", result.Error);
    }

    [Fact]
    public void ConstFnEvaluator_CallingConstFn_Evaluates()
    {
        var source = @"
const fn double(x: i32) -> i32 {
    return x * 2
}

const fn quadruple(x: i32) -> i32 {
    return double(double(x))
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        var evaluator = new ConstFnEvaluator(module);
        var result = evaluator.Evaluate("quadruple", new List<object?> { 10L });

        Assert.True(result.Success);
        Assert.Equal(40L, result.Value);  // double(double(10)) = double(20) = 40
    }

    [Fact]
    [Trait("Category", "CompilerIntegration")]
    public void ConstFnConstant_IsResolvedBeforeOrdinaryFunctionBody()
    {
        var projectRoot = Directory.GetCurrentDirectory();
        while (!File.Exists(Path.Combine(projectRoot, "Novus.sln")))
            projectRoot = Directory.GetParent(projectRoot)!.FullName;
        var path = Path.Combine(projectRoot, "Novus.Tests", "Examples", "test_const_fn.novus");
        var stdPath = Path.Combine(projectRoot, "Novus", "std");
        var source = File.ReadAllText(path);

        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();
        var analyzer = new SemanticAnalyzer(path, source, stdPath);
        Assert.True(analyzer.Analyze(tree));

        var builder = new IrBuilder(analyzer.GetResult());
        builder.SetStdLibPath(stdPath);
        builder.SetInputFilePath(path);
        var module = builder.BuildModule(tree);

        Assert.Equal(640, module.Constants["DOUBLED_WIDTH"].Value);
        Assert.Equal(520, module.Constants["BUFFER_SIZE"].Value);
        Assert.Equal(21, module.Constants["FIB_8"].Value);
        Assert.Equal(100, module.Constants["CLAMPED_VALUE"].Value);

        var actualValues = module.GetFunction("test_compile_time_constants")!.BasicBlocks
            .SelectMany(block => block.Instructions)
            .OfType<IrCall>()
            .Where(call => call.FunctionName.Contains("expect_eq", StringComparison.Ordinal))
            .Select(call => Assert.IsType<IrConstant>(call.Arguments[0]).Value)
            .ToArray();
        Assert.Equal(new long[] { 320, 200, 640, 520, 21, 100 }, actualValues);
    }

    [Fact]
    public void ConstFnValidation_ValidConstFn_NoErrors()
    {
        var source = @"
const fn pure_math(x: i32, y: i32) -> i32 {
    let sum = x + y
    let product = x * y
    return sum + product
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        var func = module.Functions.First(f => f.Name == "pure_math");
        var errors = ConstFnEvaluator.ValidateConstFn(func);

        Assert.Empty(errors);
    }

    [Fact]
    public void ConstFnValidation_CallingNonConstFn_Error()
    {
        var source = @"
fn impure(x: i32) -> i32 {
    return x
}

const fn calls_impure(x: i32) -> i32 {
    return impure(x)
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        var func = module.Functions.First(f => f.Name == "calls_impure");
        var errors = ConstFnEvaluator.ValidateConstFn(func, module);

        Assert.Single(errors);
        Assert.Contains("cannot call non-const function 'impure'", errors[0]);
    }

    [Fact]
    public void ConstFnValidation_CallingConstFn_NoError()
    {
        var source = @"
const fn helper(x: i32) -> i32 {
    return x * 2
}

const fn calls_const(x: i32) -> i32 {
    return helper(x) + 1
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        var func = module.Functions.First(f => f.Name == "calls_const");
        var errors = ConstFnEvaluator.ValidateConstFn(func, module);

        Assert.Empty(errors);
    }

    [Fact]
    public void ConstFnValidation_WithConditional_NoError()
    {
        var source = @"
const fn max(a: i32, b: i32) -> i32 {
    if a > b {
        return a
    }
    return b
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        var func = module.Functions.First(f => f.Name == "max");
        var errors = ConstFnEvaluator.ValidateConstFn(func, module);

        Assert.Empty(errors);
    }

    [Fact]
    public void ConstFnValidation_Recursive_NoError()
    {
        var source = @"
const fn factorial(n: i32) -> i32 {
    if n <= 1 {
        return 1
    }
    return n * factorial(n - 1)
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        var func = module.Functions.First(f => f.Name == "factorial");
        var errors = ConstFnEvaluator.ValidateConstFn(func, module);

        Assert.Empty(errors);
    }

    [Fact]
    public void ConstFnValidation_ReadingGlobalVariable_Error()
    {
        var source = @"
static var counter: i32 = 0

const fn read_global() -> i32 {
    return counter
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        var func = module.Functions.First(f => f.Name == "read_global");
        var errors = ConstFnEvaluator.ValidateConstFn(func, module);

        Assert.Single(errors);
        Assert.Contains("cannot read global variable 'counter'", errors[0]);
    }

    [Fact]
    public void ConstFnValidation_WritingGlobalVariable_Error()
    {
        var source = @"
static var counter: i32 = 0

const fn write_global() -> i32 {
    counter = 42
    return 0
}

pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);

        var func = module.Functions.First(f => f.Name == "write_global");
        var errors = ConstFnEvaluator.ValidateConstFn(func, module);

        // Should have at least one error about global variable access
        Assert.NotEmpty(errors);
        Assert.True(errors.Any(e => e.Contains("global variable")),
            $"Expected error about global variable, got: {string.Join(", ", errors)}");
    }

    [Fact]
    public void SemanticAnalyzer_RegularFn_RegistersFunction()
    {
        // Test two functions to ensure both get registered
        // Note: "double" is a reserved keyword in Novus (from C), so use "times_two" instead
        var source = @"
fn times_two(x: i32) -> i32 {
    return x * 2
}

fn main() -> u32 {
    return 42
}";
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        // This should NOT throw KeyNotFoundException
        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        analyzer.Analyze(tree);

        // Check if there are any errors
        Assert.False(analyzer.Diagnostics.HasErrors,
            "Expected no errors but got: " + string.Join(", ", analyzer.Diagnostics.Diagnostics.Where(d => d.IsError).Select(d => d.Message)));
    }

    [Fact]
    public void SemanticAnalyzer_ConstFn_RegistersFunction()
    {
        // Note: "double" is a reserved keyword in Novus (C interop), so use "times_two" instead
        var source = @"
const fn times_two(x: i32) -> i32 {
    return x * 2
}

pub fn main() -> i32 {
    return times_two(21)
}";
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        // Debug: Verify parsing before semantic analysis
        Assert.Equal(2, tree.functionDeclaration().Length);
        foreach (var funcDecl in tree.functionDeclaration())
        {
            var name = funcDecl.IDENTIFIER()?.GetText() ?? "NULL";
            var hasBlock = funcDecl.block() != null;
            Assert.True(hasBlock, $"Function '{name}' should have a block but block() returned null");
        }

        // This should NOT throw KeyNotFoundException
        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        analyzer.Analyze(tree);

        // Check if there are any errors
        Assert.False(analyzer.Diagnostics.HasErrors,
            "Expected no errors but got: " + string.Join(", ", analyzer.Diagnostics.Diagnostics.Where(d => d.IsError).Select(d => d.Message)));
    }
}

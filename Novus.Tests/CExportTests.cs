using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Novus.Codegen;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for C export functionality (#[export] attribute and C header generation)
/// </summary>
public class CExportTests
{
    private (DiagnosticBag Diagnostics, SemanticAnalyzer Analyzer) Analyze(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        analyzer.Analyze(tree);
        return (analyzer.Diagnostics, analyzer);
    }

    private IR.IrModule BuildIR(string source)
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
    public void Export_AttributeOnFunction_Parses()
    {
        var source = @"
#[export]
pub fn add(a: i32, b: i32) -> i32 {
    return a + b
}";
        var (diagnostics, _) = Analyze(source);

        // Should parse without errors (export is a known attribute)
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Export_MultipleExportedFunctions_Parse()
    {
        var source = @"
#[export]
pub fn add(a: i32, b: i32) -> i32 {
    return a + b
}

#[export]
pub fn multiply(x: i32, y: i32) -> i32 {
    return x * y
}";
        var (diagnostics, _) = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Export_FunctionHasIsExportedFlagSet()
    {
        var source = @"
#[export]
pub fn exported_func() -> i32 {
    return 42
}

fn not_exported() -> i32 {
    return 0
}";
        var module = BuildIR(source);

        // Find the exported function
        var exportedFunc = module.Functions.FirstOrDefault(f => f.Name == "exported_func");
        Assert.NotNull(exportedFunc);
        Assert.True(exportedFunc.IsExported);

        // Find the non-exported function
        var notExportedFunc = module.Functions.FirstOrDefault(f => f.Name == "not_exported");
        Assert.NotNull(notExportedFunc);
        Assert.False(notExportedFunc.IsExported);
    }

    [Fact]
    public void Export_GeneratedCodeDoesNotMangle()
    {
        var source = @"
#[export]
pub fn my_function(x: i32) -> i32 {
    return x + 1
}";
        var module = BuildIR(source);
        var codegen = new CCodeGenerator(
            module,
            new System.Collections.Generic.List<IR.IrStringLiteral>(),
            "68020",
            "auto",
            BuildMode.Debug,
            SafetyLevel.Full,
            null,
            false,
            null);

        var cCode = codegen.Generate();

        // Exported function should not be mangled
        Assert.Contains("int32_t my_function(int32_t x)", cCode);
        // Should not have 'static' keyword
        Assert.DoesNotContain("static int32_t my_function", cCode);
    }

    [Fact]
    public void Export_NonExportedFunction_NotInModule()
    {
        var source = @"
#[export]
pub fn exported_func(x: i32) -> i32 {
    return x + 1
}

fn internal_function(x: i32) -> i32 {
    return x + 2
}";
        var module = BuildIR(source);

        // Find the exported function
        var exportedFunc = module.Functions.FirstOrDefault(f => f.Name == "exported_func");
        Assert.NotNull(exportedFunc);
        Assert.True(exportedFunc.IsExported);

        // Non-exported functions exist too
        var internalFunc = module.Functions.FirstOrDefault(f => f.Name == "internal_function");
        Assert.NotNull(internalFunc);
        Assert.False(internalFunc.IsExported);
    }

    [Fact]
    public void Export_HeaderGeneration_ContainsExportedFunctions()
    {
        var source = @"
#[export]
pub fn identity(x: i32) -> i32 {
    return x
}

#[export]
pub fn constant() -> i32 {
    return 42
}

fn not_exported() -> i32 {
    return 0
}";
        var module = BuildIR(source);
        var codegen = new CCodeGenerator(
            module,
            new System.Collections.Generic.List<IR.IrStringLiteral>(),
            "68020",
            "auto",
            BuildMode.Debug,
            SafetyLevel.Full,
            null,
            true,  // useSharedTypesHeader
            null);

        var header = codegen.GenerateHeader();

        // Header should contain exported functions
        Assert.Contains("int32_t identity(int32_t x);", header);
        Assert.Contains("int32_t constant(void);", header);

        // Header should NOT contain non-exported function
        Assert.DoesNotContain("not_exported", header);

        // Header should have proper structure
        Assert.Contains("#ifndef NOVUS_EXPORTS_H", header);
        Assert.Contains("#define NOVUS_EXPORTS_H", header);
        // We no longer include stdint.h/stdbool.h - we define types inline or in novus_types.h
        Assert.Contains("#include <exec/types.h>", header);
        Assert.Contains("#include \"novus_types.h\"", header);
        Assert.Contains("extern \"C\"", header);
    }

    [Fact]
    public void Export_HeaderGeneration_NoExportedFunctions()
    {
        var source = @"
fn internal_only() -> i32 {
    return 42
}";
        var module = BuildIR(source);
        var codegen = new CCodeGenerator(
            module,
            new System.Collections.Generic.List<IR.IrStringLiteral>(),
            "68020",
            "auto",
            BuildMode.Debug,
            SafetyLevel.Full,
            null,
            true,
            null);

        var header = codegen.GenerateHeader();

        // Header should still be valid but indicate no exports
        Assert.Contains("// No exported functions", header);
        Assert.DoesNotContain("internal_only", header);
    }

    [Fact]
    public void Export_FunctionWithDifferentReturnTypes()
    {
        var source = @"
#[export]
pub fn returns_void() {
    return
}

#[export]
pub fn returns_bool() -> bool {
    return true
}

#[export]
pub fn returns_ptr() -> *u8 {
    return 0 as *u8
}";
        var module = BuildIR(source);
        var codegen = new CCodeGenerator(
            module,
            new System.Collections.Generic.List<IR.IrStringLiteral>(),
            "68020",
            "auto",
            BuildMode.Debug,
            SafetyLevel.Full,
            null,
            true,
            null);

        var header = codegen.GenerateHeader();

        // Check different return types are correctly mapped
        Assert.Contains("void returns_void(void);", header);
        Assert.Contains("bool returns_bool(void);", header);
        Assert.Contains("uint8_t* returns_ptr(void);", header);
    }

    [Fact]
    public void Export_ExportedFunctionCalledFromNovus()
    {
        var source = @"
#[export]
pub fn helper(x: i32) -> i32 {
    return x * 2
}

fn main() -> i32 {
    return helper(21)
}";
        var (diagnostics, _) = Analyze(source);

        // Exported functions should still be callable from Novus
        Assert.False(diagnostics.HasErrors);

        var module = BuildIR(source);
        var codegen = new CCodeGenerator(
            module,
            new System.Collections.Generic.List<IR.IrStringLiteral>(),
            "68020",
            "auto",
            BuildMode.Debug,
            SafetyLevel.Full,
            null,
            false,
            null);

        var cCode = codegen.Generate();

        // Should call exported function by its un-mangled name
        // Note: The generated code may include casts, so we check for "helper(" and "21"
        Assert.Contains("helper(", cCode);
        Assert.Contains("21", cCode);
    }

    [Fact]
    public void Export_AttributeSyntax_BothStylesSupported()
    {
        // @export style
        var source1 = @"
@export
pub fn func1() -> i32 {
    return 1
}";
        var (diagnostics1, _) = Analyze(source1);
        Assert.False(diagnostics1.HasErrors);

        // #[export] style
        var source2 = @"
#[export]
pub fn func2() -> i32 {
    return 2
}";
        var (diagnostics2, _) = Analyze(source2);
        Assert.False(diagnostics2.HasErrors);
    }

    [Fact]
    public void Export_ExternFunctionsNotExported()
    {
        var source = @"
#[export]
extern fn system_func(x: i32) -> i32

#[export]
pub fn my_func(x: i32) -> i32 {
    return x
}";
        var module = BuildIR(source);
        var codegen = new CCodeGenerator(
            module,
            new System.Collections.Generic.List<IR.IrStringLiteral>(),
            "68020",
            "auto",
            BuildMode.Debug,
            SafetyLevel.Full,
            null,
            true,
            null);

        var header = codegen.GenerateHeader();

        // Extern functions should not appear in exports header even if marked with #[export]
        Assert.DoesNotContain("system_func", header);
        // But regular exported function should
        Assert.Contains("my_func", header);
    }
}

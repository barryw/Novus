using Novus.Diagnostics;
using Novus.Preprocessing;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Comprehensive tests for the Preprocessor component
/// Tests conditional compilation directives: #if, #elif, #else, #endif
/// </summary>
public class PreprocessorTests
{
    private (string result, DiagnosticBag diagnostics) Preprocess(string source, Dictionary<string, bool>? constants = null)
    {
        constants ??= new Dictionary<string, bool>();
        var diagnostics = new DiagnosticBag();
        var preprocessor = new Preprocessor(constants, diagnostics, "test.novus");
        var result = preprocessor.Process(source);
        return (result, diagnostics);
    }

    private List<Diagnostic> GetErrors(DiagnosticBag diagnostics) =>
        diagnostics.Diagnostics.Where(d => d.IsError).ToList();

    [Fact]
    public void SimpleIf_TrueCondition_IncludesBlock()
    {
        var source = @"
fn test() {
#if DEBUG
    println(""debug mode"");
#endif
}";
        var constants = new Dictionary<string, bool> { ["DEBUG"] = true };
        var (result, diagnostics) = Preprocess(source, constants);

        Assert.Empty(GetErrors(diagnostics));
        Assert.Contains("println(\"debug mode\");", result);
    }

    [Fact]
    public void SimpleIf_FalseCondition_ExcludesBlock()
    {
        var source = @"
fn test() {
#if DEBUG
    println(""debug mode"");
#endif
    println(""always"");
}";
        var constants = new Dictionary<string, bool> { ["DEBUG"] = false };
        var (result, diagnostics) = Preprocess(source, constants);

        Assert.Empty(GetErrors(diagnostics));
        Assert.DoesNotContain("println(\"debug mode\");", result);
        Assert.Contains("println(\"always\");", result);
    }

    [Fact]
    public void IfElse_TrueCondition_IncludesIfBlock()
    {
        var source = @"
#if DEBUG
let x = 1;
#else
let x = 2;
#endif";
        var constants = new Dictionary<string, bool> { ["DEBUG"] = true };
        var (result, diagnostics) = Preprocess(source, constants);

        Assert.Empty(GetErrors(diagnostics));
        Assert.Contains("let x = 1;", result);
        Assert.DoesNotContain("let x = 2;", result);
    }

    [Fact]
    public void IfElse_FalseCondition_IncludesElseBlock()
    {
        var source = @"
#if DEBUG
let x = 1;
#else
let x = 2;
#endif";
        var constants = new Dictionary<string, bool> { ["DEBUG"] = false };
        var (result, diagnostics) = Preprocess(source, constants);

        Assert.Empty(GetErrors(diagnostics));
        Assert.DoesNotContain("let x = 1;", result);
        Assert.Contains("let x = 2;", result);
    }

    [Fact]
    public void IfElifElse_FirstConditionTrue()
    {
        var source = @"
#if AMIGA
let platform = ""Amiga"";
#elif WINDOWS
let platform = ""Windows"";
#elif LINUX
let platform = ""Linux"";
#else
let platform = ""Unknown"";
#endif";
        var constants = new Dictionary<string, bool>
        {
            ["AMIGA"] = true,
            ["WINDOWS"] = false,
            ["LINUX"] = false
        };
        var (result, diagnostics) = Preprocess(source, constants);

        Assert.Empty(GetErrors(diagnostics));
        Assert.Contains("let platform = \"Amiga\";", result);
        Assert.DoesNotContain("let platform = \"Windows\";", result);
        Assert.DoesNotContain("let platform = \"Linux\";", result);
        Assert.DoesNotContain("let platform = \"Unknown\";", result);
    }

    [Fact]
    public void IfElifElse_SecondConditionTrue()
    {
        var source = @"
#if AMIGA
let platform = ""Amiga"";
#elif WINDOWS
let platform = ""Windows"";
#elif LINUX
let platform = ""Linux"";
#else
let platform = ""Unknown"";
#endif";
        var constants = new Dictionary<string, bool>
        {
            ["AMIGA"] = false,
            ["WINDOWS"] = true,
            ["LINUX"] = false
        };
        var (result, diagnostics) = Preprocess(source, constants);

        Assert.Empty(GetErrors(diagnostics));
        Assert.DoesNotContain("let platform = \"Amiga\";", result);
        Assert.Contains("let platform = \"Windows\";", result);
        Assert.DoesNotContain("let platform = \"Linux\";", result);
        Assert.DoesNotContain("let platform = \"Unknown\";", result);
    }

    [Fact]
    public void IfElifElse_AllConditionsFalse_UsesElse()
    {
        var source = @"
#if AMIGA
let platform = ""Amiga"";
#elif WINDOWS
let platform = ""Windows"";
#else
let platform = ""Unknown"";
#endif";
        var constants = new Dictionary<string, bool>
        {
            ["AMIGA"] = false,
            ["WINDOWS"] = false
        };
        var (result, diagnostics) = Preprocess(source, constants);

        Assert.Empty(GetErrors(diagnostics));
        Assert.DoesNotContain("let platform = \"Amiga\";", result);
        Assert.DoesNotContain("let platform = \"Windows\";", result);
        Assert.Contains("let platform = \"Unknown\";", result);
    }

    [Fact]
    public void NestedIf_BothTrue()
    {
        var source = @"
#if DEBUG
    println(""debug"");
    #if VERBOSE
        println(""verbose"");
    #endif
#endif";
        var constants = new Dictionary<string, bool>
        {
            ["DEBUG"] = true,
            ["VERBOSE"] = true
        };
        var (result, diagnostics) = Preprocess(source, constants);

        Assert.Empty(GetErrors(diagnostics));
        Assert.Contains("println(\"debug\");", result);
        Assert.Contains("println(\"verbose\");", result);
    }

    [Fact]
    public void NestedIf_OuterTrueInnerFalse()
    {
        var source = @"
#if DEBUG
    println(""debug"");
    #if VERBOSE
        println(""verbose"");
    #endif
#endif";
        var constants = new Dictionary<string, bool>
        {
            ["DEBUG"] = true,
            ["VERBOSE"] = false
        };
        var (result, diagnostics) = Preprocess(source, constants);

        Assert.Empty(GetErrors(diagnostics));
        Assert.Contains("println(\"debug\");", result);
        Assert.DoesNotContain("println(\"verbose\");", result);
    }

    [Fact]
    public void NestedIf_OuterFalse_InnerNotEvaluated()
    {
        var source = @"
#if DEBUG
    println(""debug"");
    #if VERBOSE
        println(""verbose"");
    #endif
#endif";
        var constants = new Dictionary<string, bool>
        {
            ["DEBUG"] = false,
            ["VERBOSE"] = true
        };
        var (result, diagnostics) = Preprocess(source, constants);

        Assert.Empty(GetErrors(diagnostics));
        Assert.DoesNotContain("println(\"debug\");", result);
        Assert.DoesNotContain("println(\"verbose\");", result);
    }

    [Fact]
    public void PreservesLineNumbers()
    {
        var source = @"let a = 1;
#if DEBUG
let b = 2;
#endif
let c = 3;";
        var constants = new Dictionary<string, bool> { ["DEBUG"] = false };
        var (result, diagnostics) = Preprocess(source, constants);

        var lines = result.Split('\n');
        // Line 1: let a = 1;
        // Line 2: blank (was #if DEBUG)
        // Line 3: blank (was let b = 2;)
        // Line 4: blank (was #endif)
        // Line 5: let c = 3;

        Assert.Contains("let a = 1;", lines[0]);
        Assert.Equal("", lines[2].Trim()); // b line removed but line number preserved
        Assert.Contains("let c = 3;", lines[4]);
    }

    [Fact]
    public void AttributeSyntax_NotTreatedAsDirective()
    {
        var source = @"
#[inline]
fn test() {}";
        var constants = new Dictionary<string, bool>();
        var (result, diagnostics) = Preprocess(source, constants);

        Assert.Empty(GetErrors(diagnostics));
        Assert.Contains("#[inline]", result);
        Assert.Contains("fn test() {}", result);
    }

    [Fact]
    public void UnmatchedIf_ReportsError()
    {
        var source = @"
#if DEBUG
let x = 1;";
        var constants = new Dictionary<string, bool> { ["DEBUG"] = true };
        var (result, diagnostics) = Preprocess(source, constants);

        Assert.Single(GetErrors(diagnostics));
        Assert.Contains("E_PREPROC_004", GetErrors(diagnostics)[0].Code);
        Assert.Contains("unmatched #if", GetErrors(diagnostics)[0].Message);
    }

    [Fact]
    public void UnmatchedElif_ReportsError()
    {
        var source = @"
#elif DEBUG
let x = 1;
#endif";
        var constants = new Dictionary<string, bool> { ["DEBUG"] = true };
        var (result, diagnostics) = Preprocess(source, constants);

        var errors = GetErrors(diagnostics);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Code == "E_PREPROC_001" && e.Message.Contains("#elif without matching #if"));
    }

    [Fact]
    public void UnmatchedElse_ReportsError()
    {
        var source = @"
#else
let x = 1;
#endif";
        var constants = new Dictionary<string, bool>();
        var (result, diagnostics) = Preprocess(source, constants);

        var errors = GetErrors(diagnostics);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Code == "E_PREPROC_002" && e.Message.Contains("#else without matching #if"));
    }

    [Fact]
    public void UnmatchedEndif_ReportsError()
    {
        var source = @"
let x = 1;
#endif";
        var constants = new Dictionary<string, bool>();
        var (result, diagnostics) = Preprocess(source, constants);

        Assert.Single(GetErrors(diagnostics));
        Assert.Contains("E_PREPROC_003", GetErrors(diagnostics)[0].Code);
        Assert.Contains("#endif without matching #if", GetErrors(diagnostics)[0].Message);
    }

    [Fact]
    public void UndefinedConstant_ReportsError()
    {
        var source = @"
#if UNDEFINED_CONSTANT
let x = 1;
#endif";
        var constants = new Dictionary<string, bool> { ["DEBUG"] = true };
        var (result, diagnostics) = Preprocess(source, constants);

        Assert.Single(GetErrors(diagnostics));
        Assert.Contains("E_PREPROC_010", GetErrors(diagnostics)[0].Code);
        Assert.Contains("undefined preprocessor constant 'UNDEFINED_CONSTANT'", GetErrors(diagnostics)[0].Message);
    }

    [Fact]
    public void IfWithoutArgument_ReportsError()
    {
        var source = @"
#if
let x = 1;
#endif";
        var constants = new Dictionary<string, bool>();
        var (result, diagnostics) = Preprocess(source, constants);

        var errors = GetErrors(diagnostics);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Code == "E_PREPROC_005" && e.Message.Contains("#if requires exactly one constant name"));
    }

    [Fact]
    public void IfWithMultipleArguments_ReportsError()
    {
        var source = @"
#if DEBUG VERBOSE
let x = 1;
#endif";
        var constants = new Dictionary<string, bool> { ["DEBUG"] = true };
        var (result, diagnostics) = Preprocess(source, constants);

        var errors = GetErrors(diagnostics);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Code == "E_PREPROC_005");
    }

    [Fact]
    public void ElifWithoutArgument_ReportsError()
    {
        var source = @"
#if DEBUG
let x = 1;
#elif
let x = 2;
#endif";
        var constants = new Dictionary<string, bool> { ["DEBUG"] = false };
        var (result, diagnostics) = Preprocess(source, constants);

        Assert.Single(GetErrors(diagnostics));
        Assert.Contains("E_PREPROC_006", GetErrors(diagnostics)[0].Code);
        Assert.Contains("#elif requires exactly one constant name", GetErrors(diagnostics)[0].Message);
    }

    [Fact]
    public void ElseWithArguments_ReportsError()
    {
        var source = @"
#if DEBUG
let x = 1;
#else DEBUG
let x = 2;
#endif";
        var constants = new Dictionary<string, bool> { ["DEBUG"] = false };
        var (result, diagnostics) = Preprocess(source, constants);

        Assert.Single(GetErrors(diagnostics));
        Assert.Contains("E_PREPROC_007", GetErrors(diagnostics)[0].Code);
        Assert.Contains("#else takes no arguments", GetErrors(diagnostics)[0].Message);
    }

    [Fact]
    public void EndifWithArguments_ReportsError()
    {
        var source = @"
#if DEBUG
let x = 1;
#endif DEBUG";
        var constants = new Dictionary<string, bool> { ["DEBUG"] = true };
        var (result, diagnostics) = Preprocess(source, constants);

        var errors = GetErrors(diagnostics);
        Assert.NotEmpty(errors);
        Assert.Contains(errors, e => e.Code == "E_PREPROC_008" && e.Message.Contains("#endif takes no arguments"));
    }

    [Fact]
    public void UnknownDirective_ReportsError()
    {
        var source = @"
#define DEBUG
let x = 1;";
        var constants = new Dictionary<string, bool>();
        var (result, diagnostics) = Preprocess(source, constants);

        Assert.Single(GetErrors(diagnostics));
        Assert.Contains("E_PREPROC_009", GetErrors(diagnostics)[0].Code);
        Assert.Contains("unknown preprocessor directive '#define'", GetErrors(diagnostics)[0].Message);
    }

    [Fact]
    public void MultipleNestedBlocks()
    {
        var source = @"
#if A
    #if B
        #if C
            code_abc();
        #endif
    #endif
#endif";
        var constants = new Dictionary<string, bool>
        {
            ["A"] = true,
            ["B"] = true,
            ["C"] = true
        };
        var (result, diagnostics) = Preprocess(source, constants);

        Assert.Empty(GetErrors(diagnostics));
        Assert.Contains("code_abc();", result);
    }

    [Fact]
    public void ComplexNestedIfElifElse()
    {
        var source = @"
#if PLATFORM_AMIGA
    #if CPU_68000
        let cpu = ""68000"";
    #elif CPU_68020
        let cpu = ""68020"";
    #else
        let cpu = ""68040"";
    #endif
#else
    let cpu = ""x86"";
#endif";
        var constants = new Dictionary<string, bool>
        {
            ["PLATFORM_AMIGA"] = true,
            ["CPU_68000"] = false,
            ["CPU_68020"] = true
        };
        var (result, diagnostics) = Preprocess(source, constants);

        Assert.Empty(GetErrors(diagnostics));
        Assert.Contains("let cpu = \"68020\";", result);
        Assert.DoesNotContain("let cpu = \"68000\";", result);
        Assert.DoesNotContain("let cpu = \"68040\";", result);
        Assert.DoesNotContain("let cpu = \"x86\";", result);
    }

    [Fact]
    public void EmptySource_ProducesEmptyResult()
    {
        var source = "";
        var constants = new Dictionary<string, bool>();
        var (result, diagnostics) = Preprocess(source, constants);

        Assert.Empty(GetErrors(diagnostics));
        // Preprocessor may add a trailing newline
        Assert.True(string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void SourceWithNoDirectives_UnchangedExceptNewlines()
    {
        var source = @"fn test() {
    let x = 1;
    let y = 2;
    return x + y;
}";
        var constants = new Dictionary<string, bool>();
        var (result, diagnostics) = Preprocess(source, constants);

        Assert.Empty(GetErrors(diagnostics));
        Assert.Contains("fn test() {", result);
        Assert.Contains("let x = 1;", result);
        Assert.Contains("let y = 2;", result);
        Assert.Contains("return x + y;", result);
    }
}

using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Novus.Frontend;
using Xunit;
using System.IO;
using System.Linq;

namespace Novus.Tests;

/// <summary>
/// Regression test for mem.novus Result variant construction in generic functions.
/// This tests the actual failing code patterns from mem.novus lines 228, 235, 246, 248, 370, 371.
/// </summary>
public class MemNovusRegressionTest
{
    private static string GetProjectRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "Novus.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? throw new InvalidOperationException("Could not find project root");
    }

    [Fact]
    public void MemNovus_AllocationTryNew_ShouldCompile()
    {
        // This is a simplified version of Allocation<T>::try_new from mem.novus lines 222-250
        var source = @"
from std::core import Result
from amiga::sys::exec import ExecError

pub struct TestAllocation<T> {
    value: T,
}

// Generic function that returns Result<Allocation<T>, ExecError>
// Tests the exact pattern from mem.novus line 228 and 246
pub fn try_new<T>(value: T) -> Result<TestAllocation<T>, ExecError> {
    // Line 228: return Result::Ok(empty_alloc)
    let alloc = TestAllocation { value: value }
    return Result::Ok(alloc)
}

// Tests error case from line 235 and 248
pub fn try_new_error<T>() -> Result<TestAllocation<T>, ExecError> {
    return Result::Err(ExecError::NoMem)
}
";

        var diagnostics = new DiagnosticBag();
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);

        var errorListener = new NovusErrorListener(diagnostics, "test.novus", source);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(errorListener);

        var tree = parser.compilationUnit();

        Assert.False(parser.NumberOfSyntaxErrors > 0 || diagnostics.HasErrors,
            "Should not have parse errors");

        var stdPath = Path.Combine(GetProjectRoot(), "Novus", "std");
        var semanticAnalyzer = new SemanticAnalyzer("test.novus", source, stdPath);
        semanticAnalyzer.Analyze(tree);

        if (semanticAnalyzer.Diagnostics.HasErrors)
        {
            var errors = string.Join("\n", semanticAnalyzer.Diagnostics.Diagnostics
                .Where(d => d.IsError)
                .Select(d => $"{d.Code}: {d.Message} at line {d.Location?.Line}"));
            Assert.Fail($"Should not have semantic errors:\n{errors}");
        }
    }

    [Fact]
    public void MemNovus_BoxTryNew_ShouldCompile()
    {
        // This is a simplified version of Box<T>::try_new_in from mem.novus lines 368-372
        var source = @"
from std::core import Result, Option
from amiga::sys::exec import ExecError

pub struct TestBox<T> {
    value: T,
}

// Tests pattern from line 370-371
pub fn try_new<T>(value: T) -> Result<TestBox<T>, ExecError> {
    let opt: Option<TestBox<T>> = Option::Some(TestBox { value: value })
    match opt {
        Some(box) => return Result::Ok(box),
        None => return Result::Err(ExecError::NoMem)
    }
}
";

        var diagnostics = new DiagnosticBag();
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);

        var errorListener = new NovusErrorListener(diagnostics, "test.novus", source);
        parser.RemoveErrorListeners();
        parser.AddErrorListener(errorListener);

        var tree = parser.compilationUnit();

        Assert.False(parser.NumberOfSyntaxErrors > 0 || diagnostics.HasErrors,
            "Should not have parse errors");

        var stdPath = Path.Combine(GetProjectRoot(), "Novus", "std");
        var semanticAnalyzer = new SemanticAnalyzer("test.novus", source, stdPath);
        semanticAnalyzer.Analyze(tree);

        if (semanticAnalyzer.Diagnostics.HasErrors)
        {
            var errors = string.Join("\n", semanticAnalyzer.Diagnostics.Diagnostics
                .Where(d => d.IsError)
                .Select(d => $"{d.Code}: {d.Message} at line {d.Location?.Line}"));
            Assert.Fail($"Should not have semantic errors:\n{errors}");
        }
    }
}

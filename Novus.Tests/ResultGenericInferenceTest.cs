using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;
using System.IO;
using System.Linq;

namespace Novus.Tests;

[Trait("Category", "CompilerIntegration")]
public class ResultGenericInferenceTest
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
    public void ResultVariantInference_ShouldInferBothTypeParameters()
    {
        var source = @"
from std::core import Result
from amiga::sys::exec import ExecError

pub struct TestStruct {
    value: u32,
}

pub fn test_concrete() -> Result<TestStruct, ExecError> {
    let s = TestStruct { value: 42 }
    return Result::Ok(s)
}

pub fn test_error() -> Result<TestStruct, ExecError> {
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
    public void ResultVariantInference_InGenericFunction_ShouldInferBothTypeParameters()
    {
        var source = @"
from std::core import Result
from amiga::sys::exec import ExecError

pub fn test_generic<T>(value: T) -> Result<T, ExecError> {
    return Result::Ok(value)
}

pub fn test_generic_error<T>() -> Result<T, ExecError> {
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
}

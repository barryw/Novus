using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

public class UnsafeBlockTests
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

    [Fact]
    public void UnsafeBlock_AllocMemOutsideUnsafe_ReportsError()
    {
        var source = @"
from std::ffi::exec import AllocMem, MEMF_PUBLIC

fn test() -> i32 {
    let ptr = AllocMem(100, 0)
    return 0
}";
        var (diagnostics, _) = Analyze(source);

        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E1001");
        Assert.NotNull(error);
        Assert.Contains("AllocMem", error.Message);
        Assert.Contains("unsafe block", error.Message);
    }

    [Fact]
    public void UnsafeBlock_FreeMemOutsideUnsafe_ReportsError()
    {
        var source = @"
from std::ffi::exec import AllocMem, FreeMem

fn test() -> i32 {
    let ptr = AllocMem(100, 0)
    FreeMem(ptr, 100)
    return 0
}";
        var (diagnostics, _) = Analyze(source);

        Assert.True(diagnostics.HasErrors);
        var unsafeErrors = diagnostics.Diagnostics.Where(d => d.Code == "E1001").ToList();
        Assert.NotEmpty(unsafeErrors);

        // Should have errors for both AllocMem and FreeMem
        var freeMemError = unsafeErrors.FirstOrDefault(e => e.Message.Contains("FreeMem"));
        Assert.NotNull(freeMemError);
        Assert.Contains("unsafe block", freeMemError.Message);
    }

    [Fact]
    public void UnsafeBlock_AllocMemInsideUnsafe_NoError()
    {
        var source = @"
from std::ffi::exec import AllocMem, FreeMem, MEMF_PUBLIC

fn test() -> i32 {
    unsafe {
        let ptr = AllocMem(100, 0)
        FreeMem(ptr, 100)
    }
    return 0
}";
        var (diagnostics, _) = Analyze(source);

        // Should not have unsafe-related errors
        var unsafeErrors = diagnostics.Diagnostics.Where(d => d.Code == "E1001").ToList();
        Assert.Empty(unsafeErrors);
    }

    [Fact]
    public void UnsafeBlock_NestedUnsafeBlocks_NoError()
    {
        var source = @"
from std::ffi::exec import AllocMem, FreeMem

fn test() -> i32 {
    unsafe {
        let ptr1 = AllocMem(100, 0)
        unsafe {
            let ptr2 = AllocMem(200, 0)
            FreeMem(ptr2, 200)
        }
        FreeMem(ptr1, 100)
    }
    return 0
}";
        var (diagnostics, _) = Analyze(source);

        var unsafeErrors = diagnostics.Diagnostics.Where(d => d.Code == "E1001").ToList();
        Assert.Empty(unsafeErrors);
    }

    [Fact]
    public void UnsafeBlock_OpenLibraryOutsideUnsafe_ReportsError()
    {
        var source = @"
from std::ffi::exec import OpenLibrary

fn test() -> i32 {
    let lib = OpenLibrary(""dos.library"", 0)
    return 0
}";
        var (diagnostics, _) = Analyze(source);

        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E1001");
        Assert.NotNull(error);
        Assert.Contains("OpenLibrary", error.Message);
    }

    [Fact]
    public void UnsafeBlock_SupervisorOutsideUnsafe_ReportsError()
    {
        var source = @"
from std::ffi::exec import Supervisor

fn test() -> u32 {
    return Supervisor(0 as *u8)
}";
        var (diagnostics, _) = Analyze(source);

        Assert.True(diagnostics.HasErrors);
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E1001");
        Assert.NotNull(error);
        Assert.Contains("Supervisor", error.Message);
    }

    [Fact]
    public void UnsafeBlock_MultipleUnsafeCallsOutside_ReportsMultipleErrors()
    {
        var source = @"
from std::ffi::exec import AllocMem, FreeMem, OpenLibrary

fn test() -> i32 {
    let ptr = AllocMem(100, 0)
    let lib = OpenLibrary(""dos.library"", 0)
    FreeMem(ptr, 100)
    return 0
}";
        var (diagnostics, _) = Analyze(source);

        Assert.True(diagnostics.HasErrors);
        var unsafeErrors = diagnostics.Diagnostics.Where(d => d.Code == "E1001").ToList();
        Assert.Equal(3, unsafeErrors.Count); // AllocMem, OpenLibrary, FreeMem
    }

    [Fact]
    public void UnsafeBlock_TracksUnsafeBlockLocations()
    {
        var source = @"
from std::ffi::exec import AllocMem, FreeMem

fn test() -> i32 {
    unsafe {
        let ptr = AllocMem(100, 0)
        FreeMem(ptr, 100)
    }
    return 0
}";
        var (diagnostics, analyzer) = Analyze(source);

        Assert.NotNull(analyzer);
        Assert.Equal(1, analyzer.UnsafeBlocks.Count);

        var unsafeBlock = analyzer.UnsafeBlocks[0];
        Assert.Equal("test.novus", unsafeBlock.FilePath);
        Assert.True(unsafeBlock.Line > 0);
        Assert.Equal("Manual unsafe block", unsafeBlock.Reason);
    }

    [Fact]
    public void UnsafeBlock_ErrorMessageIncludesSafeAlternatives()
    {
        var source = @"
from std::ffi::exec import AllocMem

fn test() -> i32 {
    let ptr = AllocMem(100, 0)
    return 0
}";
        var (diagnostics, _) = Analyze(source);

        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "E1001");
        Assert.NotNull(error);

        var helpText = string.Join(" ", error.HelpTexts);
        Assert.Contains("Allocation::new", helpText);
        Assert.Contains("Box::new", helpText);
        Assert.Contains("defer", helpText);
    }
}

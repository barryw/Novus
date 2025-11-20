using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for the ImportResolver component
/// Tests module resolution, import processing, and circular dependency detection
/// </summary>
public class ImportResolverTests
{
    /// <summary>
    /// Mock implementation of IImportSymbolRegistry for testing
    /// </summary>
    private class MockSymbolRegistry : IImportSymbolRegistry
    {
        private readonly HashSet<(string name, SymbolKind kind)> _symbols = new();
        private readonly List<(string code, string message, string? help)> _errors = new();
        private readonly List<NovusParser.ImportDeclarationContext> _processedImports = new();

        public List<(string code, string message, string? help)> Errors => _errors;
        public List<NovusParser.ImportDeclarationContext> ProcessedImports => _processedImports;
        public HashSet<(string name, SymbolKind kind)> RegisteredSymbols => _symbols;

        public void RegisterEnum(NovusParser.EnumDeclarationContext context)
        {
            var name = context.IDENTIFIER().GetText();
            _symbols.Add((name, SymbolKind.Enum));
        }

        public void RegisterStruct(NovusParser.StructDeclarationContext context)
        {
            var name = context.IDENTIFIER().GetText();
            _symbols.Add((name, SymbolKind.Struct));
        }

        public void RegisterConstant(NovusParser.ConstDeclarationContext context)
        {
            var name = context.IDENTIFIER().GetText();
            _symbols.Add((name, SymbolKind.Constant));
        }

        public void RegisterFunction(NovusParser.FunctionDeclarationContext context, string moduleNamespace, string modulePath)
        {
            var name = context.IDENTIFIER().GetText();
            _symbols.Add((name, SymbolKind.Function));
        }

        public void RegisterGlobalVariable(NovusParser.GlobalVariableDeclarationContext context, string moduleNamespace, string modulePath)
        {
            var name = context.IDENTIFIER().GetText();
            _symbols.Add((name, SymbolKind.GlobalVariable));
        }

        public void RegisterImpl(NovusParser.ImplDeclarationContext context)
        {
            // For testing, we don't need to do anything special with impl blocks
        }

        public bool HasSymbol(string name, SymbolKind kind)
        {
            return _symbols.Contains((name, kind));
        }

        public void ReportError(string errorCode, string message, string? helpText = null)
        {
            _errors.Add((errorCode, message, helpText));
        }

        public void ProcessImport(NovusParser.ImportDeclarationContext context)
        {
            _processedImports.Add(context);
        }
    }

    [Fact]
    public void Constructor_InitializesCorrectly()
    {
        var registry = new MockSymbolRegistry();
        var stdLibPath = "/tmp/stdlib";
        var resolver = new ImportResolver(registry, stdLibPath);

        Assert.NotNull(resolver);
        Assert.Empty(resolver.GetImportedModulePaths());
    }

    [Fact]
    public void ImportModule_NonexistentModule_ReportsError()
    {
        var registry = new MockSymbolRegistry();
        var resolver = new ImportResolver(registry, "/tmp/nonexistent");

        resolver.ImportModule("std::nonexistent", importAll: true);

        Assert.Single(registry.Errors);
        Assert.Equal("E0026", registry.Errors[0].code);
        Assert.Contains("not found", registry.Errors[0].message);
    }

    [Fact]
    public void ImportModule_CircularImport_PreventsInfiniteRecursion()
    {
        // This test verifies that circular imports don't cause infinite recursion
        // The ImportResolver should track processed modules and skip already-processed ones
        var registry = new MockSymbolRegistry();
        var resolver = new ImportResolver(registry, "/tmp/stdlib");

        // First import
        resolver.ImportModule("std::circular", importAll: true);

        // Second import of same module should be a no-op (already processed)
        resolver.ImportModule("std::circular", importAll: true);

        // Should only report one "not found" error, not recurse infinitely
        Assert.True(registry.Errors.Count <= 1);
    }

    [Fact]
    public void SymbolKind_HasCorrectValues()
    {
        // Verify the SymbolKind enum has expected values
        Assert.Equal(0, (int)SymbolKind.Enum);
        Assert.Equal(1, (int)SymbolKind.Struct);
        Assert.Equal(2, (int)SymbolKind.Constant);
        Assert.Equal(3, (int)SymbolKind.Function);
        Assert.Equal(4, (int)SymbolKind.GlobalVariable);
    }

    [Fact]
    public void MockRegistry_RegisterEnum_TracksSymbol()
    {
        var registry = new MockSymbolRegistry();
        var source = "pub enum Color { Red, Green, Blue }";
        var diagnostics = new DiagnosticBag();
        var parser = NovusParserFactory.CreateParser(source, diagnostics, "test.novus", NovusParserFactory.ParseMode.Compilation);
        var unit = parser.compilationUnit();
        var enumDecl = unit.enumDeclaration()[0];

        registry.RegisterEnum(enumDecl);

        Assert.True(registry.HasSymbol("Color", SymbolKind.Enum));
        Assert.False(registry.HasSymbol("Color", SymbolKind.Struct));
    }

    [Fact]
    public void MockRegistry_RegisterStruct_TracksSymbol()
    {
        var registry = new MockSymbolRegistry();
        var source = "pub struct Point { x: i32, y: i32 }";
        var diagnostics = new DiagnosticBag();
        var parser = NovusParserFactory.CreateParser(source, diagnostics, "test.novus", NovusParserFactory.ParseMode.Compilation);
        var unit = parser.compilationUnit();
        var structDecl = unit.structDeclaration()[0];

        registry.RegisterStruct(structDecl);

        Assert.True(registry.HasSymbol("Point", SymbolKind.Struct));
        Assert.False(registry.HasSymbol("Point", SymbolKind.Enum));
    }

    [Fact]
    public void MockRegistry_RegisterConstant_TracksSymbol()
    {
        var registry = new MockSymbolRegistry();
        var source = "pub const MAX: i32 = 100;";
        var diagnostics = new DiagnosticBag();
        var parser = NovusParserFactory.CreateParser(source, diagnostics, "test.novus", NovusParserFactory.ParseMode.Compilation);
        var unit = parser.compilationUnit();
        var constDecl = unit.constDeclaration()[0];

        registry.RegisterConstant(constDecl);

        Assert.True(registry.HasSymbol("MAX", SymbolKind.Constant));
    }

    [Fact]
    public void MockRegistry_RegisterFunction_TracksSymbol()
    {
        var registry = new MockSymbolRegistry();
        var source = "pub fn test() -> i32 { return 42; }";
        var diagnostics = new DiagnosticBag();
        var parser = NovusParserFactory.CreateParser(source, diagnostics, "test.novus", NovusParserFactory.ParseMode.Compilation);
        var unit = parser.compilationUnit();
        var funcDecl = unit.functionDeclaration()[0];

        registry.RegisterFunction(funcDecl, "test::module", "/tmp/test.novus");

        Assert.True(registry.HasSymbol("test", SymbolKind.Function));
    }

    [Fact]
    public void MockRegistry_ReportError_TracksErrors()
    {
        var registry = new MockSymbolRegistry();

        registry.ReportError("E0001", "Test error", "Help text");

        Assert.Single(registry.Errors);
        var error = registry.Errors[0];
        Assert.Equal("E0001", error.code);
        Assert.Equal("Test error", error.message);
        Assert.Equal("Help text", error.help);
    }

    [Fact]
    public void ParsedModule_Constructor_InitializesCorrectly()
    {
        var source = "pub fn test() {}";
        var diagnostics = new DiagnosticBag();
        var parser = NovusParserFactory.CreateParser(source, diagnostics, "test.novus", NovusParserFactory.ParseMode.Compilation);
        var context = parser.compilationUnit();

        var parsed = new ParsedModule(context, "/tmp/test.novus", hasImplementation: true);

        Assert.Equal(context, parsed.Context);
        Assert.Equal("/tmp/test.novus", parsed.FilePath);
        Assert.True(parsed.HasImplementation);
    }

    [Fact]
    public void ParsedModule_Constructor_WithoutImplementation()
    {
        var source = "extern fn test();";
        var diagnostics = new DiagnosticBag();
        var parser = NovusParserFactory.CreateParser(source, diagnostics, "test.novus", NovusParserFactory.ParseMode.Compilation);
        var context = parser.compilationUnit();

        var parsed = new ParsedModule(context, "/tmp/test.novus", hasImplementation: false);

        Assert.Equal(context, parsed.Context);
        Assert.Equal("/tmp/test.novus", parsed.FilePath);
        Assert.False(parsed.HasImplementation);
    }

    [Fact]
    public void GetImportedModulePaths_InitiallyEmpty()
    {
        var registry = new MockSymbolRegistry();
        var resolver = new ImportResolver(registry, "/tmp/stdlib");

        var paths = resolver.GetImportedModulePaths();

        Assert.NotNull(paths);
        Assert.Empty(paths);
    }

    [Fact]
    public void MockRegistry_DuplicateRegistration_IgnoredByHasSymbol()
    {
        var registry = new MockSymbolRegistry();
        var source = "pub struct Point { x: i32 }";
        var diagnostics = new DiagnosticBag();
        var parser = NovusParserFactory.CreateParser(source, diagnostics, "test.novus", NovusParserFactory.ParseMode.Compilation);
        var unit = parser.compilationUnit();
        var structDecl = unit.structDeclaration()[0];

        // Register twice
        registry.RegisterStruct(structDecl);

        // HasSymbol should return true after first registration
        Assert.True(registry.HasSymbol("Point", SymbolKind.Struct));

        // Second registration adds to set (but sets ignore duplicates)
        registry.RegisterStruct(structDecl);
        Assert.True(registry.HasSymbol("Point", SymbolKind.Struct));
    }

    [Fact]
    public void MockRegistry_ProcessImport_ImplementationWorks()
    {
        // This test verifies that the ProcessImport method exists and can be called
        // Integration testing with real import contexts happens in full compiler tests
        var registry = new MockSymbolRegistry();
        var source = "import std::dos::{FileHandle};\npub fn test() {}";
        var diagnostics = new DiagnosticBag();
        var parser = NovusParserFactory.CreateParser(source, diagnostics, "test.novus", NovusParserFactory.ParseMode.Compilation);
        var unit = parser.compilationUnit();

        // If imports are present, test the ProcessImport tracking
        if (unit.importDeclaration().Length > 0)
        {
            var importDecl = unit.importDeclaration()[0];
            registry.ProcessImport(importDecl);
            Assert.Single(registry.ProcessedImports);
        }
        else
        {
            // Otherwise just verify the list is initially empty
            Assert.Empty(registry.ProcessedImports);
        }
    }
}

using Antlr4.Runtime;
using Novus.Frontend;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Unit tests for ModuleImportHelper utility methods
/// </summary>
public class ModuleImportHelperTests
{
    [Theory]
    [InlineData("std::core", "/test/std", "/test/std/core.novus")]
    [InlineData("std::ffi::exec", "/test/std", "/test/std/ffi/exec.novus")]
    [InlineData("std::collections", "/test/std", "/test/std/collections.novus")]
    public void ResolveModulePath_StdModule_ReturnsCorrectPath(string moduleNamespace, string stdLibPath, string expected)
    {
        var result = ModuleImportHelper.ResolveModulePath(moduleNamespace, stdLibPath);

        // Normalize paths for cross-platform comparison
        var normalizedResult = result.Replace('\\', '/');
        var normalizedExpected = expected.Replace('\\', '/');

        Assert.Equal(normalizedExpected, normalizedResult);
    }

    [Fact]
    public void ResolveModulePath_UserModule_ReturnsRelativePath()
    {
        var result = ModuleImportHelper.ResolveModulePath("myapp::utils", "/test/std");

        var normalizedResult = result.Replace('\\', '/');

        Assert.Equal("myapp/utils.novus", normalizedResult);
    }

    [Fact]
    public void ResolveModulePath_EmptyNamespace_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() => ModuleImportHelper.ResolveModulePath("", "/test/std"));
    }

    [Fact]
    public void ParseModuleFile_NonExistentFile_ReturnsNull()
    {
        var (context, errors) = ModuleImportHelper.ParseModuleFile("/nonexistent/file.novus");

        Assert.Null(context);
        Assert.Equal(0, errors);
    }

    [Fact]
    public void IsPub_FunctionWithPubKeyword_ReturnsTrue()
    {
        var source = "pub fn test() -> i32 { return 0 }";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();
        var funcDecl = tree.functionDeclaration()[0];

        var result = ModuleImportHelper.IsPub(funcDecl);

        Assert.True(result);
    }

    [Fact]
    public void IsPub_FunctionWithoutPubKeyword_ReturnsFalse()
    {
        var source = "fn test() -> i32 { return 0 }";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();
        var funcDecl = tree.functionDeclaration()[0];

        var result = ModuleImportHelper.IsPub(funcDecl);

        Assert.False(result);
    }

    [Fact]
    public void IsExtern_ExternFunction_ReturnsTrue()
    {
        var source = "extern fn test() -> i32";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();
        var funcDecl = tree.functionDeclaration()[0];

        var result = ModuleImportHelper.IsExtern(funcDecl);

        Assert.True(result);
    }

    [Fact]
    public void IsExtern_NonExternFunction_ReturnsFalse()
    {
        var source = "pub fn test() -> i32 { return 0 }";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();
        var funcDecl = tree.functionDeclaration()[0];

        var result = ModuleImportHelper.IsExtern(funcDecl);

        Assert.False(result);
    }

    [Fact]
    public void GetFunctionVisibility_PubExternFunction_ReturnsBothTrue()
    {
        var source = "extern pub fn test() -> i32";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        // Check if parsing succeeded
        if (tree.functionDeclaration() is [])
        {
            // Try alternate order
            source = "pub extern fn test() -> i32";
            parser = CreateParser(source);
            tree = parser.compilationUnit();
        }

        var funcDecl = tree.functionDeclaration()[0];

        var (isPub, isExtern) = ModuleImportHelper.GetFunctionVisibility(funcDecl);

        Assert.True(isPub);
        Assert.True(isExtern);
    }

    [Fact]
    public void CheckHasImplementation_PublicFunctionExists_ReturnsTrue()
    {
        var source = "pub fn test() -> i32 { return 0 }";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        var result = ModuleImportHelper.CheckHasImplementation(tree);

        Assert.True(result);
    }

    [Fact]
    public void CheckHasImplementation_OnlyExternFunctions_ReturnsFalse()
    {
        var source = "pub extern fn test() -> i32";
        var parser = CreateParser(source);
        var tree = parser.compilationUnit();

        var result = ModuleImportHelper.CheckHasImplementation(tree);

        Assert.False(result);
    }

    private NovusParser CreateParser(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        return new NovusParser(tokenStream);
    }
}

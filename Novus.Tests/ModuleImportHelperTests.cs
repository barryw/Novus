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
    public void ResolveModulePath_UserModule_UsesSourceDirectoryWhenProvided()
    {
        var result = ModuleImportHelper.ResolveModulePath(
            "helpers", "/test/std", Path.Combine("project", "src"));

        Assert.Equal(
            Path.Combine("project", "src", "helpers.novus"),
            result);
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
    public void ParseModuleFile_ReusesUnchangedParseAndInvalidatesChangedSource()
    {
        var path = Path.Combine(Path.GetTempPath(), $"novus-import-{Guid.NewGuid():N}.novus");
        try
        {
            File.WriteAllText(path, "pub fn value() -> i32 { return 1 }");
            var (first, firstErrors) = ModuleImportHelper.ParseModuleFile(path);
            var (second, secondErrors) = ModuleImportHelper.ParseModuleFile(path);

            Assert.Same(first, second);
            Assert.Equal(0, firstErrors);
            Assert.Equal(0, secondErrors);

            File.WriteAllText(path, "pub fn value() -> i32 { return 2 }");
            var (changed, changedErrors) = ModuleImportHelper.ParseModuleFile(path);
            Assert.NotSame(first, changed);
            Assert.Equal(0, changedErrors);
        }
        finally
        {
            File.Delete(path);
        }
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

    [Fact]
    public void BuildImportNameSet_IncludesDependenciesOfWildcardConstants()
    {
        var module = CreateParser("pub const TAG_USER: u32 = 1 << 31\npub const WA_Dummy: u32 = TAG_USER + 99\n")
            .compilationUnit();
        var request = CreateParser("from std::ffi::amiga_consts import WA_*\n")
            .compilationUnit().importDeclaration()[0];

        var names = ModuleImportHelper.BuildImportNameSet(module, false, request.importList());

        Assert.Contains("WA_Dummy", names);
        Assert.Contains("TAG_USER", names);
    }

    private NovusParser CreateParser(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        return new NovusParser(tokenStream);
    }
}

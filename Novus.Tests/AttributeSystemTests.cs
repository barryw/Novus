using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

public class AttributeSystemTests
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
    public void Attribute_SimpleAttributeOnStruct_Parses()
    {
        var source = @"
@test
pub struct TestStruct {
    value: i32,
}";
        var (diagnostics, _) = Analyze(source);

        // Should parse without errors (test is a known attribute)
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Attribute_NamedArgumentsOnStruct_ParsesCorrectly()
    {
        var source = @"
@library(name = ""example.library"", version = 1, revision = 0)
pub struct MyLibrary {
    counter: u32,
}";
        var (diagnostics, _) = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Attribute_MultipleAttributesOnStruct_ParsesAll()
    {
        var source = @"
@packed
@align(4)
pub struct AlignedStruct {
    data: [100]u8,
}";
        var (diagnostics, _) = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Attribute_OnEnum_Parses()
    {
        var source = @"
@experimental
pub enum MyEnum {
    Variant1,
    Variant2(i32),
}";
        var (diagnostics, _) = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Attribute_UnknownAttribute_GeneratesWarning()
    {
        var source = @"
@unknown_attribute
pub struct TestStruct {
    value: i32,
}";
        var (diagnostics, _) = Analyze(source);

        // Should not error, but should warn
        Assert.False(diagnostics.HasErrors);
        Assert.True(diagnostics.HasWarnings);

        var warning = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == "W2001");
        Assert.NotNull(warning);
        Assert.Contains("unknown_attribute", warning.Message);
    }

    [Fact]
    public void Attribute_DeprecatedWithArguments_Parses()
    {
        var source = @"
@deprecated(since = ""2.0"", note = ""Use new_api instead"")
pub struct OldStruct {
    value: i32,
}";
        var (diagnostics, _) = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Attribute_IntegerArgument_Parses()
    {
        var source = @"
@align(16)
pub struct AlignedStruct {
    data: u32,
}";
        var (diagnostics, _) = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Attribute_BooleanArgument_Parses()
    {
        var source = @"
@cfg(feature = true)
pub struct ConditionalStruct {
    value: i32,
}";
        var (diagnostics, _) = Analyze(source);

        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void AttributeInfo_GetString_ReturnsCorrectValue()
    {
        var attr = new AttributeInfo("test", new SourceLocation("test.novus", 1, 1, 0, ""));
        attr.NamedArgs["name"] = "example";

        Assert.Equal("example", attr.GetString("name"));
        Assert.Null(attr.GetString("missing"));
    }

    [Fact]
    public void AttributeInfo_GetInt_ReturnsCorrectValue()
    {
        var attr = new AttributeInfo("test", new SourceLocation("test.novus", 1, 1, 0, ""));
        attr.NamedArgs["version"] = 42;

        Assert.Equal(42, attr.GetInt("version"));
        Assert.Null(attr.GetInt("missing"));
    }

    [Fact]
    public void AttributeInfo_GetBool_ReturnsCorrectValue()
    {
        var attr = new AttributeInfo("test", new SourceLocation("test.novus", 1, 1, 0, ""));
        attr.NamedArgs["enabled"] = true;

        Assert.Equal(true, attr.GetBool("enabled"));
        Assert.Null(attr.GetBool("missing"));
    }

    [Fact]
    public void AttributeInfo_HasArg_ReturnsCorrectValue()
    {
        var attr = new AttributeInfo("test", new SourceLocation("test.novus", 1, 1, 0, ""));
        attr.NamedArgs["name"] = "example";

        Assert.True(attr.HasArg("name"));
        Assert.False(attr.HasArg("missing"));
    }

    [Fact]
    public void AttributeInfo_PositionalArgs_StoresCorrectly()
    {
        var attr = new AttributeInfo("test", new SourceLocation("test.novus", 1, 1, 0, ""));
        attr.PositionalArgs.Add("arg1");
        attr.PositionalArgs.Add(42);
        attr.PositionalArgs.Add(true);

        Assert.Equal(3, attr.PositionalArgs.Count);
        Assert.Equal("arg1", attr.GetPositionalArg<string>(0));
        Assert.Equal(42, attr.GetPositionalArg<int>(1));
        Assert.Equal(true, attr.GetPositionalArg<bool>(2));
    }

    [Fact]
    public void AttributeCollection_Has_ReturnsCorrectValue()
    {
        var collection = new AttributeCollection();
        var attr1 = new AttributeInfo("test", new SourceLocation("test.novus", 1, 1, 0, ""));
        var attr2 = new AttributeInfo("inline", new SourceLocation("test.novus", 2, 1, 0, ""));

        collection.Add(attr1);
        collection.Add(attr2);

        Assert.True(collection.Has("test"));
        Assert.True(collection.Has("inline"));
        Assert.False(collection.Has("missing"));
    }

    [Fact]
    public void AttributeCollection_Get_ReturnsFirstMatch()
    {
        var collection = new AttributeCollection();
        var attr1 = new AttributeInfo("test", new SourceLocation("test.novus", 1, 1, 0, ""));
        var attr2 = new AttributeInfo("test", new SourceLocation("test.novus", 2, 1, 0, ""));

        collection.Add(attr1);
        collection.Add(attr2);

        var result = collection.Get("test");
        Assert.NotNull(result);
        Assert.Equal(1, result.Location.Line);
    }

    [Fact]
    public void AttributeCollection_GetAll_ReturnsAllMatches()
    {
        var collection = new AttributeCollection();
        var attr1 = new AttributeInfo("test", new SourceLocation("test.novus", 1, 1, 0, ""));
        var attr2 = new AttributeInfo("test", new SourceLocation("test.novus", 2, 1, 0, ""));
        var attr3 = new AttributeInfo("inline", new SourceLocation("test.novus", 3, 1, 0, ""));

        collection.Add(attr1);
        collection.Add(attr2);
        collection.Add(attr3);

        var results = collection.GetAll("test");
        Assert.Equal(2, results.Count);

        var inlineResults = collection.GetAll("inline");
        Assert.Single(inlineResults);
    }

    [Fact]
    public void KnownAttributes_IsKnown_ValidatesCorrectly()
    {
        Assert.True(KnownAttributes.IsKnown("library"));
        Assert.True(KnownAttributes.IsKnown("inline"));
        Assert.True(KnownAttributes.IsKnown("test"));
        Assert.True(KnownAttributes.IsKnown("deprecated"));
        Assert.True(KnownAttributes.IsKnown("experimental"));

        Assert.False(KnownAttributes.IsKnown("unknown"));
        Assert.False(KnownAttributes.IsKnown("random"));
    }

    [Fact]
    public void KnownAttributes_All_ContainsExpectedAttributes()
    {
        var allAttrs = KnownAttributes.All;

        // Library attributes
        Assert.Contains("library", allAttrs);
        Assert.Contains("libfunc", allAttrs);
        Assert.Contains("libopen", allAttrs);

        // Code generation
        Assert.Contains("inline", allAttrs);
        Assert.Contains("packed", allAttrs);

        // Testing
        Assert.Contains("test", allAttrs);
        Assert.Contains("benchmark", allAttrs);

        // Documentation
        Assert.Contains("deprecated", allAttrs);
        Assert.Contains("experimental", allAttrs);

        // Safety
        Assert.Contains("threadsafe", allAttrs);
        Assert.Contains("unsafe", allAttrs);

        // Optimization
        Assert.Contains("cold", allAttrs);
        Assert.Contains("hot", allAttrs);
    }

    [Fact]
    public void AttributeInfo_ToString_FormatsCorrectly()
    {
        var attr = new AttributeInfo("library", new SourceLocation("test.novus", 1, 1, 0, ""));
        attr.NamedArgs["name"] = "example.library";
        attr.NamedArgs["version"] = 1;

        var str = attr.ToString();
        Assert.Contains("@library", str);
        Assert.Contains("name = example.library", str);
        Assert.Contains("version = 1", str);
    }

    [Fact]
    public void AttributeCollection_ToString_FormatsAllAttributes()
    {
        var collection = new AttributeCollection();
        var attr1 = new AttributeInfo("test", new SourceLocation("test.novus", 1, 1, 0, ""));
        var attr2 = new AttributeInfo("inline", new SourceLocation("test.novus", 2, 1, 0, ""));

        collection.Add(attr1);
        collection.Add(attr2);

        var str = collection.ToString();
        Assert.Contains("@test", str);
        Assert.Contains("@inline", str);
    }
}

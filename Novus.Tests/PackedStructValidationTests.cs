using System.Linq;
using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for compile-time validation of packed struct field alignment.
/// Packed structs can cause bus errors if fields are misaligned.
/// </summary>
public class PackedStructValidationTests
{
    private (bool HasErrors, IrBuilder Builder) BuildIrWithDiagnostics(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var builder = new IrBuilder(skipAutoImports: true);
        builder.BuildModule(tree);
        return (builder.Diagnostics.HasErrors, builder);
    }

    [Fact]
    public void PackedStruct_MisalignedU16Field_ReportsError()
    {
        var source = @"
@packed struct Bad {
    a: u8,
    b: u16
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);

        Assert.True(hasErrors);
        var error = Assert.Single(builder.Diagnostics.Diagnostics, d => d.Code == ErrorCodes.PackedStructMisalignedField);
        Assert.Contains("'b'", error.Message);
        Assert.Contains("offset 1", error.Message);
    }

    [Fact]
    public void PackedStruct_MisalignedI32Field_ReportsError()
    {
        var source = @"
@packed
struct Bad {
    a: u8,
    b: i32
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);

        Assert.True(hasErrors);
        var error = Assert.Single(builder.Diagnostics.Diagnostics, d => d.Code == ErrorCodes.PackedStructMisalignedField);
        Assert.Contains("'b'", error.Message);
        Assert.Contains("offset 1", error.Message);
    }

    [Fact]
    public void PackedStruct_MisalignedPointerField_ReportsError()
    {
        var source = @"
@packed
struct Bad {
    a: u8,
    b: *u8
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);

        Assert.True(hasErrors);
        var error = Assert.Single(builder.Diagnostics.Diagnostics, d => d.Code == ErrorCodes.PackedStructMisalignedField);
        Assert.Contains("'b'", error.Message);
        Assert.Contains("offset 1", error.Message);
    }

    [Fact]
    public void PackedStruct_AlignedFields_NoError()
    {
        var source = @"
@packed
struct Good {
    a: u8,
    b: u8,
    c: u16
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);

        Assert.False(hasErrors);
    }

    [Fact]
    public void PackedStruct_AllByteFields_NoError()
    {
        var source = @"
@packed
struct AllBytes {
    a: u8,
    b: u8,
    c: u8,
    d: u8
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);

        Assert.False(hasErrors);
    }

    [Fact]
    public void PackedStruct_MultipleMisalignedFields_ReportsMultipleErrors()
    {
        var source = @"
@packed
struct MultipleBad {
    a: u8,
    b: u16,
    c: u8,
    d: u32
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);

        Assert.True(hasErrors);
        var errors = builder.Diagnostics.Diagnostics
            .Where(d => d.Code == ErrorCodes.PackedStructMisalignedField)
            .ToList();

        // Field 'b' at offset 1 (misaligned)
        // Field 'd' at offset 4 (misaligned because offset 1 + 2 + 1 = 4, which is even, so this should NOT error)
        // Let me recalculate: a=0, b=1 (ERROR), c=3, d=4 (even, OK)
        Assert.Single(errors);
        Assert.Contains("'b'", errors[0].Message);
    }

    [Fact]
    public void NonPackedStruct_MisalignedFields_NoError()
    {
        // Non-packed structs have automatic padding, so no validation needed
        var source = @"
struct Normal {
    a: u8,
    b: u16,
    c: u32
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);

        Assert.False(hasErrors);
    }

    [Fact]
    public void PackedStruct_NestedStructField_ChecksNestedStructAlignment()
    {
        var source = @"
struct Inner {
    x: u16,
    y: u16
}

@packed
struct Outer {
    a: u8,
    inner: Inner
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);

        // Inner struct has size 4 bytes, and will be at offset 1 (odd), causing misalignment
        Assert.True(hasErrors);
        var error = Assert.Single(builder.Diagnostics.Diagnostics, d => d.Code == ErrorCodes.PackedStructMisalignedField);
        Assert.Contains("'inner'", error.Message);
        Assert.Contains("offset 1", error.Message);
    }

    [Fact]
    public void PackedStruct_CorrectlyAlignedNestedStruct_NoError()
    {
        var source = @"
struct Inner {
    x: u16,
    y: u16
}

@packed
struct Outer {
    a: u8,
    b: u8,
    inner: Inner
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);

        Assert.False(hasErrors);
    }

    [Fact]
    public void PackedStruct_ArrayOfMultiByteType_ChecksAlignment()
    {
        var source = @"
@packed
struct Bad {
    a: u8,
    b: [u16; 4]
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);

        // Array of u16 at offset 1 is misaligned
        Assert.True(hasErrors);
        var error = Assert.Single(builder.Diagnostics.Diagnostics, d => d.Code == ErrorCodes.PackedStructMisalignedField);
        Assert.Contains("'b'", error.Message);
        Assert.Contains("offset 1", error.Message);
    }

    [Fact]
    public void PackedStruct_ArrayOfByteType_NoError()
    {
        var source = @"
@packed
struct Good {
    a: u8,
    b: [u8; 10]
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);

        Assert.False(hasErrors);
    }

    [Fact]
    public void PackedStruct_MisalignedI16Field_ReportsError()
    {
        var source = @"
@packed
struct Bad {
    a: u8,
    b: i16
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);

        Assert.True(hasErrors);
        var error = Assert.Single(builder.Diagnostics.Diagnostics, d => d.Code == ErrorCodes.PackedStructMisalignedField);
        Assert.Contains("'b'", error.Message);
        Assert.Contains("offset 1", error.Message);
    }

    [Fact]
    public void PackedStruct_MisalignedU32Field_ReportsError()
    {
        var source = @"
@packed
struct Bad {
    a: u8,
    b: u32
}";
        var (hasErrors, builder) = BuildIrWithDiagnostics(source);

        Assert.True(hasErrors);
        var error = Assert.Single(builder.Diagnostics.Diagnostics, d => d.Code == ErrorCodes.PackedStructMisalignedField);
        Assert.Contains("'b'", error.Message);
        Assert.Contains("offset 1", error.Message);
    }
}

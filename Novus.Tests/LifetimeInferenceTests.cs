using Novus.IR;
using Novus.SemanticAnalysis;
using Novus.Diagnostics;
using Xunit;

namespace Novus.Tests;

public class LifetimeInferenceTests
{
    private SourceLocation Loc => new("test.novus", 1, 1, 0, "");

    [Fact]
    public void InferReturnLifetime_SelfParam_ReturnsSelfIndex()
    {
        var inference = new LifetimeInference();
        var selfParam = new IrParameter("self", new IrReferenceType(new IrStructType("Screen", new())));
        var otherParam = new IrParameter("depth", IrIntType.I32);
        var returnType = new IrReferenceType(new IrStructType("RastPort", new()));

        var result = inference.InferReturnLifetime(
            new[] { selfParam, otherParam },
            returnType
        );

        Assert.Equal(0, result.SourceParameterIndex);  // self is at index 0
        Assert.True(result.Success);
    }

    [Fact]
    public void InferReturnLifetime_SingleRefParam_ReturnsThatParamIndex()
    {
        var inference = new LifetimeInference();
        var refParam = new IrParameter("screen", new IrReferenceType(new IrStructType("Screen", new())));
        var returnType = new IrReferenceType(new IrStructType("RastPort", new()));

        var result = inference.InferReturnLifetime(
            new[] { refParam },
            returnType
        );

        Assert.Equal(0, result.SourceParameterIndex);  // Only param at index 0
        Assert.True(result.Success);
    }

    [Fact]
    public void InferReturnLifetime_MultipleRefParams_NoSelf_ReturnsError()
    {
        var inference = new LifetimeInference();
        var param1 = new IrParameter("a", new IrReferenceType(new IrStructType("A", new())));
        var param2 = new IrParameter("b", new IrReferenceType(new IrStructType("B", new())));
        var returnType = new IrReferenceType(new IrStructType("C", new()));

        var result = inference.InferReturnLifetime(
            new[] { param1, param2 },
            returnType
        );

        Assert.False(result.Success);
        Assert.Contains("multiple reference parameters", result.ErrorMessage);
    }

    [Fact]
    public void InferReturnLifetime_NoRefParams_ReturnsError()
    {
        var inference = new LifetimeInference();
        var param = new IrParameter("count", IrIntType.I32);
        var returnType = new IrReferenceType(new IrStructType("Thing", new()));

        var result = inference.InferReturnLifetime(
            new[] { param },
            returnType
        );

        Assert.False(result.Success);
        Assert.Contains("no reference parameters", result.ErrorMessage);
    }

    [Fact]
    public void InferReturnLifetime_NonRefReturn_ReturnsNoLifetimeNeeded()
    {
        var inference = new LifetimeInference();
        var param = new IrParameter("x", IrIntType.I32);
        var returnType = IrIntType.I32;

        var result = inference.InferReturnLifetime(
            new[] { param },
            returnType
        );

        Assert.True(result.Success);
        Assert.Null(result.SourceParameterIndex);  // No lifetime needed
    }

    [Fact]
    public void InferReturnLifetime_BorrowsAttribute_SelectsNamedParameter()
    {
        var attributes = new AttributeCollection();
        var borrows = new AttributeInfo(KnownAttributes.Borrows, Loc);
        borrows.PositionalArgs.Add("right");
        attributes.Add(borrows);
        var parameters = new[]
        {
            new IrParameter("left", new IrReferenceType(IrIntType.I32)),
            new IrParameter("right", new IrReferenceType(IrIntType.I32))
        };

        var result = new LifetimeInference().InferReturnLifetime(
            parameters, new IrReferenceType(IrIntType.I32), attributes);

        Assert.True(result.Success);
        Assert.Equal(1, result.SourceParameterIndex);
    }

    [Fact]
    public void InferReturnLifetime_BorrowsStatic_HasNoRuntimeSource()
    {
        var attributes = new AttributeCollection();
        var borrows = new AttributeInfo(KnownAttributes.Borrows, Loc);
        borrows.PositionalArgs.Add("static");
        attributes.Add(borrows);

        var result = new LifetimeInference().InferReturnLifetime(
            Array.Empty<IrParameter>(), new IrReferenceType(IrIntType.I32), attributes);

        Assert.True(result.Success);
        Assert.True(result.IsStatic);
        Assert.Null(result.SourceParameterIndex);
    }

    [Fact]
    public void InferReturnLifetime_RawBorrowRequiresUnsafeBoundary()
    {
        var attributes = new AttributeCollection();
        var borrows = new AttributeInfo(KnownAttributes.Borrows, Loc);
        borrows.PositionalArgs.Add("ptr");
        attributes.Add(borrows);

        var result = new LifetimeInference().InferReturnLifetime(
            new[] { new IrParameter("ptr", new IrPointerType(IrIntType.I32)) },
            new IrReferenceType(IrIntType.I32), attributes);

        Assert.False(result.Success);
        Assert.Contains("requires @unsafe", result.ErrorMessage);
    }
}

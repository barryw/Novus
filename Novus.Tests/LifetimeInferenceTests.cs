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
}

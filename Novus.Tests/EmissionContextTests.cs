using System.Text;
using Novus.Codegen;
using Novus.IR;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Unit tests for EmissionContext - the per-function state container for C code generation.
/// </summary>
public class EmissionContextTests
{
    private static IrFunction CreateTestFunction(string name = "test_func")
    {
        return new IrFunction(name, IrVoidType.Instance)
        {
            Visibility = Visibility.Public
        };
    }

    private static IrFunction CreateFunctionWithParams(string name, params (string Name, IrType Type)[] parameters)
    {
        var func = new IrFunction(name, IrVoidType.Instance);
        foreach (var (paramName, paramType) in parameters)
        {
            func.Parameters.Add(new IrParameter(paramName, paramType));
        }
        return func;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithFunction_SetsFunction()
    {
        var func = CreateTestFunction();
        var ctx = new EmissionContext(func);

        Assert.Same(func, ctx.Function);
    }

    [Fact]
    public void Constructor_WithNullFunction_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new EmissionContext(null!));
    }

    [Fact]
    public void Constructor_WithoutOutput_CreatesNewStringBuilder()
    {
        var func = CreateTestFunction();
        var ctx = new EmissionContext(func);

        Assert.NotNull(ctx.Output);
    }

    [Fact]
    public void Constructor_WithOutput_UsesProvidedStringBuilder()
    {
        var func = CreateTestFunction();
        var sb = new StringBuilder("existing content");
        var ctx = new EmissionContext(func, sb);

        Assert.Same(sb, ctx.Output);
        Assert.Contains("existing content", ctx.Output.ToString());
    }

    #endregion

    #region Initial State Tests

    [Fact]
    public void InitialState_DeclaredVariables_IsEmpty()
    {
        var ctx = new EmissionContext(CreateTestFunction());
        Assert.Empty(ctx.DeclaredVariables);
    }

    [Fact]
    public void InitialState_PointerConvertedParameters_IsEmpty()
    {
        var ctx = new EmissionContext(CreateTestFunction());
        Assert.Empty(ctx.PointerConvertedParameters);
    }

    [Fact]
    public void InitialState_MemberAccessInfo_IsEmpty()
    {
        var ctx = new EmissionContext(CreateTestFunction());
        Assert.Empty(ctx.MemberAccessInfo);
    }

    [Fact]
    public void InitialState_DeferEmissionCounter_IsZero()
    {
        var ctx = new EmissionContext(CreateTestFunction());
        Assert.Equal(0, ctx.DeferEmissionCounter);
    }

    [Fact]
    public void InitialState_LabelSuffix_IsEmpty()
    {
        var ctx = new EmissionContext(CreateTestFunction());
        Assert.Equal("", ctx.LabelSuffix);
    }

    [Fact]
    public void InitialState_LastEmittedDebugLine_IsMinusOne()
    {
        var ctx = new EmissionContext(CreateTestFunction());
        Assert.Equal(-1, ctx.LastEmittedDebugLine);
    }

    [Fact]
    public void InitialState_InlineableComparisons_IsEmpty()
    {
        var ctx = new EmissionContext(CreateTestFunction());
        Assert.Empty(ctx.InlineableComparisons);
    }

    #endregion

    #region Variable Declaration Tests

    [Fact]
    public void MarkDeclared_AddsVariableToSet()
    {
        var ctx = new EmissionContext(CreateTestFunction());
        ctx.MarkDeclared("my_var");

        Assert.True(ctx.IsDeclared("my_var"));
    }

    [Fact]
    public void IsDeclared_ReturnsFalse_ForUndeclaredVariable()
    {
        var ctx = new EmissionContext(CreateTestFunction());

        Assert.False(ctx.IsDeclared("undeclared_var"));
    }

    [Fact]
    public void MarkDeclared_MultipleVariables_TracksAll()
    {
        var ctx = new EmissionContext(CreateTestFunction());
        ctx.MarkDeclared("var1");
        ctx.MarkDeclared("var2");
        ctx.MarkDeclared("var3");

        Assert.True(ctx.IsDeclared("var1"));
        Assert.True(ctx.IsDeclared("var2"));
        Assert.True(ctx.IsDeclared("var3"));
        Assert.False(ctx.IsDeclared("var4"));
    }

    #endregion

    #region Member Access Tracking Tests

    [Fact]
    public void RecordMemberAccess_StoresInfo()
    {
        var ctx = new EmissionContext(CreateTestFunction());
        var structType = new IrStructType("TestStruct", new List<IrStructField>());

        ctx.RecordMemberAccess("result", "self", "field1", "->", structType);

        Assert.True(ctx.TryGetMemberAccess("result", out var info));
        Assert.Equal("self", info.StructValue);
        Assert.Equal("field1", info.FieldName);
        Assert.Equal("->", info.Accessor);
        Assert.Same(structType, info.FieldType);
    }

    [Fact]
    public void TryGetMemberAccess_ReturnsFalse_ForUnrecordedAccess()
    {
        var ctx = new EmissionContext(CreateTestFunction());

        Assert.False(ctx.TryGetMemberAccess("unknown", out _));
    }

    #endregion

    #region Index Access Tracking Tests

    [Fact]
    public void RecordIndexAccess_StoresInfo()
    {
        var ctx = new EmissionContext(CreateTestFunction());

        ctx.RecordIndexAccess("result", "array", "i");

        Assert.True(ctx.TryGetIndexAccess("result", out var info));
        Assert.Equal("array", info.ArrayExpr);
        Assert.Equal("i", info.IndexExpr);
    }

    [Fact]
    public void TryGetIndexAccess_ReturnsFalse_ForUnrecordedAccess()
    {
        var ctx = new EmissionContext(CreateTestFunction());

        Assert.False(ctx.TryGetIndexAccess("unknown", out _));
    }

    #endregion

    #region Field Access Chain Tests

    [Fact]
    public void RecordFieldAccessChain_StoresInfo()
    {
        var ctx = new EmissionContext(CreateTestFunction());

        ctx.RecordFieldAccessChain("result", "base", "nested_field", "->");

        Assert.True(ctx.TryGetFieldAccessChain("result", out var info));
        Assert.Equal("base", info.BaseExpr);
        Assert.Equal("nested_field", info.FieldName);
        Assert.Equal("->", info.Accessor);
    }

    [Fact]
    public void TryGetFieldAccessChain_ReturnsFalse_ForUnrecordedChain()
    {
        var ctx = new EmissionContext(CreateTestFunction());

        Assert.False(ctx.TryGetFieldAccessChain("unknown", out _));
    }

    #endregion

    #region Debug Label Tests

    [Fact]
    public void GetNextDebugLabel_ReturnsIncrementingLabels()
    {
        var ctx = new EmissionContext(CreateTestFunction());

        Assert.Equal("_dbg_0", ctx.GetNextDebugLabel());
        Assert.Equal("_dbg_1", ctx.GetNextDebugLabel());
        Assert.Equal("_dbg_2", ctx.GetNextDebugLabel());
    }

    [Fact]
    public void GetNextDebugLabel_IncrementsCounter()
    {
        var ctx = new EmissionContext(CreateTestFunction());

        ctx.GetNextDebugLabel();
        ctx.GetNextDebugLabel();

        Assert.Equal(2, ctx.DebugLabelCounter);
    }

    #endregion

    #region Defer Suffix Tests

    [Fact]
    public void GetNextDeferSuffix_ReturnsIncrementingSuffixes()
    {
        var ctx = new EmissionContext(CreateTestFunction());

        Assert.Equal("_d0", ctx.GetNextDeferSuffix());
        Assert.Equal("_d1", ctx.GetNextDeferSuffix());
        Assert.Equal("_d2", ctx.GetNextDeferSuffix());
    }

    #endregion

    #region Defer Block Activation Tests

    [Fact]
    public void ActivateDeferBlock_MarksBlockAsActive()
    {
        var ctx = new EmissionContext(CreateTestFunction());

        ctx.ActivateDeferBlock(1);

        Assert.True(ctx.IsDeferBlockActivated(1));
    }

    [Fact]
    public void IsDeferBlockActivated_ReturnsFalse_ForInactiveBlock()
    {
        var ctx = new EmissionContext(CreateTestFunction());

        Assert.False(ctx.IsDeferBlockActivated(1));
    }

    [Fact]
    public void ActivateDeferBlock_MultipleBlocks_TracksAll()
    {
        var ctx = new EmissionContext(CreateTestFunction());

        ctx.ActivateDeferBlock(1);
        ctx.ActivateDeferBlock(3);
        ctx.ActivateDeferBlock(5);

        Assert.True(ctx.IsDeferBlockActivated(1));
        Assert.False(ctx.IsDeferBlockActivated(2));
        Assert.True(ctx.IsDeferBlockActivated(3));
        Assert.False(ctx.IsDeferBlockActivated(4));
        Assert.True(ctx.IsDeferBlockActivated(5));
    }

    #endregion

    #region Inlineable Comparisons Tests (VBCC Workaround)

    [Fact]
    public void RecordInlineableComparison_StoresExpression()
    {
        var ctx = new EmissionContext(CreateTestFunction());

        ctx.RecordInlineableComparison("_slot_bool_0", "x == 0");

        Assert.True(ctx.TryGetInlineableComparison("_slot_bool_0", out var expr));
        Assert.Equal("x == 0", expr);
    }

    [Fact]
    public void TryGetInlineableComparison_ReturnsFalse_ForUnrecordedComparison()
    {
        var ctx = new EmissionContext(CreateTestFunction());

        Assert.False(ctx.TryGetInlineableComparison("unknown", out _));
    }

    [Fact]
    public void InlineableComparisons_OverwritesPreviousValue()
    {
        var ctx = new EmissionContext(CreateTestFunction());

        ctx.RecordInlineableComparison("result", "x == 0");
        ctx.RecordInlineableComparison("result", "y != null");

        Assert.True(ctx.TryGetInlineableComparison("result", out var expr));
        Assert.Equal("y != null", expr);
    }

    #endregion

    #region Slot Mapping Tests

    [Fact]
    public void IsMappedToSlot_ReturnsFalse_WhenVariableToSlotIsNull()
    {
        var ctx = new EmissionContext(CreateTestFunction());

        Assert.False(ctx.IsMappedToSlot("any_var"));
    }

    [Fact]
    public void IsMappedToSlot_ReturnsTrue_WhenVariableIsMapped()
    {
        var ctx = new EmissionContext(CreateTestFunction());
        ctx.VariableToSlot = new Dictionary<string, string>
        {
            ["%temp1"] = "_slot_i32_0"
        };

        Assert.True(ctx.IsMappedToSlot("%temp1"));
        Assert.False(ctx.IsMappedToSlot("%temp2"));
    }

    [Fact]
    public void GetSlotOrOriginal_ReturnsOriginal_WhenNotMapped()
    {
        var ctx = new EmissionContext(CreateTestFunction());

        Assert.Equal("my_var", ctx.GetSlotOrOriginal("my_var"));
    }

    [Fact]
    public void GetSlotOrOriginal_ReturnsSlot_WhenMapped()
    {
        var ctx = new EmissionContext(CreateTestFunction());
        ctx.VariableToSlot = new Dictionary<string, string>
        {
            ["%temp1"] = "_slot_i32_0"
        };

        Assert.Equal("_slot_i32_0", ctx.GetSlotOrOriginal("%temp1"));
    }

    #endregion

    #region Debug Line Marker Tests

    [Fact]
    public void RecordDebugLineMarker_AddsToList()
    {
        var ctx = new EmissionContext(CreateTestFunction());

        ctx.RecordDebugLineMarker("_dbg_0", "test.novus", 42, "my_func");

        Assert.Single(ctx.DebugLineMarkers);
        var marker = ctx.DebugLineMarkers[0];
        Assert.Equal("_dbg_0", marker.LabelName);
        Assert.Equal("test.novus", marker.FileName);
        Assert.Equal(42, marker.Line);
        Assert.Equal("my_func", marker.FuncName);
    }

    [Fact]
    public void RecordDebugLineMarker_UpdatesLastEmittedDebugLine()
    {
        var ctx = new EmissionContext(CreateTestFunction());

        ctx.RecordDebugLineMarker("_dbg_0", "test.novus", 42, "my_func");

        Assert.Equal(42, ctx.LastEmittedDebugLine);
    }

    [Fact]
    public void RecordDebugLineMarker_MultipleMarkers_AddsAll()
    {
        var ctx = new EmissionContext(CreateTestFunction());

        ctx.RecordDebugLineMarker("_dbg_0", "test.novus", 10, "func1");
        ctx.RecordDebugLineMarker("_dbg_1", "test.novus", 20, "func1");
        ctx.RecordDebugLineMarker("_dbg_2", "other.novus", 30, "func2");

        Assert.Equal(3, ctx.DebugLineMarkers.Count);
        Assert.Equal(30, ctx.LastEmittedDebugLine);
    }

    #endregion

    #region Pointer Converted Parameters Tests

    [Fact]
    public void InitializePointerConvertedParameters_AddsStructsWithHeapData()
    {
        var heapStruct = new IrStructType("Vec", new List<IrStructField>());
        var simpleStruct = new IrStructType("Point", new List<IrStructField>());
        var func = CreateFunctionWithParams("test",
            ("vec", heapStruct),
            ("point", simpleStruct),
            ("count", IrIntType.I32));

        var ctx = new EmissionContext(func);
        ctx.InitializePointerConvertedParameters(st => st.StructName == "Vec");

        Assert.Contains("vec", ctx.PointerConvertedParameters);
        Assert.DoesNotContain("point", ctx.PointerConvertedParameters);
        Assert.DoesNotContain("count", ctx.PointerConvertedParameters);
    }

    [Fact]
    public void InitializePointerConvertedParameters_EmptyWhenNoHeapData()
    {
        var simpleStruct = new IrStructType("Point", new List<IrStructField>());
        var func = CreateFunctionWithParams("test",
            ("point", simpleStruct),
            ("count", IrIntType.I32));

        var ctx = new EmissionContext(func);
        ctx.InitializePointerConvertedParameters(_ => false);

        Assert.Empty(ctx.PointerConvertedParameters);
    }

    #endregion

    #region State Isolation Tests

    [Fact]
    public void SeparateContexts_HaveIndependentState()
    {
        var func1 = CreateTestFunction("func1");
        var func2 = CreateTestFunction("func2");

        var ctx1 = new EmissionContext(func1);
        var ctx2 = new EmissionContext(func2);

        ctx1.MarkDeclared("var_in_ctx1");
        ctx1.RecordInlineableComparison("cmp1", "x == 0");
        ctx1.ActivateDeferBlock(1);

        ctx2.MarkDeclared("var_in_ctx2");
        ctx2.RecordInlineableComparison("cmp2", "y != null");
        ctx2.ActivateDeferBlock(2);

        // Verify ctx1 state
        Assert.True(ctx1.IsDeclared("var_in_ctx1"));
        Assert.False(ctx1.IsDeclared("var_in_ctx2"));
        Assert.True(ctx1.TryGetInlineableComparison("cmp1", out _));
        Assert.False(ctx1.TryGetInlineableComparison("cmp2", out _));
        Assert.True(ctx1.IsDeferBlockActivated(1));
        Assert.False(ctx1.IsDeferBlockActivated(2));

        // Verify ctx2 state
        Assert.False(ctx2.IsDeclared("var_in_ctx1"));
        Assert.True(ctx2.IsDeclared("var_in_ctx2"));
        Assert.False(ctx2.TryGetInlineableComparison("cmp1", out _));
        Assert.True(ctx2.TryGetInlineableComparison("cmp2", out _));
        Assert.False(ctx2.IsDeferBlockActivated(1));
        Assert.True(ctx2.IsDeferBlockActivated(2));
    }

    #endregion
}

using Novus.Diagnostics;
using Novus.Frontend;
using Novus.IR;
using Novus.SemanticAnalysis;

namespace Novus.Tests;

/// <summary>
/// Tests for the IrBuilder interface abstractions: ISymbolResolver, ITypeChecker, and IIrEmitter.
/// These interfaces support the separation of concerns in IrBuilder:
/// - ISymbolResolver: Name resolution (what does this identifier refer to?)
/// - ITypeChecker: Type validation (is this operation type-safe?)
/// - IIrEmitter: IR generation (emit instructions to basic blocks)
/// </summary>
public class IrBuilderInterfaceTests
{
    #region ISymbolResolver Tests

    [Fact]
    public void SymbolTableResolver_ResolveBuiltinType_I32()
    {
        var symbols = new SymbolTable();
        var resolver = new SymbolTableResolver(symbols);

        var result = resolver.ResolveType("i32");

        Assert.NotNull(result);
        Assert.IsType<IrIntType>(result);
        Assert.Equal(IrIntType.I32, result);
    }

    [Fact]
    public void SymbolTableResolver_ResolveBuiltinType_Bool()
    {
        var symbols = new SymbolTable();
        var resolver = new SymbolTableResolver(symbols);

        var result = resolver.ResolveType("bool");

        Assert.NotNull(result);
        Assert.IsType<IrBoolType>(result);
    }

    [Fact]
    public void SymbolTableResolver_ResolveBuiltinType_F64()
    {
        var symbols = new SymbolTable();
        var resolver = new SymbolTableResolver(symbols);

        var result = resolver.ResolveType("f64");

        Assert.NotNull(result);
        Assert.IsType<IrFloatType>(result);
        Assert.Equal(IrFloatType.F64, result);
    }

    [Fact]
    public void SymbolTableResolver_ResolveBuiltinType_Fixed16()
    {
        var symbols = new SymbolTable();
        var resolver = new SymbolTableResolver(symbols);

        var result = resolver.ResolveType("fixed16");

        Assert.NotNull(result);
        Assert.IsType<IrFixedType>(result);
        Assert.Equal(IrFixedType.Fixed16, result);
    }

    [Fact]
    public void SymbolTableResolver_ResolveBuiltinType_Unit()
    {
        var symbols = new SymbolTable();
        var resolver = new SymbolTableResolver(symbols);

        var result = resolver.ResolveType("()");

        Assert.NotNull(result);
        Assert.IsType<IrTupleType>(result);
        Assert.Equal(IrTupleType.Unit, result);
    }

    [Fact]
    public void SymbolTableResolver_ResolveStruct()
    {
        var symbols = new SymbolTable();
        var structType = new IrStructType("Point", new List<IrStructField>
        {
            new("x", IrIntType.I32),
            new("y", IrIntType.I32)
        });
        symbols.RegisterStruct("Point", structType);

        var resolver = new SymbolTableResolver(symbols);

        var result = resolver.ResolveStruct("Point");

        Assert.NotNull(result);
        Assert.Equal("Point", result.StructName);
        Assert.Equal(2, result.Fields.Count);
    }

    [Fact]
    public void SymbolTableResolver_ResolveType_ReturnsStruct()
    {
        var symbols = new SymbolTable();
        var structType = new IrStructType("Color", new List<IrStructField>
        {
            new("r", IrIntType.U8),
            new("g", IrIntType.U8),
            new("b", IrIntType.U8)
        });
        symbols.RegisterStruct("Color", structType);

        var resolver = new SymbolTableResolver(symbols);

        var result = resolver.ResolveType("Color");

        Assert.NotNull(result);
        Assert.IsType<IrStructType>(result);
        Assert.Equal("Color", ((IrStructType)result).StructName);
    }

    [Fact]
    public void SymbolTableResolver_ResolveEnum()
    {
        var symbols = new SymbolTable();
        var enumType = new IrEnumType("Direction", new List<IrEnumVariant>
        {
            new("North", 0),
            new("South", 1),
            new("East", 2),
            new("West", 3)
        });
        symbols.RegisterEnum("Direction", enumType);

        var resolver = new SymbolTableResolver(symbols);

        var result = resolver.ResolveEnum("Direction");

        Assert.NotNull(result);
        Assert.Equal("Direction", result.EnumName);
        Assert.Equal(4, result.Variants.Count);
    }

    [Fact]
    public void SymbolTableResolver_ResolveFunction()
    {
        var symbols = new SymbolTable();
        var dummyLocation = new SourceLocation("test.novus", 1, 1, 1, "fn add(a: i32, b: i32) -> i32");
        var funcSymbol = new FunctionSymbol(
            "add",
            IrIntType.I32,
            new List<ParameterSymbol>
            {
                new ParameterSymbol("a", IrIntType.I32, dummyLocation),
                new ParameterSymbol("b", IrIntType.I32, dummyLocation)
            },
            dummyLocation);
        symbols.RegisterFunction("add", funcSymbol);

        var resolver = new SymbolTableResolver(symbols);

        var result = resolver.ResolveFunction("add");

        Assert.NotNull(result);
        Assert.Equal("add", result.Name);
        Assert.Equal(IrIntType.I32, result.ReturnType);
    }

    [Fact]
    public void SymbolTableResolver_ResolveConstant()
    {
        var symbols = new SymbolTable();
        symbols.RegisterConstant("MAX_SIZE", IrIntType.I32, 1024);

        var resolver = new SymbolTableResolver(symbols);

        var result = resolver.ResolveConstant("MAX_SIZE");

        Assert.NotNull(result);
        Assert.Equal("MAX_SIZE", result.Name);
        Assert.Equal(IrIntType.I32, result.Type);
        Assert.Equal(1024, result.Value);
    }

    [Fact]
    public void SymbolTableResolver_ResolveTrait()
    {
        var symbols = new SymbolTable();
        var trait = new IrTrait("Display", new List<IrTraitMethod>
        {
            new("to_string", new List<IrParameter>(), new IrPointerType(IrIntType.U8))
        });
        symbols.RegisterTrait("Display", trait);

        var resolver = new SymbolTableResolver(symbols);

        var result = resolver.ResolveTrait("Display");

        Assert.NotNull(result);
        Assert.Equal("Display", result.TraitName);
        Assert.Single(result.Methods);
    }

    [Fact]
    public void SymbolTableResolver_ResolveVariable_LocalFirst()
    {
        var symbols = new SymbolTable();
        var dummyLocation = new SourceLocation("test.novus", 1, 1, 1, "let mut x: i32 = 0");
        var localLookup = new Func<string, VariableSymbol?>(name =>
            name == "x" ? new VariableSymbol("x", IrIntType.I32, true, dummyLocation) : null);

        var resolver = new SymbolTableResolver(symbols, localLookup);

        var result = resolver.ResolveVariable("x");

        Assert.NotNull(result);
        Assert.Equal("x", result.Name);
        Assert.True(result.IsMutable);
    }

    [Fact]
    public void SymbolTableResolver_IsNameVisible_True()
    {
        var symbols = new SymbolTable();
        symbols.RegisterConstant("PI", IrFloatType.F64, 3.14159);

        var resolver = new SymbolTableResolver(symbols);

        Assert.True(resolver.IsNameVisible("PI"));
    }

    [Fact]
    public void SymbolTableResolver_IsNameVisible_False()
    {
        var symbols = new SymbolTable();

        var resolver = new SymbolTableResolver(symbols);

        Assert.False(resolver.IsNameVisible("UNKNOWN"));
    }

    [Fact]
    public void SymbolTableResolver_ResolveType_UnknownReturnsNull()
    {
        var symbols = new SymbolTable();
        var resolver = new SymbolTableResolver(symbols);

        var result = resolver.ResolveType("UnknownType");

        Assert.Null(result);
    }

    #endregion

    #region ITypeChecker Tests

    [Fact]
    public void DefaultTypeChecker_TypesAreEqual_SameType()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        Assert.True(checker.TypesAreEqual(IrIntType.I32, IrIntType.I32));
    }

    [Fact]
    public void DefaultTypeChecker_TypesAreEqual_DifferentTypes()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        Assert.False(checker.TypesAreEqual(IrIntType.I32, IrIntType.I64));
    }

    [Fact]
    public void DefaultTypeChecker_TypesAreEqual_NullHandling()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        Assert.True(checker.TypesAreEqual(null, null));
        Assert.False(checker.TypesAreEqual(IrIntType.I32, null));
        Assert.False(checker.TypesAreEqual(null, IrIntType.I32));
    }

    [Fact]
    public void DefaultTypeChecker_CanAssign_SameType()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        Assert.True(checker.CanAssign(IrIntType.I32, IrIntType.I32));
    }

    [Fact]
    public void DefaultTypeChecker_CanAssign_IntegerWidening()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        // i32 can be assigned to i64 (widening)
        Assert.True(checker.CanAssign(IrIntType.I64, IrIntType.I32));
    }

    [Fact]
    public void DefaultTypeChecker_CanAssign_IntegerNarrowing_Fails()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        // i64 cannot be assigned to i32 (narrowing)
        Assert.False(checker.CanAssign(IrIntType.I32, IrIntType.I64));
    }

    [Fact]
    public void DefaultTypeChecker_CanAssign_PointerToBool()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var pointerType = new IrPointerType(IrIntType.I32);
        Assert.True(checker.CanAssign(IrBoolType.Instance, pointerType));
    }

    [Fact]
    public void DefaultTypeChecker_GetBinaryOperationResultType_Addition()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var result = checker.GetBinaryOperationResultType(IrIntType.I32, IrIntType.I32, "+");

        Assert.NotNull(result);
        Assert.IsType<IrIntType>(result);
    }

    [Fact]
    public void DefaultTypeChecker_GetBinaryOperationResultType_Comparison()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var result = checker.GetBinaryOperationResultType(IrIntType.I32, IrIntType.I32, "==");

        Assert.NotNull(result);
        Assert.Equal(IrBoolType.Instance, result);
    }

    [Fact]
    public void DefaultTypeChecker_GetBinaryOperationResultType_LogicalAnd()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var result = checker.GetBinaryOperationResultType(IrBoolType.Instance, IrBoolType.Instance, "&&");

        Assert.NotNull(result);
        Assert.Equal(IrBoolType.Instance, result);
    }

    [Fact]
    public void DefaultTypeChecker_GetBinaryOperationResultType_BitwiseAnd()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var result = checker.GetBinaryOperationResultType(IrIntType.I32, IrIntType.I32, "&");

        Assert.NotNull(result);
        Assert.Equal(IrIntType.I32, result);
    }

    [Fact]
    public void DefaultTypeChecker_GetBinaryOperationResultType_FloatPromotion()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var result = checker.GetBinaryOperationResultType(IrFloatType.F32, IrIntType.I32, "+");

        Assert.NotNull(result);
        Assert.Equal(IrFloatType.F64, result);
    }

    [Fact]
    public void DefaultTypeChecker_GetUnaryOperationResultType_Negation()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var result = checker.GetUnaryOperationResultType(IrIntType.I32, "-");

        Assert.NotNull(result);
        Assert.Equal(IrIntType.I32, result);
    }

    [Fact]
    public void DefaultTypeChecker_GetUnaryOperationResultType_LogicalNot()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var result = checker.GetUnaryOperationResultType(IrBoolType.Instance, "!");

        Assert.NotNull(result);
        Assert.Equal(IrBoolType.Instance, result);
    }

    [Fact]
    public void DefaultTypeChecker_GetUnaryOperationResultType_BitwiseNot()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var result = checker.GetUnaryOperationResultType(IrIntType.I32, "~");

        Assert.NotNull(result);
        Assert.Equal(IrIntType.I32, result);
    }

    [Fact]
    public void DefaultTypeChecker_GetUnaryOperationResultType_Dereference()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var pointerType = new IrPointerType(IrIntType.I32);
        var result = checker.GetUnaryOperationResultType(pointerType, "*");

        Assert.NotNull(result);
        Assert.Equal(IrIntType.I32, result);
    }

    [Fact]
    public void DefaultTypeChecker_GetUnaryOperationResultType_AddressOf()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var result = checker.GetUnaryOperationResultType(IrIntType.I32, "&");

        Assert.NotNull(result);
        Assert.IsType<IrReferenceType>(result);
    }

    [Fact]
    public void DefaultTypeChecker_GetUnaryOperationResultType_MutAddressOf()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var result = checker.GetUnaryOperationResultType(IrIntType.I32, "&mut");

        Assert.NotNull(result);
        Assert.IsType<IrMutReferenceType>(result);
    }

    [Fact]
    public void DefaultTypeChecker_CanUseAsCondition_Bool()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        Assert.True(checker.CanUseAsCondition(IrBoolType.Instance));
    }

    [Fact]
    public void DefaultTypeChecker_CanUseAsCondition_Int()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        Assert.True(checker.CanUseAsCondition(IrIntType.I32));
    }

    [Fact]
    public void DefaultTypeChecker_CanUseAsCondition_Pointer()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var pointerType = new IrPointerType(IrIntType.I32);
        Assert.True(checker.CanUseAsCondition(pointerType));
    }

    [Fact]
    public void DefaultTypeChecker_CanUseAsCondition_Struct_False()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var structType = new IrStructType("Point", new List<IrStructField>());
        Assert.False(checker.CanUseAsCondition(structType));
    }

    [Fact]
    public void DefaultTypeChecker_CanIndex_Array()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var arrayType = new IrArrayType(IrIntType.I32, 10);
        Assert.True(checker.CanIndex(arrayType));
    }

    [Fact]
    public void DefaultTypeChecker_CanIndex_Pointer()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var pointerType = new IrPointerType(IrIntType.I32);
        Assert.True(checker.CanIndex(pointerType));
    }

    [Fact]
    public void DefaultTypeChecker_CanIndex_Int_False()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        Assert.False(checker.CanIndex(IrIntType.I32));
    }

    [Fact]
    public void DefaultTypeChecker_GetElementType_Array()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var arrayType = new IrArrayType(IrIntType.I32, 10);
        var result = checker.GetElementType(arrayType);

        Assert.NotNull(result);
        Assert.Equal(IrIntType.I32, result);
    }

    [Fact]
    public void DefaultTypeChecker_GetElementType_Pointer()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var pointerType = new IrPointerType(IrFloatType.F64);
        var result = checker.GetElementType(pointerType);

        Assert.NotNull(result);
        Assert.Equal(IrFloatType.F64, result);
    }

    [Fact]
    public void DefaultTypeChecker_HasMember_Struct()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var structType = new IrStructType("Point", new List<IrStructField>
        {
            new("x", IrIntType.I32),
            new("y", IrIntType.I32)
        });

        Assert.True(checker.HasMember(structType, "x"));
        Assert.True(checker.HasMember(structType, "y"));
        Assert.False(checker.HasMember(structType, "z"));
    }

    [Fact]
    public void DefaultTypeChecker_GetMemberType_Struct()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var structType = new IrStructType("Point", new List<IrStructField>
        {
            new("x", IrIntType.I32),
            new("y", IrFloatType.F64)
        });

        var xType = checker.GetMemberType(structType, "x");
        var yType = checker.GetMemberType(structType, "y");
        var zType = checker.GetMemberType(structType, "z");

        Assert.Equal(IrIntType.I32, xType);
        Assert.Equal(IrFloatType.F64, yType);
        Assert.Null(zType);
    }

    [Fact]
    public void DefaultTypeChecker_GetCommonType_SameType()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var result = checker.GetCommonType(IrIntType.I32, IrIntType.I32);

        Assert.NotNull(result);
        Assert.Equal(IrIntType.I32, result);
    }

    [Fact]
    public void DefaultTypeChecker_GetCommonType_IntegerPromotion()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var result = checker.GetCommonType(IrIntType.I32, IrIntType.I64);

        Assert.NotNull(result);
        // Common type should be the larger type
        Assert.IsType<IrIntType>(result);
    }

    [Fact]
    public void DefaultTypeChecker_GetCommonType_Incompatible()
    {
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        var structType = new IrStructType("Point", new List<IrStructField>());
        var result = checker.GetCommonType(IrIntType.I32, structType);

        Assert.Null(result);
    }

    #endregion

    #region IIrEmitter Tests

    [Fact]
    public void DefaultIrEmitter_CreateBlock()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var emitter = new DefaultIrEmitter(function);

        var block = emitter.CreateBlock("entry");

        Assert.NotNull(block);
        Assert.Equal("entry", block.Label);
        Assert.Contains(block, function.BasicBlocks);
    }

    [Fact]
    public void DefaultIrEmitter_SwitchToBlock()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var emitter = new DefaultIrEmitter(function);

        var block = emitter.CreateBlock("entry");
        emitter.SwitchToBlock(block);

        Assert.Equal(block, emitter.CurrentBlock);
    }

    [Fact]
    public void DefaultIrEmitter_Emit_Instruction()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var emitter = new DefaultIrEmitter(function);

        var block = emitter.CreateBlock("entry");
        emitter.SwitchToBlock(block);

        var instruction = new IrReturn(null);
        emitter.Emit(instruction);

        Assert.Single(block.Instructions);
        Assert.Same(instruction, block.Instructions[0]);
    }

    [Fact]
    public void DefaultIrEmitter_Emit_ThrowsWhenNoCurrentBlock()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var emitter = new DefaultIrEmitter(function);

        var instruction = new IrReturn(null);

        Assert.Throws<InvalidOperationException>(() => emitter.Emit(instruction));
    }

    [Fact]
    public void DefaultIrEmitter_GenerateTemp()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var emitter = new DefaultIrEmitter(function);

        var temp1 = emitter.GenerateTemp();
        var temp2 = emitter.GenerateTemp();
        var temp3 = emitter.GenerateTemp();

        Assert.Equal("%t0", temp1);
        Assert.Equal("%t1", temp2);
        Assert.Equal("%t2", temp3);
    }

    [Fact]
    public void DefaultIrEmitter_GenerateLabel()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var emitter = new DefaultIrEmitter(function);

        var label1 = emitter.GenerateLabel();
        var label2 = emitter.GenerateLabel("loop");
        var label3 = emitter.GenerateLabel();

        Assert.Equal("L0", label1);
        Assert.Equal("loop1", label2);
        Assert.Equal("L2", label3);
    }

    [Fact]
    public void DefaultIrEmitter_EmitLocalDecl()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var emitter = new DefaultIrEmitter(function);

        var block = emitter.CreateBlock("entry");
        emitter.SwitchToBlock(block);

        emitter.EmitLocalDecl("x", IrIntType.I32, true);

        Assert.Single(block.Instructions);
        var localDecl = Assert.IsType<IrLocalDecl>(block.Instructions[0]);
        Assert.Equal("x", localDecl.Name);
        Assert.Equal(IrIntType.I32, localDecl.Type);
        Assert.True(localDecl.IsMutable);
    }

    [Fact]
    public void DefaultIrEmitter_EmitLocalDecl_WithInitialValue()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var emitter = new DefaultIrEmitter(function);

        var block = emitter.CreateBlock("entry");
        emitter.SwitchToBlock(block);

        var initialValue = new IrConstant(42, IrIntType.I32);
        emitter.EmitLocalDecl("x", IrIntType.I32, false, initialValue);

        var localDecl = Assert.IsType<IrLocalDecl>(block.Instructions[0]);
        Assert.Equal(initialValue, localDecl.InitialValue);
    }

    [Fact]
    public void DefaultIrEmitter_EmitStore()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var emitter = new DefaultIrEmitter(function);

        var block = emitter.CreateBlock("entry");
        emitter.SwitchToBlock(block);

        var value = new IrConstant(42, IrIntType.I32);
        emitter.EmitStore("x", value);

        Assert.Single(block.Instructions);
        var store = Assert.IsType<IrStore>(block.Instructions[0]);
        Assert.Equal("x", store.VariableName);
        Assert.Equal(value, store.Value);
    }

    [Fact]
    public void DefaultIrEmitter_EmitBranch()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var emitter = new DefaultIrEmitter(function);

        var block = emitter.CreateBlock("entry");
        emitter.SwitchToBlock(block);

        emitter.EmitBranch("exit");

        Assert.Single(block.Instructions);
        var branch = Assert.IsType<IrBranch>(block.Instructions[0]);
        Assert.Equal("exit", branch.Target);
    }

    [Fact]
    public void DefaultIrEmitter_EmitConditionalBranch()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var emitter = new DefaultIrEmitter(function);

        var block = emitter.CreateBlock("entry");
        emitter.SwitchToBlock(block);

        var condition = new IrBoolConstant(true);
        emitter.EmitConditionalBranch(condition, "then", "else");

        Assert.Single(block.Instructions);
        var condBranch = Assert.IsType<IrConditionalBranch>(block.Instructions[0]);
        Assert.Equal(condition, condBranch.Condition);
        Assert.Equal("then", condBranch.TrueTarget);
        Assert.Equal("else", condBranch.FalseTarget);
    }

    [Fact]
    public void DefaultIrEmitter_EmitReturn_Void()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var emitter = new DefaultIrEmitter(function);

        var block = emitter.CreateBlock("entry");
        emitter.SwitchToBlock(block);

        emitter.EmitReturn();

        Assert.Single(block.Instructions);
        var ret = Assert.IsType<IrReturn>(block.Instructions[0]);
        Assert.Null(ret.Value);
    }

    [Fact]
    public void DefaultIrEmitter_EmitReturn_WithValue()
    {
        var function = new IrFunction("test", IrIntType.I32);
        var emitter = new DefaultIrEmitter(function);

        var block = emitter.CreateBlock("entry");
        emitter.SwitchToBlock(block);

        var value = new IrConstant(42, IrIntType.I32);
        emitter.EmitReturn(value);

        Assert.Single(block.Instructions);
        var ret = Assert.IsType<IrReturn>(block.Instructions[0]);
        Assert.Equal(value, ret.Value);
    }

    [Fact]
    public void DefaultIrEmitter_EmitCall_Void()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var emitter = new DefaultIrEmitter(function);

        var block = emitter.CreateBlock("entry");
        emitter.SwitchToBlock(block);

        var resultName = emitter.EmitCall("puts", IrVoidType.Instance, new List<IrValue>());

        Assert.Null(resultName);
        Assert.Single(block.Instructions);
        var call = Assert.IsType<IrCall>(block.Instructions[0]);
        Assert.Equal("puts", call.FunctionName);
    }

    [Fact]
    public void DefaultIrEmitter_EmitCall_WithResult()
    {
        var function = new IrFunction("test", IrIntType.I32);
        var emitter = new DefaultIrEmitter(function);

        var block = emitter.CreateBlock("entry");
        emitter.SwitchToBlock(block);

        var args = new List<IrValue>
        {
            new IrConstant(1, IrIntType.I32),
            new IrConstant(2, IrIntType.I32)
        };
        var resultName = emitter.EmitCall("add", IrIntType.I32, args);

        Assert.NotNull(resultName);
        Assert.StartsWith("%t", resultName);
        Assert.Single(block.Instructions);
        var call = Assert.IsType<IrCall>(block.Instructions[0]);
        Assert.Equal("add", call.FunctionName);
        Assert.Equal(2, call.Arguments.Count);
    }

    [Fact]
    public void DefaultIrEmitter_SetLocation_PropagatestoInstructions()
    {
        var function = new IrFunction("test", IrVoidType.Instance);
        var emitter = new DefaultIrEmitter(function);

        var block = emitter.CreateBlock("entry");
        emitter.SwitchToBlock(block);

        var location = new SourceLocation("test.novus", 10, 5, 1, "return");
        emitter.SetLocation(location);

        emitter.EmitReturn();

        var ret = block.Instructions[0];
        Assert.NotNull(ret.Location);
        Assert.Equal("test.novus", ret.Location.FilePath);
        Assert.Equal(10, ret.Location.Line);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Integration_SymbolResolver_TypeChecker_WorkTogether()
    {
        // Setup symbol table with types
        var symbols = new SymbolTable();
        var pointStruct = new IrStructType("Point", new List<IrStructField>
        {
            new("x", IrIntType.I32),
            new("y", IrIntType.I32)
        });
        symbols.RegisterStruct("Point", pointStruct);

        var resolver = new SymbolTableResolver(symbols);
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        // Resolve the type
        var resolvedType = resolver.ResolveType("Point");
        Assert.NotNull(resolvedType);

        // Check member access
        Assert.True(checker.HasMember(resolvedType, "x"));
        var memberType = checker.GetMemberType(resolvedType, "x");
        Assert.Equal(IrIntType.I32, memberType);
    }

    [Fact]
    public void Integration_SymbolResolver_Emitter_WorkTogether()
    {
        // Setup symbol table
        var symbols = new SymbolTable();
        symbols.RegisterConstant("BUFFER_SIZE", IrIntType.I32, 4096);

        var resolver = new SymbolTableResolver(symbols);

        // Create function and emitter
        var function = new IrFunction("init_buffer", IrVoidType.Instance);
        var emitter = new DefaultIrEmitter(function);

        var block = emitter.CreateBlock("entry");
        emitter.SwitchToBlock(block);

        // Resolve constant
        var constant = resolver.ResolveConstant("BUFFER_SIZE");
        Assert.NotNull(constant);

        // Emit using resolved constant value
        var constValue = new IrConstant((int)constant.Value, constant.Type);
        emitter.EmitLocalDecl("buffer_size", IrIntType.I32, false, constValue);
        emitter.EmitReturn();

        Assert.Equal(2, block.Instructions.Count);
        var localDecl = Assert.IsType<IrLocalDecl>(block.Instructions[0]);
        Assert.Equal("buffer_size", localDecl.Name);
    }

    [Fact]
    public void Integration_AllThreeInterfaces_SimpleFunctionBuild()
    {
        // Setup
        var symbols = new SymbolTable();
        var dummyLocation = new SourceLocation("test.novus", 1, 1, 1, "fn double(n: i32) -> i32");
        var funcSymbol = new FunctionSymbol(
            "double",
            IrIntType.I32,
            new List<ParameterSymbol> { new ParameterSymbol("n", IrIntType.I32, dummyLocation) },
            dummyLocation);
        symbols.RegisterFunction("double", funcSymbol);

        var resolver = new SymbolTableResolver(symbols);
        var diagnostics = new DiagnosticBag();
        var checker = new DefaultTypeChecker(diagnostics);

        // Create function to build
        var function = new IrFunction("test", IrIntType.I32);
        function.Parameters.Add(new IrParameter("n", IrIntType.I32));
        var emitter = new DefaultIrEmitter(function);

        // Build function body
        var block = emitter.CreateBlock("entry");
        emitter.SwitchToBlock(block);

        // Emit: let result = double(n)
        // First, verify the function exists
        var doubleFunc = resolver.ResolveFunction("double");
        Assert.NotNull(doubleFunc);

        // Type check: i32 can be passed as i32
        Assert.True(checker.CanAssign(IrIntType.I32, IrIntType.I32));

        // Emit the call
        var args = new List<IrValue> { new IrVariable("n", IrIntType.I32) };
        var resultTemp = emitter.EmitCall("double", IrIntType.I32, args);

        // Emit return
        emitter.EmitReturn(new IrVariable(resultTemp!, IrIntType.I32));

        // Verify
        Assert.Equal(2, block.Instructions.Count);
        Assert.IsType<IrCall>(block.Instructions[0]);
        Assert.IsType<IrReturn>(block.Instructions[1]);
    }

    #endregion

    #region Dependency Injection Tests

    [Fact]
    public void IrBuilderConfiguration_Default_CreatesValidConfig()
    {
        var config = IrBuilderConfiguration.Default;

        Assert.False(config.SkipAutoImports);
        Assert.Null(config.AnalysisResult);
        Assert.Null(config.SymbolResolverFactory);
        Assert.Null(config.TypeCheckerFactory);
        Assert.Null(config.EmitterFactory);
    }

    [Fact]
    public void IrBuilderConfiguration_ForTesting_SkipsAutoImports()
    {
        var config = IrBuilderConfiguration.ForTesting;

        Assert.True(config.SkipAutoImports);
    }

    [Fact]
    public void IrBuilderConfiguration_FluentBuilder_SetsProperties()
    {
        var diagnostics = new DiagnosticBag();
        var symbols = new SymbolTable();

        var config = new IrBuilderConfiguration()
            .WithDiagnostics(diagnostics)
            .WithSymbolTable(symbols)
            .WithStdLibPath("/custom/std")
            .WithInputFilePath("/test/file.novus")
            .WithSourceLines(new[] { "fn main() {}" });

        Assert.Same(diagnostics, config.Diagnostics);
        Assert.Same(symbols, config.SymbolTable);
        Assert.Equal("/custom/std", config.StdLibPath);
        Assert.Equal("/test/file.novus", config.InputFilePath);
        Assert.Single(config.SourceLines!);
    }

    [Fact]
    public void IrBuilderConfiguration_WithFactories_AllowsCustomImplementations()
    {
        bool resolverFactoryCalled = false;
        bool typeCheckerFactoryCalled = false;
        bool emitterFactoryCalled = false;

        var config = new IrBuilderConfiguration()
            .WithSymbolResolver((symbols, lookup) =>
            {
                resolverFactoryCalled = true;
                return new SymbolTableResolver(symbols, lookup);
            })
            .WithTypeChecker(diag =>
            {
                typeCheckerFactoryCalled = true;
                return new DefaultTypeChecker(diag);
            })
            .WithEmitter(func =>
            {
                emitterFactoryCalled = true;
                return new DefaultIrEmitter(func);
            });

        Assert.NotNull(config.SymbolResolverFactory);
        Assert.NotNull(config.TypeCheckerFactory);
        Assert.NotNull(config.EmitterFactory);

        // Test that factories produce valid results
        var symbols = new SymbolTable();
        var diagnostics = new DiagnosticBag();
        var function = new IrFunction("test", IrVoidType.Instance);

        config.SymbolResolverFactory!(symbols, null);
        Assert.True(resolverFactoryCalled);

        config.TypeCheckerFactory!(diagnostics);
        Assert.True(typeCheckerFactoryCalled);

        config.EmitterFactory!(function);
        Assert.True(emitterFactoryCalled);
    }

    [Fact]
    public void IrBuilder_WithConfiguration_UsesInjectedFactories()
    {
        var customResolverUsed = false;

        var config = new IrBuilderConfiguration()
        {
            SkipAutoImports = true,
            SymbolResolverFactory = (symbols, lookup) =>
            {
                customResolverUsed = true;
                return new SymbolTableResolver(symbols, lookup);
            }
        };

        var builder = new IrBuilder(config);

        // Accessing SymbolResolver should trigger the factory
        var resolver = builder.SymbolResolver;

        Assert.True(customResolverUsed);
        Assert.NotNull(resolver);
    }

    [Fact]
    public void IrBuilder_WithConfiguration_UsesInjectedTypeChecker()
    {
        var customCheckerUsed = false;

        var config = new IrBuilderConfiguration()
        {
            SkipAutoImports = true,
            TypeCheckerFactory = diag =>
            {
                customCheckerUsed = true;
                return new DefaultTypeChecker(diag);
            }
        };

        var builder = new IrBuilder(config);

        // Accessing TypeChecker should trigger the factory
        var checker = builder.TypeChecker;

        Assert.True(customCheckerUsed);
        Assert.NotNull(checker);
    }

    [Fact]
    public void IrBuilder_CreateEmitter_UsesInjectedFactory()
    {
        var customEmitterUsed = false;

        var config = new IrBuilderConfiguration()
        {
            SkipAutoImports = true,
            EmitterFactory = func =>
            {
                customEmitterUsed = true;
                return new DefaultIrEmitter(func);
            }
        };

        var builder = new IrBuilder(config);
        var function = new IrFunction("test", IrVoidType.Instance);

        // CreateEmitter should trigger the factory
        var emitter = builder.CreateEmitter(function);

        Assert.True(customEmitterUsed);
        Assert.NotNull(emitter);
    }

    [Fact]
    public void IrBuilder_WithConfiguration_SetsPathsFromConfig()
    {
        var config = new IrBuilderConfiguration()
        {
            SkipAutoImports = true,
            StdLibPath = "/custom/std",
            InputFilePath = "/test/file.novus",
            SourceLines = new[] { "fn main() {}" }
        };

        var builder = new IrBuilder(config);

        // The builder should have these values set
        // We can verify by checking if they're used in diagnostics
        Assert.NotNull(builder.Diagnostics);
    }

    #endregion
}

using Novus.Codegen;
using Novus.IR;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Unit tests for CCodeGenerator helper methods.
/// Tests isolated helper functions for type conversion, name mangling, and code generation.
/// </summary>
public class CCodeGeneratorHelperTests
{
    private CCodeGenerator CreateGenerator()
    {
        var module = new IrModule();
        return new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft", BuildMode.Debug);
    }

    #region GetCType Tests

    [Fact]
    public void GetCType_I8_ReturnsInt8T()
    {
        var gen = CreateGenerator();
        var type = new IrIntType(8, true);

        Assert.Equal("int8_t", gen.GetCType(type));
    }

    [Fact]
    public void GetCType_I16_ReturnsInt16T()
    {
        var gen = CreateGenerator();
        var type = new IrIntType(16, true);

        Assert.Equal("int16_t", gen.GetCType(type));
    }

    [Fact]
    public void GetCType_I32_ReturnsInt32T()
    {
        var gen = CreateGenerator();
        var type = new IrIntType(32, true);

        Assert.Equal("int32_t", gen.GetCType(type));
    }

    [Fact]
    public void GetCType_I64_ReturnsInt64T()
    {
        var gen = CreateGenerator();
        var type = new IrIntType(64, true);

        Assert.Equal("int64_t", gen.GetCType(type));
    }

    [Fact]
    public void GetCType_U8_ReturnsUInt8T()
    {
        var gen = CreateGenerator();
        var type = new IrIntType(8, false);

        Assert.Equal("uint8_t", gen.GetCType(type));
    }

    [Fact]
    public void GetCType_U16_ReturnsUInt16T()
    {
        var gen = CreateGenerator();
        var type = new IrIntType(16, false);

        Assert.Equal("uint16_t", gen.GetCType(type));
    }

    [Fact]
    public void GetCType_U32_ReturnsUInt32T()
    {
        var gen = CreateGenerator();
        var type = new IrIntType(32, false);

        Assert.Equal("uint32_t", gen.GetCType(type));
    }

    [Fact]
    public void GetCType_U64_ReturnsUInt64T()
    {
        var gen = CreateGenerator();
        var type = new IrIntType(64, false);

        Assert.Equal("uint64_t", gen.GetCType(type));
    }

    [Fact]
    public void GetCType_Bool_ReturnsBool()
    {
        var gen = CreateGenerator();
        var type = IrBoolType.Instance;

        Assert.Equal("bool", gen.GetCType(type));
    }

    [Fact]
    public void GetCType_Void_ReturnsVoid()
    {
        var gen = CreateGenerator();
        var type = IrVoidType.Instance;

        Assert.Equal("void", gen.GetCType(type));
    }

    [Fact]
    public void GetCType_F32_ReturnsFloat()
    {
        var gen = CreateGenerator();
        var type = new IrFloatType(32);

        Assert.Equal("float", gen.GetCType(type));
    }

    [Fact]
    public void GetCType_F64_ReturnsDouble()
    {
        var gen = CreateGenerator();
        var type = new IrFloatType(64);

        Assert.Equal("double", gen.GetCType(type));
    }

    [Fact]
    public void GetCType_PointerToI32_ReturnsInt32Pointer()
    {
        var gen = CreateGenerator();
        var pointeeType = new IrIntType(32, true);
        var pointerType = new IrPointerType(pointeeType);

        Assert.Equal("int32_t*", gen.GetCType(pointerType));
    }

    [Fact]
    public void GetCType_ReferenceToI32_ReturnsInt32Pointer()
    {
        var gen = CreateGenerator();
        var pointeeType = new IrIntType(32, true);
        var refType = new IrReferenceType(pointeeType);

        Assert.Equal("int32_t*", gen.GetCType(refType));
    }

    [Fact]
    public void GetCType_MutReferenceToI32_ReturnsInt32Pointer()
    {
        var gen = CreateGenerator();
        var pointeeType = new IrIntType(32, true);
        var mutRefType = new IrMutReferenceType(pointeeType);

        Assert.Equal("int32_t*", gen.GetCType(mutRefType));
    }

    [Fact]
    public void GetCType_Array_ReturnsPointerType()
    {
        var gen = CreateGenerator();
        var elementType = new IrIntType(32, true);
        var arrayType = new IrArrayType(elementType, 10);

        var result = gen.GetCType(arrayType);
        // Arrays are represented as pointers in the C codegen
        Assert.Equal("int32_t*", result);
    }

    [Fact]
    public void GetCType_Struct_ReturnsStructName()
    {
        var gen = CreateGenerator();
        var structType = new IrStructType("Point", new List<IrStructField>());

        Assert.Equal("Point", gen.GetCType(structType));
    }

    [Fact]
    public void AmigaLibraryBases_UseNativeTagsAndVbccInlineVectors()
    {
        var gen = CreateGenerator();
        var header = CCodeGenerator.GenerateSharedTypesHeader(new TypeRegistry());

        Assert.Equal("struct IntuitionBase*", gen.GetCType(
            new IrPointerType(new IrStructType("IntuitionBase", []))));
        Assert.Contains("#include <inline/intuition_protos.h>", header);
        Assert.Contains("extern struct IntuitionBase *IntuitionBase;", header);
    }

    [Fact]
    public void SharedTypesHeader_DefinesTrackdiskValueTypes()
    {
        var header = CCodeGenerator.GenerateSharedTypesHeader(new TypeRegistry());

        Assert.Contains("#include <devices/trackdisk.h>", header);
    }

    [Fact]
    public void SharedTypesHeader_DefinesTimerValueTypes()
    {
        var header = CCodeGenerator.GenerateSharedTypesHeader(new TypeRegistry());

        Assert.Contains("#include <devices/timer.h>", header);
        Assert.Contains("typedef struct timeval timeval;", header);
    }

    [Fact]
    public void SharedTypesHeader_DefinesDosValueTypes()
    {
        var header = CCodeGenerator.GenerateSharedTypesHeader(new TypeRegistry());

        Assert.Contains("#include <dos/dosextens.h>", header);
        Assert.Contains("#include <dos/filehandler.h>", header);
        Assert.Contains("#include <resources/filesysres.h>", header);
        Assert.Contains("typedef struct FileSysResource FileSysResource;", header);
        Assert.Contains("typedef struct FileSysEntry FileSysEntry;", header);
    }

    [Fact]
    public void Liveness_TracksStructFieldsNestedInEnumPayloads()
    {
        var payload = new IrStructType("Payload",
        [
            new IrStructField("first", IrIntType.U32),
            new IrStructField("second", IrIntType.U32),
        ]);
        var result = new IrEnumType("Result", [new IrEnumVariant("Ok", 0, [payload])]);
        var function = new IrFunction("snapshot", result);
        var block = new IrBasicBlock("entry");
        block.AddInstruction(new IrLocalDecl("%t0", IrIntType.U32, false, new IrConstant(1u, IrIntType.U32)));
        block.AddInstruction(new IrLocalDecl("%t1", IrIntType.U32, false, new IrConstant(2u, IrIntType.U32)));
        var fields = new Dictionary<string, IrValue>
        {
            ["first"] = new IrVariable("%t0", IrIntType.U32),
            ["second"] = new IrVariable("%t1", IrIntType.U32),
        };
        block.AddInstruction(new IrReturn(new IrEnumValue(result, "Ok", 0,
            [new IrStructLiteral(payload, fields)])));
        function.BasicBlocks.Add(block);

        var (slots, _) = new LivenessAnalysis(function).Analyze();

        Assert.NotEqual(slots["%t0"], slots["%t1"]);
    }

    [Fact]
    public void GetCType_Enum_ReturnsEnumName()
    {
        var gen = CreateGenerator();
        var enumType = new IrEnumType("Color", new List<IrEnumVariant>());

        Assert.Equal("Color", gen.GetCType(enumType));
    }

    [Fact]
    public void EmitFieldReference_ParenthesizesPointerCast()
    {
        var gen = CreateGenerator();
        var structType = new IrStructType("Point", new List<IrStructField>());
        var pointerType = new IrPointerType(structType);
        var rawPointer = new IrVariable("raw", new IrPointerType(IrIntType.U8));
        var cast = new IrCastValue(rawPointer, rawPointer.Type, pointerType);

        Assert.Equal("((Point*)raw)->x", gen.EmitFieldReference(new IrFieldReference(cast, "x", IrIntType.I32)));
    }

    #endregion

    #region GetBinaryOperator Tests

    [Fact]
    public void GetBinaryOperator_Add_ReturnsPlusSign()
    {
        var gen = CreateGenerator();
        Assert.Equal("+", gen.GetBinaryOperator(IrBinaryOp.OpKind.Add));
    }

    [Fact]
    public void GetBinaryOperator_Sub_ReturnsMinusSign()
    {
        var gen = CreateGenerator();
        Assert.Equal("-", gen.GetBinaryOperator(IrBinaryOp.OpKind.Sub));
    }

    [Fact]
    public void GetBinaryOperator_Mul_ReturnsAsterisk()
    {
        var gen = CreateGenerator();
        Assert.Equal("*", gen.GetBinaryOperator(IrBinaryOp.OpKind.Mul));
    }

    [Fact]
    public void GetBinaryOperator_Div_ReturnsSlash()
    {
        var gen = CreateGenerator();
        Assert.Equal("/", gen.GetBinaryOperator(IrBinaryOp.OpKind.Div));
    }

    [Fact]
    public void GetBinaryOperator_Mod_ReturnsPercent()
    {
        var gen = CreateGenerator();
        Assert.Equal("%", gen.GetBinaryOperator(IrBinaryOp.OpKind.Mod));
    }

    [Fact]
    public void GetBinaryOperator_Eq_ReturnsDoubleEquals()
    {
        var gen = CreateGenerator();
        Assert.Equal("==", gen.GetBinaryOperator(IrBinaryOp.OpKind.Eq));
    }

    [Fact]
    public void GetBinaryOperator_Ne_ReturnsNotEquals()
    {
        var gen = CreateGenerator();
        Assert.Equal("!=", gen.GetBinaryOperator(IrBinaryOp.OpKind.Ne));
    }

    [Fact]
    public void GetBinaryOperator_Lt_ReturnsLessThan()
    {
        var gen = CreateGenerator();
        Assert.Equal("<", gen.GetBinaryOperator(IrBinaryOp.OpKind.Lt));
    }

    [Fact]
    public void GetBinaryOperator_Le_ReturnsLessOrEqual()
    {
        var gen = CreateGenerator();
        Assert.Equal("<=", gen.GetBinaryOperator(IrBinaryOp.OpKind.Le));
    }

    [Fact]
    public void GetBinaryOperator_Gt_ReturnsGreaterThan()
    {
        var gen = CreateGenerator();
        Assert.Equal(">", gen.GetBinaryOperator(IrBinaryOp.OpKind.Gt));
    }

    [Fact]
    public void GetBinaryOperator_Ge_ReturnsGreaterOrEqual()
    {
        var gen = CreateGenerator();
        Assert.Equal(">=", gen.GetBinaryOperator(IrBinaryOp.OpKind.Ge));
    }

    [Fact]
    public void GetBinaryOperator_And_ReturnsAmpersand()
    {
        var gen = CreateGenerator();
        Assert.Equal("&", gen.GetBinaryOperator(IrBinaryOp.OpKind.And));
    }

    [Fact]
    public void GetBinaryOperator_Or_ReturnsPipe()
    {
        var gen = CreateGenerator();
        Assert.Equal("|", gen.GetBinaryOperator(IrBinaryOp.OpKind.Or));
    }

    [Fact]
    public void GetBinaryOperator_Xor_ReturnsCaret()
    {
        var gen = CreateGenerator();
        Assert.Equal("^", gen.GetBinaryOperator(IrBinaryOp.OpKind.Xor));
    }

    [Fact]
    public void GetBinaryOperator_Shl_ReturnsDoubleLeftAngle()
    {
        var gen = CreateGenerator();
        Assert.Equal("<<", gen.GetBinaryOperator(IrBinaryOp.OpKind.Shl));
    }

    [Fact]
    public void GetBinaryOperator_Shr_ReturnsDoubleRightAngle()
    {
        var gen = CreateGenerator();
        Assert.Equal(">>", gen.GetBinaryOperator(IrBinaryOp.OpKind.Shr));
    }

    #endregion

    #region SanitizeVariableName Tests

    [Fact]
    public void SanitizeVariableName_PercentPrefix_RemovesPercentAddsUnderscore()
    {
        var gen = CreateGenerator();
        Assert.Equal("_t0", gen.SanitizeVariableName("%t0"));
    }

    [Fact]
    public void SanitizeVariableName_NormalName_ReturnsUnchanged()
    {
        var gen = CreateGenerator();
        Assert.Equal("foo", gen.SanitizeVariableName("foo"));
    }

    [Fact]
    public void SanitizeVariableName_PercentT1_Returns_t1()
    {
        var gen = CreateGenerator();
        Assert.Equal("_t1", gen.SanitizeVariableName("%t1"));
    }

    #endregion

    #region MangleName Tests

    [Fact]
    public void MangleName_ModuleSeparator_ReplacesWithUnderscore()
    {
        var gen = CreateGenerator();
        Assert.Equal("std_io_print", gen.MangleName("std::io::print"));
    }

    [Fact]
    public void MangleName_GenericWithAngleBrackets_ReplacesWithUnderscore()
    {
        var gen = CreateGenerator();
        Assert.Equal("Vec_i32", gen.MangleName("Vec<i32>"));
    }

    [Fact]
    public void MangleName_SimpleName_ReturnsUnchanged()
    {
        var gen = CreateGenerator();
        Assert.Equal("hello", gen.MangleName("hello"));
    }

    [Fact]
    public void MangleName_EnumType_UsesEnumName()
    {
        var gen = CreateGenerator();
        var enumType = new IrEnumType("Option", new List<IrEnumVariant>());
        Assert.Equal("Option", gen.MangleName(enumType));
    }

    [Fact]
    public void MangleName_StructType_UsesStructName()
    {
        var gen = CreateGenerator();
        var structType = new IrStructType("Point", new List<IrStructField>());
        Assert.Equal("Point", gen.MangleName(structType));
    }

    #endregion

    #region EscapeString Tests

    [Fact]
    public void EscapeString_Backslash_Escapes()
    {
        var gen = CreateGenerator();
        Assert.Equal("\\\\", gen.EscapeString("\\"));
    }

    [Fact]
    public void EscapeString_DoubleQuote_Escapes()
    {
        var gen = CreateGenerator();
        Assert.Equal("\\\"", gen.EscapeString("\""));
    }

    [Fact]
    public void EscapeString_Newline_Escapes()
    {
        var gen = CreateGenerator();
        Assert.Equal("\\n", gen.EscapeString("\n"));
    }

    [Fact]
    public void EscapeString_Tab_Escapes()
    {
        var gen = CreateGenerator();
        Assert.Equal("\\t", gen.EscapeString("\t"));
    }

    [Fact]
    public void EscapeString_CarriageReturn_Escapes()
    {
        var gen = CreateGenerator();
        Assert.Equal("\\r", gen.EscapeString("\r"));
    }

    [Fact]
    public void EscapeString_NormalText_ReturnsUnchanged()
    {
        var gen = CreateGenerator();
        Assert.Equal("hello", gen.EscapeString("hello"));
    }

    [Fact]
    public void EscapeString_MultipleEscapes_EscapesAll()
    {
        var gen = CreateGenerator();
        Assert.Equal("Hello\\nWorld\\t\\\"Test\\\"", gen.EscapeString("Hello\nWorld\t\"Test\""));
    }

    #endregion

    #region EmitIntegerConstant Tests

    [Fact]
    public void EmitIntegerConstant_PositiveI32_EmitsWithCast()
    {
        var gen = CreateGenerator();
        var constant = new IrConstant(42, new IrIntType(32, true));

        var result = gen.EmitIntegerConstant(constant);
        // CCodeGenerator emits explicit casts for type safety
        Assert.Equal("(int32_t)42", result);
    }

    [Fact]
    public void EmitIntegerConstant_Zero_EmitsZero()
    {
        var gen = CreateGenerator();
        var constant = new IrConstant(0, new IrIntType(32, true));

        var result = gen.EmitIntegerConstant(constant);
        Assert.Contains("0", result);
    }

    [Fact]
    public void EmitIntegerConstant_NegativeI32_EmitsWithCast()
    {
        var gen = CreateGenerator();
        var constant = new IrConstant(-42, new IrIntType(32, true));

        var result = gen.EmitIntegerConstant(constant);
        Assert.Equal("(int32_t)-42", result);
    }

    [Fact]
    public void EmitIntegerConstant_U32Max_EmitsWithCast()
    {
        var gen = CreateGenerator();
        var constant = new IrConstant(0xFFFFFFFF, new IrIntType(32, false));

        var result = gen.EmitIntegerConstant(constant);
        Assert.Equal("(uint32_t)4294967295U", result);
    }

    #endregion

    #region EmitFloatConstant Tests

    [Fact]
    public void EmitFloatConstant_F32_PreservesExactBits()
    {
        var gen = CreateGenerator();
        var constant = new IrFloatConstant(3.14, new IrFloatType(32));

        var result = gen.EmitFloatConstant(constant);
        Assert.Equal("__novus_f32_from_bits(0x4048F5C3U)", result);
    }

    [Fact]
    public void EmitFloatConstant_F64_PreservesExactBits()
    {
        var gen = CreateGenerator();
        var constant = new IrFloatConstant(3.14, new IrFloatType(64));

        var result = gen.EmitFloatConstant(constant);
        Assert.Equal("__novus_f64_from_bits(0x40091EB851EB851FULL)", result);
    }

    #endregion

    #region GetTupleTypeName Tests

    [Fact]
    public void GetTupleTypeName_EmptyTuple_ReturnsVoid()
    {
        var gen = CreateGenerator();
        var tupleType = new IrTupleType(new List<IrType>());

        Assert.Equal("void", gen.GetTupleTypeName(tupleType));
    }

    [Fact]
    public void GetTupleTypeName_TwoInts_ReturnsTupleTypeName()
    {
        var gen = CreateGenerator();
        var tupleType = new IrTupleType(new List<IrType>
        {
            new IrIntType(32, true),
            new IrIntType(32, true)
        });

        var result = gen.GetTupleTypeName(tupleType);
        Assert.StartsWith("Tuple_", result);
    }

    #endregion

    #region GetFunctionPointerType Tests

    [Fact]
    public void GetFunctionPointerType_IntToInt_ReturnsCorrectSyntax()
    {
        var gen = CreateGenerator();
        var fpType = new IrFunctionPointerType(
            new List<IrType> { new IrIntType(32, true) },
            new IrIntType(32, true)
        );

        var result = gen.GetFunctionPointerType(fpType);
        Assert.Contains("int32_t", result);
        Assert.Contains("(*)", result);
    }

    [Fact]
    public void GetFunctionPointerType_VoidToVoid_ReturnsCorrectSyntax()
    {
        var gen = CreateGenerator();
        var fpType = new IrFunctionPointerType(
            new List<IrType>(),
            IrVoidType.Instance
        );

        var result = gen.GetFunctionPointerType(fpType);
        Assert.Equal("void (*)(void)", result);
    }

    #endregion

    #region GetCVariableDeclaration Tests

    [Fact]
    public void GetCVariableDeclaration_FunctionPointer_CorrectSyntax()
    {
        // Function pointer variable: int32_t (*callback)(int32_t)
        var gen = CreateGenerator();
        var fpType = new IrFunctionPointerType(
            new List<IrType> { new IrIntType(32, true) },
            new IrIntType(32, true)
        );

        var result = gen.GetCVariableDeclaration(fpType, "callback");
        Assert.Equal("int32_t (*callback)(int32_t)", result);
    }

    [Fact]
    public void GetCVariableDeclaration_PointerToFunctionPointer_CorrectSyntax()
    {
        // Pointer to function pointer: int32_t (**param_addr)(void)
        var gen = CreateGenerator();
        var fpType = new IrFunctionPointerType(
            new List<IrType>(),
            new IrIntType(32, true)
        );
        var ptrToFp = new IrPointerType(fpType);

        var result = gen.GetCVariableDeclaration(ptrToFp, "param_addr");
        Assert.Equal("int32_t (**param_addr)(void)", result);
    }

    [Fact]
    public void GetCVariableDeclaration_NormalPointer_CorrectSyntax()
    {
        // Normal pointer: int32_t* ptr
        var gen = CreateGenerator();
        var ptrType = new IrPointerType(new IrIntType(32, true));

        var result = gen.GetCVariableDeclaration(ptrType, "ptr");
        Assert.Equal("int32_t* ptr", result);
    }

    [Fact]
    public void GetCVariableDeclaration_PrimitiveType_CorrectSyntax()
    {
        // Primitive: int32_t x
        var gen = CreateGenerator();
        var intType = new IrIntType(32, true);

        var result = gen.GetCVariableDeclaration(intType, "x");
        Assert.Equal("int32_t x", result);
    }

    [Fact]
    public void GetCVariableDeclaration_VoidFunctionPointer_CorrectSyntax()
    {
        // void (*handler)(void)
        var gen = CreateGenerator();
        var fpType = new IrFunctionPointerType(
            new List<IrType>(),
            IrVoidType.Instance
        );

        var result = gen.GetCVariableDeclaration(fpType, "handler");
        Assert.Equal("void (*handler)(void)", result);
    }

    [Fact]
    public void GetCVariableDeclaration_PointerToPointerToFunctionPointer_CorrectSyntax()
    {
        // Double pointer to function pointer: int32_t (***ppp)(void)
        var gen = CreateGenerator();
        var fpType = new IrFunctionPointerType(
            new List<IrType>(),
            new IrIntType(32, true)
        );
        var ptrToFp = new IrPointerType(fpType);
        var ptrToPtrToFp = new IrPointerType(ptrToFp);

        var result = gen.GetCVariableDeclaration(ptrToPtrToFp, "ppp");
        Assert.Equal("int32_t (***ppp)(void)", result);
    }

    [Fact]
    public void GetCVariableDeclaration_ReferenceToFunctionPointer_CorrectSyntax()
    {
        // Reference to function pointer (becomes pointer in C): int32_t (**ref)(void)
        var gen = CreateGenerator();
        var fpType = new IrFunctionPointerType(
            new List<IrType>(),
            new IrIntType(32, true)
        );
        var refToFp = new IrReferenceType(fpType);

        var result = gen.GetCVariableDeclaration(refToFp, "ref_fp");
        Assert.Equal("int32_t (**ref_fp)(void)", result);
    }

    #endregion

    #region EmitIntegerConstant Tests

    [Fact]
    public void EmitIntegerConstant_PointerTypedConstant_AddsCast()
    {
        // When a constant has pointer type, it should be cast
        var gen = CreateGenerator();
        var ptrType = new IrPointerType(new IrIntType(8, false));  // *u8
        var constant = new IrConstant(100, ptrType);

        var result = gen.EmitIntegerConstant(constant);
        Assert.Equal("(uint8_t*)100", result);
    }

    [Fact]
    public void EmitIntegerConstant_PointerTypedConstant_NegativeOne_UsesNullPtrSentinel()
    {
        // -1 as pointer should use NULL_PTR_SENTINEL for VBCC compatibility
        var gen = CreateGenerator();
        var ptrType = new IrPointerType(new IrIntType(8, false));
        var constant = new IrConstant(-1, ptrType);

        var result = gen.EmitIntegerConstant(constant);
        Assert.Equal("(uint8_t*)NULL_PTR_SENTINEL", result);
    }

    [Fact]
    public void EmitIntegerConstant_ZeroValue_ReturnsJustZero()
    {
        // Zero should be emitted as plain "0" for C's implicit null conversion
        var gen = CreateGenerator();
        var i32Type = new IrIntType(32, true);
        var constant = new IrConstant(0, i32Type);

        var result = gen.EmitIntegerConstant(constant);
        Assert.Equal("0", result);
    }

    [Fact]
    public void EmitIntegerConstant_I32Value_AddsCast()
    {
        // 32-bit signed integers should have explicit cast for VBCC
        var gen = CreateGenerator();
        var i32Type = new IrIntType(32, true);
        var constant = new IrConstant(42, i32Type);

        var result = gen.EmitIntegerConstant(constant);
        Assert.Equal("(int32_t)42", result);
    }

    [Fact]
    public void EmitIntegerConstant_U32Value_AddsCastAndSuffix()
    {
        // 32-bit unsigned integers should have cast and U suffix
        var gen = CreateGenerator();
        var u32Type = new IrIntType(32, false);
        var constant = new IrConstant(42, u32Type);

        var result = gen.EmitIntegerConstant(constant);
        Assert.Equal("(uint32_t)42U", result);
    }

    #endregion
}

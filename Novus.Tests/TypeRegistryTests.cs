using Antlr4.Runtime;
using Novus.Codegen;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;
using System.Text.RegularExpressions;

namespace Novus.Tests;

/// <summary>
/// Tests for TypeRegistry to improve coverage from 0% to 70%+.
/// TypeRegistry collects type definitions from modules to generate shared type headers.
/// </summary>
public class TypeRegistryTests
{
    private IrModule BuildIR(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var builder = new IrBuilder(skipAutoImports: true);
        return builder.BuildModule(tree);
    }

    [Fact]
    public void TypeRegistry_EmptyModule_HasNoTypes()
    {
        var registry = new TypeRegistry();
        var module = new IrModule();

        registry.RegisterModule(module);

        Assert.Empty(registry.EnumTypes);
        Assert.Empty(registry.StructTypes);
        Assert.Empty(registry.TupleTypes);
    }

    [Fact]
    public void TypeRegistry_SimpleStruct_RegistersStruct()
    {
        var source = @"
pub struct Point {
    x: i32,
    y: i32,
}

pub fn make_point(x: i32, y: i32) -> Point {
    return Point { x: x, y: y }
}";

        var module = BuildIR(source);
        var registry = new TypeRegistry();

        registry.RegisterModule(module);

        Assert.Single(registry.StructTypes);
        var structType = registry.StructTypes.First();
        Assert.Equal("Point", structType.Name);
        Assert.Equal(2, structType.Fields.Count);
    }

    [Fact]
    public void SharedHeader_EmitsNovusStructThatClashesWithNdkName()
    {
        var module = BuildIR(@"
pub struct Point {
    x: i16,
    y: i16,
}");
        var registry = new TypeRegistry();
        registry.RegisterModule(module);

        var header = CCodeGenerator.GenerateSharedTypesHeader(registry);

        Assert.Contains("typedef struct nv_Point nv_Point;", header);
        Assert.Contains("struct nv_Point {", header);
    }

    [Fact]
    public void SharedHeader_PrefixesNovusDeviceThatClashesWithNdkDevice()
    {
        var nativeAttributes = new AttributeCollection();
        nativeAttributes.Add(new AttributeInfo(
            KnownAttributes.ExternType,
            new SourceLocation("native.novus", 1, 1, 0, "")));
        var nativeModule = new IrModule();
        nativeModule.AddStruct(new IrStructType("Device", [], attributes: nativeAttributes));
        var module = BuildIR(@"
pub struct AudioDeviceHandle {
    value: u32,
}
pub struct Device {
    handle: AudioDeviceHandle,
}
pub enum DeviceResult {
    Ok(Device),
}");
        var registry = new TypeRegistry();
        registry.RegisterModule(nativeModule);
        registry.RegisterModule(module);

        var header = CCodeGenerator.GenerateSharedTypesHeader(registry);

        Assert.Contains("typedef struct nv_Device nv_Device;", header);
        Assert.Contains("struct nv_Device {", header);
        Assert.Contains("AudioDeviceHandle handle;", header);
        Assert.Contains("nv_Device _0;", header);
        Assert.True(
            header.IndexOf("struct AudioDeviceHandle {", StringComparison.Ordinal) <
            header.IndexOf("struct nv_Device {", StringComparison.Ordinal));
    }

    [Fact]
    public void SharedHeader_DeclaresFunctionPointerStructFields()
    {
        var module = BuildIR(@"
pub struct Handler {
    callback: fn(u32) -> i16,
}");
        var registry = new TypeRegistry();
        registry.RegisterModule(module);

        var header = CCodeGenerator.GenerateSharedTypesHeader(registry);

        Assert.Contains("int16_t (*callback)(uint32_t);", header);
    }

    [Fact]
    public void SharedHeader_KeepsNativeAndNovusStructsWithSameName()
    {
        var nativeAttributes = new AttributeCollection();
        nativeAttributes.Add(new AttributeInfo(
            KnownAttributes.ExternType,
            new SourceLocation("native.novus", 1, 1, 0, "")));
        var nativeModule = new IrModule();
        nativeModule.AddStruct(new IrStructType("Point",
        [
            new IrStructField("x", IrIntType.I16),
            new IrStructField("y", IrIntType.I16),
        ], attributes: nativeAttributes));
        var novusModule = BuildIR(@"
pub struct Point {
    x: i16,
    y: i16,
}
pub struct Ellipse {
    center: Point,
}
pub enum ShapeResult {
    Ok(Ellipse),
}");
        var registry = new TypeRegistry();
        registry.RegisterModule(nativeModule);
        registry.RegisterModule(novusModule);

        var header = CCodeGenerator.GenerateSharedTypesHeader(registry);

        Assert.Contains("typedef struct nv_Point nv_Point;", header);
        Assert.Contains("struct nv_Point {", header);
        Assert.Contains("nv_Point center;", header);
        Assert.True(
            header.IndexOf("struct nv_Point {", StringComparison.Ordinal) <
            header.IndexOf("struct Ellipse {", StringComparison.Ordinal));
    }

    [Fact]
    public void TypeRegistry_SimpleEnum_RegistersEnum()
    {
        var source = @"
pub enum Color {
    Red,
    Green,
    Blue,
}

pub fn get_red() -> Color {
    return Color::Red
}";

        var module = BuildIR(source);
        var registry = new TypeRegistry();

        registry.RegisterModule(module);

        Assert.Single(registry.EnumTypes);
        var enumType = registry.EnumTypes.First();
        Assert.Equal("Color", enumType.Name);
        Assert.Equal(3, enumType.Variants.Count);
    }

    [Fact]
    public void TypeRegistry_EnumWithAssociatedData_RegistersEnum()
    {
        var source = @"
pub enum Result {
    Ok(i32),
    Err(i32),
}

pub fn make_ok(x: i32) -> Result {
    return Result::Ok(x)
}";

        var module = BuildIR(source);
        var registry = new TypeRegistry();

        registry.RegisterModule(module);

        Assert.Single(registry.EnumTypes);
        var enumType = registry.EnumTypes.First();
        Assert.Equal("Result", enumType.Name);
        Assert.Equal(2, enumType.Variants.Count);
    }

    [Fact]
    public void TypeRegistry_PreservesAndOrdersNestedGenericEnums()
    {
        var result = new IrEnumType("Result", [
            new IrEnumVariant("Ok", 0, [new IrTupleType([])]),
            new IrEnumVariant("Err", 1, [IrIntType.I32]),
        ], cacheKey: "Result<(), i32>", typeArguments: [new IrTupleType([]), IrIntType.I32]);
        var poll = new IrEnumType("Poll", [
            new IrEnumVariant("Ready", 0, [result]),
            new IrEnumVariant("Pending", 1),
        ], cacheKey: "Poll<Result<(), i32>>", typeArguments: [result]);
        var module = new IrModule();
        module.AddEnum(result);
        module.AddEnum(poll);
        var registry = new TypeRegistry();

        registry.RegisterModule(module);

        Assert.Equal(2, registry.EnumTypes.Count());
        var header = CCodeGenerator.GenerateSharedTypesHeader(registry);
        Assert.True(header.IndexOf("// Enum: Result<(), i32>", StringComparison.Ordinal) <
                    header.IndexOf("// Enum: Poll<Result<(), i32>>", StringComparison.Ordinal));
    }

    [Fact]
    public void TypeRegistry_MultipleStructs_RegistersAll()
    {
        var source = @"
pub struct Point {
    x: i32,
    y: i32,
}

pub struct Size {
    width: i32,
    height: i32,
}

pub fn make_point(x: i32, y: i32) -> Point {
    return Point { x: x, y: y }
}

pub fn make_size(w: i32, h: i32) -> Size {
    return Size { width: w, height: h }
}";

        var module = BuildIR(source);
        var registry = new TypeRegistry();

        registry.RegisterModule(module);

        Assert.Equal(2, registry.StructTypes.Count());
        var structNames = registry.StructTypes.Select(s => s.Name).ToHashSet();
        Assert.Contains("Point", structNames);
        Assert.Contains("Size", structNames);
    }

    [Fact]
    public void TypeRegistry_MultipleEnums_RegistersAll()
    {
        var source = @"
pub enum Color {
    Red,
    Green,
}

pub enum Status {
    Active,
    Inactive,
}

pub fn get_red() -> Color {
    return Color::Red
}

pub fn get_active() -> Status {
    return Status::Active
}";

        var module = BuildIR(source);
        var registry = new TypeRegistry();

        registry.RegisterModule(module);

        Assert.Equal(2, registry.EnumTypes.Count());
        var enumNames = registry.EnumTypes.Select(e => e.Name).ToHashSet();
        Assert.Contains("Color", enumNames);
        Assert.Contains("Status", enumNames);
    }

    [Fact]
    public void TypeRegistry_NestedStruct_RegistersOuterStruct()
    {
        var source = @"
pub struct Inner {
    value: i32,
}

pub struct Outer {
    inner: Inner,
    id: i32,
}

pub fn make_outer(v: i32, i: i32) -> Outer {
    var inn: Inner = Inner { value: v }
    return Outer { inner: inn, id: i }
}";

        var module = BuildIR(source);
        var registry = new TypeRegistry();

        registry.RegisterModule(module);

        // Both structs should be registered
        Assert.Equal(2, registry.StructTypes.Count());
        var structNames = registry.StructTypes.Select(s => s.Name).ToHashSet();
        Assert.Contains("Inner", structNames);
        Assert.Contains("Outer", structNames);
    }

    [Fact]
    public void TypeRegistry_StructInParameter_RegistersStruct()
    {
        var source = @"
pub struct Point {
    x: i32,
    y: i32,
}

pub fn distance(p: Point) -> i32 {
    return p.x + p.y
}";

        var module = BuildIR(source);
        var registry = new TypeRegistry();

        registry.RegisterModule(module);

        Assert.Single(registry.StructTypes);
        Assert.Equal("Point", registry.StructTypes.First().Name);
    }

    [Fact]
    public void TypeRegistry_EnumInParameter_RegistersEnum()
    {
        var source = @"
pub enum Color {
    Red,
    Green,
}

pub fn process_color(c: Color) -> i32 {
    return 0
}";

        var module = BuildIR(source);
        var registry = new TypeRegistry();

        registry.RegisterModule(module);

        Assert.Single(registry.EnumTypes);
        Assert.Equal("Color", registry.EnumTypes.First().Name);
    }

    [Fact]
    public void TypeRegistry_StructInLocalVariable_RegistersStruct()
    {
        var source = @"
pub struct Point {
    x: i32,
    y: i32,
}

pub fn make_local() -> i32 {
    var p: Point = Point { x: 1, y: 2 }
    return p.x
}";

        var module = BuildIR(source);
        var registry = new TypeRegistry();

        registry.RegisterModule(module);

        Assert.Single(registry.StructTypes);
        Assert.Equal("Point", registry.StructTypes.First().Name);
    }

    [Fact]
    public void TypeRegistry_TupleType_RegistersTuple()
    {
        var source = @"
pub fn make_tuple() -> (i32, i32) {
    return (1, 2)
}";

        var module = BuildIR(source);
        var registry = new TypeRegistry();

        registry.RegisterModule(module);

        Assert.Single(registry.TupleTypes);
        var tupleType = registry.TupleTypes.First();
        Assert.Equal(2, tupleType.ElementTypes.Count);
    }

    [Fact]
    public void TypeRegistry_TupleInParameter_RegistersTuple()
    {
        var source = @"
pub fn process_tuple(t: (i32, i32)) -> i32 {
    return 0
}";

        var module = BuildIR(source);
        var registry = new TypeRegistry();

        registry.RegisterModule(module);

        Assert.Single(registry.TupleTypes);
        Assert.Equal(2, registry.TupleTypes.First().ElementTypes.Count);
    }

    [Fact]
    public void TypeRegistry_ArrayOfStruct_RegistersStruct()
    {
        var source = @"
pub struct Point {
    x: i32,
    y: i32,
}

pub fn make_array() -> i32 {
    var points = [Point { x: 1, y: 2 }; 3]
    return points[0].x
}";

        var module = BuildIR(source);
        var registry = new TypeRegistry();

        registry.RegisterModule(module);

        Assert.Single(registry.StructTypes);
        Assert.Equal("Point", registry.StructTypes.First().Name);
    }

    [Fact]
    public void TypeRegistry_PointerToStruct_RegistersStruct()
    {
        var source = @"
pub struct Point {
    x: i32,
    y: i32,
}

pub fn use_pointer(p: *Point) -> i32 {
    return 0
}";

        var module = BuildIR(source);
        var registry = new TypeRegistry();

        registry.RegisterModule(module);

        Assert.Single(registry.StructTypes);
        Assert.Equal("Point", registry.StructTypes.First().Name);
    }

    [Fact]
    public void TypeRegistry_ReferenceToStruct_RegistersStruct()
    {
        var source = @"
pub struct Point {
    x: i32,
    y: i32,
}

pub fn use_ref(p: &Point) -> i32 {
    return p.x
}";

        var module = BuildIR(source);
        var registry = new TypeRegistry();

        registry.RegisterModule(module);

        Assert.Single(registry.StructTypes);
        Assert.Equal("Point", registry.StructTypes.First().Name);
    }

    [Fact]
    public void TypeRegistry_MutReferenceToStruct_RegistersStruct()
    {
        var source = @"
pub struct Point {
    x: i32,
    y: i32,
}

pub fn use_mut_ref(p: &var Point) -> i32 {
    return p.x
}";

        var module = BuildIR(source);
        var registry = new TypeRegistry();

        registry.RegisterModule(module);

        Assert.Single(registry.StructTypes);
        Assert.Equal("Point", registry.StructTypes.First().Name);
    }

    [Fact]
    public void TypeRegistry_MultipleModules_CombinesTypes()
    {
        var source1 = @"
pub struct Point {
    x: i32,
    y: i32,
}

pub fn make_point() -> Point {
    return Point { x: 1, y: 2 }
}";

        var source2 = @"
pub struct Size {
    width: i32,
    height: i32,
}

pub fn make_size() -> Size {
    return Size { width: 10, height: 20 }
}";

        var module1 = BuildIR(source1);
        var module2 = BuildIR(source2);
        var registry = new TypeRegistry();

        registry.RegisterModule(module1);
        registry.RegisterModule(module2);

        Assert.Equal(2, registry.StructTypes.Count());
        var structNames = registry.StructTypes.Select(s => s.Name).ToHashSet();
        Assert.Contains("Point", structNames);
        Assert.Contains("Size", structNames);
    }

    [Fact]
    public void TypeRegistry_SameNameStructsInDifferentModules_GenerateDistinctDefinitions()
    {
        var source1 = @"
pub struct GadgetFixture {
    list: i16,
    context: i16,
    gadget: i16,
}

pub fn make_a() -> GadgetFixture {
    return GadgetFixture { list: 0, context: 0, gadget: 0 }
}";

        var source2 = @"
pub struct GadgetFixture {
    screen: i32,
    window: i32,
}

pub fn make_b() -> GadgetFixture {
    return GadgetFixture { screen: 1, window: 2 }
}";

        var moduleA = BuildIR(source1);
        var moduleB = BuildIR(source2);
        var registry = new TypeRegistry();

        registry.RegisterModule(moduleA);
        registry.RegisterModule(moduleB);

        var matching = registry.StructTypes.Where(s => s.Name == "GadgetFixture").ToList();
        Assert.Equal(2, matching.Count);

        var header = CCodeGenerator.GenerateSharedTypesHeader(registry);
        var structDefs = Regex.Matches(header, @"struct (GadgetFixture(?:__novus_[0-9a-f]{8})?) \{")
                              .Select(m => m.Groups[1].Value)
                              .ToList();
        Assert.Equal(2, structDefs.Count);
        Assert.Contains("GadgetFixture", structDefs);
        Assert.Matches(@"GadgetFixture__novus_[0-9a-f]{8}", string.Join(' ', structDefs));
    }

    [Fact]
    public void TypeRegistry_UnitTuple_NotRegistered()
    {
        var source = @"
pub fn returns_unit() -> i32 {
    return 42
}";

        var module = BuildIR(source);
        var registry = new TypeRegistry();

        registry.RegisterModule(module);

        // Unit type () should not be registered
        Assert.Empty(registry.TupleTypes);
    }

    [Fact]
    public void TypeRegistry_EnumInMatch_RegistersEnum()
    {
        var source = @"
pub enum Color {
    Red,
    Green,
}

pub fn match_color(c: Color) -> i32 {
    match c {
        Color::Red => return 1,
        Color::Green => return 2,
    }
}";

        var module = BuildIR(source);
        var registry = new TypeRegistry();

        registry.RegisterModule(module);

        Assert.Single(registry.EnumTypes);
        Assert.Equal("Color", registry.EnumTypes.First().Name);
    }

    [Fact]
    public void TypeRegistry_ComplexNestedTypes_RegistersAll()
    {
        var source = @"
pub struct Inner {
    value: i32,
}

pub struct Outer {
    inner: Inner,
    id: i32,
}

pub fn make_outer(i: i32) -> Outer {
    var inner: Inner = Inner { value: i }
    return Outer { inner: inner, id: 1 }
}";

        var module = BuildIR(source);
        var registry = new TypeRegistry();

        registry.RegisterModule(module);

        // Should register both structs
        Assert.Equal(2, registry.StructTypes.Count());
        var structNames = registry.StructTypes.Select(s => s.Name).ToHashSet();
        Assert.Contains("Inner", structNames);
        Assert.Contains("Outer", structNames);
    }

    [Fact]
    public void TypeRegistry_StaticVariable_RegistersType()
    {
        var source = @"
pub struct Point {
    x: i32,
    y: i32,
}

pub fn get_point() -> Point {
    return Point { x: 0, y: 0 }
}";

        var module = BuildIR(source);
        var registry = new TypeRegistry();

        registry.RegisterModule(module);

        Assert.Single(registry.StructTypes);
        Assert.Equal("Point", registry.StructTypes.First().Name);
    }

    [Fact]
    public void TypeRegistry_ExternalVariable_RegistersType()
    {
        var source = @"
pub struct Config {
    value: i32,
}

extern CFG: Config

pub fn get_config() -> Config {
    return CFG
}";

        var module = BuildIR(source);
        var registry = new TypeRegistry();

        registry.RegisterModule(module);

        Assert.Single(registry.StructTypes);
        Assert.Equal("Config", registry.StructTypes.First().Name);
    }
}

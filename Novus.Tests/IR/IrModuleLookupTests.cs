using Novus.IR;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests.IR;

/// <summary>
/// Tests for O(1) dictionary-based lookups in IrModule.
/// Verifies that AddEnum/AddStruct/AddTrait/AddTraitImpl properly maintain
/// internal lookup dictionaries and that Get methods return correct results.
/// </summary>
public class IrModuleLookupTests
{
    [Fact]
    public void GetEnum_AfterAddEnum_ReturnsCorrectEnum()
    {
        var module = new IrModule();
        var enumType = new IrEnumType(
            "MyEnum",
            new System.Collections.Generic.List<IrEnumVariant>
            {
                new IrEnumVariant("Variant1", 0)
            }
        );

        module.AddEnum(enumType);
        var retrieved = module.GetEnum("MyEnum");

        Assert.NotNull(retrieved);
        Assert.Same(enumType, retrieved);
        Assert.Equal("MyEnum", retrieved.EnumName);
    }

    [Fact]
    public void GetEnum_NonExistentEnum_ReturnsNull()
    {
        var module = new IrModule();
        var retrieved = module.GetEnum("NonExistent");

        Assert.Null(retrieved);
    }

    [Fact]
    public void GetStruct_AfterAddStruct_ReturnsCorrectStruct()
    {
        var module = new IrModule();
        var structType = new IrStructType(
            "MyStruct",
            new System.Collections.Generic.List<IrStructField>
            {
                new IrStructField("field1", new IrIntType(32, true))
            }
        );

        module.AddStruct(structType);
        var retrieved = module.GetStruct("MyStruct");

        Assert.NotNull(retrieved);
        Assert.Same(structType, retrieved);
        Assert.Equal("MyStruct", retrieved.StructName);
    }

    [Fact]
    public void GetStruct_NonExistentStruct_ReturnsNull()
    {
        var module = new IrModule();
        var retrieved = module.GetStruct("NonExistent");

        Assert.Null(retrieved);
    }

    [Fact]
    public void GetTrait_AfterAddTrait_ReturnsCorrectTrait()
    {
        var module = new IrModule();
        var trait = new IrTrait(
            "MyTrait",
            new System.Collections.Generic.List<IrTraitMethod>()
        );

        module.AddTrait(trait);
        var retrieved = module.GetTrait("MyTrait");

        Assert.NotNull(retrieved);
        Assert.Same(trait, retrieved);
        Assert.Equal("MyTrait", retrieved.TraitName);
    }

    [Fact]
    public void GetTrait_NonExistentTrait_ReturnsNull()
    {
        var module = new IrModule();
        var retrieved = module.GetTrait("NonExistent");

        Assert.Null(retrieved);
    }

    [Fact]
    public void GetTraitImpl_AfterAddTraitImpl_ReturnsCorrectImpl()
    {
        var module = new IrModule();
        var traitImpl = new IrTraitImpl(
            "MyTrait",
            new System.Collections.Generic.List<IrType>(),
            "MyType",
            new IrIntType(32, true)
        );

        module.AddTraitImpl(traitImpl);
        var retrieved = module.GetTraitImpl("MyTrait", "MyType");

        Assert.NotNull(retrieved);
        Assert.Same(traitImpl, retrieved);
        Assert.Equal("MyTrait", retrieved.TraitName);
        Assert.Equal("MyType", retrieved.TypeName);
    }

    [Fact]
    public void GetTraitImpl_NonExistentTraitName_ReturnsNull()
    {
        var module = new IrModule();
        var traitImpl = new IrTraitImpl(
            "MyTrait",
            new System.Collections.Generic.List<IrType>(),
            "MyType",
            new IrIntType(32, true)
        );

        module.AddTraitImpl(traitImpl);
        var retrieved = module.GetTraitImpl("NonExistentTrait", "MyType");

        Assert.Null(retrieved);
    }

    [Fact]
    public void GetTraitImpl_NonExistentTypeName_ReturnsNull()
    {
        var module = new IrModule();
        var traitImpl = new IrTraitImpl(
            "MyTrait",
            new System.Collections.Generic.List<IrType>(),
            "MyType",
            new IrIntType(32, true)
        );

        module.AddTraitImpl(traitImpl);
        var retrieved = module.GetTraitImpl("MyTrait", "NonExistentType");

        Assert.Null(retrieved);
    }

    [Fact]
    public void GetTraitImpl_CompositeKey_DistinguishesTraitAndType()
    {
        // Verify that the composite key (TraitName, TypeName) works correctly
        // and that two impls with same trait but different types are distinct
        var module = new IrModule();
        var impl1 = new IrTraitImpl(
            "Display",
            new System.Collections.Generic.List<IrType>(),
            "i32",
            new IrIntType(32, true)
        );
        var impl2 = new IrTraitImpl(
            "Display",
            new System.Collections.Generic.List<IrType>(),
            "Str",
            new IrIntType(32, true)
        );

        module.AddTraitImpl(impl1);
        module.AddTraitImpl(impl2);

        var retrieved1 = module.GetTraitImpl("Display", "i32");
        var retrieved2 = module.GetTraitImpl("Display", "Str");

        Assert.NotNull(retrieved1);
        Assert.NotNull(retrieved2);
        Assert.Same(impl1, retrieved1);
        Assert.Same(impl2, retrieved2);
        Assert.NotSame(retrieved1, retrieved2);
    }

    [Fact]
    public void MultipleEnums_GetEnum_ReturnsCorrectEnum()
    {
        var module = new IrModule();
        var enum1 = new IrEnumType("Enum1", new System.Collections.Generic.List<IrEnumVariant>());
        var enum2 = new IrEnumType("Enum2", new System.Collections.Generic.List<IrEnumVariant>());
        var enum3 = new IrEnumType("Enum3", new System.Collections.Generic.List<IrEnumVariant>());

        module.AddEnum(enum1);
        module.AddEnum(enum2);
        module.AddEnum(enum3);

        Assert.Same(enum1, module.GetEnum("Enum1"));
        Assert.Same(enum2, module.GetEnum("Enum2"));
        Assert.Same(enum3, module.GetEnum("Enum3"));
    }

    [Fact]
    public void MultipleStructs_GetStruct_ReturnsCorrectStruct()
    {
        var module = new IrModule();
        var struct1 = new IrStructType("Struct1", new System.Collections.Generic.List<IrStructField>());
        var struct2 = new IrStructType("Struct2", new System.Collections.Generic.List<IrStructField>());
        var struct3 = new IrStructType("Struct3", new System.Collections.Generic.List<IrStructField>());

        module.AddStruct(struct1);
        module.AddStruct(struct2);
        module.AddStruct(struct3);

        Assert.Same(struct1, module.GetStruct("Struct1"));
        Assert.Same(struct2, module.GetStruct("Struct2"));
        Assert.Same(struct3, module.GetStruct("Struct3"));
    }

    [Fact]
    public void MultipleTraits_GetTrait_ReturnsCorrectTrait()
    {
        var module = new IrModule();
        var trait1 = new IrTrait("Trait1", new System.Collections.Generic.List<IrTraitMethod>());
        var trait2 = new IrTrait("Trait2", new System.Collections.Generic.List<IrTraitMethod>());
        var trait3 = new IrTrait("Trait3", new System.Collections.Generic.List<IrTraitMethod>());

        module.AddTrait(trait1);
        module.AddTrait(trait2);
        module.AddTrait(trait3);

        Assert.Same(trait1, module.GetTrait("Trait1"));
        Assert.Same(trait2, module.GetTrait("Trait2"));
        Assert.Same(trait3, module.GetTrait("Trait3"));
    }

    [Fact]
    public void AddStruct_MaintainsBothListAndDictionary()
    {
        // Verify that AddStruct updates both the public Structs list
        // and the private _structLookup dictionary
        var module = new IrModule();
        var struct1 = new IrStructType("Struct1", new System.Collections.Generic.List<IrStructField>());
        var struct2 = new IrStructType("Struct2", new System.Collections.Generic.List<IrStructField>());

        module.AddStruct(struct1);
        module.AddStruct(struct2);

        // Verify list contains both
        Assert.Equal(2, module.Structs.Count);
        Assert.Contains(struct1, module.Structs);
        Assert.Contains(struct2, module.Structs);

        // Verify dictionary lookup works
        Assert.Same(struct1, module.GetStruct("Struct1"));
        Assert.Same(struct2, module.GetStruct("Struct2"));
    }

    [Fact]
    public void AddEnum_MaintainsBothListAndDictionary()
    {
        var module = new IrModule();
        var enum1 = new IrEnumType("Enum1", new System.Collections.Generic.List<IrEnumVariant>());
        var enum2 = new IrEnumType("Enum2", new System.Collections.Generic.List<IrEnumVariant>());

        module.AddEnum(enum1);
        module.AddEnum(enum2);

        // Verify list contains both
        Assert.Equal(2, module.Enums.Count);
        Assert.Contains(enum1, module.Enums);
        Assert.Contains(enum2, module.Enums);

        // Verify dictionary lookup works
        Assert.Same(enum1, module.GetEnum("Enum1"));
        Assert.Same(enum2, module.GetEnum("Enum2"));
    }

    [Fact]
    public void AddTrait_MaintainsBothListAndDictionary()
    {
        var module = new IrModule();
        var trait1 = new IrTrait("Trait1", new System.Collections.Generic.List<IrTraitMethod>());
        var trait2 = new IrTrait("Trait2", new System.Collections.Generic.List<IrTraitMethod>());

        module.AddTrait(trait1);
        module.AddTrait(trait2);

        // Verify list contains both
        Assert.Equal(2, module.Traits.Count);
        Assert.Contains(trait1, module.Traits);
        Assert.Contains(trait2, module.Traits);

        // Verify dictionary lookup works
        Assert.Same(trait1, module.GetTrait("Trait1"));
        Assert.Same(trait2, module.GetTrait("Trait2"));
    }

    [Fact]
    public void AddTraitImpl_MaintainsBothListAndDictionary()
    {
        var module = new IrModule();
        var impl1 = new IrTraitImpl("Display", new System.Collections.Generic.List<IrType>(), "i32", new IrIntType(32, true));
        var impl2 = new IrTraitImpl("Display", new System.Collections.Generic.List<IrType>(), "Str", new IrIntType(32, true));

        module.AddTraitImpl(impl1);
        module.AddTraitImpl(impl2);

        // Verify list contains both
        Assert.Equal(2, module.TraitImpls.Count);
        Assert.Contains(impl1, module.TraitImpls);
        Assert.Contains(impl2, module.TraitImpls);

        // Verify dictionary lookup works
        Assert.Same(impl1, module.GetTraitImpl("Display", "i32"));
        Assert.Same(impl2, module.GetTraitImpl("Display", "Str"));
    }

    [Fact]
    public void StructWithAttributes_GetStruct_PreservesAttributes()
    {
        var module = new IrModule();
        var attributes = new AttributeCollection();
        attributes.Add(new AttributeInfo("packed", new Novus.Diagnostics.SourceLocation("<test>", 1, 1, 0, "")));

        var structType = new IrStructType(
            "PackedStruct",
            new System.Collections.Generic.List<IrStructField>(),
            null,
            null,
            attributes
        );

        module.AddStruct(structType);
        var retrieved = module.GetStruct("PackedStruct");

        Assert.NotNull(retrieved);
        Assert.NotNull(retrieved.Attributes);
        Assert.True(retrieved.Attributes.Has("packed"));
    }
}

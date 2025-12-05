using Novus.Diagnostics;
using Novus.Frontend;
using Novus.IR;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for the TraitResolver class that handles trait implementation resolution.
/// </summary>
public class TraitResolverTests
{
    private static SourceLocation TestLocation => new SourceLocation("test.novus", 1, 1, 0, "");

    private TraitResolver CreateResolver()
    {
        var symbols = new SymbolTable();
        // Register some test traits with minimal IrTrait objects
        symbols.RegisterTrait("Copy", new IrTrait("Copy", new List<IrTraitMethod>()));
        symbols.RegisterTrait("Clone", new IrTrait("Clone", new List<IrTraitMethod>()));
        symbols.RegisterTrait("From", new IrTrait("From", new List<IrTraitMethod>(), new List<string> { "T" }));
        symbols.RegisterTrait("Iterator", new IrTrait("Iterator", new List<IrTraitMethod>(), new List<string> { "Item" }));
        symbols.RegisterTrait("Add", new IrTrait("Add", new List<IrTraitMethod>()));

        return new TraitResolver(symbols)
        {
            GetTypeCacheKeyFn = GetSimpleTypeCacheKey
        };
    }

    private string GetSimpleTypeCacheKey(IrType type)
    {
        return type switch
        {
            IrStructType st => st.StructName,
            IrEnumType et => et.EnumName,
            IrIntType it => it.IsSigned ? $"i{it.BitWidth}" : $"u{it.BitWidth}",
            IrBoolType => "bool",
            IrPointerType pt => $"*{GetSimpleTypeCacheKey(pt.PointeeType)}",
            _ => type.Name
        };
    }

    #region RegisterTraitImpl Tests

    [Fact]
    public void RegisterTraitImpl_SimpleImpl_CanBeQueried()
    {
        var resolver = CreateResolver();
        var pointType = new IrStructType("Point", new List<IrStructField>
        {
            new IrStructField("x", IrIntType.I32),
            new IrStructField("y", IrIntType.I32)
        });

        resolver.RegisterTraitImpl("Point", "Copy", new List<IrType>(), new List<string>(), TestLocation);

        Assert.True(resolver.TypeImplementsTrait(pointType, "Copy", new List<IrType>()));
    }

    [Fact]
    public void RegisterTraitImpl_WithTypeArgs_CanBeQueried()
    {
        var resolver = CreateResolver();
        var myStringType = new IrStructType("MyString", new List<IrStructField>());

        // impl From<*u8> for MyString
        resolver.RegisterTraitImpl(
            "MyString",
            "From",
            new List<IrType> { new IrPointerType(IrIntType.U8) },
            new List<string>(),
            TestLocation
        );

        Assert.True(resolver.TypeImplementsTrait(
            myStringType,
            "From",
            new List<IrType> { new IrPointerType(IrIntType.U8) }
        ));

        // Different type arg should not match
        Assert.False(resolver.TypeImplementsTrait(
            myStringType,
            "From",
            new List<IrType> { IrIntType.I32 }
        ));
    }

    [Fact]
    public void RegisterTraitImpl_GenericImpl_MatchesAnyTypeArgs()
    {
        var resolver = CreateResolver();
        var optionType = new IrStructType("Option", new List<IrStructField>());

        // impl<T> Clone for Option<T>
        resolver.RegisterTraitImpl(
            "Option",
            "Clone",
            new List<IrType>(),
            new List<string> { "T" }, // Generic impl
            TestLocation
        );

        Assert.True(resolver.TypeImplementsTrait(optionType, "Clone", new List<IrType>()));
    }

    #endregion

    #region TypeImplementsTrait Tests

    [Fact]
    public void TypeImplementsTrait_NoImpl_ReturnsFalse()
    {
        var resolver = CreateResolver();
        var pointType = new IrStructType("Point", new List<IrStructField>());

        Assert.False(resolver.TypeImplementsTrait(pointType, "Copy", new List<IrType>()));
    }

    [Fact]
    public void TypeImplementsTrait_UnknownTrait_ReturnsFalse()
    {
        var resolver = CreateResolver();
        var pointType = new IrStructType("Point", new List<IrStructField>());

        Assert.False(resolver.TypeImplementsTrait(pointType, "NonExistentTrait", new List<IrType>()));
    }

    [Fact]
    public void TypeImplementsTrait_DifferentType_ReturnsFalse()
    {
        var resolver = CreateResolver();

        resolver.RegisterTraitImpl("Point", "Copy", new List<IrType>(), new List<string>(), TestLocation);

        var rectType = new IrStructType("Rect", new List<IrStructField>());
        Assert.False(resolver.TypeImplementsTrait(rectType, "Copy", new List<IrType>()));
    }

    #endregion

    #region TypeSatisfiesBounds Tests

    [Fact]
    public void TypeSatisfiesBounds_AllBoundsSatisfied_ReturnsTrue()
    {
        var resolver = CreateResolver();
        var pointType = new IrStructType("Point", new List<IrStructField>());

        resolver.RegisterTraitImpl("Point", "Copy", new List<IrType>(), new List<string>(), TestLocation);
        resolver.RegisterTraitImpl("Point", "Clone", new List<IrType>(), new List<string>(), TestLocation);

        var bounds = new List<IrTraitBound>
        {
            new IrTraitBound("Copy", new List<IrType>()),
            new IrTraitBound("Clone", new List<IrType>())
        };

        Assert.True(resolver.TypeSatisfiesBounds(pointType, bounds, null, null));
    }

    [Fact]
    public void TypeSatisfiesBounds_MissingBound_ReturnsFalse()
    {
        var resolver = CreateResolver();
        var pointType = new IrStructType("Point", new List<IrStructField>());

        resolver.RegisterTraitImpl("Point", "Copy", new List<IrType>(), new List<string>(), TestLocation);
        // Not registering Clone

        var bounds = new List<IrTraitBound>
        {
            new IrTraitBound("Copy", new List<IrType>()),
            new IrTraitBound("Clone", new List<IrType>())
        };

        Assert.False(resolver.TypeSatisfiesBounds(pointType, bounds, null, null));
    }

    [Fact]
    public void TypeSatisfiesBounds_EmptyBounds_ReturnsTrue()
    {
        var resolver = CreateResolver();
        var pointType = new IrStructType("Point", new List<IrStructField>());

        Assert.True(resolver.TypeSatisfiesBounds(pointType, new List<IrTraitBound>(), null, null));
    }

    [Fact]
    public void TypeSatisfiesBounds_ReportsError()
    {
        var resolver = CreateResolver();
        var pointType = new IrStructType("Point", new List<IrStructField>());
        var diagnostics = new DiagnosticBag();

        var bounds = new List<IrTraitBound>
        {
            new IrTraitBound("Copy", new List<IrType>())
        };

        resolver.TypeSatisfiesBounds(pointType, bounds, diagnostics, TestLocation);

        Assert.True(diagnostics.HasErrors);
        Assert.Contains(diagnostics.Diagnostics, d => d.Code == "E0277");
    }

    #endregion

    #region ValidateGenericConstraints Tests

    [Fact]
    public void ValidateGenericConstraints_NullWhereClause_ReturnsTrue()
    {
        var resolver = CreateResolver();

        Assert.True(resolver.ValidateGenericConstraints(
            null,
            new List<string> { "T" },
            new List<IrType> { IrIntType.I32 },
            null,
            TestLocation
        ));
    }

    [Fact]
    public void ValidateGenericConstraints_EmptyConstraints_ReturnsTrue()
    {
        var resolver = CreateResolver();
        var whereClause = new IrWhereClause(new List<IrTypeConstraint>());

        Assert.True(resolver.ValidateGenericConstraints(
            whereClause,
            new List<string> { "T" },
            new List<IrType> { IrIntType.I32 },
            null,
            TestLocation
        ));
    }

    [Fact]
    public void ValidateGenericConstraints_SatisfiedConstraint_ReturnsTrue()
    {
        var resolver = CreateResolver();
        var pointType = new IrStructType("Point", new List<IrStructField>());

        resolver.RegisterTraitImpl("Point", "Copy", new List<IrType>(), new List<string>(), TestLocation);

        var whereClause = new IrWhereClause(new List<IrTypeConstraint>
        {
            new IrTypeConstraint("T", new List<IrTraitBound>
            {
                new IrTraitBound("Copy", new List<IrType>())
            })
        });

        Assert.True(resolver.ValidateGenericConstraints(
            whereClause,
            new List<string> { "T" },
            new List<IrType> { pointType },
            null,
            TestLocation
        ));
    }

    [Fact]
    public void ValidateGenericConstraints_UnsatisfiedConstraint_ReturnsFalse()
    {
        var resolver = CreateResolver();
        var pointType = new IrStructType("Point", new List<IrStructField>());
        // Not registering Copy impl

        var whereClause = new IrWhereClause(new List<IrTypeConstraint>
        {
            new IrTypeConstraint("T", new List<IrTraitBound>
            {
                new IrTraitBound("Copy", new List<IrType>())
            })
        });

        Assert.False(resolver.ValidateGenericConstraints(
            whereClause,
            new List<string> { "T" },
            new List<IrType> { pointType },
            null,
            TestLocation
        ));
    }

    #endregion

    #region CanConvertViaFromTrait Tests

    [Fact]
    public void CanConvertViaFromTrait_ImplExists_ReturnsTrue()
    {
        var resolver = CreateResolver();

        // impl From<i32> for MyType
        resolver.RegisterTraitImpl(
            "MyType",
            "From",
            new List<IrType> { IrIntType.I32 },
            new List<string>(),
            TestLocation
        );

        var sourceType = IrIntType.I32;
        var targetType = new IrStructType("MyType", new List<IrStructField>());

        Assert.True(resolver.CanConvertViaFromTrait(sourceType, targetType));
    }

    [Fact]
    public void CanConvertViaFromTrait_NoImpl_ReturnsFalse()
    {
        var resolver = CreateResolver();

        var sourceType = IrIntType.I32;
        var targetType = new IrStructType("MyType", new List<IrStructField>());

        Assert.False(resolver.CanConvertViaFromTrait(sourceType, targetType));
    }

    [Fact]
    public void CanConvertViaFromTrait_WrongSourceType_ReturnsFalse()
    {
        var resolver = CreateResolver();

        // impl From<i32> for MyType
        resolver.RegisterTraitImpl(
            "MyType",
            "From",
            new List<IrType> { IrIntType.I32 },
            new List<string>(),
            TestLocation
        );

        var sourceType = IrIntType.U64; // Different from impl
        var targetType = new IrStructType("MyType", new List<IrStructField>());

        Assert.False(resolver.CanConvertViaFromTrait(sourceType, targetType));
    }

    #endregion

    #region TryGetIteratorItemType Tests

    [Fact]
    public void TryGetIteratorItemType_ImplExists_ReturnsItemType()
    {
        var resolver = CreateResolver();

        // impl Iterator<Item=i32> for MyIterator
        resolver.RegisterTraitImpl(
            "MyIterator",
            "Iterator",
            new List<IrType> { IrIntType.I32 },
            new List<string>(),
            TestLocation
        );

        var iteratorType = new IrStructType("MyIterator", new List<IrStructField>());

        Assert.True(resolver.TryGetIteratorItemType(iteratorType, out var itemType));
        Assert.NotNull(itemType);
        Assert.IsType<IrIntType>(itemType);
        Assert.Equal(32, ((IrIntType)itemType!).BitWidth);
    }

    [Fact]
    public void TryGetIteratorItemType_NoImpl_ReturnsFalse()
    {
        var resolver = CreateResolver();
        var someType = new IrStructType("SomeType", new List<IrStructField>());

        Assert.False(resolver.TryGetIteratorItemType(someType, out var itemType));
        Assert.Null(itemType);
    }

    #endregion

    #region HasTraitImpl Tests

    [Fact]
    public void HasTraitImpl_ImplExists_ReturnsTrue()
    {
        var resolver = CreateResolver();
        resolver.RegisterTraitImpl("Point", "Copy", new List<IrType>(), new List<string>(), TestLocation);

        Assert.True(resolver.HasTraitImpl("Point", "Copy"));
    }

    [Fact]
    public void HasTraitImpl_NoImpl_ReturnsFalse()
    {
        var resolver = CreateResolver();

        Assert.False(resolver.HasTraitImpl("Point", "Copy"));
    }

    [Fact]
    public void HasTraitImpl_DifferentTrait_ReturnsFalse()
    {
        var resolver = CreateResolver();
        resolver.RegisterTraitImpl("Point", "Copy", new List<IrType>(), new List<string>(), TestLocation);

        Assert.False(resolver.HasTraitImpl("Point", "Clone"));
    }

    #endregion

    #region GetAllImpls Tests

    [Fact]
    public void GetAllImpls_ReturnsAllRegisteredImpls()
    {
        var resolver = CreateResolver();

        resolver.RegisterTraitImpl("Point", "Copy", new List<IrType>(), new List<string>(), TestLocation);
        resolver.RegisterTraitImpl("Point", "Clone", new List<IrType>(), new List<string>(), TestLocation);
        resolver.RegisterTraitImpl("Rect", "Copy", new List<IrType>(), new List<string>(), TestLocation);

        var impls = resolver.GetAllImpls();

        Assert.Equal(3, impls.Count);
    }

    [Fact]
    public void GetAllImpls_EmptyResolver_ReturnsEmpty()
    {
        var resolver = CreateResolver();

        Assert.Empty(resolver.GetAllImpls());
    }

    #endregion

    #region Clear Tests

    [Fact]
    public void Clear_RemovesAllImpls()
    {
        var resolver = CreateResolver();

        resolver.RegisterTraitImpl("Point", "Copy", new List<IrType>(), new List<string>(), TestLocation);
        resolver.RegisterTraitImpl("Point", "Clone", new List<IrType>(), new List<string>(), TestLocation);

        Assert.Equal(2, resolver.GetAllImpls().Count);

        resolver.Clear();

        Assert.Empty(resolver.GetAllImpls());
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void TypeImplementsTrait_EnumType_WorksCorrectly()
    {
        var resolver = CreateResolver();
        var colorType = new IrEnumType("Color", new List<IrEnumVariant>
        {
            new IrEnumVariant("Red", 0),
            new IrEnumVariant("Green", 1),
            new IrEnumVariant("Blue", 2)
        }, null);

        resolver.RegisterTraitImpl("Color", "Copy", new List<IrType>(), new List<string>(), TestLocation);

        Assert.True(resolver.TypeImplementsTrait(colorType, "Copy", new List<IrType>()));
    }

    [Fact]
    public void TypeImplementsTrait_PointerType_ExtractsBaseTypeName()
    {
        var resolver = CreateResolver();

        resolver.RegisterTraitImpl("Point", "Copy", new List<IrType>(), new List<string>(), TestLocation);

        // Should extract "Point" from *Point
        var ptrType = new IrPointerType(new IrStructType("Point", new List<IrStructField>()));

        Assert.True(resolver.TypeImplementsTrait(ptrType, "Copy", new List<IrType>()));
    }

    [Fact]
    public void TypeImplementsTrait_PrimitiveType_WorksCorrectly()
    {
        var resolver = CreateResolver();

        resolver.RegisterTraitImpl("i32", "Add", new List<IrType>(), new List<string>(), TestLocation);

        Assert.True(resolver.TypeImplementsTrait(IrIntType.I32, "Add", new List<IrType>()));
    }

    #endregion
}

using Novus.IR;
using Xunit;

namespace Novus.Tests;

public class TypeInternerTests
{
    [Fact]
    public void GetPointerType_SamePointeeType_ReturnsSameInstance()
    {
        var interner = new TypeInterner();

        var ptr1 = interner.GetPointerType(IrIntType.I32);
        var ptr2 = interner.GetPointerType(IrIntType.I32);

        Assert.Same(ptr1, ptr2);
    }

    [Fact]
    public void GetPointerType_DifferentPointeeType_ReturnsDifferentInstances()
    {
        var interner = new TypeInterner();

        var ptr1 = interner.GetPointerType(IrIntType.I32);
        var ptr2 = interner.GetPointerType(IrIntType.I16);

        Assert.NotSame(ptr1, ptr2);
    }

    [Fact]
    public void GetArrayType_SameElementTypeAndLength_ReturnsSameInstance()
    {
        var interner = new TypeInterner();

        var arr1 = interner.GetArrayType(IrIntType.I32, 10);
        var arr2 = interner.GetArrayType(IrIntType.I32, 10);

        Assert.Same(arr1, arr2);
    }

    [Fact]
    public void GetArrayType_DifferentLength_ReturnsDifferentInstances()
    {
        var interner = new TypeInterner();

        var arr1 = interner.GetArrayType(IrIntType.I32, 10);
        var arr2 = interner.GetArrayType(IrIntType.I32, 20);

        Assert.NotSame(arr1, arr2);
    }

    [Fact]
    public void GetArrayType_DifferentElementType_ReturnsDifferentInstances()
    {
        var interner = new TypeInterner();

        var arr1 = interner.GetArrayType(IrIntType.I32, 10);
        var arr2 = interner.GetArrayType(IrIntType.I16, 10);

        Assert.NotSame(arr1, arr2);
    }

    [Fact]
    public void GetReferenceType_SamePointeeType_ReturnsSameInstance()
    {
        var interner = new TypeInterner();

        var ref1 = interner.GetReferenceType(IrIntType.I32);
        var ref2 = interner.GetReferenceType(IrIntType.I32);

        Assert.Same(ref1, ref2);
    }

    [Fact]
    public void GetMutReferenceType_SamePointeeType_ReturnsSameInstance()
    {
        var interner = new TypeInterner();

        var ref1 = interner.GetMutReferenceType(IrIntType.I32);
        var ref2 = interner.GetMutReferenceType(IrIntType.I32);

        Assert.Same(ref1, ref2);
    }

    [Fact]
    public void GetReferenceType_DifferentMutability_ReturnsDifferentInstances()
    {
        var interner = new TypeInterner();

        var immutRef = interner.GetReferenceType(IrIntType.I32);
        var mutRef = interner.GetMutReferenceType(IrIntType.I32);

        Assert.NotSame(immutRef, mutRef);
    }

    [Fact]
    public void GetFunctionPointerType_SameSignature_ReturnsSameInstance()
    {
        var interner = new TypeInterner();

        var params1 = new List<IrType> { IrIntType.I32, IrIntType.I16 };
        var params2 = new List<IrType> { IrIntType.I32, IrIntType.I16 };

        var fn1 = interner.GetFunctionPointerType(params1, IrVoidType.Instance);
        var fn2 = interner.GetFunctionPointerType(params2, IrVoidType.Instance);

        Assert.Same(fn1, fn2);
    }

    [Fact]
    public void GetFunctionPointerType_DifferentReturnType_ReturnsDifferentInstances()
    {
        var interner = new TypeInterner();

        var params1 = new List<IrType> { IrIntType.I32 };
        var params2 = new List<IrType> { IrIntType.I32 };

        var fn1 = interner.GetFunctionPointerType(params1, IrVoidType.Instance);
        var fn2 = interner.GetFunctionPointerType(params2, IrIntType.I32);

        Assert.NotSame(fn1, fn2);
    }

    [Fact]
    public void GetFunctionPointerType_DifferentParameterCount_ReturnsDifferentInstances()
    {
        var interner = new TypeInterner();

        var params1 = new List<IrType> { IrIntType.I32 };
        var params2 = new List<IrType> { IrIntType.I32, IrIntType.I16 };

        var fn1 = interner.GetFunctionPointerType(params1, IrVoidType.Instance);
        var fn2 = interner.GetFunctionPointerType(params2, IrVoidType.Instance);

        Assert.NotSame(fn1, fn2);
    }

    [Fact]
    public void RegisterStructType_SameName_ReturnsSameInstance()
    {
        var interner = new TypeInterner();

        var fields1 = new List<IrStructField>
        {
            new IrStructField("x", IrIntType.I32),
            new IrStructField("y", IrIntType.I32)
        };
        var struct1 = new IrStructType("Point", fields1);

        var fields2 = new List<IrStructField>
        {
            new IrStructField("x", IrIntType.I32),
            new IrStructField("y", IrIntType.I32)
        };
        var struct2 = new IrStructType("Point", fields2);

        var registered1 = interner.RegisterStructType(struct1);
        var registered2 = interner.RegisterStructType(struct2);

        // Should return the first registered instance
        Assert.Same(registered1, registered2);
        Assert.Same(registered1, struct1);
    }

    [Fact]
    public void RegisterEnumType_SameName_ReturnsSameInstance()
    {
        var interner = new TypeInterner();

        var variants1 = new List<IrEnumVariant>
        {
            new IrEnumVariant("Some", 0, new List<IrType> { IrIntType.I32 }),
            new IrEnumVariant("None", 1)
        };
        var enum1 = new IrEnumType("Option", variants1);

        var variants2 = new List<IrEnumVariant>
        {
            new IrEnumVariant("Some", 0, new List<IrType> { IrIntType.I32 }),
            new IrEnumVariant("None", 1)
        };
        var enum2 = new IrEnumType("Option", variants2);

        var registered1 = interner.RegisterEnumType(enum1);
        var registered2 = interner.RegisterEnumType(enum2);

        // Should return the first registered instance
        Assert.Same(registered1, registered2);
        Assert.Same(registered1, enum1);
    }

    [Fact]
    public void GetGenericType_SameName_ReturnsSameInstance()
    {
        var interner = new TypeInterner();

        var generic1 = interner.GetGenericType("T");
        var generic2 = interner.GetGenericType("T");

        Assert.Same(generic1, generic2);
    }

    [Fact]
    public void GetGenericType_DifferentName_ReturnsDifferentInstances()
    {
        var interner = new TypeInterner();

        var genericT = interner.GetGenericType("T");
        var genericE = interner.GetGenericType("E");

        Assert.NotSame(genericT, genericE);
    }

    [Fact]
    public void NestedTypes_AreInterned()
    {
        var interner = new TypeInterner();

        // Create *[i32; 10]
        var arr1 = interner.GetArrayType(IrIntType.I32, 10);
        var ptr1 = interner.GetPointerType(arr1);

        // Create *[i32; 10] again
        var arr2 = interner.GetArrayType(IrIntType.I32, 10);
        var ptr2 = interner.GetPointerType(arr2);

        // Both array types should be the same instance
        Assert.Same(arr1, arr2);

        // Both pointer types should be the same instance
        Assert.Same(ptr1, ptr2);
    }

    [Fact]
    public void Clear_RemovesAllCachedTypes()
    {
        var interner = new TypeInterner();

        var ptr1 = interner.GetPointerType(IrIntType.I32);

        interner.Clear();

        var ptr2 = interner.GetPointerType(IrIntType.I32);

        // After clear, should get a different instance
        Assert.NotSame(ptr1, ptr2);
    }

    [Fact]
    public void ComplexNestedType_IsInterned()
    {
        var interner = new TypeInterner();

        // Create fn(&[10:]*i32) -> &mut i16
        var intType = IrIntType.I32;
        var ptrToInt = interner.GetPointerType(intType);
        var arrayOfPtrs = interner.GetArrayType(ptrToInt, 10);
        var refToArray = interner.GetReferenceType(arrayOfPtrs);
        var mutRefToI16 = interner.GetMutReferenceType(IrIntType.I16);
        var fn1 = interner.GetFunctionPointerType(new List<IrType> { refToArray }, mutRefToI16);

        // Create the same type again
        var ptrToInt2 = interner.GetPointerType(intType);
        var arrayOfPtrs2 = interner.GetArrayType(ptrToInt2, 10);
        var refToArray2 = interner.GetReferenceType(arrayOfPtrs2);
        var mutRefToI162 = interner.GetMutReferenceType(IrIntType.I16);
        var fn2 = interner.GetFunctionPointerType(new List<IrType> { refToArray2 }, mutRefToI162);

        // All intermediate types should be the same
        Assert.Same(ptrToInt, ptrToInt2);
        Assert.Same(arrayOfPtrs, arrayOfPtrs2);
        Assert.Same(refToArray, refToArray2);
        Assert.Same(mutRefToI16, mutRefToI162);
        Assert.Same(fn1, fn2);
    }

    [Fact]
    public void GetStructType_UnregisteredName_ReturnsNull()
    {
        var interner = new TypeInterner();

        var result = interner.GetStructType("NonExistent");

        Assert.Null(result);
    }

    [Fact]
    public void GetEnumType_UnregisteredName_ReturnsNull()
    {
        var interner = new TypeInterner();

        var result = interner.GetEnumType("NonExistent");

        Assert.Null(result);
    }
}

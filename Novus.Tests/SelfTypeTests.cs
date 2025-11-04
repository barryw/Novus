using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class SelfTypeTests
{
    private IrModule BuildIr(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();
        var builder = new IrBuilder(skipAutoImports: true);
        return builder.BuildModule(tree);
    }

    // TODO: Re-enable this test once trait syntax is fully implemented
    // [Fact]
    // public void SelfTypeInTraitImpl_ResolvesToImplementingType()
    // {
    //     var code = @"
    // struct DosError {
    //     code: i32
    // }
    //
    // struct NovusError {
    //     kind: i32,
    //     message: i32
    // }
    //
    // trait From<T> {
    //     pub fn convert(err: T) -> Self
    // }
    //
    // impl From<DosError> for NovusError {
    //     pub fn convert(err: DosError) -> Self {
    //         let result: NovusError = NovusError {
    //             kind: 1,
    //             message: err.code
    //         }
    //         return result
    //     }
    // }
    //
    // pub fn main() -> i32 {
    //     let dos_err: DosError = DosError { code: 42 }
    //     let novus_err: NovusError = NovusError::convert(dos_err)
    //     return novus_err.message
    // }
    // ";
    //
    //     var module = BuildIr(code);
    //     Assert.NotNull(module);
    //
    //     // Find the trait impl method
    //     var convertMethod = module.Functions.FirstOrDefault(f => f.Name.Contains("NovusError") && f.Name.Contains("convert"));
    //     Assert.NotNull(convertMethod);
    //
    //     // The return type should be NovusError, not IrSelfType
    //     Assert.IsType<IrStructType>(convertMethod.ReturnType);
    //     var returnStructType = (IrStructType)convertMethod.ReturnType;
    //     Assert.Equal("NovusError", returnStructType.Name);
    // }

    [Fact]
    public void SelfTypeInInherentImpl_ResolvesToImplementingType()
    {
        var code = @"
struct Builder {
    value: i32
}

impl Builder {
    pub fn new() -> Self {
        return Builder { value: 0 }
    }

    pub fn with_value(value: i32) -> Self {
        return Builder { value: value }
    }
}

pub fn main() -> i32 {
    let b1: Builder = Builder::new()
    let b2: Builder = Builder::with_value(42)
    return b2.value
}
";

        var module = BuildIr(code);
        Assert.NotNull(module);

        // Find the impl methods
        var newMethod = module.Functions.FirstOrDefault(f => f.Name == "Builder::new");
        var withValueMethod = module.Functions.FirstOrDefault(f => f.Name == "Builder::with_value");

        Assert.NotNull(newMethod);
        Assert.NotNull(withValueMethod);

        // Both return types should be Builder, not IrSelfType
        Assert.IsType<IrStructType>(newMethod.ReturnType);
        Assert.Equal("Builder", ((IrStructType)newMethod.ReturnType).Name);

        Assert.IsType<IrStructType>(withValueMethod.ReturnType);
        Assert.Equal("Builder", ((IrStructType)withValueMethod.ReturnType).Name);
    }

    // NOTE: Self type validation happens in semantic analysis, not IR building.
    // Using Self outside impl/trait blocks will fail during codegen when trying to resolve the type.
    // See SemanticAnalyzer for proper validation tests.

    [Fact]
    public void SelfTypeInReferenceType_ResolvesCorrectly()
    {
        var code = @"
struct Node {
    value: i32
}

impl Node {
    pub fn create(value: i32) -> Self {
        return Node { value: value }
    }

    pub fn with_ref(next: &Self) -> i32 {
        return 42
    }
}

pub fn main() -> i32 {
    let node: Node = Node::create(42)
    return node.value
}
";

        var module = BuildIr(code);
        Assert.NotNull(module);

        // Find with_ref method
        var withRefMethod = module.Functions.FirstOrDefault(f => f.Name == "Node::with_ref");
        Assert.NotNull(withRefMethod);

        // First parameter should be &Node (reference to Node), not &Self
        Assert.True(withRefMethod.Parameters.Count >= 1);
        var nextParam = withRefMethod.Parameters[0];
        Assert.IsType<IrReferenceType>(nextParam.Type);
        var refType = (IrReferenceType)nextParam.Type;
        Assert.IsType<IrStructType>(refType.PointeeType);
        Assert.Equal("Node", ((IrStructType)refType.PointeeType).Name);
    }

    [Fact]
    public void SelfTypeInGenericImpl_ResolvesToMonomorphizedType()
    {
        var code = @"
struct Box<T> {
    value: T
}

impl<T> Box<T> {
    pub fn new(value: T) -> Self {
        return Box { value: value }
    }

    pub fn get(&self) -> T {
        return self.value
    }
}

pub fn main() -> i32 {
    let b: Box<i32> = Box::new(42)
    return b.get()
}
";

        var module = BuildIr(code);
        Assert.NotNull(module);

        // Find the monomorphized new method for Box<i32>
        var newMethod = module.Functions.FirstOrDefault(f => f.Name.Contains("Box") && f.Name.Contains("new"));
        Assert.NotNull(newMethod);

        // Return type should be the monomorphized Box<i32> struct
        Assert.IsType<IrStructType>(newMethod.ReturnType);
        var boxType = (IrStructType)newMethod.ReturnType;
        Assert.Contains("Box", boxType.Name);

        // The struct should have i32 as its field type
        Assert.Single(boxType.Fields);
        Assert.Equal("i32", boxType.Fields[0].Type.Name);
    }
}

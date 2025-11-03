using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class TraitTests
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

    [Fact]
    public void BuildIr_SimpleTrait_Compiles()
    {
        var source = @"
trait Printable {
    fn print(self) -> i32
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        Assert.Single(module.Traits);
        Assert.Equal("Printable", module.Traits[0].TraitName);
    }

    [Fact]
    public void BuildIr_GenericTrait_Compiles()
    {
        var source = @"
trait Iterator<T> {
    fn next(&mut self) -> T
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        Assert.Single(module.Traits);
        Assert.Equal("Iterator", module.Traits[0].TraitName);
        Assert.Single(module.Traits[0].GenericParameters);
        Assert.Equal("T", module.Traits[0].GenericParameters[0]);
    }

    [Fact]
    public void BuildIr_TraitWithMultipleMethods_Compiles()
    {
        var source = @"
trait Drawable {
    fn draw(self) -> i32
    fn clear(self) -> i32
    fn update(&mut self) -> i32
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        Assert.Single(module.Traits);
        Assert.Equal(3, module.Traits[0].Methods.Count);
    }

    [Fact]
    public void BuildIr_TraitImpl_NonGeneric_Compiles()
    {
        var source = @"
trait Printable {
    fn print(self) -> i32
}
struct Point {
    x: i32,
    y: i32
}
impl Printable for Point {
    fn print(self) -> i32 {
        return self.x + self.y
    }
}
pub fn main() -> i32 {
    let p = Point { x: 10, y: 20 }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        Assert.Single(module.Traits);

        // Check that trait impl method was registered with correct mangled name
        var mangledName = "Point_Printable_print";
        var method = module.Functions.FirstOrDefault(f => f.Name == mangledName);
        Assert.NotNull(method);
    }

    [Fact]
    public void BuildIr_TraitImpl_WithGenericTrait_Compiles()
    {
        var source = @"
trait Iterator<T> {
    fn next(&mut self) -> T
}
struct Counter {
    count: i32
}
impl Iterator<i32> for Counter {
    fn next(&mut self) -> i32 {
        self.count = self.count + 1
        return self.count
    }
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        Assert.Single(module.Traits);

        // Check that trait impl method was registered with type args in mangled name
        var mangledName = "Counter_Iterator_i32_next";
        var method = module.Functions.FirstOrDefault(f => f.Name == mangledName);
        Assert.NotNull(method);
        Assert.Equal("next", method.Name.Split('_').Last());
    }

    [Fact]
    public void BuildIr_TraitImpl_MultipleTypeArgs_Compiles()
    {
        var source = @"
trait Converter<T, U> {
    fn convert(self, value: T) -> U
}
struct IntToString {
    dummy: i32
}
impl Converter<i32, i32> for IntToString {
    fn convert(self, value: i32) -> i32 {
        return value
    }
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        // Check mangling with multiple type args
        var mangledName = "IntToString_Converter_i32_i32_convert";
        var method = module.Functions.FirstOrDefault(f => f.Name == mangledName);
        Assert.NotNull(method);
    }

    [Fact]
    public void BuildIr_TraitImpl_WithMutSelf_Compiles()
    {
        var source = @"
trait Incrementable {
    fn increment(&mut self) -> i32
}
struct Counter {
    value: i32
}
impl Incrementable for Counter {
    fn increment(&mut self) -> i32 {
        self.value = self.value + 1
        return self.value
    }
}
pub fn main() -> i32 {
    let mut c = Counter { value: 0 }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        // Verify method has mutable self parameter
        var mangledName = "Counter_Incrementable_increment";
        var method = module.Functions.FirstOrDefault(f => f.Name == mangledName);
        Assert.NotNull(method);
        Assert.NotEmpty(method.Parameters);
        Assert.Equal("self", method.Parameters[0].Name);
    }

    [Fact]
    public void BuildIr_MultipleTraits_Compiles()
    {
        var source = @"
trait Printable {
    fn print(self) -> i32
}
trait Drawable {
    fn draw(self) -> i32
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
        Assert.Equal(2, module.Traits.Count);
    }

    [Fact]
    public void BuildIr_TraitAndInherentImpl_SameType_Compiles()
    {
        var source = @"
trait Printable {
    fn print(self) -> i32
}
struct Point {
    x: i32
}
impl Point {
    fn new() -> Point {
        return Point { x: 0 }
    }
}
impl Printable for Point {
    fn print(self) -> i32 {
        return self.x
    }
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        // Should have both inherent method and trait method
        var inherentMethod = module.Functions.FirstOrDefault(f => f.Name == "Point::new");
        var traitMethod = module.Functions.FirstOrDefault(f => f.Name == "Point_Printable_print");

        Assert.NotNull(inherentMethod);
        Assert.NotNull(traitMethod);
    }

    [Fact]
    public void BuildIr_TraitWithRefSelf_Compiles()
    {
        var source = @"
trait Getter {
    fn get(&self) -> i32
}
struct Container {
    value: i32
}
impl Getter for Container {
    fn get(&self) -> i32 {
        return self.value
    }
}
pub fn main() -> i32 {
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);

        var mangledName = "Container_Getter_get";
        var method = module.Functions.FirstOrDefault(f => f.Name == mangledName);
        Assert.NotNull(method);
    }
}

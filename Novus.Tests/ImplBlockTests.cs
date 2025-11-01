using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class ImplBlockTests
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
    public void BuildIr_ImplBlock_SingleMethod_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
impl Point {
    fn getX(self) -> i32 {
        return self.x
    }
}
pub fn main() -> i32 {
    let p = Point { x: 10, y: 20 }
    return p.getX()
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ImplBlock_MultipleMethodsSameType_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
impl Point {
    fn getX(self) -> i32 {
        return self.x
    }
    fn getY(self) -> i32 {
        return self.y
    }
    fn sum(self) -> i32 {
        return self.x + self.y
    }
}
pub fn main() -> i32 {
    let p = Point { x: 10, y: 20 }
    return p.getX() + p.getY() + p.sum()
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ImplBlock_MethodChaining_Compiles()
    {
        var source = @"
struct Counter {
    value: i32
}
impl Counter {
    fn increment(mut self) -> Counter {
        self.value = self.value + 1
        return self
    }
    fn getValue(self) -> i32 {
        return self.value
    }
}
pub fn main() -> i32 {
    let c = Counter { value: 0 }
    return c.increment().increment().getValue()
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ImplBlock_MutSelfMethod_Compiles()
    {
        var source = @"
struct Counter {
    count: i32
}
impl Counter {
    fn increment(mut self) {
        self.count = self.count + 1
    }
}
pub fn main() -> i32 {
    var c = Counter { count: 0 }
    c.increment()
    return c.count
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ImplBlock_MethodWithParameters_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
impl Point {
    fn add(self, dx: i32, dy: i32) -> Point {
        return Point { x: self.x + dx, y: self.y + dy }
    }
}
pub fn main() -> i32 {
    let p = Point { x: 10, y: 20 }
    let p2 = p.add(5, 10)
    return p2.x + p2.y
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ImplBlock_StaticMethod_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
impl Point {
    fn new(x: i32, y: i32) -> Point {
        return Point { x: x, y: y }
    }
}
pub fn main() -> i32 {
    let p = Point::new(10, 20)
    return p.x + p.y
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ImplBlock_MixedStaticAndInstanceMethods_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
impl Point {
    fn new(x: i32, y: i32) -> Point {
        return Point { x: x, y: y }
    }
    fn sum(self) -> i32 {
        return self.x + self.y
    }
}
pub fn main() -> i32 {
    let p = Point::new(10, 20)
    return p.sum()
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ImplBlock_PublicMethod_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
impl Point {
    pub fn getX(self) -> i32 {
        return self.x
    }
}
pub fn main() -> i32 {
    let p = Point { x: 10, y: 20 }
    return p.getX()
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ImplBlock_PrivateMethod_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
impl Point {
    fn getX(self) -> i32 {
        return self.x
    }
    pub fn getValue(self) -> i32 {
        return self.getX()
    }
}
pub fn main() -> i32 {
    let p = Point { x: 10, y: 20 }
    return p.getValue()
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ImplBlock_MethodCallingMethod_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
impl Point {
    fn getX(self) -> i32 {
        return self.x
    }
    fn getY(self) -> i32 {
        return self.y
    }
    fn sum(self) -> i32 {
        return self.getX() + self.getY()
    }
}
pub fn main() -> i32 {
    let p = Point { x: 10, y: 20 }
    return p.sum()
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ImplBlock_MethodWithComplexBody_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
impl Point {
    fn distance(self) -> i32 {
        let xx = self.x * self.x
        let yy = self.y * self.y
        return xx + yy
    }
}
pub fn main() -> i32 {
    let p = Point { x: 3, y: 4 }
    return p.distance()
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ImplBlock_MethodWithConditional_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
impl Point {
    fn max(self) -> i32 {
        if self.x > self.y {
            return self.x
        }
        return self.y
    }
}
pub fn main() -> i32 {
    let p = Point { x: 10, y: 20 }
    return p.max()
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ImplBlock_MethodWithLoop_Compiles()
    {
        var source = @"
struct Counter {
    limit: i32
}
impl Counter {
    fn sumToLimit(self) -> i32 {
        var sum = 0
        var i = 0
        while i < self.limit {
            sum = sum + i
            i = i + 1
        }
        return sum
    }
}
pub fn main() -> i32 {
    let c = Counter { limit: 10 }
    return c.sumToLimit()
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ImplBlock_NestedStructMethod_Compiles()
    {
        var source = @"
struct Inner {
    value: i32
}
struct Outer {
    inner: Inner
}
impl Outer {
    fn getValue(self) -> i32 {
        return self.inner.value
    }
}
pub fn main() -> i32 {
    let o = Outer { inner: Inner { value: 42 } }
    return o.getValue()
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ImplBlock_MethodReturningStruct_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
impl Point {
    fn clone(self) -> Point {
        return Point { x: self.x, y: self.y }
    }
}
pub fn main() -> i32 {
    let p1 = Point { x: 10, y: 20 }
    let p2 = p1.clone()
    return p2.x + p2.y
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ImplBlock_MethodWithPointerParam_Compiles()
    {
        var source = @"
struct Node {
    value: i32,
    next: *Node
}
impl Node {
    fn hasNext(self) -> bool {
        return self.next != 0 as *Node
    }
}
pub fn main() -> i32 {
    let n = Node { value: 42, next: 0 as *Node }
    if n.hasNext() {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ImplBlock_MultipleImplBlocksSameStruct_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
impl Point {
    fn getX(self) -> i32 {
        return self.x
    }
}
impl Point {
    fn getY(self) -> i32 {
        return self.y
    }
}
pub fn main() -> i32 {
    let p = Point { x: 10, y: 20 }
    return p.getX() + p.getY()
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ImplBlock_MethodWithEnumReturn_Compiles()
    {
        var source = @"
enum Result {
    Ok,
    Error
}
struct Operation {
    success: bool
}
impl Operation {
    fn getResult(self) -> Result {
        if self.success {
            return Result::Ok
        }
        return Result::Error
    }
}
pub fn main() -> i32 {
    let op = Operation { success: true }
    let r = op.getResult()
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ImplBlock_MethodWithArrayField_Compiles()
    {
        var source = @"
struct Container {
    data: [5]i32,
    count: i32
}
impl Container {
    fn getFirst(self) -> i32 {
        return self.data[0]
    }
}
pub fn main() -> i32 {
    let c = Container {
        data: {1, 2, 3, 4, 5},
        count: 5
    }
    return c.getFirst()
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ImplBlock_ConstructorPattern_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
impl Point {
    fn origin() -> Point {
        return Point { x: 0, y: 0 }
    }
}
pub fn main() -> i32 {
    let p = Point::origin()
    return p.x + p.y
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ImplBlock_BuilderPattern_Compiles()
    {
        var source = @"
struct Config {
    width: i32,
    height: i32
}
impl Config {
    fn new() -> Config {
        return Config { width: 0, height: 0 }
    }
    fn withWidth(mut self, w: i32) -> Config {
        self.width = w
        return self
    }
    fn withHeight(mut self, h: i32) -> Config {
        self.height = h
        return self
    }
}
pub fn main() -> i32 {
    let cfg = Config::new().withWidth(800).withHeight(600)
    return cfg.width + cfg.height
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ImplBlock_SelfInExpression_Compiles()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}
impl Point {
    fn isPositive(self) -> bool {
        return self.x > 0 && self.y > 0
    }
}
pub fn main() -> i32 {
    let p = Point { x: 10, y: 20 }
    if p.isPositive() {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}

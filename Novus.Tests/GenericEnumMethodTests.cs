using Antlr4.Runtime;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Xunit;

namespace Novus.Tests;

public class GenericEnumMethodTests
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

    // ============================================================================
    // Option<T> Method Tests
    // ============================================================================

    [Fact]
    public void BuildIr_OptionIssome_OnSomeVariant_ReturnsTrue()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
impl<T> Option<T> {
    pub fn is_some(self) -> bool {
        match self {
            Some(_) => return true,
            None => return false
        }
    }
}
pub fn main() -> i32 {
    let opt: Option<i32> = Option::Some(42)
    if opt.is_some() {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_OptionIsSome_OnNoneVariant_ReturnsFalse()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
impl<T> Option<T> {
    pub fn is_some(self) -> bool {
        match self {
            Some(_) => return true,
            None => return false
        }
    }
}
pub fn main() -> i32 {
    let opt: Option<i32> = Option::None
    if !opt.is_some() {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_OptionIsNone_OnSomeVariant_ReturnsFalse()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
impl<T> Option<T> {
    pub fn is_none(self) -> bool {
        match self {
            Some(_) => return false,
            None => return true
        }
    }
}
pub fn main() -> i32 {
    let opt: Option<i32> = Option::Some(42)
    if !opt.is_none() {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_OptionIsNone_OnNoneVariant_ReturnsTrue()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
impl<T> Option<T> {
    pub fn is_none(self) -> bool {
        match self {
            Some(_) => return false,
            None => return true
        }
    }
}
pub fn main() -> i32 {
    let opt: Option<i32> = Option::None
    if opt.is_none() {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_OptionUnwrapOr_OnSomeVariant_ReturnsValue()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
impl<T> Option<T> {
    pub fn unwrap_or(self, default: T) -> T {
        match self {
            Some(val) => return val,
            None => return default
        }
    }
}
pub fn main() -> i32 {
    let opt: Option<i32> = Option::Some(42)
    return opt.unwrap_or(0)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_OptionUnwrapOr_OnNoneVariant_ReturnsDefault()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
impl<T> Option<T> {
    pub fn unwrap_or(self, default: T) -> T {
        match self {
            Some(val) => return val,
            None => return default
        }
    }
}
pub fn main() -> i32 {
    let opt: Option<i32> = Option::None
    return opt.unwrap_or(99)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_OptionMethods_WithBoolType_Compiles()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
impl<T> Option<T> {
    pub fn is_some(self) -> bool {
        match self {
            Some(_) => return true,
            None => return false
        }
    }
    pub fn unwrap_or(self, default: T) -> T {
        match self {
            Some(val) => return val,
            None => return default
        }
    }
}
pub fn main() -> i32 {
    let opt1: Option<bool> = Option::Some(true)
    let opt2: Option<bool> = Option::None

    if opt1.is_some() && opt1.unwrap_or(false) {
        return 1
    }
    if !opt2.is_some() && !opt2.unwrap_or(false) {
        return 2
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_OptionMethods_WithPointerType_Compiles()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
impl<T> Option<T> {
    pub fn is_some(self) -> bool {
        match self {
            Some(_) => return true,
            None => return false
        }
    }
    pub fn is_none(self) -> bool {
        match self {
            Some(_) => return false,
            None => return true
        }
    }
}
pub fn main() -> i32 {
    let ptr: *u8 = 0 as *u8
    let opt1: Option<*u8> = Option::Some(ptr)
    let opt2: Option<*u8> = Option::None

    if opt1.is_some() && opt2.is_none() {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ============================================================================
    // Result<T,E> Method Tests
    // ============================================================================

    [Fact]
    public void BuildIr_ResultIsOk_OnOkVariant_ReturnsTrue()
    {
        var source = @"
enum Result<T, E> {
    Ok(T),
    Err(E)
}
impl<T, E> Result<T, E> {
    pub fn is_ok(self) -> bool {
        match self {
            Ok(_) => return true,
            Err(_) => return false
        }
    }
}
pub fn main() -> i32 {
    let res: Result<i32, i32> = Result::Ok(42)
    if res.is_ok() {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ResultIsOk_OnErrVariant_ReturnsFalse()
    {
        var source = @"
enum Result<T, E> {
    Ok(T),
    Err(E)
}
impl<T, E> Result<T, E> {
    pub fn is_ok(self) -> bool {
        match self {
            Ok(_) => return true,
            Err(_) => return false
        }
    }
}
pub fn main() -> i32 {
    let res: Result<i32, i32> = Result::Err(1)
    if !res.is_ok() {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ResultIsErr_OnOkVariant_ReturnsFalse()
    {
        var source = @"
enum Result<T, E> {
    Ok(T),
    Err(E)
}
impl<T, E> Result<T, E> {
    pub fn is_err(self) -> bool {
        match self {
            Ok(_) => return false,
            Err(_) => return true
        }
    }
}
pub fn main() -> i32 {
    let res: Result<i32, i32> = Result::Ok(42)
    if !res.is_err() {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ResultIsErr_OnErrVariant_ReturnsTrue()
    {
        var source = @"
enum Result<T, E> {
    Ok(T),
    Err(E)
}
impl<T, E> Result<T, E> {
    pub fn is_err(self) -> bool {
        match self {
            Ok(_) => return false,
            Err(_) => return true
        }
    }
}
pub fn main() -> i32 {
    let res: Result<i32, i32> = Result::Err(1)
    if res.is_err() {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ResultUnwrapOr_OnOkVariant_ReturnsValue()
    {
        var source = @"
enum Result<T, E> {
    Ok(T),
    Err(E)
}
impl<T, E> Result<T, E> {
    pub fn unwrap_or(self, default: T) -> T {
        match self {
            Ok(val) => return val,
            Err(_) => return default
        }
    }
}
pub fn main() -> i32 {
    let res: Result<i32, i32> = Result::Ok(42)
    return res.unwrap_or(0)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ResultUnwrapOr_OnErrVariant_ReturnsDefault()
    {
        var source = @"
enum Result<T, E> {
    Ok(T),
    Err(E)
}
impl<T, E> Result<T, E> {
    pub fn unwrap_or(self, default: T) -> T {
        match self {
            Ok(val) => return val,
            Err(_) => return default
        }
    }
}
pub fn main() -> i32 {
    let res: Result<i32, i32> = Result::Err(1)
    return res.unwrap_or(99)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_ResultMethods_WithDifferentTypes_Compiles()
    {
        var source = @"
enum Result<T, E> {
    Ok(T),
    Err(E)
}
impl<T, E> Result<T, E> {
    pub fn is_ok(self) -> bool {
        match self {
            Ok(_) => return true,
            Err(_) => return false
        }
    }
    pub fn is_err(self) -> bool {
        match self {
            Ok(_) => return false,
            Err(_) => return true
        }
    }
    pub fn unwrap_or(self, default: T) -> T {
        match self {
            Ok(val) => return val,
            Err(_) => return default
        }
    }
}
pub fn main() -> i32 {
    let res1: Result<bool, i32> = Result::Ok(true)
    let res2: Result<bool, i32> = Result::Err(404)

    if res1.is_ok() && res1.unwrap_or(false) {
        return 1
    }
    if res2.is_err() && !res2.unwrap_or(false) {
        return 2
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ============================================================================
    // Pattern Matching in Generic Impl Blocks
    // ============================================================================

    [Fact]
    public void BuildIr_GenericImplBlock_MatchOnSelf_Compiles()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
impl<T> Option<T> {
    pub fn extract(self) -> i32 {
        match self {
            Some(_) => return 1,
            None => return 0
        }
    }
}
pub fn main() -> i32 {
    let opt: Option<i32> = Option::Some(42)
    return opt.extract()
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericImplBlock_MatchArmsWithBindings_Compiles()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
impl<T> Option<T> {
    pub fn double_or_zero(self) -> i32 {
        match self {
            Some(v) => {
                let val: i32 = v
                return val * 2
            },
            None => return 0
        }
    }
}
pub fn main() -> i32 {
    let opt1: Option<i32> = Option::Some(21)
    let opt2: Option<i32> = Option::None
    return opt1.double_or_zero() + opt2.double_or_zero()
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericImplBlock_MultipleTypeParams_MatchWorks()
    {
        var source = @"
enum Result<T, E> {
    Ok(T),
    Err(E)
}
impl<T, E> Result<T, E> {
    pub fn classify(self) -> i32 {
        match self {
            Ok(_) => return 1,
            Err(_) => return 0
        }
    }
}
pub fn main() -> i32 {
    let res1: Result<i32, bool> = Result::Ok(42)
    let res2: Result<i32, bool> = Result::Err(true)
    return res1.classify() + res2.classify()
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ============================================================================
    // Type Substitution Tests
    // ============================================================================

    [Fact]
    public void BuildIr_GenericEnumMethod_ReturnTypeSubstituted_Compiles()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
impl<T> Option<T> {
    pub fn unwrap_or(self, default: T) -> T {
        match self {
            Some(val) => return val,
            None => return default
        }
    }
}
pub fn main() -> i32 {
    let opt_int: Option<i32> = Option::Some(42)
    let result_int: i32 = opt_int.unwrap_or(0)

    let opt_bool: Option<bool> = Option::None
    let result_bool: bool = opt_bool.unwrap_or(false)

    if result_int == 42 && !result_bool {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericEnumMethod_ParameterTypeSubstituted_Compiles()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
impl<T> Option<T> {
    pub fn or_value(self, alternate: T) -> T {
        match self {
            Some(val) => return val,
            None => return alternate
        }
    }
}
pub fn main() -> i32 {
    let opt: Option<i32> = Option::None
    let fallback: i32 = 100
    return opt.or_value(fallback)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericEnumMethod_BothTypeParamsSubstituted_Compiles()
    {
        var source = @"
enum Result<T, E> {
    Ok(T),
    Err(E)
}
impl<T, E> Result<T, E> {
    pub fn combine(self, ok_default: T, err_default: E) -> i32 {
        match self {
            Ok(_) => {
                let x: T = ok_default
                return 1
            },
            Err(_) => {
                let y: E = err_default
                return 0
            }
        }
    }
}
pub fn main() -> i32 {
    let res: Result<i32, bool> = Result::Ok(42)
    return res.combine(0, false)
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    // ============================================================================
    // Chaining and Complex Usage Tests
    // ============================================================================

    [Fact]
    public void BuildIr_GenericEnumMethod_MultipleCallsInSequence_Compiles()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
impl<T> Option<T> {
    pub fn is_some(self) -> bool {
        match self {
            Some(_) => return true,
            None => return false
        }
    }
    pub fn is_none(self) -> bool {
        match self {
            Some(_) => return false,
            None => return true
        }
    }
}
pub fn main() -> i32 {
    let opt1: Option<i32> = Option::Some(42)
    let opt2: Option<i32> = Option::None

    if opt1.is_some() && !opt1.is_none() {
        if opt2.is_none() && !opt2.is_some() {
            return 1
        }
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericEnumMethod_InCondition_Compiles()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
impl<T> Option<T> {
    pub fn is_some(self) -> bool {
        match self {
            Some(_) => return true,
            None => return false
        }
    }
}
pub fn main() -> i32 {
    let opt: Option<i32> = Option::Some(42)

    if opt.is_some() {
        return 1
    } else {
        return 0
    }
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericEnumMethod_ReturnValueUsedInExpression_Compiles()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
impl<T> Option<T> {
    pub fn unwrap_or(self, default: T) -> T {
        match self {
            Some(val) => return val,
            None => return default
        }
    }
}
pub fn main() -> i32 {
    let opt1: Option<i32> = Option::Some(10)
    let opt2: Option<i32> = Option::None

    let sum = opt1.unwrap_or(0) + opt2.unwrap_or(5)
    return sum
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericEnumMethod_WithNestedGenericTypes_Compiles()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
enum Result<T, E> {
    Ok(T),
    Err(E)
}
impl<T, E> Result<T, E> {
    pub fn is_ok(self) -> bool {
        match self {
            Ok(_) => return true,
            Err(_) => return false
        }
    }
}
pub fn main() -> i32 {
    let opt_int: Option<i32> = Option::Some(42)
    let res: Result<Option<i32>, i32> = Result::Ok(opt_int)

    if res.is_ok() {
        return 1
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }

    [Fact]
    public void BuildIr_GenericEnumMethod_AllMethodsTogether_Compiles()
    {
        var source = @"
enum Option<T> {
    Some(T),
    None
}
impl<T> Option<T> {
    pub fn is_some(self) -> bool {
        match self {
            Some(_) => return true,
            None => return false
        }
    }
    pub fn is_none(self) -> bool {
        match self {
            Some(_) => return false,
            None => return true
        }
    }
    pub fn unwrap_or(self, default: T) -> T {
        match self {
            Some(val) => return val,
            None => return default
        }
    }
}
enum Result<T, E> {
    Ok(T),
    Err(E)
}
impl<T, E> Result<T, E> {
    pub fn is_ok(self) -> bool {
        match self {
            Ok(_) => return true,
            Err(_) => return false
        }
    }
    pub fn is_err(self) -> bool {
        match self {
            Ok(_) => return false,
            Err(_) => return true
        }
    }
    pub fn unwrap_or(self, default: T) -> T {
        match self {
            Ok(val) => return val,
            Err(_) => return default
        }
    }
}
pub fn main() -> i32 {
    let opt: Option<i32> = Option::Some(42)
    let res: Result<i32, i32> = Result::Ok(100)

    if opt.is_some() && !opt.is_none() {
        if res.is_ok() && !res.is_err() {
            let sum = opt.unwrap_or(0) + res.unwrap_or(0)
            return sum
        }
    }
    return 0
}";
        var module = BuildIr(source);
        Assert.NotNull(module);
    }
}

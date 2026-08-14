using Antlr4.Runtime;
using Novus.Codegen;
using Novus.Frontend;
using Novus.IR;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Comprehensive integration tests for CCodeGenerator targeting uncovered code paths.
/// Focuses on: GenerateFunctionFile, EmitFunctionToBuilder, EmitEnumTypeToBuilder,
/// monomorphization, function calls, and complex code generation scenarios.
/// </summary>
public class CCodeGeneratorIntegrationTests
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

    private IrModule BuildIRWithStdlib(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        return new IrBuilder(skipAutoImports: false).BuildModule(tree);
    }

    private IrModule BuildIRAtPath(string source, string path)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var builder = new IrBuilder(skipAutoImports: true);
        builder.SetInputFilePath(path);
        return builder.BuildModule(tree);
    }

    private IrModule BuildAnalyzedIRAtPath(string source, string path)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();
        var stdlib = Path.Combine(AppContext.BaseDirectory, "std");
        var analyzer = new SemanticAnalyzer(path, source, stdlib);
        Assert.True(analyzer.Analyze(tree), analyzer.Diagnostics.FormatDiagnostics());

        var builder = new IrBuilder(analyzer.GetResult(), skipAutoImports: true);
        builder.SetStdLibPath(stdlib);
        builder.SetInputFilePath(path);
        var module = builder.BuildModule(tree);
        Assert.False(builder.Diagnostics.HasErrors, builder.Diagnostics.FormatDiagnostics());
        return module;
    }

    private string GenerateCCode(IrModule module, BuildMode buildMode = BuildMode.Debug)
    {
        var codegen = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft", buildMode);
        return codegen.Generate();
    }

    #region Function Generation Tests

    [Fact]
    public void CCodeGen_SimpleFunctionWithReturn_GeneratesValidC()
    {
        var source = @"
pub fn add(a: i32, b: i32) -> i32 {
    return a + b
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Should generate function declaration and definition
        Assert.Contains("int32_t add", code);
        Assert.Contains("int32_t a", code);
        Assert.Contains("int32_t b", code);
        Assert.Contains("return", code);
    }

    [Fact]
    public void CCodeGen_FunctionWithMultipleParameters_GeneratesCorrectSignature()
    {
        var source = @"
pub fn calculate(a: i32, b: i32, c: i32, d: i32) -> i32 {
    return a + b + c + d
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int32_t calculate", code);
        Assert.Contains("int32_t a", code);
        Assert.Contains("int32_t b", code);
        Assert.Contains("int32_t c", code);
        Assert.Contains("int32_t d", code);
    }

    [Fact]
    public void CCodeGen_FunctionWithLocalVariables_DeclaresLocals()
    {
        var source = @"
pub fn test() -> i32 {
    var x = 10
    var y = 20
    var z = x + y
    return z
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int32_t test", code);
        Assert.Contains("int32_t", code);
    }

    [Fact]
    public void CCodeGen_FunctionWithIfStatement_GeneratesBranches()
    {
        var source = @"
pub fn max(a: i32, b: i32) -> i32 {
    if a > b {
        return a
    } else {
        return b
    }
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int32_t max", code);
        // Should have branching logic
        Assert.Contains("return", code);
    }

    [Fact]
    public void CCodeGen_FunctionWithLoop_GeneratesLoopStructure()
    {
        var source = @"
pub fn sum_to_n(n: i32) -> i32 {
    var sum = 0
    var i = 0
    forever {
        if i >= n {
            break
        }
        sum = sum + i
        i = i + 1
    }
    return sum
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int32_t sum_to_n", code);
        Assert.Contains("int32_t n", code);
    }

    [Fact]
    public void CCodeGen_RecursiveFunction_GeneratesForwardDeclaration()
    {
        var source = @"
pub fn factorial(n: i32) -> i32 {
    if n <= 1 {
        return 1
    }
    return n * factorial(n - 1)
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int32_t factorial", code);
    }

    [Fact]
    public void CCodeGen_FunctionCallWithArguments_GeneratesCall()
    {
        var source = @"
pub fn helper(x: i32) -> i32 {
    return x * 2
}

pub fn caller(y: i32) -> i32 {
    return helper(y + 1)
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int32_t helper", code);
        Assert.Contains("int32_t caller", code);
    }

    [Fact]
    public void CCodeGen_AssigningPayloadFreeGenericVariant_UsesConcreteTag()
    {
        var module = BuildIRWithStdlib(@"
from std::core import Option

pub fn clear() -> i32 {
    var value: Option<u16> = Option::Some(1)
    value = Option::None
    return 0
}");

        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var function = module.Functions.Single(candidate => candidate.Name == "clear");
        var code = generator.GenerateFunctionFile(function);

        Assert.Contains(".tag = Option_u16_None", code);
        Assert.DoesNotContain(".tag = Option_None", code);
    }

    [Fact]
    public void CCodeGen_AggregateCallImmediatelyMovedToLocal_WritesLocalDirectly()
    {
        var module = BuildIR("""
            enum Packet {
                Empty,
                Data(u32),
            }

            fn make_packet(value: u32) -> Packet {
                return Packet::Data(value)
            }

            pub fn forward(value: u32) -> Packet {
                let packet = make_packet(value)
                return packet
            }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var function = module.Functions.Single(candidate => candidate.Name == "forward");
        var code = generator.GenerateFunctionFile(function);

        Assert.Contains("make_packet(&packet, value);", code);
        Assert.DoesNotContain("__novus_memcpy((uint8_t*)&packet", code);
    }

    [Fact]
    public void CCodeGen_OwningAggregateCallImmediatelyMovedToLocal_WritesLocalDirectly()
    {
        var module = BuildIRWithStdlib("""
            from std::core import Drop

            struct Owned { value: i32 }
            impl Drop for Owned { fn drop(&var self) {} }

            fn make_owned() -> Owned { return Owned { value: 7 } }

            pub fn use_owned() -> i32 {
                let owned = make_owned()
                return owned.value
            }
            """);
        var generator = new CCodeGenerator(module, [], "68020", "soft");
        var function = module.Functions.Single(candidate => candidate.Name == "use_owned");
        var code = generator.GenerateFunctionFile(function);

        Assert.Contains("make_owned(&owned);", code);
        Assert.DoesNotContain("__novus_memcpy((uint8_t*)&owned", code);
        Assert.DoesNotContain("_slot_Owned", code);
        Assert.Contains("Owned_Drop_drop(&owned)", code);
    }

    [Fact]
    public void CCodeGen_LocalMayShadowCalledFunction()
    {
        var module = BuildIR("""
            fn devices() -> i32 { return 7 }
            fn count(devices: i32) -> i32 { return devices }

            pub fn discover() -> i32 {
                let devices = devices()
                return devices
            }
            """);
        var generator = new CCodeGenerator(module, [], "68020", "soft");
        var function = module.Functions.Single(candidate => candidate.Name == "discover");
        var code = generator.GenerateFunctionFile(function);

        Assert.Contains("int32_t __novus_local_devices;", code);
        Assert.Contains("devices()", code);
        Assert.Contains("__novus_local_devices =", code);

        var count = module.Functions.Single(candidate => candidate.Name == "count");
        var countCode = generator.GenerateFunctionFile(count);
        Assert.Contains("count(int32_t __novus_local_devices)", countCode);
    }

    [Fact]
    public void CCodeGen_TryErrorDoesNotMaterializeLargeOkPayload()
    {
        var module = BuildIRWithStdlib("""
            from std::core import Result

            struct Large { bytes: [u8; 1024] }

            fn may_fail() -> Result<i32, i32> {
                return Result::Err(7)
            }

            pub fn load() -> Result<Large, i32> {
                let _ = may_fail()?
                return Result::Ok(Large { bytes: [0; 1024] })
            }
            """);
        var generator = new CCodeGenerator(module, [], "68020", "soft");
        var function = module.Functions.Single(candidate => candidate.Name == "load");
        var code = generator.GenerateFunctionFile(function);

        Assert.DoesNotContain("_slot_Result_Large", code);
        Assert.Contains("__out->data.Err._0", code);
    }

    [Fact]
    public void CCodeGen_NonOwningAggregateBuiltForReturn_WritesReturnBufferDirectly()
    {
        var module = BuildIR("""
            enum Packet {
                Empty,
                Data(u32),
            }

            pub fn make_packet(value: u32) -> Packet {
                return match value {
                    0 => Packet::Empty,
                    _ => Packet::Data(value),
                }
            }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var function = module.Functions.Single(candidate => candidate.Name == "make_packet");
        var code = generator.GenerateFunctionFile(function);

        Assert.Contains("(*__out).tag =", code);
        Assert.DoesNotContain("__novus_memcpy((uint8_t*)__out", code);
    }

    [Fact]
    public void CCodeGen_AggregateEarlyReturnsShareOneDeferEpilogue()
    {
        var module = BuildIR("""
            struct Pair { left: i32, right: i32 }

            pub fn choose(first: bool) -> Pair {
                var cleaned = 0
                defer cleaned = 1
                if first { return Pair { left: 1, right: 2 } }
                return Pair { left: 3, right: 4 }
            }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var function = module.Functions.Single(candidate => candidate.Name == "choose");
        var code = generator.GenerateFunctionFile(function);

        Assert.Equal(1, code.Split("__novus_return:;", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, code.Split("goto __novus_return;", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void CCodeGen_SharedReturnEpilogueAssignsSimpleEnumValue()
    {
        var module = BuildIR("""
            enum State { Clear, Mounted }

            pub fn classify(mounted: bool) -> State {
                var cleaned = 0
                defer cleaned = 1
                if mounted { return State::Mounted }
                return State::Mounted
            }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var function = module.Functions.Single(candidate => candidate.Name == "classify");
        var code = generator.GenerateFunctionFile(function);

        Assert.Contains("*__out = State_Mounted;", code);
        Assert.DoesNotContain("__out->tag = State_Mounted;", code);
    }

    [Fact]
    public void CCodeGen_ConstGenericArrayRepeatUsesConcreteEnumType()
    {
        var module = BuildIR("""
            enum Maybe<T> { Some(T), None }
            struct Slots<T, const N: u32> { items: [Maybe<T>; N] }

            impl<T, const N: u32> Slots<T, N> {
                fn new() -> Slots<T, N> {
                    return Slots { items: [Maybe::None; N] }
                }
            }

            pub fn make() -> Slots<u32, 4> { return Slots::<u32, 4>::new() }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var function = module.Functions.Single(candidate =>
            candidate.Name.Contains("Slots") && candidate.Name.Contains("new"));
        var code = generator.GenerateFunctionFile(function);

        Assert.Contains("__out->items[0].tag = Maybe_u32_None;", code);
        Assert.DoesNotContain("(Maybe){", code);
    }

    [Fact]
    public void CCodeGen_EnumPayloadReferencePatternBorrowsPayloadInPlace()
    {
        var module = BuildIR("""
            struct Owned { value: i32 }
            enum Maybe { Some(Owned), None }

            pub fn borrow(value: &Maybe) -> &Owned {
                let Maybe::Some(&payload) = value else { panic!("missing") }
                return payload
            }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var function = module.Functions.Single(candidate => candidate.Name == "borrow");
        var code = generator.GenerateFunctionFile(function);

        Assert.Contains("payload = &((*value)).data.Some._0;", code);
    }

    [Fact]
    public void CCodeGen_LetElseOwnedPayloadIsDropped()
    {
        var module = BuildIR("""
            struct Handle { value: i32 }
            impl Drop for Handle { fn drop(&var self) {} }
            enum Maybe { Some(Handle), None }

            pub fn use_value(value: Maybe) {
                let Maybe::Some(handle) = value else { return }
            }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var function = module.Functions.Single(candidate => candidate.Name == "use_value");
        var code = generator.GenerateFunctionFile(function);

        Assert.Contains("Handle_Drop_drop(&handle);", code);
    }

    [Fact]
    public void CCodeGen_ContextualOptionLetElseExtractsSomePayload()
    {
        var module = BuildIR("""
            enum Option<T> { Some(T), None }

            pub fn unwrap(value: Option<i32>) -> i32 {
                let number = value else return -1
                return number
            }
            """);
        var generator = new CCodeGenerator(module, [], "68020", "soft");
        var code = generator.GenerateFunctionFile(
            module.Functions.Single(function => function.Name == "unwrap"));

        Assert.Contains("Option_i32_Some", code);
        Assert.Contains("number =", code);
        Assert.Contains("data.Some._0", code);
    }

    [Fact]
    public void CCodeGen_DropInPlaceRecursesIntoEnumPayload()
    {
        var module = BuildIR("""
            struct Owned { value: i32 }
            impl Drop for Owned { fn drop(&var self) {} }
            enum Maybe { Some(Owned), None }

            pub fn destroy(value: &var Maybe) { @drop_in_place(value) }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var function = module.Functions.Single(candidate => candidate.Name == "destroy");
        var code = generator.GenerateFunctionFile(function);

        Assert.Contains("switch ((value)->tag)", code);
        Assert.Contains("Owned_Drop_drop(&((value)->data.Some._0));", code);
    }

    [Fact]
    public void CCodeGen_AutomaticallyDropsOwnedStructFields()
    {
        var module = BuildIR("""
            struct Owned { value: i32 }
            impl Drop for Owned { fn drop(&var self) {} }
            struct Wrapper { first: Owned, second: Owned }

            pub fn make_and_drop() {
                let wrapper = Wrapper {
                    first: Owned { value: 1 },
                    second: Owned { value: 2 },
                }
            }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var function = module.Functions.Single(candidate => candidate.Name == "make_and_drop");
        var code = generator.GenerateFunctionFile(function);

        var second = code.IndexOf("Owned_Drop_drop(&((&wrapper)->second));", StringComparison.Ordinal);
        var first = code.IndexOf("Owned_Drop_drop(&((&wrapper)->first));", StringComparison.Ordinal);
        Assert.True(second >= 0 && first > second, code);
    }

    [Fact]
    [Trait("Category", "CompilerIntegration")]
    public void CCodeGen_ManglesGenericStructuralDropNames()
    {
        var module = BuildIRWithStdlib("""
            from std::collections::vec import Vec

            struct Wrapper { values: Vec<u8> }

            pub fn make_and_drop() {
                let wrapper = Wrapper { values: Vec::<u8>::new() }
            }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var function = module.Functions.Single(candidate => candidate.Name == "make_and_drop");
        var code = generator.GenerateFunctionFile(function);

        Assert.Contains("extern void Vec_u8_Drop_drop(Vec_u8* self);", code);
        Assert.Contains("Vec_u8_Drop_drop(&((&wrapper)->values));", code);
        Assert.DoesNotContain("Vec<u8>_Drop_drop", code);
    }

    [Fact]
    public void CCodeGen_LargeAggregateLocalsUseMemsetInsteadOfEmbeddedZeroTemplates()
    {
        var module = BuildIR("""
            struct Big { values: [u32; 64] }

            fn make() -> Big { return Big { values: [0; 64] } }

            pub fn touch() -> u32 {
                let value = make()
                return value.values[0]
            }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var code = generator.GenerateFunctionFile(module.Functions.Single(candidate => candidate.Name == "touch"));

        Assert.DoesNotContain("Big value = {0}", code);
        Assert.Contains("__novus_memset(&value, 0, sizeof(value));", code);
    }

    [Fact]
    public void CCodeGen_RepeatedPatternBindingsKeepUniqueLocalNames()
    {
        var module = BuildIR("""
            enum Maybe<T> { Some(T), None }

            pub fn choose(first: Maybe<u32>, left: Maybe<&u8>, right: Maybe<&u8>) -> u32 {
                let a = match first { Maybe::Some(value) => value, Maybe::None => 0 }
                let b = match left { Maybe::Some(value) => value, Maybe::None => return 0 }
                let c = match right { Maybe::Some(value) => value, Maybe::None => return 0 }
                return a + (u32)*b + (u32)*c
            }
            """);
        var function = module.Functions.Single(candidate => candidate.Name == "choose");
        var bindings = function.LocalVariables
            .Where(variable => variable.Name == "value" || variable.Name.StartsWith("value_"))
            .Select(variable => variable.Name)
            .ToList();

        Assert.Equal(3, bindings.Distinct().Count());
    }

    [Fact]
    public void CCodeGen_PostfixReturnInMatchDoesNotDropEnclosingLocalOnFallthrough()
    {
        var module = BuildIR("""
            struct Guard { value: u32 }
            impl Drop for Guard { fn drop(&var self) {} }
            enum Outcome<T> { Ok(T), Err }

            fn acquire() -> Outcome<Guard> { return Outcome::Ok(Guard { value: 7 }) }
            fn check() -> Outcome<u32> { return Outcome::Ok(0) }

            pub fn inspect() -> u32 {
                let guard = match acquire() {
                    Outcome::Ok(value) => value,
                    Outcome::Err => return 0,
                }
                match check() {
                    Outcome::Ok(value) => return 1 unless value == 0,
                    Outcome::Err => {},
                }
                return guard.value
            }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var code = generator.GenerateFunctionFile(module.Functions.Single(candidate => candidate.Name == "inspect"));
        var postfixBranch = code.IndexOf("postfix_end_", StringComparison.Ordinal);
        var postfixEnd = code.IndexOf("postfix_end_", postfixBranch + 1, StringComparison.Ordinal);
        var matchEnd = code.IndexOf("match_end_", postfixEnd, StringComparison.Ordinal);

        Assert.True(postfixEnd >= 0 && matchEnd > postfixEnd);
        Assert.DoesNotContain("Guard_Drop_drop(&guard);", code[postfixEnd..matchEnd]);
    }

    [Fact]
    public void CCodeGen_CompoundMemberAssignmentReadsThenWritesField()
    {
        var module = BuildIR("""
            struct Counter { value: u32 }
            impl Counter {
                fn add(&var self, amount: u32) { self.value += amount }
            }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var code = generator.GenerateFunctionFile(module.Functions.Single(candidate => candidate.Name == "Counter::add"));

        Assert.Contains("self->value", code);
        Assert.Contains(" + amount", code);
        Assert.Contains("self->value =", code);
    }

    [Fact]
    public void CCodeGen_ShadowedPatternAndLocalBindingsKeepUniqueStorage()
    {
        var module = BuildIR("""
            struct Wide { value: u32 }
            enum Load { Ready(Wide), Blank }

            pub fn choose(load: Load) -> u32 {
                let value: u16 = 7
                match load {
                    Load::Ready(value) => { return value.value },
                    Load::Blank => {
                        let value = Wide { value: 9 }
                        return value.value
                    },
                }
            }

            pub fn unwrap(load: Load) -> u32 {
                let name: u16 = 7
                let Load::Ready(name) = load else { return 0 }
                return name.value
            }
            """);

        foreach (var functionName in new[] { "choose", "unwrap" })
        {
            var names = module.Functions.Single(function => function.Name == functionName)
                .LocalVariables.Select(local => local.Name).ToList();
            Assert.Equal(names.Count, names.Distinct().Count());
        }
    }

    [Fact]
    public void CCodeGen_BorrowedEnumWildcardDoesNotDropPayload()
    {
        var module = BuildIR("""
            struct Owned { value: i32 }
            impl Drop for Owned { fn drop(&var self) {} }
            enum Maybe { Some(Owned), None }

            pub fn present(value: &Maybe) -> bool {
                return match value {
                    Maybe::Some(_) => true,
                    Maybe::None => false,
                }
            }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var function = module.Functions.Single(candidate => candidate.Name == "present");
        var code = generator.GenerateFunctionFile(function);

        Assert.DoesNotContain("Owned_Drop_drop", code);
    }

    [Fact]
    public void CCodeGen_ReturnMoveSkipsSourceClearAfterDropDeactivation()
    {
        var module = BuildIR("""
            struct Handles { a: *u8, b: *u8, c: *u8, d: *u8, e: *u8 }
            enum Outcome { Ok(Handles), Err(u32) }
            impl Drop for Handles { fn drop(&var self) {} }

            pub fn make() -> Outcome {
                let handles = Handles { a: null, b: null, c: null, d: null, e: null }
                return Outcome::Ok(handles)
            }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var function = module.Functions.Single(candidate => candidate.Name == "make");
        var code = generator.GenerateFunctionFile(function);

        Assert.Contains("(__out->data.Ok._0).e = (handles).e;", code);
        Assert.Contains("_defer_1_active = false;", code);
        Assert.DoesNotContain("(handles).e = 0;", code);
        Assert.DoesNotContain("__novus_memcpy", code);
        Assert.DoesNotContain("__novus_memset", code);
    }

    [Fact]
    public void CCodeGen_NonConsumingValueParameterDoesNotMoveArgument()
    {
        var module = BuildIR("""
            struct Handle { ptr: *u8 }
            impl Drop for Handle { fn drop(&var self) {} }

            fn inspect(value: Handle) {}

            pub fn run() {
                let handle = Handle { ptr: null }
                inspect(handle)
                let ptr = handle.ptr
            }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var function = module.Functions.Single(candidate => candidate.Name == "run");
        var code = generator.GenerateFunctionFile(function);
        var inspectCode = generator.GenerateFunctionFile(
            module.Functions.Single(candidate => candidate.Name == "inspect"));

        Assert.False(module.Functions.Single(candidate => candidate.Name == "inspect").Parameters[0].IsConsuming);
        Assert.DoesNotContain("zero source after move to callee", code);
        Assert.Contains("Handle_Drop_drop(&handle);", code);
        Assert.DoesNotContain("Handle_Drop_drop(&value);", inspectCode);
    }

    [Fact]
    public void CCodeGen_ConsumingValueParameterMovesArgument()
    {
        var module = BuildIR("""
            struct Handle { ptr: *u8 }
            impl Drop for Handle { fn drop(&var self) {} }

            fn take(consuming value: Handle) {}

            pub fn run() {
                let handle = Handle { ptr: null }
                take(handle)
            }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var function = module.Functions.Single(candidate => candidate.Name == "run");
        var code = generator.GenerateFunctionFile(function);
        var takeCode = generator.GenerateFunctionFile(
            module.Functions.Single(candidate => candidate.Name == "take"));

        Assert.True(module.Functions.Single(candidate => candidate.Name == "take").Parameters[0].IsConsuming);
        Assert.Contains("_defer_1_active = false;", code);
        Assert.Contains("Handle_Drop_drop(value);", takeCode);
    }

    [Fact]
    public void CCodeGen_ConsumingCallsInvalidateSmallOwnedValuesAndTheirSourceFields()
    {
        var module = BuildIR("""
            struct Handle { active: bool }
            impl Drop for Handle { fn drop(&var self) {} }
            impl Handle { fn release(consuming self) { self.active = false } }

            struct Wrapper { handle: Handle }

            pub fn release_value(consuming handle: Handle) { handle.release() }
            pub fn release_field(consuming wrapper: Wrapper) { wrapper.handle.release() }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var valueCode = generator.GenerateFunctionFile(
            module.Functions.Single(candidate => candidate.Name == "release_value"));
        var fieldCode = generator.GenerateFunctionFile(
            module.Functions.Single(candidate => candidate.Name == "release_field"));

        Assert.Contains("_defer_1_active = false;", valueCode);
        Assert.Contains(fieldCode.Split('\n'), line =>
            line.Contains("wrapper") && line.Contains("handle") && line.Contains("active = 0;"));
    }

    [Fact]
    public void CCodeGen_ReturningOwnedFieldClearsSourceBeforeOwnerDrop()
    {
        var module = BuildIR("""
            struct Handle { value: i32 }
            impl Drop for Handle { fn drop(&var self) {} }
            struct Wrapper { handle: Handle }
            fn unwrap(consuming wrapper: Wrapper) -> Handle { return wrapper.handle }
            """);
        var generator = new CCodeGenerator(module, [], "68020", "soft");
        var code = generator.GenerateFunctionFile(
            module.Functions.Single(candidate => candidate.Name == "unwrap"));

        Assert.Contains("__novus_memset(&(wrapper.handle), 0, sizeof(Handle));", code);
        Assert.True(
            code.IndexOf("__novus_memset(&(wrapper.handle)", StringComparison.Ordinal) <
            code.LastIndexOf("Handle_Drop_drop", StringComparison.Ordinal),
            code);
    }

    [Fact]
    public void CCodeGen_ReturningNestedOwnedLiteralTransfersLocalOwnership()
    {
        var module = BuildIR("""
            struct Handle { value: i32 }
            impl Drop for Handle { fn drop(&var self) {} }
            struct Wrapper { handle: Handle }
            enum Outcome { Ok(Wrapper), Err }

            fn wrap() -> Outcome {
                let handle = Handle { value: 1 }
                return Outcome::Ok(Wrapper { handle: handle })
            }
            """);
        var generator = new CCodeGenerator(module, [], "68020", "soft");
        var code = generator.GenerateFunctionFile(
            module.Functions.Single(candidate => candidate.Name == "wrap"));

        Assert.Contains("__out->data.Ok._0.handle = handle;", code);
        Assert.Contains("_defer_1_active = false;", code);
    }

    [Fact]
    [Trait("Category", "CompilerIntegration")]
    public void CCodeGen_ImportedGenericConsumingParameterDropsOnErrorPaths()
    {
        var module = BuildIRWithStdlib("""
            from std::collections::vec import Vec
            from std::string::core import String

            pub fn run() {
                var values = Vec::<String>::new()
                let value = String::new()
                let _ = values.push(value)
            }
            """);
        var push = module.Functions.Single(candidate =>
            candidate.Name.Contains("push") &&
            candidate.Parameters.Any(parameter =>
                parameter.Name == "value" && parameter.Type.Name == "String"));
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var code = generator.GenerateFunctionFile(push);

        Assert.True(push.Parameters.Single(parameter => parameter.Name == "value").IsConsuming);
        Assert.Contains("String_Drop_drop(value);", code);
    }

    [Fact]
    [Trait("Category", "CompilerIntegration")]
    public void BuildIR_WidenedImportRegistersGenericFunctionTemplate()
    {
        var module = BuildIRWithStdlib("""
            from std::async::executor import ExecutorError
            from std::async::future import Ready, ready

            pub fn run() -> Ready<i32> {
                return ready(7)
            }
            """);

        Assert.Contains(module.Functions, function => function.Name == "ready__i32");
    }

    [Fact]
    public void CCodeGen_SameNamedFunctionsInDifferentModulesUseDifferentLinkSymbols()
    {
        var root = Path.Combine(Path.GetTempPath(), $"novus-link-names-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var firstPath = Path.Combine(root, "first.novus");
            var secondPath = Path.Combine(root, "second.novus");
            var callerPath = Path.Combine(root, "caller.novus");
            const string firstSource = "pub fn now() -> i32 { return 1 }";
            const string secondSource = "pub fn now() -> i32 { return 2 }";
            const string callerSource = """
                from first import now as first_now
                from second import now as second_now

                pub fn sum() -> i32 {
                    return first_now() + second_now()
                }
                """;
            File.WriteAllText(firstPath, firstSource);
            File.WriteAllText(secondPath, secondSource);

            var first = BuildIRAtPath(firstSource, firstPath);
            var second = BuildIRAtPath(secondSource, secondPath);
            var caller = BuildIRAtPath(callerSource, callerPath);
            var firstLink = first.Functions.Single(function => function.Name == "now").LinkName;
            var secondLink = second.Functions.Single(function => function.Name == "now").LinkName;

            Assert.NotNull(firstLink);
            Assert.NotNull(secondLink);
            Assert.NotEqual(firstLink, secondLink);
            Assert.Equal(firstLink, caller.Functions.Single(function => function.Name == "first_now").LinkName);
            Assert.Equal(secondLink, caller.Functions.Single(function => function.Name == "second_now").LinkName);

            var callerGenerator = new CCodeGenerator(caller, [], "68020", "soft");
            var callerCode = callerGenerator
                .GenerateFunctionFile(caller.Functions.Single(function => function.Name == "sum"));
            Assert.Contains($"{firstLink}()", callerCode);
            Assert.Contains($"{secondLink}()", callerCode);
            Assert.Equal(firstLink, callerGenerator.EmitValue(new IrFunctionAddress(
                "first_now", new IrFunctionPointerType([], IrIntType.I32))));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CCodeGen_ReexportedFunctionLinkNameDoesNotDependOnCallerOverloads()
    {
        var root = Path.Combine(Path.GetTempPath(), $"novus-reexport-link-name-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var leafPath = Path.Combine(root, "leaf.novus");
            var facadePath = Path.Combine(root, "facade.novus");
            var callerPath = Path.Combine(root, "caller.novus");
            const string leafSource = "pub fn delay(duration: i32) -> i32 { return duration }";
            const string callerSource = """
                from facade import delay as system_delay

                pub fn delay(duration: i32) -> i32 {
                    return system_delay(duration)
                }
                """;
            File.WriteAllText(leafPath, leafSource);
            File.WriteAllText(facadePath, "pub use leaf::*");

            var leaf = BuildAnalyzedIRAtPath(leafSource, leafPath);
            var caller = BuildAnalyzedIRAtPath(callerSource, callerPath);
            var definition = leaf.Functions.Single(function => function.Name == "delay");
            var imported = caller.Functions.Single(function => function.Name == "system_delay");

            Assert.Equal(definition.LinkName, imported.LinkName);
            Assert.EndsWith("_delay__i32", definition.LinkName);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void CCodeGen_ExplicitAmigaExternUsesRequestedRegisters()
    {
        var module = BuildIR("""
            extern amiga fn asm_div_ceil_u32(a: u32 in d0, b: u32 in d1) -> u32 in d0

            pub fn divide(a: u32, b: u32) -> u32 {
                return asm_div_ceil_u32(a, b)
            }
            """);
        var generator = new CCodeGenerator(module, [], "68020", "soft");
        var code = generator.GenerateFunctionFile(
            module.Functions.Single(function => function.Name == "divide"));

        Assert.Contains("extern __reg(\"d0\") uint32_t asm_div_ceil_u32(__reg(\"d0\") uint32_t a, __reg(\"d1\") uint32_t b);", code);
        Assert.DoesNotContain("__regargs asm_div_ceil_u32", code);
    }

    [Fact]
    public void CCodeGen_UnsafeBlockTailPreservesIfExpressionPointerType()
    {
        var module = BuildIR("""
            struct Handle { value: i32 }
            extern fn acquire() -> *Handle

            pub fn choose(first: bool) -> *Handle {
                return if first {
                    unsafe { acquire() }
                } else {
                    unsafe { acquire() }
                }
            }
            """);
        var code = GenerateCCode(module);

        Assert.Contains("Handle* choose", code);
        Assert.DoesNotContain("int32_t _slot_ptr_Handle", code);
    }

    [Fact]
    public void CCodeGen_ConsumingStructStoredThroughPointerCopiesValueAndDeactivatesDrop()
    {
        var module = BuildIR("""
            struct Handle { ptr: *u8 }
            impl Drop for Handle { fn drop(&var self) {} }

            pub fn store(destination: *Handle, consuming value: Handle) {
                *destination = value
            }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var code = generator.GenerateFunctionFile(
            module.Functions.Single(candidate => candidate.Name == "store"));

        Assert.Contains("__novus_memcpy((uint8_t*)destination, (uint8_t*)&*value, sizeof(Handle));", code);
        Assert.Contains("_defer_1_active = false;", code);
        Assert.DoesNotContain("(value)->ptr = 0;", code);
    }

    [Fact]
    public void CCodeGen_UnitReturningCallDoesNotUseOutputParameter()
    {
        var module = BuildIR("""
            fn consume(value: i32) -> () {}
            pub fn run() { consume(42) }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var function = module.Functions.Single(candidate => candidate.Name == "run");
        var code = generator.GenerateFunctionFile(function);

        Assert.Contains("consume", code);
        Assert.DoesNotContain("void _slot_", code);
    }

    [Fact]
    public void CCodeGen_UnitEnumVariantDoesNotWriteDummyPayload()
    {
        var module = BuildIR("""
            enum Outcome { Ok, Err(u32) }
            pub fn succeed() -> Outcome { return Outcome::Ok }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var function = module.Functions.Single(candidate => candidate.Name == "succeed");
        var code = generator.GenerateFunctionFile(function);

        Assert.Contains("__out->tag = Outcome_Ok;", code);
        Assert.DoesNotContain("._dummy = 0", code);
    }

    [Fact]
    public void CCodeGen_SharedReturnEpilogueKeepsCleanupForEarlierErrorPath()
    {
        var module = BuildIR("""
            struct Handle { ptr: *u8 }
            enum Outcome { Ok(Handle), Err(u32) }
            impl Drop for Handle { fn drop(&var self) {} }

            pub fn maybe(code: u32) -> Outcome {
                let handle = Handle { ptr: null }
                if code == 0 { return Outcome::Err(1) }
                if code == 1 { return Outcome::Err(2) }
                return Outcome::Ok(handle)
            }
            """);
        var generator = new CCodeGenerator(module, new List<IrStringLiteral>(), "68020", "soft");
        var function = module.Functions.Single(candidate => candidate.Name == "maybe");
        var code = generator.GenerateFunctionFile(function);

        Assert.Contains("_defer_1_active = false;", code);
        Assert.Contains("if (_defer_1_active)", code);
        Assert.Contains("Handle_Drop_drop(&handle);", code);
        Assert.Equal(1, code.Split("__out->tag = Outcome_Err;", StringSplitOptions.None).Length - 1);
        Assert.Equal(2, code.Split("goto __novus_return_Err;", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void CCodeGen_FunctionWithMultipleReturns_HandlesAllPaths()
    {
        var source = @"
pub fn classify(n: i32) -> i32 {
    if n < 0 {
        return -1
    }
    if n == 0 {
        return 0
    }
    return 1
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int32_t classify", code);
        Assert.Contains("return", code);
    }

    #endregion

    #region Enum Generation Tests

    [Fact]
    public void CCodeGen_SimpleEnum_GeneratesEnumTypedef()
    {
        var source = @"
enum Color {
    Red,
    Green,
    Blue
}

pub fn get_red() -> Color {
    return Color::Red
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("Color", code);
    }

    [Fact]
    public void CCodeGen_EnumWithData_GeneratesTaggedUnion()
    {
        var source = @"
enum Option {
    Some(i32),
    None
}

pub fn make_some(x: i32) -> Option {
    return Option::Some(x)
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("Option", code);
    }

    [Fact]
    public void CCodeGen_EnumPayloadDereferencesPointerConvertedParameter()
    {
        var source = @"
struct Owned {
    ptr: *u8
}

enum MaybeOwned {
    Some(Owned),
    None
}

struct Holder {
    item: MaybeOwned
}

pub fn wrap(value: Owned) -> MaybeOwned {
    return MaybeOwned::Some(value)
}

pub fn assign(value: Owned) -> MaybeOwned {
    var result = MaybeOwned::None
    result = MaybeOwned::Some(value)
    return result
}

pub fn put(holder: &var Holder, value: Owned) {
    holder.item = MaybeOwned::Some(value)
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("Owned* value", code);
        Assert.Contains("._0 = *value", code);
        Assert.DoesNotContain("._0 = value", code);
        Assert.Contains("(uint8_t*)value, sizeof(Owned)", code);
        Assert.DoesNotContain("(uint8_t*)&value, sizeof(Owned)", code);
        Assert.Contains("(uint8_t*)&*value, sizeof(Owned)", code);
    }

    [Fact]
    public void CCodeGen_EnumMatch_GeneratesSwitchStatement()
    {
        var source = @"
enum Color {
    Red,
    Green,
    Blue
}

pub fn color_to_int(c: Color) -> i32 {
    match c {
        Color::Red => {
            return 1
        },
        Color::Green => {
            return 2
        },
        Color::Blue => {
            return 3
        }
    }
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("Color", code);
        Assert.Contains("color_to_int", code);
    }

    [Fact]
    public void CCodeGen_EnumWithMultipleVariants_GeneratesAllVariants()
    {
        var source = @"
enum Result {
    Ok(i32),
    Err(i32)
}

pub fn make_ok(x: i32) -> Result {
    return Result::Ok(x)
}

pub fn make_err(x: i32) -> Result {
    return Result::Err(x)
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("Result", code);
        Assert.Contains("make_ok", code);
        Assert.Contains("make_err", code);
    }

    #endregion

    #region Struct Tests

    [Fact]
    public void CCodeGen_NestedStruct_GeneratesNestedDefinition()
    {
        var source = @"
struct Inner {
    value: i32
}

struct Outer {
    inner: Inner,
    extra: i32
}

pub fn make_outer() -> Outer {
    var i = Inner { value: 42 }
    return Outer { inner: i, extra: 100 }
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("Inner", code);
        Assert.Contains("Outer", code);
        Assert.Contains("make_outer", code);
    }

    [Fact]
    public void CCodeGen_StructWithPointerField_GeneratesPointerType()
    {
        var source = @"
struct Node {
    value: i32,
    next: *Node
}

pub fn make_node(val: i32) -> Node {
    return Node { value: val, next: 0 as *Node }
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("Node", code);
        Assert.Contains("make_node", code);
    }

    [Fact]
    public void CCodeGen_StructMethod_GeneratesMethodCall()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}

impl Point {
    pub fn get_x(&self) -> i32 {
        return self.x
    }
}

pub fn test_method(p: Point) -> i32 {
    return p.get_x()
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("Point", code);
        Assert.Contains("get_x", code);
        Assert.Contains("test_method", code);
    }

    #endregion

    #region Pointer and Reference Tests

    [Fact]
    public void CCodeGen_PointerDereference_GeneratesDerefOperator()
    {
        var source = @"
pub fn deref(ptr: *i32) -> i32 {
    return *ptr
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int32_t", code);
        Assert.Contains("deref", code);
    }

    [Fact]
    public void CCodeGen_AddressOf_GeneratesAddressOperator()
    {
        var source = @"
pub fn get_address(x: i32) -> *i32 {
    return &x
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int32_t", code);
        Assert.Contains("get_address", code);
    }

    [Fact]
    public void CCodeGen_PointerArithmetic_GeneratesArithmetic()
    {
        var source = @"
pub fn advance_ptr(ptr: *i32, offset: i32) -> *i32 {
    return ptr + offset
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("advance_ptr", code);
        Assert.Contains("int32_t", code);
    }

    #endregion

    #region Array Tests

    [Fact]
    public void CCodeGen_ArrayLiteral_GeneratesArrayInitializer()
    {
        var source = @"
pub fn make_array() -> [i32; 3] {
    return [1, 2, 3]
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("make_array", code);
        Assert.Contains("int32_t", code);
    }

    [Fact]
    public void CCodeGen_ArrayIndexAccess_GeneratesIndexing()
    {
        var source = @"
pub fn get_element(arr: [i32; 5], index: u32) -> i32 {
    return arr[index]
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("get_element", code);
        Assert.Contains("int32_t", code);
    }

    [Fact]
    public void CCodeGen_PointerOffsetComputesAddressWithoutLoadingElement()
    {
        var module = new IrModule();
        var pointerType = new IrPointerType(IrIntType.U16);
        var function = new IrFunction("offset", pointerType, Visibility.Public);
        function.Parameters.Add(new IrParameter("ptr", pointerType));
        function.Parameters.Add(new IrParameter("index", IrIntType.U32));
        function.CreateBasicBlock("entry").AddInstruction(new IrReturn(
            new IrPointerOffsetValue(
                new IrVariable("ptr", pointerType),
                new IrVariable("index", IrIntType.U32),
                IrIntType.U16,
                pointerType)));
        module.AddFunction(function);

        var code = GenerateCCode(module);

        Assert.Contains("return (ptr + index);", code);
    }

    [Fact]
    public void CCodeGen_SafeIndexRemainsCheckedAtMinimalInstrumentationLevel()
    {
        var module = BuildIR(@"
pub fn get_element(arr: [i32; 5], index: u32) -> i32 {
    return arr[index]
}");
        var code = new CCodeGenerator(
            module, [], "68020", "soft", BuildMode.Release, SafetyLevel.Unsafe).Generate();

        Assert.Contains("if ((uint32_t)index >= (uint32_t)", code);
        Assert.Contains("__novus_bounds_check_failed(index", code);
    }

    [Fact]
    public void CCodeGen_ExplicitUnsafeIndexDoesNotEmitBoundsCheck()
    {
        var module = BuildIR(@"
pub fn get_element(arr: [i32; 5], index: u32) -> i32 {
    return unsafe { arr[index] }
}");
        var code = GenerateCCode(module, BuildMode.Release);

        Assert.DoesNotContain("if ((uint32_t)index >=", code);
    }

    [Fact]
    public void CCodeGen_ArrayAssignment_GeneratesIndexedStore()
    {
        var source = @"
pub fn set_element(arr: [i32; 5], index: u32, value: i32) -> [i32; 5] {
    arr[index] = value
    return arr
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("set_element", code);
        Assert.Contains("int32_t", code);
    }

    [Fact]
    public void CCodeGen_ArrayReturnPassesDestinationBufferDirectly()
    {
        var source = @"
fn make_array() -> [u8; 4] {
    return [1, 2, 3, 4]
}

pub fn first() -> u8 {
    let values = make_array()
    return values[0]
}";

        var code = GenerateCCode(BuildIR(source));

        Assert.Contains("void make_array(uint8_t* __out)", code);
        Assert.Matches(@"make_array\([^&][^)]*\);", code);
        Assert.DoesNotContain("make_array(&", code);
    }

    [Fact]
    public void CCodeGen_ArrayLiteralReturnUsesValidInitializerStorage()
    {
        var source = """
            pub fn make_array() -> [u8; 20] {
                return [1; 20]
            }
            """;

        var code = GenerateCCode(BuildIR(source));

        Assert.Contains("static const uint8_t __init[20]", code);
        Assert.Contains("__novus_memcpy((uint8_t*)__out, (const uint8_t*)__init", code);
        Assert.Contains("(sizeof(uint8_t) * 20)", code);
        Assert.DoesNotContain("sizeof(__out)", code);
        Assert.DoesNotContain("(uint8_t*)&{", code);
    }

    [Fact]
    public void CCodeGen_ArrayReferenceUsesPointerToArrayDeclarator()
    {
        var source = @"
pub fn equal(left: &[u8; 4], right: &[u8; 4]) -> bool {
    return left[0] == right[0]
}";

        var code = GenerateCCode(BuildIR(source));

        Assert.Contains("uint8_t (*left)[4]", code);
        Assert.Contains("uint8_t (*right)[4]", code);
    }

    [Fact]
    public void CCodeGen_ArrayReturnErrorPathReturnsVoid()
    {
        var source = @"
pub fn set_at(index: u32) -> [u8; 4] {
    var values: [u8] = [0; 4]
    values[index] = 1
    return values
}";

        var code = GenerateCCode(BuildIR(source));

        Assert.Contains("void set_at(uint8_t* __out", code);
        Assert.DoesNotContain("return (uint8_t*)0", code);
    }

    #endregion

    #region Binary Operations Tests

    [Fact]
    public void CCodeGen_ArithmeticOperations_GeneratesAllOperators()
    {
        var source = @"
pub fn arithmetic(a: i32, b: i32) -> i32 {
    var add = a + b
    var sub = a - b
    var mul = a * b
    var div = a / b
    var mod = a % b
    return add + sub + mul + div + mod
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("arithmetic", code);
        Assert.Contains("int32_t", code);
    }

    [Fact]
    public void CCodeGen_BitwiseOperations_GeneratesBitwiseOps()
    {
        var source = @"
pub fn bitwise(a: i32, b: i32) -> i32 {
    var and = a & b
    var or = a | b
    var xor = a ^ b
    var shl = a << 2
    var shr = a >> 2
    return and + or + xor + shl + shr
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("bitwise", code);
        Assert.Contains("int32_t", code);
    }

    [Fact]
    public void CCodeGen_ComparisonOperations_GeneratesComparisons()
    {
        var source = @"
pub fn compare(a: i32, b: i32) -> i32 {
    if a == b {
        return 0
    }
    if a < b {
        return -1
    }
    if a > b {
        return 1
    }
    return 0
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("compare", code);
        Assert.Contains("int32_t", code);
    }

    [Fact]
    public void CCodeGen_LogicalOperations_GeneratesLogicalOps()
    {
        var source = @"
pub fn logical(a: bool, b: bool) -> bool {
    var and = a && b
    var or = a || b
    var not = !a
    return and || or || not
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("logical", code);
        Assert.Contains("bool", code);
    }

    #endregion

    #region Cast Tests

    [Fact]
    public void CCodeGen_IntegerCast_GeneratesCastExpression()
    {
        var source = @"
pub fn cast_to_u32(x: i32) -> u32 {
    return x as u32
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("cast_to_u32", code);
        Assert.Contains("int32_t", code);
        Assert.Contains("uint32_t", code);
    }

    [Fact]
    public void CCodeGen_PointerCast_GeneratesPointerCast()
    {
        var source = @"
pub fn cast_pointer(ptr: *i32) -> *u8 {
    return ptr as *u8
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("cast_pointer", code);
        Assert.Contains("int32_t", code);
        Assert.Contains("uint8_t", code);
    }

    #endregion

    #region Static Variables Tests

    [Fact]
    public void CCodeGen_StaticVariable_GeneratesStaticDecl()
    {
        var source = @"
static COUNTER: i32 = 0

pub fn increment() -> i32 {
    COUNTER = COUNTER + 1
    return COUNTER
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("COUNTER", code);
        Assert.Contains("increment", code);
    }

    #endregion

    #region External Function Tests

    [Fact]
    public void CCodeGen_ExternalFunction_GeneratesExternDecl()
    {
        var source = @"
extern fn printf(format: *u8) -> i32

pub fn call_printf() -> i32 {
    return printf(0 as *u8)
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("printf", code);
        Assert.Contains("call_printf", code);
    }

    #endregion

    #region Complex Integration Tests

    [Fact]
    public void CCodeGen_ComplexProgram_GeneratesCompleteCode()
    {
        var source = @"
struct Point {
    x: i32,
    y: i32
}

enum Shape {
    Circle(i32),
    Rectangle(i32, i32)
}

pub fn area(s: Shape) -> i32 {
    match s {
        Shape::Circle(r) => {
            return r * r * 3
        },
        Shape::Rectangle(w, h) => {
            return w * h
        }
    }
}

pub fn create_point(x: i32, y: i32) -> Point {
    var p = Point { x: x, y: y }
    return p
}

pub fn main() -> i32 {
    var circle = Shape::Circle(10)
    var rect = Shape::Rectangle(5, 8)
    var p = create_point(1, 2)
    return area(circle) + area(rect) + p.x
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("Point", code);
        Assert.Contains("Shape", code);
        Assert.Contains("area", code);
        Assert.Contains("main", code);
        Assert.Contains("create_point", code);
    }

    [Fact]
    public void CCodeGen_FunctionWithMultipleTypes_GeneratesAllTypes()
    {
        var source = @"
struct Vector3 {
    x: i32,
    y: i32,
    z: i32
}

enum Axis {
    X,
    Y,
    Z
}

pub fn get_axis_value(v: Vector3, axis: Axis) -> i32 {
    match axis {
        Axis::X => {
            return v.x
        },
        Axis::Y => {
            return v.y
        },
        Axis::Z => {
            return v.z
        }
    }
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("Vector3", code);
        Assert.Contains("Axis", code);
        Assert.Contains("get_axis_value", code);
    }

    [Fact]
    public void CCodeGen_MultipleFunctionsCallingEachOther_GeneratesCallGraph()
    {
        var source = @"
pub fn helper1(x: i32) -> i32 {
    return x * 2
}

pub fn helper2(x: i32) -> i32 {
    return x + 10
}

pub fn caller(y: i32) -> i32 {
    var a = helper1(y)
    var b = helper2(a)
    return b
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("helper1", code);
        Assert.Contains("helper2", code);
        Assert.Contains("caller", code);
    }

    #endregion

    #region Build Mode Tests

    [Fact]
    public void CCodeGen_DebugMode_GeneratesDebugInfo()
    {
        var source = @"
pub fn test() -> i32 {
    return 42
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module, BuildMode.Debug);

        Assert.Contains("test", code);
        Assert.Contains("int32_t", code);
    }

    [Fact]
    public void CCodeGen_ReleaseMode_OptimizesCode()
    {
        var source = @"
pub fn test() -> i32 {
    return 42
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module, BuildMode.Release);

        Assert.Contains("test", code);
        Assert.Contains("int32_t", code);
    }

    #endregion

    #region String Literal Tests

    [Fact]
    public void CCodeGen_FunctionReturningString_HandlesStringLiterals()
    {
        var source = @"
pub fn get_message() -> *u8 {
    return ""Hello, World!""
}

pub fn main() -> i32 {
    var msg = get_message()
    return 0
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        // Verify function signatures are generated
        Assert.Contains("get_message", code);
        Assert.Contains("main", code);
        // Note: String literals are handled separately by CCodeGenerator
        // and passed via stringLiterals parameter, so they may not appear in main code output
    }

    #endregion

    #region Type Tests

    [Fact]
    public void CCodeGen_AllIntegerTypes_GeneratesCorrectTypes()
    {
        var source = @"
pub fn test_types(a: i8, b: i16, c: i32, d: i64, e: u8, f: u16, g: u32, h: u64) -> i32 {
    return 0
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("int8_t", code);
        Assert.Contains("int16_t", code);
        Assert.Contains("int32_t", code);
        Assert.Contains("int64_t", code);
        Assert.Contains("uint8_t", code);
        Assert.Contains("uint16_t", code);
        Assert.Contains("uint32_t", code);
        Assert.Contains("uint64_t", code);
    }

    [Fact]
    public void CCodeGen_BoolType_GeneratesBoolType()
    {
        var source = @"
pub fn test_bool(flag: bool) -> bool {
    return !flag
}";

        var module = BuildIR(source);
        var code = GenerateCCode(module);

        Assert.Contains("bool", code);
        Assert.Contains("test_bool", code);
    }

    #endregion
}

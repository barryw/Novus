# Compiler Architecture Refactoring - Code Examples

This document provides concrete code examples showing how to refactor the Novus compiler according to the recommendations in `COMPILER_ARCHITECTURE_REVIEW.md`.

---

## Example 1: Extract TypeResolver from IrBuilder

### Before (Current)

```csharp
// In IrBuilder.cs - type resolution mixed with IR building
public class IrBuilder : NovusBaseVisitor<object?>
{
    private IrType ParseType(NovusParser.TypeContext context)
    {
        // Parse type from AST
        if (context is NovusParser.PointerTypeContext ptrCtx)
        {
            var pointeeType = ParseType(ptrCtx.type());
            return new IrPointerType(pointeeType);
        }
        // ... 200 more lines of type parsing ...
    }

    public override object? VisitVariableDeclaration(...)
    {
        // Mix of type resolution AND IR building
        var type = ParseType(ctx.type());  // Type resolution
        var initializer = Visit(ctx.expression());  // IR building
        var localDecl = new IrLocalDecl(...);  // IR building
        _currentBlock.AddInstruction(localDecl);
        return null;
    }
}
```

### After (Proposed)

```csharp
// NEW: Novus.Core/Frontend/TypeResolver.cs
public class TypeResolver
{
    private readonly SymbolTable _symbols;
    private readonly string _filePath;
    private readonly DiagnosticBag _diagnostics;

    // Single responsibility: resolve all types in AST
    public TypedAST Resolve(NovusParser.CompilationUnitContext ast)
    {
        var typedAst = new TypedAST();

        // Phase 1: Register all type names (structs, enums, traits)
        foreach (var structDecl in ast.structDeclaration())
            RegisterStructPlaceholder(structDecl, typedAst);

        foreach (var enumDecl in ast.enumDeclaration())
            RegisterEnumStub(enumDecl, typedAst);

        // Phase 2: Resolve field types (all names are now known)
        foreach (var structDecl in ast.structDeclaration())
            ResolveStructFields(structDecl, typedAst);

        foreach (var enumDecl in ast.enumDeclaration())
            ResolveEnumVariants(enumDecl, typedAst);

        // Phase 3: Resolve function signatures
        foreach (var funcDecl in ast.functionDeclaration())
            ResolveFunctionSignature(funcDecl, typedAst);

        return typedAst;
    }

    private IrType ResolveType(NovusParser.TypeContext context)
    {
        if (context is NovusParser.PointerTypeContext ptrCtx)
        {
            var pointeeType = ResolveType(ptrCtx.type());
            return new IrPointerType(pointeeType);
        }
        else if (context is NovusParser.NamedTypeContext namedCtx)
        {
            var typeName = namedCtx.typeName().GetText();
            var type = _symbols.LookupType(typeName);
            if (type == null)
            {
                _diagnostics.ReportError("E0001",
                    $"Type '{typeName}' not found",
                    GetLocation(namedCtx));
                return ErrorType.Instance;  // Sentinel, not null
            }
            return type;
        }
        // ... handle other type forms ...
    }
}

// NEW: TypedAST.cs - bridge between type resolution and IR building
public class TypedAST
{
    // Map each AST node to its resolved type
    public Dictionary<IParseTree, IrType> NodeTypes { get; } = new();

    // Complete type definitions
    public Dictionary<string, IrStructType> Structs { get; } = new();
    public Dictionary<string, IrEnumType> Enums { get; } = new();
    public Dictionary<string, IrTrait> Traits { get; } = new();

    // Function signatures
    public Dictionary<string, FunctionSignature> Functions { get; } = new();

    public IrType GetType(IParseTree node)
    {
        if (NodeTypes.TryGetValue(node, out var type))
            return type;
        return ErrorType.Instance;  // Should never happen if resolution succeeded
    }
}

// REFACTORED: IrBuilder.cs - now much simpler
public class HirBuilder
{
    private readonly TypedAST _typedAst;  // Pre-resolved types

    public HirBuilder(TypedAST typedAst)
    {
        _typedAst = typedAst;
    }

    public IrModule Build(NovusParser.CompilationUnitContext ast)
    {
        var module = new IrModule();

        // Single pass: all types are already resolved
        foreach (var funcDecl in ast.functionDeclaration())
            BuildFunction(funcDecl, module);

        return module;
    }

    private void BuildFunction(NovusParser.FunctionDeclarationContext ctx, IrModule module)
    {
        // Type is already resolved - just use it
        var funcSignature = _typedAst.Functions[ctx.IDENTIFIER().GetText()];
        var function = new IrFunction(funcSignature.Name, funcSignature.ReturnType);

        // Build function body (types already known)
        _currentFunction = function;
        _currentBlock = function.CreateBasicBlock("entry");
        Visit(ctx.block());

        module.AddFunction(function);
    }

    public override object? VisitVariableDeclaration(...)
    {
        // Type is already resolved - no parsing needed
        var type = _typedAst.GetType(ctx.type());
        var initializer = Visit(ctx.expression());
        var localDecl = new IrLocalDecl(ctx.name, type, ...);
        _currentBlock.AddInstruction(localDecl);
        return null;
    }
}
```

**Benefits:**
- TypeResolver is independently testable (no IR building mixed in)
- HirBuilder is simpler (no type resolution, just IR construction)
- Types are computed once, used everywhere
- Errors caught earlier (before IR building starts)

---

## Example 2: Run Semantic Analysis Before IR Building

### Before (Current)

```csharp
// In Compiler.cs
public CompilationResult Compile(string sourceCode)
{
    // Parse source
    var ast = ParseSource(sourceCode);

    // Build IR (types not yet validated!)
    var irBuilder = new IrBuilder();
    var module = irBuilder.BuildModule(ast);

    // Semantic analysis runs AFTER IR is built
    var analyzer = new SemanticAnalyzer(filePath, sourceCode);
    var isValid = analyzer.Analyze(ast);

    if (!isValid)
    {
        // Oops, IR may be malformed due to type errors
        return CompilationResult.Failure(analyzer.Diagnostics);
    }

    // Optimize
    var optimized = Optimize(module);

    // Generate code
    var cCode = GenerateC(optimized);
    return CompilationResult.Success(cCode);
}
```

### After (Proposed)

```csharp
// In Compiler.cs
public CompilationResult Compile(string sourceCode)
{
    // Parse source
    var ast = ParseSource(sourceCode);

    // Step 1: Resolve all types
    var typeResolver = new TypeResolver(filePath, sourceCode);
    var typedAst = typeResolver.Resolve(ast);

    if (typeResolver.Diagnostics.HasErrors)
    {
        return CompilationResult.Failure(typeResolver.Diagnostics);
    }

    // Step 2: Semantic analysis (works on TypedAST)
    var analyzer = new SemanticAnalyzer(filePath, sourceCode);
    var isValid = analyzer.Analyze(typedAst);  // Note: TypedAST, not raw AST

    if (!isValid)
    {
        // Type errors caught BEFORE IR building
        return CompilationResult.Failure(analyzer.Diagnostics);
    }

    // Step 3: Build HIR (types are validated, IR will be well-formed)
    var hirBuilder = new HirBuilder(typedAst);
    var hirModule = hirBuilder.Build(ast);

    // Step 4: Lower HIR to LIR
    var lirModule = LowerToLir(hirModule);

    // Step 5: Optimize LIR
    var optimized = Optimize(lirModule);

    // Step 6: Generate code
    var cCode = GenerateC(optimized);
    return CompilationResult.Success(cCode);
}
```

**Benefits:**
- Type errors caught early (before IR building)
- IR is guaranteed to be well-formed
- Clear phase boundaries
- Each phase is independently testable

---

## Example 3: Split IrBuilder by Concern

### Before (Current)

```csharp
// IrBuilder.Expressions.cs - 5,984 lines!
public partial class IrBuilder
{
    public override object? VisitBinaryExpression(...)
    {
        var left = Visit(ctx.left);
        var right = Visit(ctx.right);
        var op = GetBinaryOp(ctx.op.Type);
        // ... 50 lines of type checking, coercion, etc ...
        var result = new IrBinaryOp(...);
        return result;
    }

    public override object? VisitCallExpression(...)
    {
        // ... 200 lines of method resolution, generic instantiation, etc ...
    }

    public override object? VisitMatchExpression(...)
    {
        // ... 300 lines of pattern matching compilation ...
    }

    // ... 100+ more expression visitors ...
}
```

### After (Proposed)

```csharp
// NEW: ExpressionHirBuilder.cs (~3,000 lines)
public class ExpressionHirBuilder
{
    private readonly TypedAST _typedAst;
    private readonly IrFunction _currentFunction;
    private readonly IrBasicBlock _currentBlock;

    public IrValue BuildExpression(NovusParser.ExpressionContext ctx)
    {
        return ctx switch
        {
            NovusParser.BinaryExpressionContext binary => BuildBinary(binary),
            NovusParser.CallExpressionContext call => BuildCall(call),
            NovusParser.MatchExpressionContext match => BuildMatch(match),
            // ... other expression types ...
            _ => throw new NotImplementedException()
        };
    }

    private IrValue BuildBinary(NovusParser.BinaryExpressionContext ctx)
    {
        var left = BuildExpression(ctx.left);
        var right = BuildExpression(ctx.right);
        var op = GetBinaryOp(ctx.op.Type);

        // Type is already resolved - no validation needed here
        var resultType = _typedAst.GetType(ctx);

        var tempName = GenerateTempName();
        var binOp = new IrBinaryOp(tempName, op, left, right, resultType);
        _currentBlock.AddInstruction(binOp);

        return new IrVariable(tempName, resultType);
    }

    private IrValue BuildCall(NovusParser.CallExpressionContext ctx)
    {
        // Delegate to specialized builder
        var callBuilder = new CallExpressionBuilder(_typedAst);
        return callBuilder.BuildCall(ctx, _currentBlock);
    }
}

// NEW: CallExpressionBuilder.cs (~500 lines)
// Specialized builder for call expressions
public class CallExpressionBuilder
{
    private readonly TypedAST _typedAst;

    public IrValue BuildCall(
        NovusParser.CallExpressionContext ctx,
        IrBasicBlock block)
    {
        // Extract callee
        var callee = ctx.callee;
        var args = ctx.arguments;

        // Resolve function (already done in TypedAST)
        var funcSignature = _typedAst.GetFunctionSignature(callee);

        // Build argument expressions
        var argValues = BuildArguments(args);

        // Emit call instruction
        var call = new IrCall(funcSignature.MangledName, funcSignature.ReturnType);
        foreach (var arg in argValues)
            call.Arguments.Add(arg);

        block.AddInstruction(call);
        return new IrVariable(call.ResultName, funcSignature.ReturnType);
    }
}

// NEW: MatchExpressionBuilder.cs (~700 lines)
// Specialized builder for match expressions
public class MatchExpressionBuilder
{
    private readonly TypedAST _typedAst;
    private readonly ExpressionHirBuilder _exprBuilder;

    public IrValue BuildMatch(
        NovusParser.MatchExpressionContext ctx,
        IrFunction function)
    {
        // Build scrutinee
        var scrutinee = _exprBuilder.BuildExpression(ctx.scrutinee);

        // Generate blocks for each arm
        var armBlocks = new List<IrBasicBlock>();
        foreach (var arm in ctx.matchArm())
        {
            var armBlock = function.CreateBasicBlock($"match_arm_{arm.Start.Line}");
            armBlocks.Add(armBlock);
        }

        var endBlock = function.CreateBasicBlock("match_end");

        // Build pattern tests and arm bodies
        BuildMatchArms(ctx, scrutinee, armBlocks, endBlock);

        return new IrVariable(...);  // Result phi node
    }
}

// REFACTORED: HirBuilder.cs - now just coordinates
public class HirBuilder
{
    private readonly TypedAST _typedAst;
    private readonly ExpressionHirBuilder _exprBuilder;
    private readonly StatementHirBuilder _stmtBuilder;
    private readonly FunctionHirBuilder _funcBuilder;

    public HirBuilder(TypedAST typedAst)
    {
        _typedAst = typedAst;
        _exprBuilder = new ExpressionHirBuilder(typedAst);
        _stmtBuilder = new StatementHirBuilder(typedAst, _exprBuilder);
        _funcBuilder = new FunctionHirBuilder(typedAst, _stmtBuilder);
    }

    public IrModule Build(NovusParser.CompilationUnitContext ast)
    {
        var module = new IrModule();

        // Delegate to specialized builders
        foreach (var funcDecl in ast.functionDeclaration())
            _funcBuilder.BuildFunction(funcDecl, module);

        foreach (var structDecl in ast.structDeclaration())
            BuildStruct(structDecl, module);

        return module;
    }
}
```

**Benefits:**
- Each builder is < 1,000 lines (manageable size)
- Specialized builders for complex features (calls, matches)
- Easy to test each builder independently
- Clear separation of concerns

---

## Example 4: Add HIR Lowering Pass

### Before (Current)

```csharp
// defer blocks are compiled away immediately in IrBuilder
public override object? VisitDeferStatement(...)
{
    // Register defer block for cleanup
    var deferBlock = _currentFunction.CreateBasicBlock("defer");
    _scopeDeferStack.Peek().Add(deferBlock);

    // Build defer body immediately
    _currentBlock = deferBlock;
    Visit(ctx.block());

    // Lost: no HIR representation of defer
}
```

### After (Proposed)

```csharp
// NEW: Preserve defer as HIR instruction
public class HirDefer : HirInstruction
{
    public IrBasicBlock DeferredBlock { get; set; }
    public int ScopeLevel { get; set; }  // Track which scope owns this defer

    public HirDefer(IrBasicBlock deferredBlock, int scopeLevel)
    {
        DeferredBlock = deferredBlock;
        ScopeLevel = scopeLevel;
    }
}

// In HirBuilder - emit HIR defer instruction
public override object? VisitDeferStatement(...)
{
    var deferBlock = _currentFunction.CreateBasicBlock("defer");

    // Build defer body
    var savedBlock = _currentBlock;
    _currentBlock = deferBlock;
    Visit(ctx.block());
    _currentBlock = savedBlock;

    // Emit HIR defer instruction (not lowered yet)
    var hirDefer = new HirDefer(deferBlock, _scopeLevel);
    _currentBlock.AddInstruction(hirDefer);

    return null;
}

// NEW: HirLoweringPass.cs - lower HIR to LIR
public class HirLoweringPass
{
    public IrModule Lower(IrModule hirModule)
    {
        var lirModule = new IrModule();

        foreach (var function in hirModule.Functions)
        {
            var loweredFunc = LowerFunction(function);
            lirModule.AddFunction(loweredFunc);
        }

        return lirModule;
    }

    private IrFunction LowerFunction(IrFunction hirFunc)
    {
        var lirFunc = new IrFunction(hirFunc.Name, hirFunc.ReturnType);

        foreach (var block in hirFunc.BasicBlocks)
        {
            var lirBlock = lirFunc.CreateBasicBlock(block.Label);

            foreach (var instruction in block.Instructions)
            {
                if (instruction is HirDefer defer)
                {
                    // Lower defer to explicit cleanup calls
                    LowerDefer(defer, lirBlock, lirFunc);
                }
                else
                {
                    // Pass through non-HIR instructions
                    lirBlock.AddInstruction(instruction);
                }
            }
        }

        return lirFunc;
    }

    private void LowerDefer(HirDefer defer, IrBasicBlock block, IrFunction func)
    {
        // Insert cleanup code at appropriate scope exit points
        // Algorithm:
        // 1. Find all exits from the defer's scope (returns, breaks, continues)
        // 2. Insert branch to defer block before each exit
        // 3. After defer block, branch to original exit target

        var exitPoints = FindScopeExits(func, defer.ScopeLevel);
        foreach (var exitBlock in exitPoints)
        {
            // Insert branch to defer block
            var deferLabel = defer.DeferredBlock.Label;
            exitBlock.AddInstruction(new IrBranch(deferLabel));

            // After defer block, branch to original exit
            defer.DeferredBlock.AddInstruction(new IrBranch(exitBlock.OriginalTarget));
        }
    }
}
```

**Benefits:**
- defer preserved in HIR (better for optimization, error reporting)
- Lowering logic is centralized (not scattered across IrBuilder)
- Can optimize defer blocks before lowering
- Can perform analysis on HIR (escape analysis, etc.)

---

## Example 5: Better Error Recovery

### Before (Current)

```csharp
// Lots of null checks throughout IrBuilder
var structType = _symbols.LookupStruct(typeName);
if (structType == null)
{
    _diagnostics.ReportError(...);
    return null;  // Problem: null propagates
}

// Later code may crash on null
var fieldType = structType.GetField(fieldName).Type;  // NullReferenceException if structType is null!
```

### After (Proposed)

```csharp
// NEW: ErrorType.cs - sentinel for type errors
public class ErrorType : IrType
{
    public static readonly ErrorType Instance = new();

    private ErrorType() { }

    public override int SizeInBytes => 0;
    public override string Name => "<error>";

    // All operations on ErrorType are no-ops (fail-safe)
    public IrStructField? GetField(string name) => null;
}

// In TypeResolver - return ErrorType instead of null
var structType = _symbols.LookupStruct(typeName);
if (structType == null)
{
    _diagnostics.ReportError(...);
    return ErrorType.Instance;  // Sentinel, not null
}

// Later code doesn't crash - ErrorType is safe
var fieldType = structType.GetField(fieldName)?.Type ?? ErrorType.Instance;

// Type checking handles ErrorType gracefully
public bool IsAssignable(IrType from, IrType to)
{
    // If either type is error, assume assignment is valid (error already reported)
    if (from is ErrorType || to is ErrorType)
        return true;

    // Normal type checking
    return TypesAreCompatible(from, to);
}
```

**Benefits:**
- Compilation continues after errors (catch more errors per run)
- No NullReferenceExceptions in compiler
- ErrorType acts as "poison" value that propagates safely
- Better developer experience (see all errors, not just first one)

---

## Example 6: Unit Testing Individual Components

### Before (Current)

```csharp
// Can only test full pipeline
[Fact]
public void TestCompilation()
{
    var source = @"
        struct Point { x: i32, y: i32 }
        fn main() -> i32 { return 0 }
    ";

    var compiler = new Compiler();
    var result = compiler.Compile(source);

    Assert.True(result.Success);
    // Problem: If test fails, which component broke?
}
```

### After (Proposed)

```csharp
// Can test each component independently

[Fact]
public void TypeResolver_ParsesPointerType()
{
    var source = "*i32";
    var ast = ParseTypeExpression(source);

    var resolver = new TypeResolver("test.novus", source);
    var type = resolver.ResolveType(ast);

    Assert.IsType<IrPointerType>(type);
    var ptrType = (IrPointerType)type;
    Assert.IsType<IrIntType>(ptrType.PointeeType);
}

[Fact]
public void TypeResolver_ReportsErrorForUnknownType()
{
    var source = "UnknownType";
    var ast = ParseTypeExpression(source);

    var resolver = new TypeResolver("test.novus", source);
    var type = resolver.ResolveType(ast);

    Assert.IsType<ErrorType>(type);
    Assert.True(resolver.Diagnostics.HasErrors);
    Assert.Contains("UnknownType", resolver.Diagnostics.Errors[0].Message);
}

[Fact]
public void SemanticAnalyzer_DetectsUseAfterMove()
{
    var typedAst = CreateTypedAst(@"
        fn test() {
            let s = String::from(""hello"");
            consume(s);
            print(s);  // Error: use after move
        }
    ");

    var analyzer = new SemanticAnalyzer("test.novus", source);
    var isValid = analyzer.Analyze(typedAst);

    Assert.False(isValid);
    Assert.Contains("use after move", analyzer.Diagnostics.Errors[0].Message);
}

[Fact]
public void HirBuilder_EmitsDeferInstruction()
{
    var typedAst = CreateTypedAst(@"
        fn test() {
            defer { cleanup(); }
            do_work();
        }
    ");

    var builder = new HirBuilder(typedAst);
    var module = builder.Build(ast);

    var testFunc = module.GetFunction("test");
    var deferInstr = testFunc.BasicBlocks[0].Instructions
        .OfType<HirDefer>()
        .FirstOrDefault();

    Assert.NotNull(deferInstr);
}

[Fact]
public void HirLoweringPass_InsertsCleanupCalls()
{
    var hirModule = CreateHirModule(@"
        fn test() {
            defer { cleanup(); }
            return;
        }
    ");

    var lowering = new HirLoweringPass();
    var lirModule = lowering.Lower(hirModule);

    var testFunc = lirModule.GetFunction("test");

    // Check that cleanup call is inserted before return
    var returnInstr = testFunc.BasicBlocks[0].Instructions
        .OfType<IrReturn>()
        .FirstOrDefault();

    Assert.NotNull(returnInstr);

    // Cleanup call should be before return
    var cleanupCall = testFunc.BasicBlocks[0].Instructions
        .OfType<IrCall>()
        .Where(c => c.FunctionName == "cleanup")
        .FirstOrDefault();

    Assert.NotNull(cleanupCall);
    Assert.True(testFunc.BasicBlocks[0].Instructions.IndexOf(cleanupCall) <
                testFunc.BasicBlocks[0].Instructions.IndexOf(returnInstr));
}

// Integration test still exists, but now failures are easier to debug
[Fact]
public void Integration_FullPipeline()
{
    var source = @"
        struct Point { x: i32, y: i32 }
        fn main() -> i32 {
            let p = Point { x: 10, y: 20 };
            return p.x + p.y;
        }
    ";

    var compiler = new Compiler();
    var result = compiler.Compile(source);

    Assert.True(result.Success);
    Assert.Contains("int32_t main(void)", result.GeneratedCode);
}
```

**Benefits:**
- Unit tests are fast (no full pipeline)
- Easy to isolate failures (test one component at a time)
- Better code coverage (can test edge cases per component)
- Faster development (run unit tests frequently)

---

## Summary

These examples show how to refactor the Novus compiler from a monolithic architecture to a clean, modular pipeline:

1. **Extract TypeResolver** - Separate type resolution from IR building
2. **Reorder phases** - Type resolution → Semantic analysis → IR building
3. **Split IrBuilder** - Create focused builders per concern
4. **Add HIR lowering** - Preserve high-level constructs longer
5. **Use ErrorType** - Better error recovery
6. **Unit test components** - Test each phase independently

The result: A more maintainable, testable, and extensible compiler that's easier to evolve as the language grows.

# Reference Lifetime Tracking Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Prevent use-after-free bugs by tracking reference lifetimes at compile time.

**Architecture:** Add a BorrowGraph to track borrow relationships between variables. During semantic analysis, record borrows when references are created and validate at scope exit that no reference outlives its source. Use implicit lifetime inference (Rust-style elision) for method returns.

**Tech Stack:** C# (.NET 9), xUnit for tests, existing SemanticAnalyzer/BorrowChecker infrastructure.

---

## Task 1: Add BorrowGraph Data Structures

**Files:**
- Modify: `Novus.Core/SemanticAnalysis/BorrowChecker.cs`

**Step 1: Write the failing test**

Create `Novus.Tests/BorrowGraphTests.cs`:

```csharp
using Novus.Diagnostics;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

public class BorrowGraphTests
{
    [Fact]
    public void BorrowGraph_AddBorrow_TracksRelationship()
    {
        var graph = new BorrowGraph();
        var loc = new SourceLocation("test.novus", 1, 1, 0, "");

        graph.RegisterVariable(1, "screen", scopeDepth: 1, loc);
        graph.RegisterVariable(2, "rp", scopeDepth: 1, loc);
        graph.AddBorrow(borrowerId: 2, sourceId: 1, loc, mutable: false);

        var chain = graph.GetBorrowChain(2);
        Assert.Contains(1, chain);
    }

    [Fact]
    public void BorrowGraph_TransitiveBorrow_TracksFullChain()
    {
        var graph = new BorrowGraph();
        var loc = new SourceLocation("test.novus", 1, 1, 0, "");

        graph.RegisterVariable(1, "screen", scopeDepth: 1, loc);
        graph.RegisterVariable(2, "rp", scopeDepth: 1, loc);
        graph.RegisterVariable(3, "pen", scopeDepth: 1, loc);
        graph.AddBorrow(borrowerId: 2, sourceId: 1, loc, mutable: false);
        graph.AddBorrow(borrowerId: 3, sourceId: 2, loc, mutable: false);

        var chain = graph.GetBorrowChain(3);
        Assert.Equal(3, chain.Count); // pen -> rp -> screen
        Assert.Equal(3, chain[0]);
        Assert.Equal(2, chain[1]);
        Assert.Equal(1, chain[2]);
    }

    [Fact]
    public void BorrowGraph_GetDanglingBorrows_FindsOutlivingReferences()
    {
        var graph = new BorrowGraph();
        var loc = new SourceLocation("test.novus", 1, 1, 0, "");

        graph.RegisterVariable(1, "screen", scopeDepth: 2, loc);  // Inner scope
        graph.RegisterVariable(2, "rp", scopeDepth: 1, loc);       // Outer scope
        graph.AddBorrow(borrowerId: 2, sourceId: 1, loc, mutable: false);

        var dangling = graph.GetDanglingBorrowsAtScopeExit(scopeDepth: 2);
        Assert.Single(dangling);
        Assert.Equal(2, dangling[0].BorrowerId);
        Assert.Equal(1, dangling[0].SourceId);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test Novus.Tests --filter "FullyQualifiedName~BorrowGraphTests" -v n`
Expected: FAIL - BorrowGraph class doesn't exist

**Step 3: Write minimal implementation**

Add to `Novus.Core/SemanticAnalysis/BorrowChecker.cs`:

```csharp
/// <summary>
/// Represents a borrow relationship in the graph.
/// </summary>
public class BorrowEdge
{
    public int SourceId { get; init; }
    public int BorrowerId { get; init; }
    public SourceLocation BorrowLocation { get; init; } = null!;
    public bool IsMutable { get; init; }
}

/// <summary>
/// Tracks the scope where a variable is valid.
/// </summary>
public class VariableLifetime
{
    public int VariableId { get; init; }
    public string VariableName { get; init; } = "";
    public int ScopeDepth { get; init; }
    public SourceLocation DeclLocation { get; init; } = null!;
    public SourceLocation? DropLocation { get; set; }
}

/// <summary>
/// Represents a dangling borrow detected at scope exit.
/// </summary>
public class DanglingBorrow
{
    public int BorrowerId { get; init; }
    public int SourceId { get; init; }
    public BorrowEdge Edge { get; init; } = null!;
    public VariableLifetime BorrowerLifetime { get; init; } = null!;
    public VariableLifetime SourceLifetime { get; init; } = null!;
}

/// <summary>
/// Tracks borrow relationships between variables for lifetime analysis.
/// Nodes are variables, edges are borrow relationships.
/// </summary>
public class BorrowGraph
{
    private readonly Dictionary<int, List<BorrowEdge>> _borrowsFrom = new();
    private readonly Dictionary<int, VariableLifetime> _lifetimes = new();

    public void RegisterVariable(int variableId, string name, int scopeDepth, SourceLocation declLocation)
    {
        _lifetimes[variableId] = new VariableLifetime
        {
            VariableId = variableId,
            VariableName = name,
            ScopeDepth = scopeDepth,
            DeclLocation = declLocation
        };
        _borrowsFrom[variableId] = new List<BorrowEdge>();
    }

    public void AddBorrow(int borrowerId, int sourceId, SourceLocation location, bool mutable)
    {
        if (!_borrowsFrom.ContainsKey(borrowerId))
            _borrowsFrom[borrowerId] = new List<BorrowEdge>();

        _borrowsFrom[borrowerId].Add(new BorrowEdge
        {
            SourceId = sourceId,
            BorrowerId = borrowerId,
            BorrowLocation = location,
            IsMutable = mutable
        });
    }

    /// <summary>
    /// Gets the full borrow chain for a variable.
    /// Returns [variableId, source1, source2, ...] where each borrows from the next.
    /// </summary>
    public List<int> GetBorrowChain(int variableId)
    {
        var chain = new List<int> { variableId };
        var visited = new HashSet<int> { variableId };
        var current = variableId;

        while (_borrowsFrom.TryGetValue(current, out var edges) && edges.Count > 0)
        {
            var sourceId = edges[0].SourceId;  // Follow first borrow
            if (visited.Contains(sourceId))
                break;  // Prevent cycles
            chain.Add(sourceId);
            visited.Add(sourceId);
            current = sourceId;
        }

        return chain;
    }

    /// <summary>
    /// Finds borrows that will become dangling when the given scope exits.
    /// A borrow is dangling if the borrower outlives the source.
    /// </summary>
    public List<DanglingBorrow> GetDanglingBorrowsAtScopeExit(int scopeDepth)
    {
        var dangling = new List<DanglingBorrow>();

        // Find all variables being dropped at this scope
        var droppedVars = _lifetimes.Values
            .Where(lt => lt.ScopeDepth == scopeDepth)
            .ToList();

        foreach (var dropped in droppedVars)
        {
            // Find anything that borrows from this dropped variable
            foreach (var (borrowerId, edges) in _borrowsFrom)
            {
                foreach (var edge in edges)
                {
                    if (edge.SourceId == dropped.VariableId)
                    {
                        // Check if borrower outlives source
                        if (_lifetimes.TryGetValue(borrowerId, out var borrowerLifetime))
                        {
                            if (borrowerLifetime.ScopeDepth < scopeDepth)
                            {
                                dangling.Add(new DanglingBorrow
                                {
                                    BorrowerId = borrowerId,
                                    SourceId = dropped.VariableId,
                                    Edge = edge,
                                    BorrowerLifetime = borrowerLifetime,
                                    SourceLifetime = dropped
                                });
                            }
                        }
                    }
                }
            }
        }

        return dangling;
    }

    public VariableLifetime? GetLifetime(int variableId)
    {
        return _lifetimes.TryGetValue(variableId, out var lt) ? lt : null;
    }

    public void SetDropLocation(int variableId, SourceLocation location)
    {
        if (_lifetimes.TryGetValue(variableId, out var lt))
            lt.DropLocation = location;
    }

    public void Clear()
    {
        _borrowsFrom.Clear();
        _lifetimes.Clear();
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test Novus.Tests --filter "FullyQualifiedName~BorrowGraphTests" -v n`
Expected: PASS (3 tests)

**Step 5: Commit**

```bash
git add Novus.Core/SemanticAnalysis/BorrowChecker.cs Novus.Tests/BorrowGraphTests.cs
git commit -m "feat: add BorrowGraph for reference lifetime tracking"
```

---

## Task 2: Add Lifetime Inference Logic

**Files:**
- Modify: `Novus.Core/SemanticAnalysis/BorrowChecker.cs`
- Create: `Novus.Tests/LifetimeInferenceTests.cs`

**Step 1: Write the failing test**

Create `Novus.Tests/LifetimeInferenceTests.cs`:

```csharp
using Novus.IR;
using Novus.SemanticAnalysis;
using Novus.Diagnostics;
using Xunit;

namespace Novus.Tests;

public class LifetimeInferenceTests
{
    private SourceLocation Loc => new("test.novus", 1, 1, 0, "");

    [Fact]
    public void InferReturnLifetime_SelfParam_ReturnsSelfId()
    {
        var inference = new LifetimeInference();
        var selfParam = new IrParameter("self", new IrReferenceType(new IrStructType("Screen", new())), 1);
        var otherParam = new IrParameter("depth", IrIntType.I32, 2);
        var returnType = new IrReferenceType(new IrStructType("RastPort", new()));

        var result = inference.InferReturnLifetime(
            new[] { selfParam, otherParam },
            returnType
        );

        Assert.Equal(1, result.SourceParameterId);
        Assert.True(result.Success);
    }

    [Fact]
    public void InferReturnLifetime_SingleRefParam_ReturnsThatParamId()
    {
        var inference = new LifetimeInference();
        var refParam = new IrParameter("screen", new IrReferenceType(new IrStructType("Screen", new())), 1);
        var returnType = new IrReferenceType(new IrStructType("RastPort", new()));

        var result = inference.InferReturnLifetime(
            new[] { refParam },
            returnType
        );

        Assert.Equal(1, result.SourceParameterId);
        Assert.True(result.Success);
    }

    [Fact]
    public void InferReturnLifetime_MultipleRefParams_NoSelf_ReturnsError()
    {
        var inference = new LifetimeInference();
        var param1 = new IrParameter("a", new IrReferenceType(new IrStructType("A", new())), 1);
        var param2 = new IrParameter("b", new IrReferenceType(new IrStructType("B", new())), 2);
        var returnType = new IrReferenceType(new IrStructType("C", new()));

        var result = inference.InferReturnLifetime(
            new[] { param1, param2 },
            returnType
        );

        Assert.False(result.Success);
        Assert.Contains("multiple reference parameters", result.ErrorMessage);
    }

    [Fact]
    public void InferReturnLifetime_NoRefParams_ReturnsError()
    {
        var inference = new LifetimeInference();
        var param = new IrParameter("count", IrIntType.I32, 1);
        var returnType = new IrReferenceType(new IrStructType("Thing", new()));

        var result = inference.InferReturnLifetime(
            new[] { param },
            returnType
        );

        Assert.False(result.Success);
        Assert.Contains("no reference parameters", result.ErrorMessage);
    }

    [Fact]
    public void InferReturnLifetime_NonRefReturn_ReturnsNoLifetimeNeeded()
    {
        var inference = new LifetimeInference();
        var param = new IrParameter("x", IrIntType.I32, 1);
        var returnType = IrIntType.I32;

        var result = inference.InferReturnLifetime(
            new[] { param },
            returnType
        );

        Assert.True(result.Success);
        Assert.Null(result.SourceParameterId);  // No lifetime needed
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test Novus.Tests --filter "FullyQualifiedName~LifetimeInferenceTests" -v n`
Expected: FAIL - LifetimeInference class doesn't exist

**Step 3: Write minimal implementation**

Add to `Novus.Core/SemanticAnalysis/BorrowChecker.cs`:

```csharp
/// <summary>
/// Result of lifetime inference for a method return.
/// </summary>
public class LifetimeInferenceResult
{
    public bool Success { get; init; }
    public int? SourceParameterId { get; init; }
    public string? ErrorMessage { get; init; }

    public static LifetimeInferenceResult Ok(int? sourceId = null) => new()
    {
        Success = true,
        SourceParameterId = sourceId
    };

    public static LifetimeInferenceResult Error(string message) => new()
    {
        Success = false,
        ErrorMessage = message
    };
}

/// <summary>
/// Infers lifetimes for method returns based on Rust-style elision rules.
/// </summary>
public class LifetimeInference
{
    /// <summary>
    /// Infers which parameter's lifetime the return reference should be tied to.
    /// Rules:
    /// 1. If &self exists, return ties to self
    /// 2. If exactly one reference param, return ties to that
    /// 3. If multiple ref params without self, error
    /// 4. If no ref params but returning reference, error
    /// </summary>
    public LifetimeInferenceResult InferReturnLifetime(
        IEnumerable<IrParameter> parameters,
        IrType returnType)
    {
        // Not a reference return - no lifetime needed
        if (returnType is not IrReferenceType and not IrMutReferenceType)
        {
            return LifetimeInferenceResult.Ok(null);
        }

        var paramList = parameters.ToList();
        var refParams = paramList
            .Where(p => p.Type is IrReferenceType or IrMutReferenceType)
            .ToList();

        // Rule 1: &self always wins
        var selfParam = refParams.FirstOrDefault(p => p.Name == "self");
        if (selfParam != null)
        {
            return LifetimeInferenceResult.Ok(selfParam.Id);
        }

        // Rule 2: Exactly one reference param
        if (refParams.Count == 1)
        {
            return LifetimeInferenceResult.Ok(refParams[0].Id);
        }

        // Rule 3: Multiple ref params without self - ambiguous
        if (refParams.Count > 1)
        {
            return LifetimeInferenceResult.Error(
                "cannot infer lifetime: multiple reference parameters without &self");
        }

        // Rule 4: No ref params but returning reference
        return LifetimeInferenceResult.Error(
            "method returns reference but has no reference parameters to borrow from");
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test Novus.Tests --filter "FullyQualifiedName~LifetimeInferenceTests" -v n`
Expected: PASS (5 tests)

**Step 5: Commit**

```bash
git add Novus.Core/SemanticAnalysis/BorrowChecker.cs Novus.Tests/LifetimeInferenceTests.cs
git commit -m "feat: add LifetimeInference for Rust-style elision rules"
```

---

## Task 3: Add Struct Field Reference Restriction

**Files:**
- Modify: `Novus.Core/SemanticAnalysis/SemanticAnalyzer.cs`
- Create: `Novus.Tests/ReferenceLifetimeTests.cs`

**Step 1: Write the failing test**

Create `Novus.Tests/ReferenceLifetimeTests.cs`:

```csharp
using Antlr4.Runtime;
using Novus.Diagnostics;
using Novus.Frontend;
using Novus.Parser;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for reference lifetime tracking.
/// </summary>
public class ReferenceLifetimeTests
{
    private DiagnosticBag Analyze(string source)
    {
        var inputStream = new AntlrInputStream(source);
        var lexer = new NovusLexer(inputStream);
        var tokenStream = new AngleBracketTokenStream(lexer);
        var parser = new NovusParser(tokenStream);
        var tree = parser.compilationUnit();

        var analyzer = new SemanticAnalyzer("test.novus", source, "std");
        analyzer.Analyze(tree);
        return analyzer.Diagnostics;
    }

    private void AssertHasError(string source, string errorCode, string? messageFragment = null)
    {
        var diagnostics = Analyze(source);
        Assert.True(diagnostics.HasErrors, $"Expected error {errorCode} but found none");
        var error = diagnostics.Diagnostics.FirstOrDefault(d => d.Code == errorCode);
        Assert.NotNull(error);

        if (messageFragment != null)
        {
            var fullText = error.Message + " " + string.Join(" ", error.HelpTexts ?? new List<string>());
            Assert.Contains(messageFragment, fullText, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void AssertCompiles(string source)
    {
        var diagnostics = Analyze(source);
        if (diagnostics.HasErrors)
        {
            var errors = string.Join("\n", diagnostics.Diagnostics.Select(d => $"{d.Code}: {d.Message}"));
            Assert.Fail($"Expected no errors but found:\n{errors}");
        }
    }

    // ===== NEGATIVE TESTS: Should produce errors =====

    [Fact]
    public void ReferenceInStruct_ProducesError()
    {
        var code = @"
struct BadCache {
    rp: &i32
}

fn main() {}
";
        AssertHasError(code, "E0106", "cannot contain reference");
    }

    [Fact]
    public void MutableReferenceInStruct_ProducesError()
    {
        var code = @"
struct BadMut {
    data: &var i32
}

fn main() {}
";
        AssertHasError(code, "E0106", "cannot contain reference");
    }

    // ===== POSITIVE TESTS: Should compile =====

    [Fact]
    public void PointerInStruct_Compiles()
    {
        var code = @"
struct GoodCache {
    ptr: *i32
}

fn main() {}
";
        AssertCompiles(code);
    }

    [Fact]
    public void ReferenceAsLocalVariable_Compiles()
    {
        var code = @"
fn test() {
    let x: i32 = 42
    let r: &i32 = &x
}

fn main() {}
";
        AssertCompiles(code);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test Novus.Tests --filter "FullyQualifiedName~ReferenceLifetimeTests.ReferenceInStruct" -v n`
Expected: FAIL - no error E0106 produced (struct with reference currently compiles)

**Step 3: Write minimal implementation**

Find the struct field processing in `SemanticAnalyzer.cs` and add the check. Look for `VisitStructDecl` or similar:

```csharp
// In SemanticAnalyzer.cs, in the struct field processing section
// Add this check when processing each field type:

private void ValidateStructFieldType(string structName, string fieldName, IrType fieldType, SourceLocation location)
{
    if (fieldType is IrReferenceType or IrMutReferenceType)
    {
        var pointeeType = fieldType is IrReferenceType rt ? rt.PointeeType :
                         ((IrMutReferenceType)fieldType).PointeeType;

        _diagnostics.Add(new Diagnostic(
            DiagnosticSeverity.Error,
            "E0106",
            $"struct `{structName}` cannot contain reference field `{fieldName}`",
            location,
            helpTexts: new List<string>
            {
                "references have lifetimes that cannot be expressed in struct fields yet",
                "consider these alternatives:",
                $"  - use a raw pointer: `{fieldName}: *{pointeeType.Name}`",
                $"  - use an owned type instead of a reference",
                $"  - pass the reference as a function parameter instead of storing it"
            }
        ));
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test Novus.Tests --filter "FullyQualifiedName~ReferenceLifetimeTests" -v n`
Expected: PASS (4 tests)

**Step 5: Commit**

```bash
git add Novus.Core/SemanticAnalysis/SemanticAnalyzer.cs Novus.Tests/ReferenceLifetimeTests.cs
git commit -m "feat: reject reference types in struct fields (E0106)"
```

---

## Task 4: Integrate BorrowGraph into SemanticAnalyzer

**Files:**
- Modify: `Novus.Core/SemanticAnalysis/SemanticAnalyzer.cs`
- Modify: `Novus.Core/SemanticAnalysis/BorrowChecker.cs`

**Step 1: Write the failing test**

Add to `Novus.Tests/ReferenceLifetimeTests.cs`:

```csharp
[Fact]
public void ReferenceOutlivesSource_InnerScope_ProducesError()
{
    var code = @"
fn test() {
    let r: &i32
    {
        let x: i32 = 42
        r = &x
    }
    let y = *r
}

fn main() {}
";
    AssertHasError(code, "E0597", "does not live long enough");
}

[Fact]
public void ReferenceInSameScope_Compiles()
{
    var code = @"
fn test() {
    let x: i32 = 42
    let r: &i32 = &x
    let y = *r
}

fn main() {}
";
    AssertCompiles(code);
}

[Fact]
public void ReferenceInInnerScope_SourceInOuter_Compiles()
{
    var code = @"
fn test() {
    let x: i32 = 42
    {
        let r: &i32 = &x
        let y = *r
    }
}

fn main() {}
";
    AssertCompiles(code);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test Novus.Tests --filter "ReferenceOutlivesSource" -v n`
Expected: FAIL - no error E0597 produced

**Step 3: Write minimal implementation**

Modify `BorrowChecker.cs` to expose the BorrowGraph, and modify `SemanticAnalyzer.cs` to:
1. Register variables with the BorrowGraph when declared
2. Record borrows when `&x` is assigned
3. Validate at scope exit

```csharp
// In BorrowChecker.cs, add:
public class BorrowChecker
{
    // ... existing code ...

    public BorrowGraph BorrowGraph { get; } = new();

    public void Reset()
    {
        _movedVariables.Clear();
        _controlFlowStack.Clear();
        BorrowGraph.Clear();
    }
}

// In SemanticAnalyzer.cs:
// 1. When declaring a variable, register it:
//    _borrowChecker.BorrowGraph.RegisterVariable(varId, name, _scopeDepth, location);

// 2. When processing &x (borrow expression), if assigning to variable:
//    _borrowChecker.BorrowGraph.AddBorrow(targetVarId, sourceVarId, location, mutable);

// 3. When exiting a scope, validate:
//    var dangling = _borrowChecker.BorrowGraph.GetDanglingBorrowsAtScopeExit(_scopeDepth);
//    foreach (var d in dangling) EmitLifetimeError(d);
```

**Step 4: Run test to verify it passes**

Run: `dotnet test Novus.Tests --filter "FullyQualifiedName~ReferenceLifetimeTests" -v n`
Expected: PASS (7 tests)

**Step 5: Commit**

```bash
git add Novus.Core/SemanticAnalysis/SemanticAnalyzer.cs Novus.Core/SemanticAnalysis/BorrowChecker.cs Novus.Tests/ReferenceLifetimeTests.cs
git commit -m "feat: integrate BorrowGraph for scope-based lifetime validation"
```

---

## Task 5: Add Method Return Lifetime Tracking

**Files:**
- Modify: `Novus.Core/SemanticAnalysis/SemanticAnalyzer.cs`
- Add tests to: `Novus.Tests/ReferenceLifetimeTests.cs`

**Step 1: Write the failing test**

Add to `Novus.Tests/ReferenceLifetimeTests.cs`:

```csharp
[Fact]
public void MethodReturnReference_OutlivesReceiver_ProducesError()
{
    var code = @"
struct Container {
    value: i32
}

impl Container {
    fn get_ref(&self) -> &i32 {
        return &self.value
    }
}

fn test() {
    let r: &i32
    {
        let c = Container { value: 42 }
        r = c.get_ref()
    }
    let x = *r
}

fn main() {}
";
    AssertHasError(code, "E0597", "does not live long enough");
}

[Fact]
public void MethodReturnReference_SameScope_Compiles()
{
    var code = @"
struct Container {
    value: i32
}

impl Container {
    fn get_ref(&self) -> &i32 {
        return &self.value
    }
}

fn test() {
    let c = Container { value: 42 }
    let r = c.get_ref()
    let x = *r
}

fn main() {}
";
    AssertCompiles(code);
}

[Fact]
public void MethodReturnReference_MultipleRefParams_NoSelf_ProducesError()
{
    var code = @"
fn pick(a: &i32, b: &i32) -> &i32 {
    return a
}

fn main() {}
";
    AssertHasError(code, "E0106", "cannot infer lifetime");
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test Novus.Tests --filter "MethodReturnReference" -v n`
Expected: FAIL - lifetime not tracked through method calls

**Step 3: Write minimal implementation**

In `SemanticAnalyzer.cs`, when processing a method call that returns a reference:
1. Use `LifetimeInference.InferReturnLifetime()` to determine source parameter
2. Map that parameter to the actual argument variable
3. Record a borrow from the result to that argument

```csharp
// When processing method call expression:
var returnLifetime = _lifetimeInference.InferReturnLifetime(method.Parameters, method.ReturnType);
if (returnLifetime.Success && returnLifetime.SourceParameterId.HasValue)
{
    // Find which argument corresponds to that parameter
    var argIndex = method.Parameters.ToList().FindIndex(p => p.Id == returnLifetime.SourceParameterId);
    if (argIndex >= 0 && argIndex < arguments.Count)
    {
        var sourceArgVarId = GetVariableIdFromExpression(arguments[argIndex]);
        if (sourceArgVarId.HasValue)
        {
            // Result borrows from the argument
            // (recorded when result is assigned to a variable)
            _pendingBorrowSource = sourceArgVarId.Value;
        }
    }
}
else if (!returnLifetime.Success)
{
    // Emit the inference error
    EmitError("E0106", returnLifetime.ErrorMessage!, methodLocation);
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test Novus.Tests --filter "FullyQualifiedName~ReferenceLifetimeTests" -v n`
Expected: PASS (10 tests)

**Step 5: Commit**

```bash
git add Novus.Core/SemanticAnalysis/SemanticAnalyzer.cs Novus.Tests/ReferenceLifetimeTests.cs
git commit -m "feat: track lifetimes through method returns with inference"
```

---

## Task 6: Add Unsafe Reference-to-Pointer Conversion

**Files:**
- Modify: `Novus.Core/Frontend/IrBuilder.Expressions.cs`
- Add tests to: `Novus.Tests/ReferenceLifetimeTests.cs`

**Step 1: Write the failing test**

Add to `Novus.Tests/ReferenceLifetimeTests.cs`:

```csharp
[Fact]
public void ReferenceToPointer_OutsideUnsafe_ProducesError()
{
    var code = @"
fn test() {
    let x: i32 = 42
    let r: &i32 = &x
    let p: *i32 = (*i32)r
}

fn main() {}
";
    AssertHasError(code, "E0133", "requires `unsafe`");
}

[Fact]
public void ReferenceToPointer_InsideUnsafe_Compiles()
{
    var code = @"
fn test() {
    let x: i32 = 42
    let r: &i32 = &x
    let p: *i32 = unsafe { (*i32)r }
}

fn main() {}
";
    AssertCompiles(code);
}

[Fact]
public void PointerToPointer_NoUnsafeRequired_Compiles()
{
    var code = @"
fn test() {
    let x: i32 = 42
    unsafe {
        let p1: *i32 = &x
        let p2: *i32 = p1
    }
}

fn main() {}
";
    AssertCompiles(code);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test Novus.Tests --filter "ReferenceToPointer" -v n`
Expected: FAIL - no error E0133 produced for ref-to-ptr outside unsafe

**Step 3: Write minimal implementation**

In `IrBuilder.Expressions.cs`, in the cast handling:

```csharp
// When processing cast expression:
if (sourceType is IrReferenceType or IrMutReferenceType)
{
    if (targetType is IrPointerType)
    {
        if (!_inUnsafeBlock)
        {
            _diagnostics.Add(new Diagnostic(
                DiagnosticSeverity.Error,
                "E0133",
                "converting reference to raw pointer requires `unsafe` block",
                location,
                helpTexts: new List<string>
                {
                    "references have lifetime guarantees that raw pointers do not",
                    "wrap the conversion in `unsafe { ... }` if you can guarantee the pointer remains valid"
                }
            ));
            return IrErrorValue.Instance;
        }
    }
}
```

**Step 4: Run test to verify it passes**

Run: `dotnet test Novus.Tests --filter "FullyQualifiedName~ReferenceLifetimeTests" -v n`
Expected: PASS (13 tests)

**Step 5: Commit**

```bash
git add Novus.Core/Frontend/IrBuilder.Expressions.cs Novus.Tests/ReferenceLifetimeTests.cs
git commit -m "feat: require unsafe for reference-to-pointer conversion (E0133)"
```

---

## Task 7: Add Comprehensive Edge Case Tests

**Files:**
- Add tests to: `Novus.Tests/ReferenceLifetimeTests.cs`

**Step 1: Write additional test cases**

Add to `Novus.Tests/ReferenceLifetimeTests.cs`:

```csharp
// ===== TRANSITIVE BORROW TESTS =====

[Fact]
public void TransitiveBorrow_InnerSourceDropped_ProducesError()
{
    var code = @"
struct Outer {
    inner: Inner
}

struct Inner {
    value: i32
}

impl Outer {
    fn get_inner(&self) -> &Inner {
        return &self.inner
    }
}

impl Inner {
    fn get_value(&self) -> &i32 {
        return &self.value
    }
}

fn test() {
    let v: &i32
    {
        let o = Outer { inner: Inner { value: 42 } }
        let i = o.get_inner()
        v = i.get_value()
    }
    let x = *v
}

fn main() {}
";
    AssertHasError(code, "E0597", "does not live long enough");
}

// ===== CONTROL FLOW TESTS =====

[Fact]
public void ReferenceInIfBranch_SourceInOuter_Compiles()
{
    var code = @"
fn test(cond: bool) {
    let x: i32 = 42
    if cond {
        let r: &i32 = &x
        let y = *r
    }
}

fn main() {}
";
    AssertCompiles(code);
}

[Fact]
public void ReferenceAssignedInBranch_UsedAfter_ProducesError()
{
    var code = @"
fn test(cond: bool) {
    let r: &i32
    if cond {
        let x: i32 = 42
        r = &x
    } else {
        let y: i32 = 99
        r = &y
    }
    let z = *r
}

fn main() {}
";
    AssertHasError(code, "E0597", "does not live long enough");
}

// ===== LOOP TESTS =====

[Fact]
public void ReferenceInLoop_SourceOutside_Compiles()
{
    var code = @"
fn test() {
    let x: i32 = 42
    var i: i32 = 0
    while i < 10 {
        let r: &i32 = &x
        i = i + 1
    }
}

fn main() {}
";
    AssertCompiles(code);
}

// ===== MATCH ARM TESTS =====

[Fact]
public void ReferenceInMatchArm_SourceInOuter_Compiles()
{
    var code = @"
enum Status {
    Ok,
    Error
}

fn test(s: Status) {
    let x: i32 = 42
    match s {
        Status::Ok => {
            let r: &i32 = &x
            let y = *r
        },
        Status::Error => {}
    }
}

fn main() {}
";
    AssertCompiles(code);
}

// ===== RETURN REFERENCE TO LOCAL TESTS =====

[Fact]
public void ReturnReferenceToLocal_ProducesError()
{
    var code = @"
fn bad() -> &i32 {
    let x: i32 = 42
    return &x
}

fn main() {}
";
    AssertHasError(code, "E0515", "cannot return reference to local");
}

// ===== MUTABLE REFERENCE TESTS =====

[Fact]
public void MutableReference_SameScope_Compiles()
{
    var code = @"
fn test() {
    var x: i32 = 42
    let r: &var i32 = &var x
    *r = 100
}

fn main() {}
";
    AssertCompiles(code);
}

[Fact]
public void MutableReferenceOutlivesSource_ProducesError()
{
    var code = @"
fn test() {
    let r: &var i32
    {
        var x: i32 = 42
        r = &var x
    }
    *r = 100
}

fn main() {}
";
    AssertHasError(code, "E0597", "does not live long enough");
}
```

**Step 2: Run all tests**

Run: `dotnet test Novus.Tests --filter "FullyQualifiedName~ReferenceLifetimeTests" -v n`
Expected: PASS (21 tests)

**Step 3: Commit**

```bash
git add Novus.Tests/ReferenceLifetimeTests.cs
git commit -m "test: add comprehensive edge case tests for reference lifetimes"
```

---

## Task 8: Update stdlib to Use References

**Files:**
- Modify: `Novus/std/ui/screen.novus`

**Step 1: Write a test for the real use case**

Add to `Novus.Tests/ReferenceLifetimeTests.cs`:

```csharp
[Fact]
public void ScreenHandle_RastPort_ReferenceLifetime_Compiles()
{
    // This is the homepage example - should compile
    var code = @"
from amiga::sys::intuition import ScreenHandle
from amiga::raw::graphics import SetAPen, RectFill

pub fn main() -> i32 {
    let result = ScreenHandle::lores(""Demo Screen"", 5)

    match result {
        Result::Ok(screen) => {
            let rp = screen.rastport()
            SetAPen(rp, 2)
            RectFill(rp, 10, 20, 100, 80)
            return 0
        },
        Result::Err(_) => {
            return 1
        }
    }
}
";
    AssertCompiles(code);
}
```

**Step 2: Change rastport() return type**

In `Novus/std/ui/screen.novus`, change:
```novus
// FROM:
pub fn rastport(&self) -> *RastPort {
    unsafe {
        let rp_ref = &(*self.screen).RastPort
        return (*RastPort)rp_ref
    }
}

// TO:
pub fn rastport(&self) -> &RastPort {
    unsafe {
        return &(*self.screen).RastPort
    }
}
```

**Step 3: Run all tests including integration**

Run: `dotnet test Novus.Tests -v n`
Expected: All tests pass including the new homepage example test

**Step 4: Compile homepage example to verify**

Run: `dotnet run --project Novus -- compile Novus.Tests/Examples/homepage_example.novus -o /tmp/homepage_test`
Expected: Compiles successfully

**Step 5: Commit**

```bash
git add Novus/std/ui/screen.novus Novus.Tests/ReferenceLifetimeTests.cs
git commit -m "feat: change ScreenHandle::rastport to return reference with lifetime"
```

---

## Task 9: Full Test Suite Verification

**Step 1: Run complete test suite**

Run: `dotnet test Novus.Tests -v n`
Expected: All tests pass

**Step 2: Compile all examples**

Run:
```bash
for f in Novus.Tests/Examples/*.novus; do
    echo "=== $f ==="
    dotnet run --project Novus -- compile "$f" -o /tmp/$(basename "$f" .novus) 2>&1 | head -5
done
```
Expected: All examples compile (or expected errors for intentionally broken examples)

**Step 3: Final commit**

```bash
git add -A
git commit -m "feat: complete reference lifetime tracking implementation

- BorrowGraph tracks borrow relationships between variables
- LifetimeInference implements Rust-style elision rules
- Scope-based validation detects dangling references (E0597)
- References in struct fields rejected (E0106)
- Reference-to-pointer requires unsafe (E0133)
- Return reference to local rejected (E0515)
- 22 comprehensive tests covering positive and negative cases
- ScreenHandle::rastport() now returns safe reference"
```

---

## Summary

**Tests created:** 22 test cases covering:
- Positive: valid reference usage patterns that should compile
- Negative: lifetime violations that should produce specific errors

**Error codes added:**
- `E0597` - borrowed value does not live long enough
- `E0106` - cannot contain reference / cannot infer lifetime
- `E0133` - unsafe required for reference-to-pointer
- `E0515` - cannot return reference to local variable

**Files modified:**
- `Novus.Core/SemanticAnalysis/BorrowChecker.cs` - BorrowGraph, LifetimeInference
- `Novus.Core/SemanticAnalysis/SemanticAnalyzer.cs` - integration
- `Novus.Core/Frontend/IrBuilder.Expressions.cs` - unsafe check
- `Novus/std/ui/screen.novus` - return reference instead of pointer
- `Novus.Tests/BorrowGraphTests.cs` - unit tests for graph
- `Novus.Tests/LifetimeInferenceTests.cs` - unit tests for inference
- `Novus.Tests/ReferenceLifetimeTests.cs` - integration tests

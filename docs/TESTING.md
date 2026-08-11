# Novus Compiler - Testing Documentation

## Test Suite Overview

The Novus compiler has a comprehensive test suite with **77 passing tests** covering all major components of the compilation pipeline.

### Test Structure

```
Novus.Tests/
├── ParserTests.cs          # 24 tests - Lexer and parser validation
├── IrBuilderTests.cs       # 19 tests - AST to IR conversion
├── CodeGeneratorTests.cs   # 20 tests - 68k assembly generation
└── EndToEndTests.cs        # 14 tests - Full compilation pipeline
```

## Test Categories

### 1. Parser Tests (24 tests)

**Purpose:** Validate that Novus source code is correctly parsed into an AST.

**Coverage:**
- Function declarations with parameters and return types
- Arithmetic expressions (+, -, *, /, %)
- Comparison operators (==, !=, <, >, <=, >=)
- Integer literals with type suffixes (u8, u16, i32, etc.)
- Negative literals
- Let statements with and without type annotations
- Comments (line and block)
- Nested expressions
- Multiple functions
- Error detection for invalid syntax

**Example Tests:**
```csharp
[Fact]
public void Parse_SimpleFunction_Success()
{
    var source = @"
fn main() -> u32 {
    return 42
}";
    var parser = CreateParser(source);
    var tree = parser.compilationUnit();

    Assert.Equal(0, parser.NumberOfSyntaxErrors);
}

[Fact]
public void Parse_FunctionWithParameters_Success()
{
    var source = @"
fn add(a: i32, b: i32) -> i32 {
    return a + b
}";
    // Validates parameter parsing
}
```

### 2. IR Builder Tests (19 tests)

**Purpose:** Verify correct conversion from AST to Intermediate Representation.

**Coverage:**
- Function creation with correct names and types
- Parameter extraction and typing
- Return instructions
- Binary operations (add, sub, mul, div, mod)
- Type preservation through IR
- Temporary variable generation
- Constant folding
- Expression chaining

**Example Tests:**
```csharp
[Fact]
public void BuildIr_SimpleReturn_CreatesFunction()
{
    var module = BuildIr("fn main() -> u32 { return 42 }");

    Assert.Single(module.Functions);
    Assert.Equal("main", module.Functions[0].Name);
    Assert.Equal("u32", module.Functions[0].ReturnType.Name);
}

[Fact]
public void BuildIr_Addition_CreatesBinaryOp()
{
    var module = BuildIr("fn test() -> u32 { return 10 + 20 }");

    var binOp = module.Functions[0].BasicBlocks[0].Instructions[0];
    Assert.IsType<IrBinaryOp>(binOp);
    Assert.Equal(IrBinaryOp.OpKind.Add, binOp.Operation);
}
```

### 3. Code Generator Tests (20 tests)

**Purpose:** Ensure correct 68k assembly is generated from IR.

**Coverage:**
- Proper vasm syntax (section, xdef)
- CPU target headers (68020, 68040, etc.)
- Return value in d0 (Amiga ABI compliance)
- moveq optimization for small constants
- Signed vs unsigned instructions (muls vs mulu)
- 68020 baseline vs newer instruction selection
- Comment generation for debugging
- Multiple functions
- Arithmetic operations

**Example Tests:**
```csharp
[Fact]
public void Generate_ReturnSmallConstant_UsesMoveq()
{
    var asm = GenerateAssembly("fn main() -> u32 { return 10 }");

    // Should use moveq optimization
    Assert.Contains("moveq\t#10,d0", asm);
}

[Fact]
public void Generate_UnsignedMultiply_UsesMulu()
{
    var asm = GenerateAssembly(
        "fn test() -> u32 { return 5u32 * 6u32 }",
        "68020");

    Assert.Contains("mulu.l\td1,d0", asm);
}
```

### 4. End-to-End Tests (14 tests)

**Purpose:** Validate the complete compilation pipeline from source to assembly.

**Coverage:**
- Full compilation success
- CPU target differences
- Signed vs unsigned type handling
- Operator precedence
- moveq optimization in context
- Multiple type sizes
- Complex expressions
- Error handling

**Example Tests:**
```csharp
[Theory]
[InlineData("68020")]
[InlineData("68020")]
[InlineData("68060")]
public void Compile_AllCPUTargets_Success(string cpuTarget)
{
    var asm = CompileToAssembly("fn main() -> u32 { return 42 }", cpuTarget);

    Assert.NotEmpty(asm);
    Assert.Contains(cpuTarget.ToUpper(), asm);
}

[Fact]
public void Compile_ComplexExpression_Success()
{
    var asm = CompileToAssembly(
        "fn calculate() -> u32 { return (10 + 20) * 2 }");

    Assert.Contains("add.l", asm);
    Assert.Contains("mul", asm);
}
```

## Running Tests

### Run All Tests
```bash
dotnet test
```

### Run Specific Test Class
```bash
dotnet test --filter "FullyQualifiedName~ParserTests"
dotnet test --filter "FullyQualifiedName~IrBuilderTests"
dotnet test --filter "FullyQualifiedName~CodeGeneratorTests"
dotnet test --filter "FullyQualifiedName~EndToEndTests"
```

### Run Specific Test
```bash
dotnet test --filter "FullyQualifiedName~Generate_ReturnSmallConstant_UsesMoveq"
```

### Run with Verbose Output
```bash
dotnet test --logger "console;verbosity=detailed"
```

## Test Results Summary

| Test Suite | Tests | Status |
|------------|-------|--------|
| ParserTests | 24 | ✅ All Passing |
| IrBuilderTests | 19 | ✅ All Passing |
| CodeGeneratorTests | 20 | ✅ All Passing |
| EndToEndTests | 14 | ✅ All Passing |
| **Total** | **77** | **✅ All Passing** |

## Key Test Insights

### 1. Type System Validation
Tests verify that:
- Signed types use `muls`, `divs`
- Unsigned types use `mulu`, `divu`
- Type suffixes (u8, i32, etc.) are correctly parsed
- Type information flows through IR to codegen

### 2. CPU Target Awareness
Tests validate:
- 68020: Uses native 32-bit operations
- 68020+: Uses native 32-bit multiply/divide
- Target-specific header generation

### 3. Optimization Verification
Tests confirm:
- `moveq` is used for constants -128 to +127
- Proper instruction sizing (.b, .w, .l)
- Efficient register usage

### 4. Amiga ABI Compliance
Tests ensure:
- Return values in d0
- Proper function prologue/epilogue
- vasm-compatible syntax

## Test Maintenance

### Adding New Tests

1. **Parser Changes:** Add tests to `ParserTests.cs`
2. **IR Changes:** Add tests to `IrBuilderTests.cs`
3. **Codegen Changes:** Add tests to `CodeGeneratorTests.cs`
4. **New Features:** Add end-to-end tests to `EndToEndTests.cs`

### Test Naming Convention

```csharp
[Category]_[Scenario]_[ExpectedBehavior]

// Examples:
Parse_SimpleFunction_Success
BuildIr_Addition_CreatesBinaryOp
Generate_ReturnSmallConstant_UsesMoveq
Compile_ComplexExpression_Success
```

### Test Organization

- **Arrange**: Set up test data (source code)
- **Act**: Run the compiler component
- **Assert**: Verify expected output

```csharp
[Fact]
public void TestName()
{
    // Arrange
    var source = "fn test() -> u32 { return 42 }";

    // Act
    var result = CompileToAssembly(source);

    // Assert
    Assert.Contains("moveq\t#42,d0", result);
}
```

## Test Coverage Goals

### Current Coverage ✅
- Basic arithmetic operations
- Type system
- CPU targets
- Function declarations
- Return statements

### Future Coverage 🔄
- Control flow (if/else, loops)
- Function calls
- Local variables
- Arrays and structures
- Pointers
- AmigaOS library calls
- Async/await
- Hardware DSLs

## Continuous Integration

Tests should be run:
- Before every commit
- On pull requests
- In CI/CD pipeline
- After dependency updates

## Test Performance

Current test execution time: **~50ms**

- Fast feedback loop
- Suitable for TDD workflow
- Can run on every save

## Debugging Failed Tests

### View Test Output
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run Single Test with Debug
```bash
dotnet test --filter "TestName" --logger "console;verbosity=detailed"
```

### Common Failure Patterns

1. **Assembly mismatch**: Check generated vs expected assembly
2. **Type errors**: Verify type suffixes in test literals
3. **CPU target**: Ensure correct target for instruction set

## Test Quality Metrics

- ✅ Clear, descriptive test names
- ✅ One assertion per logical concept
- ✅ Isolated tests (no dependencies)
- ✅ Fast execution (<1s total)
- ✅ Comprehensive coverage of critical paths

## Contributing Tests

When adding features:
1. Write tests first (TDD)
2. Ensure all existing tests pass
3. Add tests for error cases
4. Document complex test scenarios

---

**Last Updated:** 2025-10-25
**Test Suite Version:** 1.0
**Total Tests:** 77
**Pass Rate:** 100%

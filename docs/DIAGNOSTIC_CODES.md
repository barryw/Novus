# Novus Diagnostic Code Registry

This document lists all diagnostic error and warning codes used in the Novus compiler.

## Code Format

All diagnostic codes follow the format `Xyyyy` where:
- `X` is either `E` (error) or `W` (warning)
- `yyyy` is a 4-digit decimal number

## Code Ranges

### Error Codes

| Range | Category | Description |
|-------|----------|-------------|
| E0001-E0099 | Parser & Basic Semantics | Parse errors, syntax errors, basic semantic errors |
| E0100-E0999 | Core Semantics | Type errors, name resolution, general semantic analysis |
| E1000-E1999 | Safety & Unsafe | Safety violations, unsafe block requirements |
| E2000-E2999 | Type System | Type inference, type checking, conversions, generics |
| E3000-E3999 | Control Flow | Pattern matching, exhaustiveness, reachability |
| E4000-E4999 | Static Analysis | SSA-based analysis (unused vars, uninitialized vars) |
| E9000-E9999 | Preprocessor | Preprocessor directive errors |

### Warning Codes

| Range | Category | Description |
|-------|----------|-------------|
| W0001-W0999 | Basic Warnings | Type mixing, unreachable code, loss of precision |
| W2000-W2999 | Attributes | Unknown attributes, attribute validation |
| W4000-W4999 | Static Analysis | SSA-based warnings (dead stores, redundant assignments) |

## Defined Codes

### Errors

#### E0xxx - Parser & Basic Semantics
- **E0001**: Parse error (syntax error from ANTLR)
- **E0026**: Module not found during import
- **E0027**: Import resolution error
- **E0028**: Import path error
- **E0050**: Circular import detected
- **E0054**: Type name already defined
- **E0099**: Reserved for future use
- **E0999**: Array literal errors (empty arrays, invalid repeat counts)

#### E1xxx - Safety & Unsafe
- **E1001**: Operation requires unsafe block

#### E2xxx - Type System
- E2001-E2904: Type inference, checking, conversions (see SemanticAnalyzer.cs)

#### E3xxx - Control Flow
- E3000-E3404: Pattern matching, control flow analysis (see SemanticAnalyzer.cs)

#### E4xxx - Static Analysis (SSA)
- **E4001**: Variable may be used before it is initialized

#### E9xxx - Preprocessor
- **E9001**: `#elif` without matching `#if`
- **E9002**: `#else` without matching `#if`
- **E9003**: `#endif` without matching `#if`
- **E9004**: Unmatched `#if` directive (unclosed block)
- **E9005**: `#if` requires exactly one constant name
- **E9006**: `#elif` requires exactly one constant name
- **E9007**: `#else` takes no arguments
- **E9008**: `#endif` takes no arguments
- **E9009**: Unknown preprocessor directive
- **E9010**: Undefined preprocessor constant

### Warnings

#### W0xxx - Basic Warnings
- **W0001**: Mixing signed and unsigned types in arithmetic operation
- **W0002**: Cast may lose precision
- **W0003**: Unreachable code detected

#### W2xxx - Attributes
- **W2001**: Unknown attribute

#### W4xxx - Static Analysis (SSA)
- **W4001**: Variable is assigned but never used
- **W4002**: Variable is declared but never used
- **W4003**: Variable is assigned a value that is never used (redundant assignment)

## Adding New Diagnostic Codes

When adding a new diagnostic code:

1. Choose the appropriate range based on the error category
2. Find the next available code in that range
3. Add the code to this document
4. Use the code consistently in all error messages
5. Update tests to check for the new code

## Notes

- All codes must be unique across the entire codebase
- Codes should never be reused even if the original diagnostic is removed
- The 4-digit format allows for 9999 codes per category (E/W)
- Ranges are organized to allow for future expansion within each category

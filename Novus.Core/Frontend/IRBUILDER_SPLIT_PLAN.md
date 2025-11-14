# IrBuilder.cs Splitting Plan

## Current Status

- **File Size**: 11,163 lines (459 KB)
- **Status**: Single file, marked as `partial class` (ready for splitting)
- **Methods**: ~140 methods across 7 major categories
- **Risk Level**: HIGH - Core compiler infrastructure, must not break 1483 tests

## Structure Analysis

### Method Categories (by line range and count)

1. **Import & Module Processing** (lines 882-2516, ~1634 lines, ~20 methods)
   - ProcessImport
   - ImportModule, ImportModuleSpecificSymbols
   - RegisterAllEnumStubsForImport, RegisterEnumStubsForImport, FillEnumVariantsForImport
   - RegisterAllStructPlaceholdersForImport, RegisterStructPlaceholdersForImport, FillStructFieldsForImport
   - RegisterConstantsForImport, RegisterTraitsForImport, RegisterFunctionsForImport
   - ExpandStructDependencies, Parse FunctionParameters, ParseSelfParameter

2. **Generic Instantiation** (lines 1439-2248, ~809 lines, ~10 methods)
   - InstantiateGenericMethod, InstantiateGenericEnumMethod
   - Instantiate GenericFunction
   - MonomorphizeEnum, SubstituteType, SubstituteGenericTypes
   - InferGenericFunctionTypes, InferGenericEnumTypeArguments
   - BuildGenericFunctionMangledName, ExtractGenericTypeMapping

3. **Declaration Registration** (lines 2619-3062, ~443 lines, ~10 methods)
   - RegisterConstant, RegisterStatic, RegisterExternalVariable
   - RegisterEnum, FillEnumVariants
   - RegisterStruct, RegisterTrait
   - StoreGenericMethodTemplate, GenerateMethodMangledName
   - ParseAttributesSimple, ParseWhereClause, ParseTraitBound

4. **Statement Visitors** (lines 3062-5037, ~1975 lines, ~20 methods)
   - VisitFunctionDeclaration, VisitBlock
   - VisitReturnStatement, VisitVariableDeclaration
   - VisitAssignmentStatement, ProcessAssignmentStatement
   - VisitExpressionStatement
   - VisitIfStatement, VisitIfConditionExpression, VisitIfConditionLet, VisitIfConditionVar
   - VisitWhileStatement, VisitForCStyle, VisitForInLoop, VisitForeverStatement
   - VisitBreakStatement, VisitDeferBlock, VisitDeferExpression
   - VisitAssertStatement, VisitPanicStatement
   - HandleTupleDestructuring

5. **Expression Visitors** (lines 5037-9485, ~4448 lines, ~50 methods)
   - VisitPrimaryExpr, VisitCallExpr (HUGE - ~1600 lines)
   - TryResolveGenericFromMethodCall, HandleMethodCallIr (complex method call logic)
   - VisitBorrowExpr, VisitIndexExpr
   - VisitArrayLiteral, VisitArrayRepeatLiteral
   - Binary operators: VisitAdditiveExpr, VisitMultiplicativeExpr, VisitShiftExpr, VisitBitwiseAndExpr, etc.
   - VisitComparisonExpr, VisitUnaryExpr
   - Visit PostIncrementExpr, VisitPostDecrementExpr, VisitPreIncrementExpr, VisitPreDecrementExpr
   - VisitTryExpr, VisitLogicalAndExpr, VisitLogicalOrExpr, VisitTernaryExpr
   - Literal visitors: VisitFloatLiteral, VisitIntegerLiteral, VisitBinaryLiteral, VisitHexLiteral, VisitBoolLiteral, VisitNullLiteral
   - VisitStringLiteral, VisitInterpolatedStringLiteral (with complex formatting logic)
   - VisitSizeofExpr, VisitIdentifierExpr, VisitSelfExpr
   - VisitParenExpr, VisitUnitLiteral, VisitTupleLiteral
   - VisitStructLiteral, VisitStructArrayInit
   - VisitMemberAccessExpr, VisitTurboFishExpr, VisitPathExpr

6. **Pattern Matching** (lines 9485-10016, ~531 lines, ~4 methods)
   - ExpandedMatchArm (nested class)
   - FlattenPipePattern, ExpandMatchArms
   - VisitMatchExpr (complex pattern matching lowering)

7. **Type & Helper Methods** (lines 10016-11163, ~1147 lines, ~25 methods)
   - ParseType, MapPrimitiveTypeName
   - ContainsGenericTypes, TypesAreEqual, TypeContainsGeneric
   - SubstituteGenericTypes, GetTypeCacheKey
   - ParseIntegerLiteral, ParseFloatLiteral, ParseBinaryLiteral, ParseHexLiteral
   - ParseTypeFromMangledName, GetMangledTypeName
   - EnsureDropMethodInstantiated, TypeHasDropMethod
   - PushDeferScope, PopDeferScope, InjectAutomaticDrop
   - ExtractGenericTypeMapping, ParseWhereClause, ParseTraitBound
   - ExtractTypeNameDependencies, IsPrimitiveTypeName
   - TypesEqual, GetTypeName, TryConvertViaFromTrait
   - HandlePostIncrementDecrement, StoreToLvalue, HandlePreIncrementDecrement
   - ProcessEscapeSequences, etc.

## Recommended Splitting Strategy

### Phase 1: Low-Risk Extractions (Do First)

These are self-contained, have few dependencies:

1. **IrBuilder.PatternMatching.cs** (~531 lines)
   - Self-contained pattern matching logic
   - Only depends on core visitor infrastructure
   - Minimal coupling to other methods

2. **IrBuilder.TypeHelpers.cs** (~600 lines)
   - Type parsing and validation methods
   - Utility functions used throughout
   - Clear boundaries

3. **IrBuilder.DropHelpers.cs** (~200 lines)
   - RAII/defer management
   - Self-contained scope management
   - Clear ownership

### Phase 2: Medium-Risk Extractions

4. **IrBuilder.Declarations.cs** (~443 lines)
   - Registration methods for structs, enums, traits, constants
   - Some interdependencies with import logic
   - Requires careful ordering

5. **IrBuilder.Imports.cs** (~1634 lines)
   - Largest single category
   - Complex dependencies on declaration methods
   - Must extract after Declarations

### Phase 3: High-Risk Extractions (Do Last)

6. **IrBuilder.Generics.cs** (~809 lines)
   - Complex type substitution logic
   - Used by both imports and expressions
   - Many cross-cutting concerns

7. **IrBuilder.Statements.cs** (~1975 lines)
   - Statement visitor methods
   - Depends on expression visitors
   - Complex control flow logic

8. **IrBuilder.Expressions.cs** (~4448 lines)
   - Largest and most complex category
   - VisitCallExpr alone is ~1600 lines
   - Central to entire IR builder
   - Should be split LAST or kept as-is

## Extraction Process (Per File)

For each partial class extraction:

1. **Prepare**
   - Create backup: `cp IrBuilder.cs IrBuilder.cs.backup`
   - Identify exact line ranges for extraction
   - List all dependencies (methods called, fields accessed)

2. **Extract**
   - Create new file: `IrBuilder.{Category}.cs`
   - Copy header (usings, namespace, class declaration with `partial`)
   - Copy methods with exact formatting
   - Add XML doc comment explaining category

3. **Remove**
   - Delete extracted methods from main file
   - Keep all fields, constructors, and core infrastructure in main file
   - Preserve exact line formatting (no reformatting!)

4. **Verify**
   - Compile: `dotnet build -c Release`
   - Run all tests: `dotnet test --no-build`
   - Check test count: Must still be 1483 tests passing
   - If any failures, restore backup and investigate

5. **Commit**
   - Create atomic commit for this extraction only
   - Clear commit message: "refactor: extract IrBuilder.{Category} partial class"

## Key Constraints

- **DO NOT** modify any method signatures
- **DO NOT** change any field declarations
- **DO NOT** reformat or "clean up" code
- **DO NOT** combine multiple extractions in one commit
- **DO** keep all fields in the main IrBuilder.cs file
- **DO** test after every single extraction
- **DO** commit after each successful extraction
- **DO** be prepared to roll back if tests fail

## Progress Tracking

- [ ] Phase 0: Mark class as `partial` ✅ (DONE)
- [ ] Phase 1.1: Extract IrBuilder.PatternMatching.cs
- [ ] Phase 1.2: Extract IrBuilder.TypeHelpers.cs
- [ ] Phase 1.3: Extract IrBuilder.DropHelpers.cs
- [ ] Phase 2.1: Extract IrBuilder.Declarations.cs
- [ ] Phase 2.2: Extract IrBuilder.Imports.cs
- [ ] Phase 3.1: Extract IrBuilder.Generics.cs
- [ ] Phase 3.2: Extract IrBuilder.Statements.cs
- [ ] Phase 3.3: Consider splitting IrBuilder.Expressions.cs (or leave as-is)

## Expected Final State

After all extractions:

- **IrBuilder.cs**: ~3000 lines (fields, constructors, BuildModule, core infrastructure)
- **IrBuilder.PatternMatching.cs**: ~530 lines
- **IrBuilder.TypeHelpers.cs**: ~600 lines
- **IrBuilder.DropHelpers.cs**: ~200 lines
- **IrBuilder.Declarations.cs**: ~440 lines
- **IrBuilder.Imports.cs**: ~1600 lines
- **IrBuilder.Generics.cs**: ~800 lines
- **IrBuilder.Statements.cs**: ~2000 lines
- **IrBuilder.Expressions.cs**: ~4400 lines (or split further)

**Total**: Same 11,163 lines, but split across 8-9 files for maintainability.

## Tools

A Python script `/tmp/smart_split_irbuilder.py` has been created to automate extraction, but needs refinement. Manual extraction is safer for critical infrastructure.

## Notes

- This file is generated as part of addressing technical debt
- The class has been marked as `partial` but extraction has not yet been performed
- All 1483 tests currently pass
- Proceed with caution and test thoroughly after each extraction

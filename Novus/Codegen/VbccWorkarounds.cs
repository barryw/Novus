namespace Novus.Codegen;

/// <summary>
/// Registry of VBCC optimizer workarounds implemented in the code generator.
///
/// Each workaround addresses a specific VBCC bug or limitation. This registry serves as:
/// 1. Documentation of known issues
/// 2. Version tracking for when workarounds can be removed
/// 3. Reference for debugging compilation issues
///
/// When updating VBCC, check if any workarounds can be removed by testing against
/// the regression tests in Novus.Tests/VbccRegressionTests.cs
/// </summary>
public static class VbccWorkarounds
{
    /// <summary>
    /// The minimum tested VBCC version. Workarounds are verified against this version.
    /// </summary>
    public const string MinTestedVersion = "0.9h";

    /// <summary>
    /// The maximum tested VBCC version. Update this when testing with newer versions.
    /// </summary>
    public const string MaxTestedVersion = "0.9j";

    /// <summary>
    /// VBCC_001: Condition flag clobbering in conditional branches
    ///
    /// AFFECTED VERSIONS: VBCC 0.9h and earlier (possibly fixed in 0.9i+, untested)
    ///
    /// PROBLEM: VBCC's optimizer can move stack cleanup instructions between a comparison
    /// result store and the subsequent conditional branch, clobbering the CPU condition flags.
    ///
    /// GENERATED CODE (problematic):
    /// <code>
    ///   cmp.l d0,d1        ; Compare values
    ///   seq d2             ; Store condition result (equal) in d2
    ///   addq.l #4,sp       ; Stack cleanup INSERTED HERE - clobbers condition flags!
    ///   tst.b d2           ; Test d2 (but flags already wrong)
    ///   beq .L1            ; Branch uses clobbered flags
    /// </code>
    ///
    /// SOLUTION: Inline comparison expressions directly into if() statements instead of
    /// storing to intermediate variables. Track inlineable comparisons in EmissionContext.
    ///
    /// IMPLEMENTATION:
    /// - CCodeGenerator tracks comparison results in _inlineableComparisons dictionary
    /// - EmitConditionalBranch checks dictionary and inlines expressions
    /// - EmissionContext.InlineableComparisons provides the storage
    ///
    /// TEST COVERAGE:
    /// - Novus.Tests/Examples/13_bool_types.novus
    /// - Novus.Tests/CCodeGeneratorIntegrationTests.cs
    ///
    /// TO VERIFY FIX: Test with VBCC [version] by compiling examples without
    /// the workaround and running on real hardware or UAE.
    /// </summary>
    public const string VBCC_001_ConditionFlagClobbering = "VBCC_001";

    /// <summary>
    /// VBCC_002: Struct-by-value assignment generates broken code
    ///
    /// AFFECTED VERSIONS: VBCC 0.9h through 0.9j (all tested versions)
    ///
    /// PROBLEM: VBCC cannot properly compile struct-by-value assignment for structs
    /// containing certain field layouts. The generated code may copy incorrect bytes
    /// or fail to compile entirely.
    ///
    /// GENERATED CODE (problematic):
    /// <code>
    ///   MyStruct s = other_struct;  // May generate incorrect copy sequence
    /// </code>
    ///
    /// SOLUTION: Use __novus_memcpy() for all struct copies instead of direct assignment.
    /// This also provides strict aliasing safety as a bonus.
    ///
    /// IMPLEMENTATION:
    /// - CCodeGenerator.EmitStructCopy uses memcpy pattern
    /// - All IrStore instructions for struct types use memcpy
    ///
    /// TEST COVERAGE:
    /// - Novus.Tests/StructOperationsTests.cs
    /// - Novus.Tests/Examples/struct_literal_test.novus
    /// </summary>
    public const string VBCC_002_StructByValueAssignment = "VBCC_002";

    /// <summary>
    /// VBCC_003: Out-parameter pattern for struct/enum return values
    ///
    /// AFFECTED VERSIONS: All VBCC versions (design limitation, not a bug)
    ///
    /// PROBLEM: VBCC has limited support for returning structs by value, especially
    /// for larger structs. The calling convention may not match expectations.
    ///
    /// SOLUTION: Use an out-parameter pattern where functions that return structs/enums
    /// instead take a pointer parameter and write to it, returning void.
    ///
    /// GENERATED CODE:
    /// <code>
    /// // Novus: fn create() -> MyStruct { ... }
    /// // C:     void create(MyStruct* __out) { ... *__out = result; }
    /// </code>
    ///
    /// IMPLEMENTATION:
    /// - CCodeGenerator.EmitFunctionSignature adds __out parameter
    /// - Return statements write to __out instead of returning directly
    /// - Call sites pass address of result variable
    ///
    /// TEST COVERAGE:
    /// - Extensive coverage across all struct/enum returning functions
    /// </summary>
    public const string VBCC_003_OutParameterPattern = "VBCC_003";

    /// <summary>
    /// VBCC_004: Compound literal alignment on 68k
    ///
    /// AFFECTED VERSIONS: VBCC 0.9h (possibly others)
    ///
    /// PROBLEM: Compound literals like (Type){...} may have incorrect alignment on 68k,
    /// causing bus errors when accessed. This is particularly problematic for structs
    /// with 32-bit or 64-bit fields.
    ///
    /// SOLUTION: Avoid compound literals entirely. Use static const temporaries or
    /// element-by-element initialization instead.
    ///
    /// IMPLEMENTATION:
    /// - CCodeGenerator.EmitSafeArrayCopy uses element-by-element for small arrays
    /// - Large arrays use memcpy from static const arrays
    /// - Struct initialization uses explicit field assignment
    ///
    /// TEST COVERAGE:
    /// - Novus.Tests/ArrayTests.cs
    /// - Novus.Tests/StructOperationsTests.cs
    /// </summary>
    public const string VBCC_004_CompoundLiteralAlignment = "VBCC_004";

    /// <summary>
    /// Checks if a specific workaround is needed for the given VBCC version.
    /// Returns true if the workaround should be applied.
    /// </summary>
    /// <param name="workaroundId">The workaround identifier (e.g., VBCC_001)</param>
    /// <param name="vbccVersion">The VBCC version string (e.g., "0.9h")</param>
    /// <returns>True if the workaround is needed, false if it can be skipped</returns>
    public static bool IsWorkaroundNeeded(string workaroundId, string? vbccVersion)
    {
        // For now, always apply all workarounds since we don't have version-specific testing
        // In the future, this could check version ranges and skip workarounds for fixed versions
        return true;
    }

    /// <summary>
    /// Gets a human-readable description of a workaround for error messages and diagnostics.
    /// </summary>
    public static string GetDescription(string workaroundId)
    {
        return workaroundId switch
        {
            VBCC_001_ConditionFlagClobbering => "Inline comparisons to avoid condition flag clobbering",
            VBCC_002_StructByValueAssignment => "Use memcpy for struct copies",
            VBCC_003_OutParameterPattern => "Out-parameter pattern for struct/enum returns",
            VBCC_004_CompoundLiteralAlignment => "Avoid compound literals for 68k alignment",
            _ => $"Unknown workaround: {workaroundId}"
        };
    }
}

using System.Text;
using Novus.IR;

namespace Novus.Codegen;

/// <summary>
/// Encapsulates per-function state during C code generation.
/// This class isolates mutable state that varies per function, making the code generator
/// more testable and enabling potential future parallel compilation.
/// </summary>
/// <remarks>
/// The EmissionContext addresses several architectural issues:
/// 1. State isolation: Each function gets its own context, preventing state leakage
/// 2. Testability: Context can be created independently for unit testing
/// 3. Thread safety: With isolated contexts, functions could be generated in parallel
/// 4. Clarity: Groups related state together with clear ownership
/// </remarks>
public sealed class EmissionContext
{
    /// <summary>
    /// The function currently being emitted.
    /// </summary>
    public IrFunction Function { get; }

    /// <summary>
    /// Track which variables have been declared in the current function
    /// to avoid redeclaration errors when the same variable is assigned in multiple branches.
    /// </summary>
    public HashSet<string> DeclaredVariables { get; } = new();

    /// <summary>
    /// Track which function parameters were converted to pointers in the C signature
    /// (due to TypeContainsHeapData) so we don't add &amp; when passing them to other functions.
    /// </summary>
    public HashSet<string> PointerConvertedParameters { get; } = new();

    /// <summary>
    /// Track member access instructions for move semantic analysis.
    /// Maps result variable name -> (source struct, field name, accessor, field type).
    /// </summary>
    public Dictionary<string, (string StructValue, string FieldName, string Accessor, IrStructType FieldType)> MemberAccessInfo { get; } = new();

    /// <summary>
    /// Track index access instructions so we can reconstruct lvalue expressions when taking address.
    /// Maps result variable name -> (array expression, index expression).
    /// </summary>
    public Dictionary<string, (string ArrayExpr, string IndexExpr)> IndexAccessInfo { get; } = new();

    /// <summary>
    /// Track field access chains so we can reconstruct lvalue expressions for stores and address-of.
    /// Maps result variable name -> (base expression, field name, accessor).
    /// This allows reconstructing chains like self->timereq->tr_node from intermediate variables.
    /// </summary>
    public Dictionary<string, (string BaseExpr, string FieldName, string Accessor)> FieldAccessChainInfo { get; } = new();

    /// <summary>
    /// Counter for defer block emissions to ensure unique labels across multiple defer cleanup sites.
    /// </summary>
    public int DeferEmissionCounter { get; set; }

    /// <summary>
    /// Suffix to append to labels during defer block emission (empty for normal code).
    /// </summary>
    public string LabelSuffix { get; set; } = "";

    /// <summary>
    /// Track which defer blocks have been activated (had their IrDefer instruction emitted)
    /// at the current point in the code flow.
    /// Maps defer block -> defer index (1-based, matching _defer_N_active variable).
    /// </summary>
    public HashSet<int> ActivatedDeferBlocks { get; } = new();

    /// <summary>
    /// Block-scoped variable tracking for reduced stack usage.
    /// Maps block label -> set of variable names scoped to that block.
    /// </summary>
    public Dictionary<string, HashSet<string>>? BlockScopedVars { get; set; }

    /// <summary>
    /// Local variable declarations collected from IrLocalDecl instructions.
    /// Maps variable name -> (Type, IsArray, ArraySize).
    /// </summary>
    public Dictionary<string, (IrType Type, bool IsArray, int ArraySize)>? LocalDeclVars { get; set; }

    /// <summary>
    /// Variables that have been stored to (from IrStore instructions).
    /// Maps variable name -> type.
    /// </summary>
    public Dictionary<string, IrType>? StoredVars { get; set; }

    /// <summary>
    /// Liveness-based variable slot mapping for stack usage reduction.
    /// Maps IR variable names to shared slot names (e.g., "_formatter0" -> "_slot_StackFormatter_0").
    /// </summary>
    public Dictionary<string, string>? VariableToSlot { get; set; }

    /// <summary>
    /// Maps slot names to their types (for declaration).
    /// </summary>
    public Dictionary<string, IrType>? SlotTypes { get; set; }

    /// <summary>
    /// Statement-level debug symbol tracking for exact line numbers in crash dialogs.
    /// Maps label name -> (file, line, function name) for all emitted debug line markers.
    /// </summary>
    public List<(string LabelName, string FileName, int Line, string FuncName)> DebugLineMarkers { get; } = new();

    /// <summary>
    /// Track the last emitted line number to avoid duplicate markers for same line.
    /// </summary>
    public int LastEmittedDebugLine { get; set; } = -1;

    /// <summary>
    /// Counter for unique debug label generation within a function.
    /// </summary>
    public int DebugLabelCounter { get; set; }

    /// <summary>
    /// Track last emitted #line directive line to avoid duplicates.
    /// </summary>
    public int LastLineDirectiveLine { get; set; } = -1;

    /// <summary>
    /// Track last emitted #line directive file to avoid duplicates.
    /// </summary>
    public string? LastLineDirectiveFile { get; set; }

    /// <summary>
    /// VBCC WORKAROUND: Track comparison expressions that can be inlined into conditional branches.
    /// Maps result variable names to their comparison expressions,
    /// e.g., "_slot_bool_0" -> "_slot_i32_s_0 == 0".
    /// </summary>
    /// <remarks>
    /// AFFECTED VERSIONS: VBCC 0.9h and earlier (tested with vbcc 0.9h, AmigaOS target)
    ///
    /// PROBLEM: VBCC's optimizer can move stack cleanup instructions between a comparison
    /// result store and the subsequent conditional branch, clobbering the CPU condition flags.
    ///
    /// SOLUTION: Instead of storing the comparison result to a variable and then testing it,
    /// we inline the comparison expression directly into the if() statement.
    /// </remarks>
    public Dictionary<string, string> InlineableComparisons { get; } = new();

    /// <summary>
    /// The output StringBuilder for this function's generated code.
    /// </summary>
    public StringBuilder Output { get; }

    /// <summary>
    /// Creates a new emission context for the specified function.
    /// </summary>
    /// <param name="function">The function to emit code for.</param>
    /// <param name="output">Optional StringBuilder. If null, creates a new one.</param>
    public EmissionContext(IrFunction function, StringBuilder? output = null)
    {
        Function = function ?? throw new ArgumentNullException(nameof(function));
        Output = output ?? new StringBuilder();
    }

    /// <summary>
    /// Initializes pointer-converted parameters from the function's parameter list.
    /// Call this after construction to set up parameter tracking.
    /// </summary>
    /// <param name="typeContainsHeapData">Function to check if a struct type contains heap data.</param>
    public void InitializePointerConvertedParameters(Func<IrStructType, bool> typeContainsHeapData)
    {
        foreach (var param in Function.Parameters)
        {
            if (param.Type is IrStructType structType && typeContainsHeapData(structType))
            {
                PointerConvertedParameters.Add(param.Name);
            }
        }
    }

    /// <summary>
    /// Runs liveness analysis and sets up variable slot mapping.
    /// </summary>
    public void RunLivenessAnalysis()
    {
        var livenessAnalysis = new LivenessAnalysis(Function);
        var (variableToSlot, slotTypes) = livenessAnalysis.Analyze();
        VariableToSlot = variableToSlot;
        SlotTypes = slotTypes;
    }

    /// <summary>
    /// Gets the next unique debug label name for this function.
    /// </summary>
    public string GetNextDebugLabel()
    {
        return $"_dbg_{DebugLabelCounter++}";
    }

    /// <summary>
    /// Gets the next unique defer emission suffix for this function.
    /// </summary>
    public string GetNextDeferSuffix()
    {
        return $"_d{DeferEmissionCounter++}";
    }

    /// <summary>
    /// Checks if a variable name is mapped to a shared slot.
    /// </summary>
    public bool IsMappedToSlot(string varName)
    {
        return VariableToSlot?.ContainsKey(varName) ?? false;
    }

    /// <summary>
    /// Gets the slot name for a variable, or returns the original name if not mapped.
    /// </summary>
    public string GetSlotOrOriginal(string varName)
    {
        if (VariableToSlot != null && VariableToSlot.TryGetValue(varName, out var slotName))
        {
            return slotName;
        }
        return varName;
    }

    /// <summary>
    /// Records a member access for later reconstruction of lvalue expressions.
    /// </summary>
    public void RecordMemberAccess(string resultVar, string structValue, string fieldName, string accessor, IrStructType fieldType)
    {
        MemberAccessInfo[resultVar] = (structValue, fieldName, accessor, fieldType);
    }

    /// <summary>
    /// Records an index access for later reconstruction of lvalue expressions.
    /// </summary>
    public void RecordIndexAccess(string resultVar, string arrayExpr, string indexExpr)
    {
        IndexAccessInfo[resultVar] = (arrayExpr, indexExpr);
    }

    /// <summary>
    /// Records a field access chain for later reconstruction of lvalue expressions.
    /// </summary>
    public void RecordFieldAccessChain(string resultVar, string baseExpr, string fieldName, string accessor)
    {
        FieldAccessChainInfo[resultVar] = (baseExpr, fieldName, accessor);
    }

    /// <summary>
    /// Tries to get member access info for a result variable.
    /// </summary>
    public bool TryGetMemberAccess(string resultVar, out (string StructValue, string FieldName, string Accessor, IrStructType FieldType) info)
    {
        return MemberAccessInfo.TryGetValue(resultVar, out info);
    }

    /// <summary>
    /// Tries to get index access info for a result variable.
    /// </summary>
    public bool TryGetIndexAccess(string resultVar, out (string ArrayExpr, string IndexExpr) info)
    {
        return IndexAccessInfo.TryGetValue(resultVar, out info);
    }

    /// <summary>
    /// Tries to get field access chain info for a result variable.
    /// </summary>
    public bool TryGetFieldAccessChain(string resultVar, out (string BaseExpr, string FieldName, string Accessor) info)
    {
        return FieldAccessChainInfo.TryGetValue(resultVar, out info);
    }

    /// <summary>
    /// Marks a variable as declared, preventing duplicate declarations.
    /// </summary>
    public void MarkDeclared(string varName)
    {
        DeclaredVariables.Add(varName);
    }

    /// <summary>
    /// Checks if a variable has been declared.
    /// </summary>
    public bool IsDeclared(string varName)
    {
        return DeclaredVariables.Contains(varName);
    }

    /// <summary>
    /// Records an inlineable comparison expression (VBCC workaround).
    /// </summary>
    public void RecordInlineableComparison(string resultVar, string expression)
    {
        InlineableComparisons[resultVar] = expression;
    }

    /// <summary>
    /// Tries to get an inlineable comparison expression for a result variable.
    /// </summary>
    public bool TryGetInlineableComparison(string resultVar, out string? expression)
    {
        return InlineableComparisons.TryGetValue(resultVar, out expression);
    }

    /// <summary>
    /// Activates a defer block at the current point in the code flow.
    /// </summary>
    public void ActivateDeferBlock(int blockIndex)
    {
        ActivatedDeferBlocks.Add(blockIndex);
    }

    /// <summary>
    /// Checks if a defer block has been activated.
    /// </summary>
    public bool IsDeferBlockActivated(int blockIndex)
    {
        return ActivatedDeferBlocks.Contains(blockIndex);
    }

    /// <summary>
    /// Records a debug line marker for crash reporting.
    /// </summary>
    public void RecordDebugLineMarker(string labelName, string fileName, int line, string funcName)
    {
        DebugLineMarkers.Add((labelName, fileName, line, funcName));
        LastEmittedDebugLine = line;
    }
}

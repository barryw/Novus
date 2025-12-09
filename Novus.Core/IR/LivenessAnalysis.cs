namespace Novus.IR;

/// <summary>
/// Represents the liveness interval of a variable within a function.
/// Used to determine when variables can share the same storage slot.
/// </summary>
public class LivenessInterval
{
    public string VariableName { get; }
    public IrType Type { get; }
    public int DefInstruction { get; set; }  // Global instruction index where defined
    public int LastUseInstruction { get; set; }  // Global instruction index of last use

    public LivenessInterval(string variableName, IrType type, int defInstruction)
    {
        VariableName = variableName;
        Type = type;
        DefInstruction = defInstruction;
        LastUseInstruction = defInstruction;
    }

    /// <summary>
    /// Check if this interval overlaps with another.
    /// Two intervals [a, b] and [c, d] overlap if a <= d && c <= b
    /// </summary>
    public bool OverlapsWith(LivenessInterval other)
    {
        return DefInstruction <= other.LastUseInstruction &&
               other.DefInstruction <= LastUseInstruction;
    }

    public override string ToString()
    {
        return $"{VariableName}: [{DefInstruction}, {LastUseInstruction}]";
    }
}

/// <summary>
/// Performs instruction-level liveness analysis on IR functions.
/// This is used to identify variables with non-overlapping lifetimes
/// that can share the same storage slot to reduce stack usage.
/// </summary>
public class LivenessAnalysis
{
    private readonly IrFunction _function;

    // Maps variable name to its liveness interval
    private readonly Dictionary<string, LivenessInterval> _intervals = new();

    // Maps IR variable name to its assigned slot name
    private readonly Dictionary<string, string> _variableToSlot = new();

    // Maps slot name to type (for declaration)
    private readonly Dictionary<string, IrType> _slotTypes = new();

    // Global instruction counter for computing intervals
    private int _instructionIndex = 0;

    public LivenessAnalysis(IrFunction function)
    {
        _function = function;
    }

    /// <summary>
    /// Perform liveness analysis and compute slot assignments.
    /// Returns a mapping from IR variable names to slot names.
    /// </summary>
    public (Dictionary<string, string> VariableToSlot, Dictionary<string, IrType> SlotTypes) Analyze()
    {
        // Phase 1: Build liveness intervals
        BuildIntervals();

        // Phase 2: Assign slots using interval graph coloring
        AssignSlots();

        return (_variableToSlot, _slotTypes);
    }

    /// <summary>
    /// Build liveness intervals by scanning all instructions.
    /// </summary>
    private void BuildIntervals()
    {
        _instructionIndex = 0;

        foreach (var block in _function.BasicBlocks)
        {
            foreach (var instruction in block.Instructions)
            {
                // Record uses (must be done before definitions to handle self-referential ops)
                RecordUses(instruction);

                // Record definitions
                RecordDefinition(instruction);

                _instructionIndex++;
            }
        }
    }

    /// <summary>
    /// Record variable definitions from an instruction.
    /// </summary>
    private void RecordDefinition(IrInstruction instruction)
    {
        switch (instruction)
        {
            case IrLocalDecl localDecl:
                // Only track temporaries (named variables should keep their names)
                if (IsTemporaryVariable(localDecl.Name))
                {
                    CreateOrUpdateInterval(localDecl.Name, localDecl.Type, isDefinition: true);
                }
                break;

            case IrBinaryOp binOp:
                if (IsTemporaryVariable(binOp.ResultName))
                {
                    CreateOrUpdateInterval(binOp.ResultName, binOp.Type, isDefinition: true);
                }
                break;

            case IrCall call when call.ResultName != null:
                if (IsTemporaryVariable(call.ResultName))
                {
                    CreateOrUpdateInterval(call.ResultName, call.ReturnType, isDefinition: true);
                }
                break;

            case IrIndirectCall indirectCall when indirectCall.ResultName != null:
                if (IsTemporaryVariable(indirectCall.ResultName))
                {
                    CreateOrUpdateInterval(indirectCall.ResultName, indirectCall.ReturnType, isDefinition: true);
                }
                break;

            case IrIndexAccess indexAccess:
                if (IsTemporaryVariable(indexAccess.ResultName))
                {
                    CreateOrUpdateInterval(indexAccess.ResultName, indexAccess.ElementType, isDefinition: true);
                }
                break;

            case IrMemberAccess memberAccess:
                if (IsTemporaryVariable(memberAccess.ResultName))
                {
                    CreateOrUpdateInterval(memberAccess.ResultName, memberAccess.FieldType, isDefinition: true);
                }
                break;

            case IrStore store:
                // Stores to temporaries define them
                if (IsTemporaryVariable(store.VariableName))
                {
                    CreateOrUpdateInterval(store.VariableName, store.Value.Type, isDefinition: true);
                }
                break;

            case IrExtractTag extractTag:
                if (IsTemporaryVariable(extractTag.ResultName))
                {
                    // Enum tags are always int32_t in the C code generator
                    CreateOrUpdateInterval(extractTag.ResultName, IrIntType.I32, isDefinition: true);
                }
                break;

            case IrExtractVariantData extractData:
                if (IsTemporaryVariable(extractData.ResultName))
                {
                    CreateOrUpdateInterval(extractData.ResultName, extractData.DataType, isDefinition: true);
                }
                break;
        }
    }

    /// <summary>
    /// Record variable uses from an instruction.
    /// </summary>
    private void RecordUses(IrInstruction instruction)
    {
        switch (instruction)
        {
            case IrLocalDecl localDecl:
                if (localDecl.InitialValue != null)
                {
                    RecordValueUses(localDecl.InitialValue);
                }
                break;

            case IrStore store:
                RecordValueUses(store.Value);
                break;

            case IrBinaryOp binOp:
                RecordValueUses(binOp.Left);
                RecordValueUses(binOp.Right);
                break;

            case IrCall call:
                foreach (var arg in call.Arguments)
                {
                    RecordValueUses(arg);
                }
                break;

            case IrIndirectCall indirectCall:
                RecordValueUses(indirectCall.FunctionPointer);
                foreach (var arg in indirectCall.Arguments)
                {
                    RecordValueUses(arg);
                }
                break;

            case IrReturn ret when ret.Value != null:
                RecordValueUses(ret.Value);
                break;

            case IrConditionalBranch condBranch:
                RecordValueUses(condBranch.Condition);
                break;

            case IrIndexAccess indexAccess:
                RecordValueUses(indexAccess.Array);
                RecordValueUses(indexAccess.Index);
                break;

            case IrIndexStore indexStore:
                RecordValueUses(indexStore.Array);
                RecordValueUses(indexStore.Index);
                RecordValueUses(indexStore.Value);
                break;

            case IrMemberAccess memberAccess:
                RecordValueUses(memberAccess.Struct);
                break;

            case IrMemberStore memberStore:
                RecordValueUses(memberStore.Struct);
                RecordValueUses(memberStore.Value);
                break;

            case IrIndexedFieldStore indexedFieldStore:
                RecordValueUses(indexedFieldStore.Array);
                RecordValueUses(indexedFieldStore.Index);
                RecordValueUses(indexedFieldStore.Value);
                break;

            case IrDereferenceStore derefStore:
                RecordValueUses(derefStore.Pointer);
                RecordValueUses(derefStore.Value);
                break;

            case IrAssert assert:
                RecordValueUses(assert.Condition);
                break;

            case IrMatch match:
                RecordValueUses(match.MatchValue);
                break;

            case IrExtractTag extractTag:
                RecordValueUses(extractTag.EnumValue);
                break;

            case IrExtractVariantData extractData:
                RecordValueUses(extractData.EnumValue);
                break;
        }
    }

    /// <summary>
    /// Recursively record uses within an IrValue.
    /// </summary>
    private void RecordValueUses(IrValue value)
    {
        switch (value)
        {
            case IrVariable variable:
                if (IsTemporaryVariable(variable.Name))
                {
                    RecordUse(variable.Name);
                }
                break;

            case IrBorrowValue borrow:
                RecordValueUses(borrow.BorrowedValue);
                break;

            case IrDereferenceValue deref:
                RecordValueUses(deref.PointerValue);
                break;

            case IrCastValue cast:
                RecordValueUses(cast.Value);
                break;

            case IrFieldReference fieldRef:
                RecordValueUses(fieldRef.Struct);
                break;

            case IrIndexedFieldAccess indexedField:
                RecordValueUses(indexedField.Array);
                RecordValueUses(indexedField.Index);
                break;

            case IrTupleElementAccess tupleAccess:
                RecordValueUses(tupleAccess.Tuple);
                break;

            case IrStructLiteral structLit:
                foreach (var fieldValue in structLit.FieldValues.Values)
                {
                    RecordValueUses(fieldValue);
                }
                break;

            case IrTupleLiteral tupleLit:
                foreach (var element in tupleLit.Elements)
                {
                    RecordValueUses(element);
                }
                break;

            case IrArrayLiteral arrayLit:
                foreach (var element in arrayLit.Elements)
                {
                    RecordValueUses(element);
                }
                break;
        }
    }

    private void CreateOrUpdateInterval(string varName, IrType type, bool isDefinition)
    {
        if (!_intervals.ContainsKey(varName))
        {
            _intervals[varName] = new LivenessInterval(varName, type, _instructionIndex);
        }
        else if (isDefinition)
        {
            // Update definition point if this is a redefinition
            _intervals[varName].DefInstruction = Math.Min(_intervals[varName].DefInstruction, _instructionIndex);
        }
    }

    private void RecordUse(string varName)
    {
        if (_intervals.TryGetValue(varName, out var interval))
        {
            interval.LastUseInstruction = Math.Max(interval.LastUseInstruction, _instructionIndex);
        }
    }

    /// <summary>
    /// Check if a variable name is a temporary (generated by compiler).
    /// Temporaries start with % or _ followed by specific patterns.
    /// </summary>
    private bool IsTemporaryVariable(string name)
    {
        // Compiler-generated temporaries use patterns like:
        // %t0, %t1, ... (SSA temporaries)
        // _formatter0, _formatter1, ... (f-string formatters)
        // _t0, _t1, ... (general temporaries)
        return name.StartsWith("%") ||
               name.StartsWith("_formatter") ||
               name.StartsWith("_t");
    }

    /// <summary>
    /// Assign slots to variables using interval graph coloring.
    /// Variables with non-overlapping lifetimes of the same type can share a slot.
    /// </summary>
    private void AssignSlots()
    {
        // Group intervals by their type's C representation
        var typeGroups = new Dictionary<string, List<LivenessInterval>>();

        foreach (var interval in _intervals.Values)
        {
            // Skip void types - they don't need slots (no runtime representation)
            if (interval.Type is IrVoidType)
                continue;

            var typeKey = GetTypeKey(interval.Type);
            if (!typeGroups.ContainsKey(typeKey))
            {
                typeGroups[typeKey] = new List<LivenessInterval>();
            }
            typeGroups[typeKey].Add(interval);
        }

        // For each type group, assign slots using greedy coloring
        foreach (var (typeKey, intervals) in typeGroups)
        {
            // Sort by definition point for greedy allocation
            var sortedIntervals = intervals.OrderBy(i => i.DefInstruction).ToList();

            // Track active slots: (slot name, end point of current occupant)
            var activeSlots = new List<(string SlotName, int EndPoint, IrType Type)>();
            var slotCounter = 0;

            foreach (var interval in sortedIntervals)
            {
                // Find a slot that's free (its current occupant's lifetime ended)
                string? assignedSlot = null;

                for (int i = 0; i < activeSlots.Count; i++)
                {
                    var (slotName, endPoint, slotType) = activeSlots[i];

                    // Slot is free if previous occupant's lifetime ended before this one starts
                    // Also ensure types match exactly (not just by typeKey)
                    if (endPoint < interval.DefInstruction && TypesAreCompatible(slotType, interval.Type))
                    {
                        assignedSlot = slotName;
                        activeSlots[i] = (slotName, interval.LastUseInstruction, interval.Type);
                        break;
                    }
                }

                // No free slot found, create a new one
                if (assignedSlot == null)
                {
                    assignedSlot = $"_slot_{typeKey}_{slotCounter++}";
                    activeSlots.Add((assignedSlot, interval.LastUseInstruction, interval.Type));
                    _slotTypes[assignedSlot] = interval.Type;
                }

                _variableToSlot[interval.VariableName] = assignedSlot;
            }
        }
    }

    /// <summary>
    /// Get a key that identifies the type for slot grouping.
    /// Types with the same key can potentially share slots.
    /// </summary>
    private string GetTypeKey(IrType type)
    {
        var key = type switch
        {
            IrIntType intType => $"i{intType.BitWidth}_{(intType.IsSigned ? "s" : "u")}",
            IrBoolType => "bool",
            IrFloatType floatType => $"f{floatType.BitWidth}",
            IrFixedType fixedType => $"fixed{fixedType.BitWidth}",
            IrPointerType ptrType => $"ptr_{GetTypeKey(ptrType.PointeeType)}",
            IrReferenceType refType => $"ref_{GetTypeKey(refType.PointeeType)}",
            IrMutReferenceType mutRefType => $"mutref_{GetTypeKey(mutRefType.PointeeType)}",
            IrStructType structType => structType.CacheKey ?? structType.StructName,
            IrEnumType enumType => enumType.CacheKey ?? enumType.EnumName,
            IrTupleType tupleType => $"tuple_{string.Join("_", tupleType.ElementTypes.Select(GetTypeKey))}",
            IrArrayType arrayType => $"array_{arrayType.Length}_{GetTypeKey(arrayType.ElementType)}",
            IrVoidType => "void",
            _ => type.Name
        };

        // Sanitize the key to be a valid C identifier (replace invalid chars with underscores)
        return SanitizeTypeKey(key);
    }

    /// <summary>
    /// Sanitize a type key to be a valid C identifier.
    /// Replaces characters like &lt;, &gt;, comma, space with underscores.
    /// </summary>
    private static string SanitizeTypeKey(string key)
    {
        var result = new System.Text.StringBuilder(key.Length);
        foreach (var ch in key)
        {
            if (char.IsLetterOrDigit(ch) || ch == '_')
            {
                result.Append(ch);
            }
            else
            {
                result.Append('_');
            }
        }
        return result.ToString();
    }

    /// <summary>
    /// Check if two types are compatible for slot sharing.
    /// </summary>
    private bool TypesAreCompatible(IrType a, IrType b)
    {
        // For now, require exact type match for safety
        // This could be relaxed for types with the same size and alignment
        return GetTypeKey(a) == GetTypeKey(b);
    }
}

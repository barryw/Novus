using Novus.Diagnostics;
using Novus.IR;

namespace Novus.SemanticAnalysis;

/// <summary>
/// Tracks ownership and move semantics for memory safety.
/// Implements a simplified borrow checker that detects use-after-move errors.
///
/// This class manages:
/// - Move tracking for non-Copy types
/// - Partial move tracking for struct fields
/// - Control flow sensitivity (branches, loops, match arms)
/// - Drop tracking for automatic resource cleanup (RAII)
/// </summary>
public class BorrowChecker
{
    private readonly Dictionary<int, MoveInfo> _movedVariables = new();
    private readonly Stack<ControlFlowContext> _controlFlowStack = new();
    private readonly Dictionary<int, DropInfo> _dropInfo;

    /// <summary>
    /// Delegate to check if a type implements a trait.
    /// Allows borrow checker to be independent of the full type system.
    /// </summary>
    public Func<IrType, string, List<IrType>, bool>? TypeImplementsTraitFn { get; set; }

    /// <summary>
    /// Creates a new BorrowChecker that shares drop info state with the semantic analyzer.
    /// </summary>
    public BorrowChecker(Dictionary<int, DropInfo> dropInfo)
    {
        _dropInfo = dropInfo;
    }

    /// <summary>
    /// Resets all move tracking state. Call this at the start of each function/method.
    /// </summary>
    public void Reset()
    {
        _movedVariables.Clear();
        _controlFlowStack.Clear();
    }

    /// <summary>
    /// Enters a new control flow branch for move tracking.
    /// </summary>
    public void EnterBranch(ControlFlowKind kind)
    {
        _controlFlowStack.Push(new ControlFlowContext
        {
            Kind = kind,
            MovesInBranch = new Dictionary<int, MoveInfo>()
        });
    }

    /// <summary>
    /// Exits the current control flow branch and returns moves made in that branch.
    /// </summary>
    public Dictionary<int, MoveInfo> ExitBranch()
    {
        if (_controlFlowStack.Count == 0)
        {
            return new Dictionary<int, MoveInfo>();
        }
        return _controlFlowStack.Pop().MovesInBranch;
    }

    /// <summary>
    /// Records that a variable has been moved.
    /// </summary>
    public void RecordMove(int variableId, MoveInfo moveInfo)
    {
        if (_controlFlowStack.Count > 0)
        {
            // Inside a branch - track in branch context
            var context = _controlFlowStack.Peek();
            context.MovesInBranch[variableId] = moveInfo;
        }
        else
        {
            // Top level - directly mark as moved
            _movedVariables[variableId] = moveInfo;
        }

        // Also mark in drop info so we don't call drop on moved values
        if (_dropInfo.TryGetValue(variableId, out var dropInfo))
        {
            dropInfo.WasMoved = true;
        }
    }

    /// <summary>
    /// Records that a specific field of a struct has been moved (partial move).
    /// </summary>
    public void RecordFieldMove(int variableId, string variableName, string fieldName,
                                 SourceLocation moveLocation, string reason)
    {
        var targetDict = _controlFlowStack.Count > 0
            ? _controlFlowStack.Peek().MovesInBranch
            : _movedVariables;

        if (targetDict.TryGetValue(variableId, out var existing))
        {
            // Variable already has move tracking
            if (existing.MovedFields == null)
            {
                // Whole struct already moved - this is a use-after-move error
                // Will be caught by CheckVariableNotMoved
                return;
            }

            // Add this field to the set of moved fields
            existing.MovedFields.Add(fieldName);
        }
        else
        {
            // First field move for this variable
            targetDict[variableId] = new MoveInfo
            {
                VariableName = variableName,
                VariableId = variableId,
                MoveLocation = moveLocation,
                Reason = reason,
                MovedFields = new HashSet<string> { fieldName }
            };
        }

        // Also track in drop info for partial moves
        if (_dropInfo.TryGetValue(variableId, out var dropInfo))
        {
            // Initialize MovedFields set if null
            dropInfo.MovedFields ??= new HashSet<string>();
            dropInfo.MovedFields.Add(fieldName);
        }
    }

    /// <summary>
    /// Records that a variable was moved in a loop iteration.
    /// Used for special error messaging.
    /// </summary>
    public void RecordLoopMove(int variableId, MoveInfo moveInfo)
    {
        _movedVariables[variableId] = moveInfo;
    }

    /// <summary>
    /// Merges moves from multiple branches (e.g., if/else arms, match arms).
    /// A variable is considered moved if it was moved in ANY branch.
    /// </summary>
    public void MergeBranchMoves(params Dictionary<int, MoveInfo>?[] branchMoves)
    {
        foreach (var moves in branchMoves)
        {
            if (moves == null) continue;

            foreach (var (varId, moveInfo) in moves)
            {
                if (!_movedVariables.ContainsKey(varId))
                {
                    _movedVariables[varId] = new MoveInfo
                    {
                        VariableId = varId,
                        VariableName = moveInfo.VariableName,
                        MoveLocation = moveInfo.MoveLocation,
                        Reason = $"value moved in conditional branch: {moveInfo.Reason}",
                        MovedFields = moveInfo.MovedFields != null ? new HashSet<string>(moveInfo.MovedFields) : null
                    };
                }
            }
        }
    }

    /// <summary>
    /// Merges moves from a list of branches (e.g., multiple match arms).
    /// </summary>
    public void MergeBranchMoves(List<Dictionary<int, MoveInfo>> branchMoves)
    {
        foreach (var moves in branchMoves)
        {
            foreach (var (varId, moveInfo) in moves)
            {
                if (!_movedVariables.ContainsKey(varId))
                {
                    _movedVariables[varId] = new MoveInfo
                    {
                        VariableId = varId,
                        VariableName = moveInfo.VariableName,
                        MoveLocation = moveInfo.MoveLocation,
                        Reason = $"value moved in conditional branch: {moveInfo.Reason}",
                        MovedFields = moveInfo.MovedFields != null ? new HashSet<string>(moveInfo.MovedFields) : null
                    };
                }
            }
        }
    }

    /// <summary>
    /// Checks if a variable has been moved (whole or partial).
    /// Returns the MoveInfo if moved, null otherwise.
    /// </summary>
    public MoveInfo? GetMoveInfo(int variableId)
    {
        return _movedVariables.TryGetValue(variableId, out var info) ? info : null;
    }

    /// <summary>
    /// Checks if a variable has been fully moved (not partial).
    /// </summary>
    public bool IsFullyMoved(int variableId)
    {
        return _movedVariables.TryGetValue(variableId, out var info) && info.MovedFields == null;
    }

    /// <summary>
    /// Checks if a specific field has been moved.
    /// </summary>
    public bool IsFieldMoved(int variableId, string fieldName)
    {
        if (!_movedVariables.TryGetValue(variableId, out var info))
            return false;

        // If MovedFields is null, entire value was moved
        if (info.MovedFields == null)
            return true;

        return info.MovedFields.Contains(fieldName);
    }

    /// <summary>
    /// Determines if a type implements Copy semantics (can be copied instead of moved).
    /// Primitive types like i32, u32, bool, etc. are always Copy.
    /// Pointer types are Copy (copying a pointer doesn't move the pointed-to data).
    /// Structs and enums can implement the Copy trait to enable copying.
    /// </summary>
    public bool IsCopyType(IrType type)
    {
        // Primitives are always Copy
        if (type is IrIntType or IrBoolType)
            return true;

        // Pointers are Copy (copying the pointer value, not the pointed-to data)
        if (type is IrPointerType)
            return true;

        // Function pointers are Copy
        if (type is IrFunctionPointerType)
            return true;

        // Check if struct implements Copy trait
        if (type is IrStructType structType)
        {
            return TypeImplementsTraitFn?.Invoke(structType, "Copy", new List<IrType>()) ?? false;
        }

        // Check if enum implements Copy trait
        if (type is IrEnumType enumType)
        {
            return TypeImplementsTraitFn?.Invoke(enumType, "Copy", new List<IrType>()) ?? false;
        }

        // Arrays are non-Copy (contain elements that may need individual handling)
        if (type is IrArrayType)
            return false;

        // References are Copy (like pointers, just copying the reference)
        if (type is IrReferenceType)
            return true;

        // Default to non-Copy for safety
        return false;
    }
}

/// <summary>
/// Information about a moved value.
/// </summary>
public class MoveInfo
{
    public string VariableName { get; init; } = "";
    public int VariableId { get; init; }
    public SourceLocation MoveLocation { get; init; } = null!;
    public string Reason { get; init; } = "";

    /// <summary>
    /// Tracks which fields of a struct have been moved.
    /// - null means the entire value was moved
    /// - empty set means the variable exists but no fields moved yet
    /// - non-empty set lists the specific fields that have been moved
    /// </summary>
    public HashSet<string>? MovedFields { get; set; }
}

/// <summary>
/// Types of control flow constructs that affect move tracking.
/// </summary>
public enum ControlFlowKind
{
    If,
    MatchArm,
    While,
    Block
}

/// <summary>
/// Context for tracking moves within a control flow branch.
/// </summary>
internal class ControlFlowContext
{
    public ControlFlowKind Kind { get; init; }
    public Dictionary<int, MoveInfo> MovesInBranch { get; init; } = new();
}

/// <summary>
/// Drop tracking - tracks which variables need automatic drop calls when they go out of scope.
/// </summary>
public class DropInfo
{
    public required int VariableId { get; init; }
    public required string VariableName { get; init; }
    public required IrType VariableType { get; init; }
    public required SourceLocation DeclLocation { get; init; }
    public bool WasMoved { get; set; }  // Don't drop if moved
    public HashSet<string>? MovedFields { get; set; }  // For partial moves
}

/// <summary>
/// Tracks variables that need dropping per scope.
/// </summary>
public class ScopeDropInfo
{
    public List<DropInfo> VariablesToDrop { get; } = new();
}

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
    /// Graph tracking borrow relationships for lifetime validation.
    /// </summary>
    public BorrowGraph BorrowGraph { get; } = new();

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
        BorrowGraph.Clear();
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

public class BorrowConflict
{
    public BorrowEdge ExistingBorrow { get; init; } = null!;
    public bool RequestedMutable { get; init; }
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

    public BorrowConflict? AddBorrow(int borrowerId, int sourceId, SourceLocation location, bool mutable)
    {
        if (!_borrowsFrom.ContainsKey(borrowerId))
            _borrowsFrom[borrowerId] = new List<BorrowEdge>();

        var conflict = FindConflict(sourceId, mutable);
        if (conflict != null)
            return conflict;

        _borrowsFrom[borrowerId].Add(new BorrowEdge
        {
            SourceId = sourceId,
            BorrowerId = borrowerId,
            BorrowLocation = location,
            IsMutable = mutable
        });
        return null;
    }

    public BorrowConflict? FindConflict(int sourceId, bool mutable)
    {
        var existing = _borrowsFrom.Values
            .SelectMany(edges => edges)
            .FirstOrDefault(edge => edge.SourceId == sourceId && (mutable || edge.IsMutable));

        return existing != null && _lifetimes.TryGetValue(sourceId, out var sourceLifetime)
            ? new BorrowConflict
            {
                ExistingBorrow = existing,
                RequestedMutable = mutable,
                SourceLifetime = sourceLifetime
            }
            : null;
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

    public void ExitScope(int scopeDepth)
    {
        var expiredIds = _lifetimes.Values
            .Where(lifetime => lifetime.ScopeDepth == scopeDepth)
            .Select(lifetime => lifetime.VariableId)
            .ToHashSet();

        foreach (var variableId in expiredIds)
        {
            _borrowsFrom.Remove(variableId);
            _lifetimes.Remove(variableId);
        }

        foreach (var edges in _borrowsFrom.Values)
            edges.RemoveAll(edge => expiredIds.Contains(edge.SourceId));
    }

    public void RemoveBorrower(int borrowerId)
    {
        _borrowsFrom.Remove(borrowerId);
    }

    public void Clear()
    {
        _borrowsFrom.Clear();
        _lifetimes.Clear();
    }
}

/// <summary>
/// Result of lifetime inference for a method return.
/// </summary>
public class LifetimeInferenceResult
{
    public bool Success { get; init; }
    public int? SourceParameterIndex { get; init; }
    public bool IsStatic { get; init; }
    public string? ErrorMessage { get; init; }

    public static LifetimeInferenceResult Ok(int? sourceIndex = null) => new()
    {
        Success = true,
        SourceParameterIndex = sourceIndex
    };

    public static LifetimeInferenceResult Static() => new()
    {
        Success = true,
        IsStatic = true
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
    public static bool RequiresLifetime(IrType type) => RequiresLifetime(type, new HashSet<IrType>());

    public static bool ContainsMutableReference(IrType type) =>
        ContainsMutableReference(type, new HashSet<IrType>());

    private static bool RequiresLifetime(IrType type, HashSet<IrType> seen)
    {
        if (!seen.Add(type)) return false;
        return type switch
        {
            IrReferenceType or IrMutReferenceType => true,
            IrStructType value =>
                value.Fields.Any(field => RequiresLifetime(field.Type, seen)) ||
                value.TypeArguments?.Any(item => RequiresLifetime(item, seen)) == true,
            IrEnumType value =>
                value.Variants.Any(variant =>
                    variant.AssociatedData.Any(item => RequiresLifetime(item, seen))) ||
                value.TypeArguments?.Any(item => RequiresLifetime(item, seen)) == true,
            IrTupleType value => value.ElementTypes.Any(item => RequiresLifetime(item, seen)),
            IrArrayType value => RequiresLifetime(value.ElementType, seen),
            _ => false
        };
    }

    private static bool ContainsMutableReference(IrType type, HashSet<IrType> seen)
    {
        if (!seen.Add(type)) return false;
        return type switch
        {
            IrMutReferenceType => true,
            IrReferenceType => false,
            IrStructType value =>
                value.Fields.Any(field => ContainsMutableReference(field.Type, seen)) ||
                value.TypeArguments?.Any(item => ContainsMutableReference(item, seen)) == true,
            IrEnumType value =>
                value.Variants.Any(variant =>
                    variant.AssociatedData.Any(item => ContainsMutableReference(item, seen))) ||
                value.TypeArguments?.Any(item => ContainsMutableReference(item, seen)) == true,
            IrTupleType value => value.ElementTypes.Any(item => ContainsMutableReference(item, seen)),
            IrArrayType value => ContainsMutableReference(value.ElementType, seen),
            _ => false
        };
    }

    /// <summary>
    /// Infers which parameter's lifetime the return reference should be tied to.
    /// Rules:
    /// 1. If &self exists, return ties to self
    /// 2. If exactly one reference param, return ties to that
    /// 3. If multiple ref params without self, error
    /// 4. If no ref params but returning reference, error
    /// </summary>
    public LifetimeInferenceResult InferReturnLifetime<T>(
        IEnumerable<T> parameters,
        IrType returnType,
        AttributeCollection? attributes = null)
        where T : IParameterInfo
    {
        // Not a reference return - no lifetime needed
        if (!RequiresLifetime(returnType))
        {
            return LifetimeInferenceResult.Ok(null);
        }

        var paramList = parameters.ToList();

        var explicitBorrow = attributes?.Get(KnownAttributes.Borrows);
        if (explicitBorrow != null)
        {
            var sourceName = explicitBorrow.GetPositionalArg<string>(0);
            if (string.IsNullOrWhiteSpace(sourceName))
                return LifetimeInferenceResult.Error("@borrows requires a parameter name or `static`");

            if (sourceName == "static")
                return LifetimeInferenceResult.Static();

            var sourceIndex = paramList.FindIndex(parameter =>
                parameter.ParameterName == sourceName);
            if (sourceIndex < 0)
                return LifetimeInferenceResult.Error(
                    $"@borrows names unknown parameter `{sourceName}`");

            var sourceType = paramList[sourceIndex].ParameterType;
            if (!RequiresLifetime(sourceType) && sourceType is not IrPointerType)
                return LifetimeInferenceResult.Error(
                    $"@borrows source `{sourceName}` must be a reference, borrowed view, or raw pointer");

            if (sourceType is IrPointerType &&
                attributes?.Has(KnownAttributes.Unsafe) != true)
                return LifetimeInferenceResult.Error(
                    $"@borrows from raw pointer `{sourceName}` requires @unsafe");

            return LifetimeInferenceResult.Ok(sourceIndex);
        }

        // Owner-tied aggregate views participate exactly like direct references.
        var refParams = paramList
            .Select((p, idx) => (Index: idx, Param: p))
            .Where(x => RequiresLifetime(x.Param.ParameterType))
            .ToList();

        // Rule 1: &self always wins
        var selfParam = refParams.FirstOrDefault(x => x.Param.ParameterName == "self");
        if (selfParam.Param != null)
        {
            return LifetimeInferenceResult.Ok(selfParam.Index);
        }

        // Rule 2: Exactly one reference param
        if (refParams.Count == 1)
        {
            return LifetimeInferenceResult.Ok(refParams[0].Index);
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

/// <summary>
/// Interface for parameter types that can be used with LifetimeInference.
/// </summary>
public interface IParameterInfo
{
    string ParameterName { get; }
    IrType ParameterType { get; }
}

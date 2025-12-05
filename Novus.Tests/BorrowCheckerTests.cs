using Novus.Diagnostics;
using Novus.IR;
using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for the BorrowChecker class that handles move semantics and ownership tracking.
/// </summary>
public class BorrowCheckerTests
{
    private static SourceLocation TestLocation => new SourceLocation("test.novus", 1, 1, 0, "");
    private static SourceLocation TestLocation2 => new SourceLocation("test.novus", 2, 1, 0, "");
    private static SourceLocation TestLocation3 => new SourceLocation("test.novus", 3, 1, 0, "");

    private BorrowChecker CreateBorrowChecker()
    {
        return new BorrowChecker(new Dictionary<int, DropInfo>())
        {
            // Mock trait implementation - for these tests, nothing implements Copy
            TypeImplementsTraitFn = (type, trait, typeArgs) => false
        };
    }

    [Fact]
    public void IsCopyType_Primitives_ReturnTrue()
    {
        var checker = CreateBorrowChecker();

        Assert.True(checker.IsCopyType(IrIntType.I32));
        Assert.True(checker.IsCopyType(IrIntType.U8));
        Assert.True(checker.IsCopyType(IrIntType.I64));
    }

    [Fact]
    public void IsCopyType_Pointers_ReturnTrue()
    {
        var checker = CreateBorrowChecker();

        Assert.True(checker.IsCopyType(new IrPointerType(IrIntType.I32)));
        Assert.True(checker.IsCopyType(new IrPointerType(new IrPointerType(IrIntType.U8))));
    }

    [Fact]
    public void IsCopyType_FunctionPointers_ReturnTrue()
    {
        var checker = CreateBorrowChecker();

        var fnPtr = new IrFunctionPointerType(
            new List<IrType> { IrIntType.I32 },
            IrIntType.I32
        );
        Assert.True(checker.IsCopyType(fnPtr));
    }

    [Fact]
    public void IsCopyType_Arrays_ReturnFalse()
    {
        var checker = CreateBorrowChecker();

        Assert.False(checker.IsCopyType(new IrArrayType(IrIntType.I32, 10)));
    }

    [Fact]
    public void IsCopyType_Structs_ReturnFalseByDefault()
    {
        var checker = CreateBorrowChecker();

        var structType = new IrStructType("Point", new List<IrStructField>
        {
            new IrStructField("x", IrIntType.I32),
            new IrStructField("y", IrIntType.I32)
        });
        Assert.False(checker.IsCopyType(structType));
    }

    [Fact]
    public void IsCopyType_StructsWithCopyTrait_ReturnTrue()
    {
        var checker = new BorrowChecker(new Dictionary<int, DropInfo>())
        {
            // Mock trait implementation - Point implements Copy
            TypeImplementsTraitFn = (type, trait, typeArgs) =>
                type is IrStructType st && st.StructName == "Point" && trait == "Copy"
        };

        var pointType = new IrStructType("Point", new List<IrStructField>
        {
            new IrStructField("x", IrIntType.I32),
            new IrStructField("y", IrIntType.I32)
        });
        Assert.True(checker.IsCopyType(pointType));

        var otherType = new IrStructType("Other", new List<IrStructField>());
        Assert.False(checker.IsCopyType(otherType));
    }

    [Fact]
    public void RecordMove_TracksFullMove()
    {
        var checker = CreateBorrowChecker();

        var moveInfo = new MoveInfo
        {
            VariableName = "x",
            VariableId = 1,
            MoveLocation = TestLocation,
            Reason = "moved by assignment"
        };

        checker.RecordMove(1, moveInfo);

        Assert.True(checker.IsFullyMoved(1));
        Assert.NotNull(checker.GetMoveInfo(1));
        Assert.Equal("x", checker.GetMoveInfo(1)!.VariableName);
    }

    [Fact]
    public void RecordFieldMove_TracksPartialMove()
    {
        var checker = CreateBorrowChecker();

        checker.RecordFieldMove(1, "point", "x", TestLocation, "field moved");

        Assert.False(checker.IsFullyMoved(1)); // Not fully moved
        Assert.True(checker.IsFieldMoved(1, "x")); // Field x is moved
        Assert.False(checker.IsFieldMoved(1, "y")); // Field y is not moved
    }

    [Fact]
    public void RecordFieldMove_MultipleFieldsMoved()
    {
        var checker = CreateBorrowChecker();

        checker.RecordFieldMove(1, "point", "x", TestLocation, "field moved");
        checker.RecordFieldMove(1, "point", "y", TestLocation, "field moved");

        Assert.True(checker.IsFieldMoved(1, "x"));
        Assert.True(checker.IsFieldMoved(1, "y"));
        Assert.False(checker.IsFieldMoved(1, "z"));
    }

    [Fact]
    public void IsFieldMoved_WhenFullyMoved_ReturnsTrueForAllFields()
    {
        var checker = CreateBorrowChecker();

        var moveInfo = new MoveInfo
        {
            VariableName = "point",
            VariableId = 1,
            MoveLocation = TestLocation,
            Reason = "moved",
            MovedFields = null // null means entire value moved
        };

        checker.RecordMove(1, moveInfo);

        Assert.True(checker.IsFieldMoved(1, "x"));
        Assert.True(checker.IsFieldMoved(1, "y"));
        Assert.True(checker.IsFieldMoved(1, "anyField"));
    }

    [Fact]
    public void Reset_ClearsAllMoves()
    {
        var checker = CreateBorrowChecker();

        checker.RecordMove(1, new MoveInfo
        {
            VariableName = "x",
            VariableId = 1,
            MoveLocation = TestLocation,
            Reason = "moved"
        });

        Assert.True(checker.IsFullyMoved(1));

        checker.Reset();

        Assert.False(checker.IsFullyMoved(1));
        Assert.Null(checker.GetMoveInfo(1));
    }

    [Fact]
    public void EnterBranch_ExitBranch_TracksBranchMoves()
    {
        var checker = CreateBorrowChecker();

        checker.EnterBranch(ControlFlowKind.If);

        checker.RecordMove(1, new MoveInfo
        {
            VariableName = "x",
            VariableId = 1,
            MoveLocation = TestLocation,
            Reason = "moved in branch"
        });

        // Inside branch, the move should NOT be visible at top level yet
        Assert.False(checker.IsFullyMoved(1));

        var branchMoves = checker.ExitBranch();

        // After exiting, moves should be in the returned dictionary
        Assert.True(branchMoves.ContainsKey(1));
        Assert.Equal("x", branchMoves[1].VariableName);
    }

    [Fact]
    public void MergeBranchMoves_MergesMovesFromMultipleBranches()
    {
        var checker = CreateBorrowChecker();

        // Create moves in two different branches
        var thenMoves = new Dictionary<int, MoveInfo>
        {
            [1] = new MoveInfo
            {
                VariableName = "x",
                VariableId = 1,
                MoveLocation = TestLocation,
                Reason = "moved in then"
            }
        };

        var elseMoves = new Dictionary<int, MoveInfo>
        {
            [2] = new MoveInfo
            {
                VariableName = "y",
                VariableId = 2,
                MoveLocation = TestLocation2,
                Reason = "moved in else"
            }
        };

        checker.MergeBranchMoves(thenMoves, elseMoves);

        // Both moves should now be recorded
        Assert.True(checker.IsFullyMoved(1));
        Assert.True(checker.IsFullyMoved(2));
    }

    [Fact]
    public void MergeBranchMoves_List_MergesAllBranches()
    {
        var checker = CreateBorrowChecker();

        var branches = new List<Dictionary<int, MoveInfo>>
        {
            new() { [1] = new MoveInfo { VariableName = "a", VariableId = 1, MoveLocation = TestLocation, Reason = "arm1" } },
            new() { [2] = new MoveInfo { VariableName = "b", VariableId = 2, MoveLocation = TestLocation2, Reason = "arm2" } },
            new() { [3] = new MoveInfo { VariableName = "c", VariableId = 3, MoveLocation = TestLocation3, Reason = "arm3" } }
        };

        checker.MergeBranchMoves(branches);

        Assert.True(checker.IsFullyMoved(1));
        Assert.True(checker.IsFullyMoved(2));
        Assert.True(checker.IsFullyMoved(3));
    }

    [Fact]
    public void RecordLoopMove_TracksLoopMoves()
    {
        var checker = CreateBorrowChecker();

        checker.RecordLoopMove(1, new MoveInfo
        {
            VariableName = "x",
            VariableId = 1,
            MoveLocation = TestLocation,
            Reason = "moved in loop"
        });

        Assert.True(checker.IsFullyMoved(1));
        Assert.Contains("moved in loop", checker.GetMoveInfo(1)!.Reason);
    }

    [Fact]
    public void DropInfo_UpdatedWhenMoved()
    {
        var dropInfo = new Dictionary<int, DropInfo>
        {
            [1] = new DropInfo
            {
                VariableId = 1,
                VariableName = "x",
                VariableType = IrIntType.I32,
                DeclLocation = TestLocation,
                WasMoved = false
            }
        };

        var checker = new BorrowChecker(dropInfo);

        checker.RecordMove(1, new MoveInfo
        {
            VariableName = "x",
            VariableId = 1,
            MoveLocation = TestLocation2,
            Reason = "moved"
        });

        // The drop info should be updated
        Assert.True(dropInfo[1].WasMoved);
    }

    [Fact]
    public void DropInfo_FieldMoveTracked()
    {
        var dropInfo = new Dictionary<int, DropInfo>
        {
            [1] = new DropInfo
            {
                VariableId = 1,
                VariableName = "point",
                VariableType = new IrStructType("Point", new List<IrStructField>
                {
                    new IrStructField("x", IrIntType.I32),
                    new IrStructField("y", IrIntType.I32)
                }),
                DeclLocation = TestLocation,
                WasMoved = false,
                MovedFields = null
            }
        };

        var checker = new BorrowChecker(dropInfo);

        checker.RecordFieldMove(1, "point", "x", TestLocation2, "field moved");

        // The drop info should have the moved field tracked
        Assert.NotNull(dropInfo[1].MovedFields);
        Assert.Contains("x", dropInfo[1].MovedFields!);
    }
}

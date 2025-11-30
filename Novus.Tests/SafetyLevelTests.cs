using Novus;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for SafetyLevel and SafetyLevelExtensions to improve coverage from 39.3% to 100%.
/// Tests all extension methods and safety level configurations.
/// </summary>
public class SafetyLevelTests
{
    [Fact]
    public void SafetyLevel_Unsafe_EnableBoundsChecking_ReturnsFalse()
    {
        var level = SafetyLevel.Unsafe;

        Assert.False(level.EnableBoundsChecking());
    }

    [Fact]
    public void SafetyLevel_Basic_EnableBoundsChecking_ReturnsTrue()
    {
        var level = SafetyLevel.Basic;

        Assert.True(level.EnableBoundsChecking());
    }

    [Fact]
    public void SafetyLevel_Full_EnableBoundsChecking_ReturnsTrue()
    {
        var level = SafetyLevel.Full;

        Assert.True(level.EnableBoundsChecking());
    }

    [Fact]
    public void SafetyLevel_Paranoid_EnableBoundsChecking_ReturnsTrue()
    {
        var level = SafetyLevel.Paranoid;

        Assert.True(level.EnableBoundsChecking());
    }

    [Fact]
    public void SafetyLevel_Unsafe_EnableDivisionByZeroChecks_ReturnsFalse()
    {
        var level = SafetyLevel.Unsafe;

        Assert.False(level.EnableDivisionByZeroChecks());
    }

    [Fact]
    public void SafetyLevel_Basic_EnableDivisionByZeroChecks_ReturnsTrue()
    {
        var level = SafetyLevel.Basic;

        Assert.True(level.EnableDivisionByZeroChecks());
    }

    [Fact]
    public void SafetyLevel_Full_EnableDivisionByZeroChecks_ReturnsTrue()
    {
        var level = SafetyLevel.Full;

        Assert.True(level.EnableDivisionByZeroChecks());
    }

    [Fact]
    public void SafetyLevel_Paranoid_EnableDivisionByZeroChecks_ReturnsTrue()
    {
        var level = SafetyLevel.Paranoid;

        Assert.True(level.EnableDivisionByZeroChecks());
    }

    [Fact]
    public void SafetyLevel_Unsafe_EnableOverflowChecks_ReturnsFalse()
    {
        var level = SafetyLevel.Unsafe;

        Assert.False(level.EnableOverflowChecks());
    }

    [Fact]
    public void SafetyLevel_Basic_EnableOverflowChecks_ReturnsFalse()
    {
        var level = SafetyLevel.Basic;

        Assert.False(level.EnableOverflowChecks());
    }

    [Fact]
    public void SafetyLevel_Full_EnableOverflowChecks_ReturnsTrue()
    {
        var level = SafetyLevel.Full;

        Assert.True(level.EnableOverflowChecks());
    }

    [Fact]
    public void SafetyLevel_Paranoid_EnableOverflowChecks_ReturnsTrue()
    {
        var level = SafetyLevel.Paranoid;

        Assert.True(level.EnableOverflowChecks());
    }

    [Fact]
    public void SafetyLevel_Unsafe_EnableNullChecks_ReturnsFalse()
    {
        var level = SafetyLevel.Unsafe;

        Assert.False(level.EnableNullChecks());
    }

    [Fact]
    public void SafetyLevel_Basic_EnableNullChecks_ReturnsFalse()
    {
        var level = SafetyLevel.Basic;

        Assert.False(level.EnableNullChecks());
    }

    [Fact]
    public void SafetyLevel_Full_EnableNullChecks_ReturnsTrue()
    {
        var level = SafetyLevel.Full;

        Assert.True(level.EnableNullChecks());
    }

    [Fact]
    public void SafetyLevel_Paranoid_EnableNullChecks_ReturnsTrue()
    {
        var level = SafetyLevel.Paranoid;

        Assert.True(level.EnableNullChecks());
    }

    [Fact]
    public void SafetyLevel_Unsafe_EnableParanoidChecks_ReturnsFalse()
    {
        var level = SafetyLevel.Unsafe;

        Assert.False(level.EnableParanoidChecks());
    }

    [Fact]
    public void SafetyLevel_Basic_EnableParanoidChecks_ReturnsFalse()
    {
        var level = SafetyLevel.Basic;

        Assert.False(level.EnableParanoidChecks());
    }

    [Fact]
    public void SafetyLevel_Full_EnableParanoidChecks_ReturnsFalse()
    {
        var level = SafetyLevel.Full;

        Assert.False(level.EnableParanoidChecks());
    }

    [Fact]
    public void SafetyLevel_Paranoid_EnableParanoidChecks_ReturnsTrue()
    {
        var level = SafetyLevel.Paranoid;

        Assert.True(level.EnableParanoidChecks());
    }

    [Fact]
    public void SafetyLevel_GetDefaultForBuildMode_Debug_ReturnsFull()
    {
        var level = SafetyLevelExtensions.GetDefaultForBuildMode(BuildMode.Debug);

        Assert.Equal(SafetyLevel.Full, level);
    }

    [Fact]
    public void SafetyLevel_GetDefaultForBuildMode_Release_ReturnsBasic()
    {
        var level = SafetyLevelExtensions.GetDefaultForBuildMode(BuildMode.Release);

        Assert.Equal(SafetyLevel.Basic, level);
    }

    [Fact]
    public void SafetyLevel_GetDescription_Unsafe_ReturnsCorrectDescription()
    {
        var level = SafetyLevel.Unsafe;

        var description = level.GetDescription();

        Assert.Equal("No runtime checks (footguns enabled)", description);
    }

    [Fact]
    public void SafetyLevel_GetDescription_Basic_ReturnsCorrectDescription()
    {
        var level = SafetyLevel.Basic;

        var description = level.GetDescription();

        Assert.Equal("Basic safety checks (bounds, division-by-zero)", description);
    }

    [Fact]
    public void SafetyLevel_GetDescription_Full_ReturnsCorrectDescription()
    {
        var level = SafetyLevel.Full;

        var description = level.GetDescription();

        Assert.Equal("Full safety checks (overflow, memory tracking, leak detection)", description);
    }

    [Fact]
    public void SafetyLevel_GetDescription_Paranoid_ReturnsCorrectDescription()
    {
        var level = SafetyLevel.Paranoid;

        var description = level.GetDescription();

        Assert.Equal("Paranoid mode (guard bytes, memory poisoning, maximum safety)", description);
    }

    [Fact]
    public void SafetyLevel_GetDescription_Invalid_ReturnsUnknown()
    {
        var level = (SafetyLevel)999;

        var description = level.GetDescription();

        Assert.Equal("Unknown", description);
    }

    [Fact]
    public void SafetyLevel_EnumValues_AreOrdered()
    {
        Assert.Equal(0, (int)SafetyLevel.Unsafe);
        Assert.Equal(1, (int)SafetyLevel.Basic);
        Assert.Equal(2, (int)SafetyLevel.Full);
        Assert.Equal(3, (int)SafetyLevel.Paranoid);
    }

    [Fact]
    public void SafetyLevel_Comparison_UnsafeLessThanBasic()
    {
        Assert.True(SafetyLevel.Unsafe < SafetyLevel.Basic);
    }

    [Fact]
    public void SafetyLevel_Comparison_BasicLessThanFull()
    {
        Assert.True(SafetyLevel.Basic < SafetyLevel.Full);
    }

    [Fact]
    public void SafetyLevel_Comparison_FullLessThanParanoid()
    {
        Assert.True(SafetyLevel.Full < SafetyLevel.Paranoid);
    }

    [Fact]
    public void SafetyLevel_AllLevels_GetDescription_NotNull()
    {
        foreach (SafetyLevel level in Enum.GetValues<SafetyLevel>())
        {
            var description = level.GetDescription();
            Assert.NotNull(description);
            Assert.NotEmpty(description);
        }
    }

    [Fact]
    public void SafetyLevel_AllLevels_BoundsCheckingLogic_IsConsistent()
    {
        // Unsafe: no bounds checking
        Assert.False(SafetyLevel.Unsafe.EnableBoundsChecking());

        // Basic and above: bounds checking enabled
        Assert.True(SafetyLevel.Basic.EnableBoundsChecking());
        Assert.True(SafetyLevel.Full.EnableBoundsChecking());
        Assert.True(SafetyLevel.Paranoid.EnableBoundsChecking());
    }

    [Fact]
    public void SafetyLevel_AllLevels_OverflowCheckingLogic_IsConsistent()
    {
        // Unsafe and Basic: no overflow checking (expensive)
        Assert.False(SafetyLevel.Unsafe.EnableOverflowChecks());
        Assert.False(SafetyLevel.Basic.EnableOverflowChecks());

        // Full and Paranoid: overflow checking enabled
        Assert.True(SafetyLevel.Full.EnableOverflowChecks());
        Assert.True(SafetyLevel.Paranoid.EnableOverflowChecks());
    }

    [Fact]
    public void SafetyLevel_Paranoid_EnablesAllChecks()
    {
        var level = SafetyLevel.Paranoid;

        Assert.True(level.EnableBoundsChecking());
        Assert.True(level.EnableDivisionByZeroChecks());
        Assert.True(level.EnableOverflowChecks());
        Assert.True(level.EnableNullChecks());
        Assert.True(level.EnableParanoidChecks());
    }

    [Fact]
    public void SafetyLevel_Unsafe_DisablesAllChecks()
    {
        var level = SafetyLevel.Unsafe;

        Assert.False(level.EnableBoundsChecking());
        Assert.False(level.EnableDivisionByZeroChecks());
        Assert.False(level.EnableOverflowChecks());
        Assert.False(level.EnableNullChecks());
        Assert.False(level.EnableParanoidChecks());
    }
}

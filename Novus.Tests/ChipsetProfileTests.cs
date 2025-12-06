using Novus.SemanticAnalysis;
using Xunit;

namespace Novus.Tests;

/// <summary>
/// Tests for ChipsetProfile and ChipsetCapabilities.
/// </summary>
public class ChipsetProfileTests
{
    #region Profile Parsing

    [Theory]
    [InlineData("OCS", ChipsetProfile.OCS)]
    [InlineData("ocs", ChipsetProfile.OCS)]
    [InlineData("ECS", ChipsetProfile.ECS)]
    [InlineData("ecs", ChipsetProfile.ECS)]
    [InlineData("AGA", ChipsetProfile.AGA)]
    [InlineData("aga", ChipsetProfile.AGA)]
    [InlineData("auto", ChipsetProfile.Auto)]
    [InlineData("AUTO", ChipsetProfile.Auto)]
    [InlineData("", ChipsetProfile.Auto)]
    [InlineData(null, ChipsetProfile.Auto)]
    public void Parse_ReturnsCorrectProfile(string? input, ChipsetProfile expected)
    {
        var result = ChipsetCapabilities.Parse(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Parse_InvalidInput_ReturnsAuto()
    {
        var result = ChipsetCapabilities.Parse("INVALID");
        Assert.Equal(ChipsetProfile.Auto, result);
    }

    #endregion

    #region Copper WAIT Position Validation

    [Fact]
    public void OCS_WaitPosition_MaxLine255()
    {
        // OCS Copper can only wait for lines 0-255
        Assert.True(ChipsetCapabilities.IsWaitPositionValid(255, 0, ChipsetProfile.OCS, out _));
        Assert.False(ChipsetCapabilities.IsWaitPositionValid(256, 0, ChipsetProfile.OCS, out _));
    }

    [Fact]
    public void ECS_WaitPosition_ExtendedRange()
    {
        // ECS can wait for lines 0-312 (PAL)
        Assert.True(ChipsetCapabilities.IsWaitPositionValid(256, 0, ChipsetProfile.ECS, out _));
        Assert.True(ChipsetCapabilities.IsWaitPositionValid(312, 0, ChipsetProfile.ECS, out _));
        Assert.False(ChipsetCapabilities.IsWaitPositionValid(313, 0, ChipsetProfile.ECS, out _));
    }

    [Fact]
    public void AGA_WaitPosition_SameAsECS()
    {
        // AGA has same wait range as ECS
        Assert.True(ChipsetCapabilities.IsWaitPositionValid(312, 0, ChipsetProfile.AGA, out _));
        Assert.False(ChipsetCapabilities.IsWaitPositionValid(313, 0, ChipsetProfile.AGA, out _));
    }

    [Fact]
    public void Auto_WaitPosition_UsesMostRestrictive()
    {
        // Auto mode uses OCS limits for compile-time validation
        Assert.True(ChipsetCapabilities.IsWaitPositionValid(255, 0, ChipsetProfile.Auto, out _));
        Assert.False(ChipsetCapabilities.IsWaitPositionValid(256, 0, ChipsetProfile.Auto, out _));
    }

    [Fact]
    public void WaitPosition_HorizontalRange()
    {
        // All chipsets: horizontal 0-226
        Assert.True(ChipsetCapabilities.IsWaitPositionValid(0, 0, ChipsetProfile.OCS, out _));
        Assert.True(ChipsetCapabilities.IsWaitPositionValid(0, 226, ChipsetProfile.OCS, out _));
        Assert.False(ChipsetCapabilities.IsWaitPositionValid(0, 227, ChipsetProfile.OCS, out _));
        Assert.False(ChipsetCapabilities.IsWaitPositionValid(0, -1, ChipsetProfile.OCS, out _));
    }

    [Fact]
    public void OCS_WaitPosition_Line256_ReturnsHelpfulError()
    {
        ChipsetCapabilities.IsWaitPositionValid(256, 0, ChipsetProfile.OCS, out var error);
        Assert.NotNull(error);
        Assert.Contains("OCS", error);
        Assert.Contains("ECS", error);
    }

    #endregion

    #region Color Validation

    [Fact]
    public void OCS_ColorIndex_Max32()
    {
        Assert.True(ChipsetCapabilities.IsColorIndexValid(0, ChipsetProfile.OCS, out _));
        Assert.True(ChipsetCapabilities.IsColorIndexValid(31, ChipsetProfile.OCS, out _));
        Assert.False(ChipsetCapabilities.IsColorIndexValid(32, ChipsetProfile.OCS, out _));
    }

    [Fact]
    public void AGA_ColorIndex_Max256()
    {
        Assert.True(ChipsetCapabilities.IsColorIndexValid(31, ChipsetProfile.AGA, out _));
        Assert.True(ChipsetCapabilities.IsColorIndexValid(255, ChipsetProfile.AGA, out _));
        Assert.False(ChipsetCapabilities.IsColorIndexValid(256, ChipsetProfile.AGA, out _));
    }

    [Fact]
    public void OCS_ColorValue_12Bit()
    {
        // OCS/ECS: 12-bit color (0x000-0xFFF)
        Assert.True(ChipsetCapabilities.IsColorValueValid(0x000, ChipsetProfile.OCS, out _));
        Assert.True(ChipsetCapabilities.IsColorValueValid(0xFFF, ChipsetProfile.OCS, out _));
        Assert.False(ChipsetCapabilities.IsColorValueValid(0x1000, ChipsetProfile.OCS, out _));
    }

    [Fact]
    public void AGA_ColorValue_24Bit()
    {
        // AGA: 24-bit color
        Assert.True(ChipsetCapabilities.IsColorValueValid(0xFFFFFF, ChipsetProfile.AGA, out _));
        Assert.False(ChipsetCapabilities.IsColorValueValid(0x1000000, ChipsetProfile.AGA, out _));
    }

    #endregion

    #region Bitplane Limits

    [Theory]
    [InlineData(ChipsetProfile.OCS, false, 5)]
    [InlineData(ChipsetProfile.OCS, true, 6)]  // HAM6
    [InlineData(ChipsetProfile.ECS, false, 5)]
    [InlineData(ChipsetProfile.ECS, true, 6)]  // HAM6
    [InlineData(ChipsetProfile.AGA, false, 8)]
    [InlineData(ChipsetProfile.AGA, true, 8)]  // HAM8
    public void MaxBitplanes_ReturnsCorrectValue(ChipsetProfile profile, bool isHam, int expected)
    {
        Assert.Equal(expected, ChipsetCapabilities.MaxBitplanes(profile, isHam));
    }

    #endregion

    #region Sprite Width

    [Fact]
    public void SpriteWidth_MustBe16()
    {
        // Hardware constraint: all Amiga sprites are 16 pixels wide
        Assert.True(ChipsetCapabilities.IsSpriteWidthValid(16, ChipsetProfile.OCS, out _));
        Assert.True(ChipsetCapabilities.IsSpriteWidthValid(16, ChipsetProfile.AGA, out _));

        Assert.False(ChipsetCapabilities.IsSpriteWidthValid(8, ChipsetProfile.OCS, out _));
        Assert.False(ChipsetCapabilities.IsSpriteWidthValid(32, ChipsetProfile.OCS, out _));
    }

    [Fact]
    public void SpriteWidth_InvalidWidth_ReturnsHelpfulError()
    {
        ChipsetCapabilities.IsSpriteWidthValid(32, ChipsetProfile.OCS, out var error);
        Assert.NotNull(error);
        Assert.Contains("16 pixels", error);
        Assert.Contains("hardware constraint", error);
    }

    #endregion

    #region Blitter Size

    [Fact]
    public void BlitterSize_ValidRange()
    {
        Assert.True(ChipsetCapabilities.IsBlitterSizeValid(16, 16, ChipsetProfile.OCS, out _));
        Assert.True(ChipsetCapabilities.IsBlitterSizeValid(1024, 1024, ChipsetProfile.OCS, out _));
    }

    [Fact]
    public void BlitterSize_TooLarge()
    {
        Assert.False(ChipsetCapabilities.IsBlitterSizeValid(1025, 100, ChipsetProfile.OCS, out _));
        Assert.False(ChipsetCapabilities.IsBlitterSizeValid(100, 1025, ChipsetProfile.OCS, out _));
    }

    [Fact]
    public void BlitterSize_OCS_MustBeWordAligned()
    {
        // OCS/ECS require word-aligned width
        Assert.True(ChipsetCapabilities.IsBlitterSizeValid(16, 100, ChipsetProfile.OCS, out _));
        Assert.True(ChipsetCapabilities.IsBlitterSizeValid(32, 100, ChipsetProfile.OCS, out _));
        Assert.False(ChipsetCapabilities.IsBlitterSizeValid(17, 100, ChipsetProfile.OCS, out var error));
        Assert.Contains("multiple of 16", error);
    }

    [Fact]
    public void BlitterSize_AGA_NoAlignmentRestriction()
    {
        // AGA can have arbitrary widths (byte-aligned)
        // Note: This may not be strictly true but represents enhanced capabilities
        Assert.True(ChipsetCapabilities.IsBlitterSizeValid(17, 100, ChipsetProfile.AGA, out _));
    }

    #endregion

    #region Register Access

    [Fact]
    public void RegisterAccess_CustomChipRange()
    {
        // Valid custom chip range
        Assert.True(ChipsetCapabilities.IsRegisterAccessible(0xDFF000, ChipsetProfile.OCS));
        Assert.True(ChipsetCapabilities.IsRegisterAccessible(0xDFF1FF, ChipsetProfile.OCS));

        // Outside custom chip range
        Assert.False(ChipsetCapabilities.IsRegisterAccessible(0xDFEFFF, ChipsetProfile.OCS));
        Assert.False(ChipsetCapabilities.IsRegisterAccessible(0xDFF200, ChipsetProfile.OCS));
        Assert.False(ChipsetCapabilities.IsRegisterAccessible(0x000000, ChipsetProfile.OCS));
    }

    #endregion
}

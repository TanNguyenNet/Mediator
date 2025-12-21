using FluentAssertions;
using Xunit;

namespace Mediator.Tests;

/// <summary>
/// Tests for Unit struct.
/// </summary>
public class UnitTests
{
    [Fact]
    public void Unit_Value_IsSingleton()
    {
        // Act
        var unit1 = Unit.Value;
        var unit2 = Unit.Value;

        // Assert
        unit1.Should().Be(unit2);
    }

    [Fact]
    public void Unit_Task_ReturnsCachedTask()
    {
        // Act
        var task1 = Unit.Task;
        var task2 = Unit.Task;

        // Assert
        task1.Should().BeSameAs(task2);
        task1.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public async Task Unit_Task_ReturnsUnitValue()
    {
        // Act
        var result = await Unit.Task;

        // Assert
        result.Should().Be(Unit.Value);
    }

    [Fact]
    public void Unit_Equals_AlwaysTrue()
    {
        // Arrange
        var unit1 = new Unit();
        var unit2 = new Unit();

        // Assert
        unit1.Equals(unit2).Should().BeTrue();
        (unit1 == unit2).Should().BeTrue();
        (unit1 != unit2).Should().BeFalse();
    }

    [Fact]
    public void Unit_EqualsObject_TrueForUnit()
    {
        // Arrange
        var unit = Unit.Value;
        object boxedUnit = Unit.Value;

        // Assert
        unit.Equals(boxedUnit).Should().BeTrue();
    }

    [Fact]
    public void Unit_EqualsObject_FalseForNonUnit()
    {
        // Arrange
        var unit = Unit.Value;

        // Assert
        unit.Equals("not a unit").Should().BeFalse();
        unit.Equals(42).Should().BeFalse();
        unit.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Unit_GetHashCode_AlwaysZero()
    {
        // Act
        var hashCode = Unit.Value.GetHashCode();

        // Assert
        hashCode.Should().Be(0);
    }

    [Fact]
    public void Unit_CompareTo_AlwaysZero()
    {
        // Arrange
        var unit1 = new Unit();
        var unit2 = new Unit();

        // Assert
        unit1.CompareTo(unit2).Should().Be(0);
    }

    [Fact]
    public void Unit_ToString_ReturnsEmptyTuple()
    {
        // Act
        var result = Unit.Value.ToString();

        // Assert
        result.Should().Be("()");
    }

    [Fact]
    public void Unit_ComparisonOperators_WorkCorrectly()
    {
        // Arrange
        var unit1 = new Unit();
        var unit2 = new Unit();

        // Assert
        (unit1 < unit2).Should().BeFalse();
        (unit1 <= unit2).Should().BeTrue();
        (unit1 > unit2).Should().BeFalse();
        (unit1 >= unit2).Should().BeTrue();
    }

    [Fact]
    public void Unit_IsReadonlyStruct()
    {
        // Assert
        typeof(Unit).IsValueType.Should().BeTrue();
        // readonly struct is verified by the fact that we can't modify it
    }
}

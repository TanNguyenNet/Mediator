using System.Runtime.CompilerServices;

namespace Mediator;

/// <summary>
/// Represents a void type, since void is not a valid return type in C#.
/// This is a readonly struct to avoid heap allocations.
/// </summary>
public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
{
    /// <summary>
    /// Gets the singleton Unit value.
    /// </summary>
    public static readonly Unit Value = new();

    /// <summary>
    /// Gets a cached completed Task returning Unit.
    /// Use this to avoid allocating a new Task for void operations.
    /// </summary>
    public static readonly Task<Unit> Task = System.Threading.Tasks.Task.FromResult(Value);

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override int GetHashCode() => 0;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Unit other) => true;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override bool Equals(object? obj) => obj is Unit;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Unit other) => 0;

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    int IComparable.CompareTo(object? obj) => 0;

    /// <inheritdoc />
    public override string ToString() => "()";

    /// <summary>
    /// Determines whether two Unit instances are equal.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>
    /// Determines whether two Unit instances are not equal.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Unit left, Unit right) => false;

    /// <summary>
    /// Compares two Unit instances.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Unit left, Unit right) => false;

    /// <summary>
    /// Compares two Unit instances.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Unit left, Unit right) => true;

    /// <summary>
    /// Compares two Unit instances.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Unit left, Unit right) => false;

    /// <summary>
    /// Compares two Unit instances.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Unit left, Unit right) => true;
}

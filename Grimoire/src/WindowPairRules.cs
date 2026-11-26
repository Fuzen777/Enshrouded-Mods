using System;

namespace Grimoire;

/// <summary>
/// The geometry of two mod windows kept side by side (WindowPair), without the game so the tests run without Kiln. Rects are in any one
/// unit (pixels or fractions of the viewport), left and top smaller than right and bottom. The same file is in Grimoire and Better Spells;
/// only the namespace differs.
/// </summary>
public static class WindowPairRules
{
    public readonly record struct Rect(float Left, float Top, float Right, float Bottom)
    {
        public float Width => Right - Left;
        public float Height => Bottom - Top;
    }

    /// <summary>
    /// Whether a window <paramref name="width"/> wide goes on the right of the other window (whose edges are given as fractions of the
    /// viewport): on the preferred side when it fits there, else on the side with more room. A width of 0 (not known yet) takes the
    /// preferred side.
    /// </summary>
    public static bool BesideOnRight(float otherLeft, float otherRight, float width, bool preferRight)
    {
        float roomRight = 1f - otherRight;
        float roomLeft = otherLeft;
        return preferRight ? width <= roomRight || roomRight >= roomLeft : !(width <= roomLeft || roomLeft >= roomRight);
    }

    /// <summary>The two windows touch side by side: one's left edge on the other's right edge (within <paramref name="tolerance"/>), and
    /// they overlap in height.</summary>
    public static bool Adjacent(Rect me, Rect other, float tolerance) =>
        OverlapInHeight(me, other)
        && (MathF.Abs(me.Left - other.Right) <= tolerance || MathF.Abs(me.Right - other.Left) <= tolerance);

    /// <summary>Whether <paramref name="me"/> is on the right side of <paramref name="other"/> (its centre further right).</summary>
    public static bool OnRight(Rect me, Rect other) => me.Left + me.Right > other.Left + other.Right;

    /// <summary>The top left corner that puts a window <paramref name="width"/> wide against <paramref name="other"/> on the given side,
    /// its top <paramref name="offsetY"/> below the other's top (negative: above).</summary>
    public static (float Left, float Top) Docked(Rect other, float width, bool onRight, float offsetY) =>
        (onRight ? other.Right : other.Left - width, other.Top + offsetY);

    /// <summary>
    /// Where a window dropped near the other one snaps to: against it when the facing edges are within <paramref name="distance"/> and
    /// the two overlap in height, with the tops lined up too when they are within that distance; null when it is not near.
    /// </summary>
    public static (float Left, float Top)? Snap(Rect me, Rect other, float distance)
    {
        if (!OverlapInHeight(me, other))
        {
            return null;
        }
        float left;
        if (MathF.Abs(me.Left - other.Right) <= distance)
        {
            left = other.Right;
        }
        else if (MathF.Abs(me.Right - other.Left) <= distance)
        {
            left = other.Left - me.Width;
        }
        else
        {
            return null;
        }
        float top = MathF.Abs(me.Top - other.Top) <= distance ? other.Top : me.Top;
        return (left, top);
    }

    private static bool OverlapInHeight(Rect a, Rect b) => a.Top < b.Bottom && b.Top < a.Bottom;
}

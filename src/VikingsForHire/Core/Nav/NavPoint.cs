using System;

namespace VikingsForHire.Core.Nav
{
    /// <summary>A point in the world (Core can't use UnityEngine.Vector3; the game side converts with NavConvert).</summary>
    public readonly struct NavPoint : IEquatable<NavPoint>
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;

        public NavPoint(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float Distance(NavPoint o)
        {
            float dx = X - o.X, dy = Y - o.Y, dz = Z - o.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        public float DistanceXZ(NavPoint o)
        {
            float dx = X - o.X, dz = Z - o.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public static NavPoint Lerp(NavPoint a, NavPoint b, float t) =>
            new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

        public bool Equals(NavPoint o) => X == o.X && Y == o.Y && Z == o.Z;
        public override bool Equals(object? obj) => obj is NavPoint o && Equals(o);
        public override int GetHashCode() => (X.GetHashCode() * 397 ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode();

        public override string ToString() => FormattableString.Invariant($"({X:0.0}, {Y:0.0}, {Z:0.0})");
    }
}

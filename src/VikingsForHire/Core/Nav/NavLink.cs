using System.Collections.Generic;

namespace VikingsForHire.Core.Nav
{
    public enum NavLinkKind
    {
        Door,
        Stair,
    }

    /// <summary>
    /// One way through that the game's walking map doesn't know: a door (open or closed) or a stair/ladder between two
    /// floors. A and B are its ends: the two sides of a door, or the bottom and top of a stair. A rescan builds new link
    /// objects, so a route holding one keeps a valid copy of it.
    /// </summary>
    public sealed class NavLink
    {
        public int Id { get; internal set; }
        public NavLinkKind Kind { get; }
        public NavPoint A { get; }
        public NavPoint B { get; }
        /// <summary>A stair's surface points from A to B (both ends included); empty for a door.</summary>
        public IReadOnlyList<NavPoint> Waypoints { get; }
        public float Length { get; }
        public bool IsLadder { get; }
        /// <summary>The piece's ZDOID as text, for finding the door again, logs and the overlay.</summary>
        public string PieceId { get; }
        public string Prefab { get; }
        /// <summary>Game time (s) until which routes don't use it (a locked door, a stair it couldn't walk).</summary>
        public double BlockedUntil { get; set; }

        public NavLink(NavLinkKind kind, NavPoint a, NavPoint b, IReadOnlyList<NavPoint>? waypoints, string pieceId, string prefab,
            bool isLadder = false)
        {
            Kind = kind;
            A = a;
            B = b;
            Waypoints = waypoints ?? new List<NavPoint>();
            PieceId = pieceId;
            Prefab = prefab;
            IsLadder = isLadder;
            float length = 0f;
            for (int i = 1; i < Waypoints.Count; i++)
                length += Waypoints[i - 1].Distance(Waypoints[i]);
            Length = length > 0f ? length : a.Distance(b);
        }

        /// <summary>Cost of using it in a route: a door counts as a few steps, a stair as its length.</summary>
        public float Cost => Kind == NavLinkKind.Door ? 2.4f : Length;

        public bool Blocked(double now) => now < BlockedUntil;

        public NavPoint End(bool fromA) => fromA ? B : A;
        public NavPoint Start(bool fromA) => fromA ? A : B;

        public override string ToString() => $"{Kind.ToString().ToLowerInvariant()}#{Id}";
    }
}

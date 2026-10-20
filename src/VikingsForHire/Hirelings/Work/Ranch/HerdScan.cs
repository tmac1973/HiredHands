using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Core.Orders;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Work.Ranch
{
    /// <summary>The herd in an area: every tame of a catalog kind (or its young), as the Rancher's rules see them.</summary>
    internal static class HerdScan
    {
        // Far enough to find the far walls and corners of a big pen from a boar standing by one wall (a 20 m stone-walled
        // yard: at 15 m a boar near one side was "out of its pen").
        private const float PenReach = 32f;
        private static readonly int PenMask = LayerMask.GetMask("piece", "piece_nonsolid");
        // Each animal's pen state as last seen (by ZDO id): true once it's been seen walled in.
        // Each animal's pen, once it's been seen in one: the Pen sign's spot, or where it stood walled in (no sign about).
        private static readonly Dictionary<ZDOID, Vector3> Home = new();

        // A bank of earth counts as a wall too (Tim's pen is a terrace cut below the base: its boars inside read as out, as
        // the lines to the bank met no building): ground rising this much above the animal within TerrainReach.
        private const float TerrainReach = 12f;
        private const float TerrainWall = 1.2f;
        private static readonly int TerrainMask = LayerMask.GetMask("terrain");

        /// <summary>
        /// Walled in on every side: in all 8 directions, low down (at a quarter, half or nearly a metre up), a building piece
        /// within 32 m or a steep bank of ground (1.2 m up) within 12 m.
        /// </summary>
        public static bool Enclosed(Vector3 at) => OpenSides(at).Count == 0;

        /// <summary>The directions (degrees from north) in which nothing walls an animal in: empty when it's enclosed.</summary>
        public static List<int> OpenSides(Vector3 at)
        {
            var open = new List<int>();
            Vector3 from = at + Vector3.up * 0.5f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                if (WallAt(at, dir))
                    continue;
                if (Physics.Raycast(from, dir, out RaycastHit bank, TerrainReach, TerrainMask, QueryTriggerInteraction.Ignore) &&
                    ZoneSystem.instance != null && ZoneSystem.instance.GetGroundHeight(bank.point + dir * 0.6f) >= at.y + TerrainWall)
                    continue;
                open.Add(i * 45);
            }
            return open;
        }

        // Each way is looked along at a few heights: one line at knee height grazed the top of half walls sunk half into the
        // ground (0.5 m showing), and a boar standing a hair higher read as out of the pen it was pressed against
        // (VFH-RANCH-11: "escaped", not "crowded").
        private static readonly float[] WallHeights = { 0.25f, 0.5f, 0.9f };

        private static bool WallAt(Vector3 at, Vector3 dir)
        {
            foreach (float h in WallHeights)
                if (Physics.Raycast(at + Vector3.up * h, dir, PenReach, PenMask, QueryTriggerInteraction.Ignore))
                    return true;
            return false;
        }

        /// <summary>
        /// In a pen: inside a Pen sign's circle where there are signs near (a terrace dug below the base, a walled basement:
        /// the signs say which is the pen), else walled in all round.
        /// </summary>
        public static bool Penned(Vector3 at)
        {
            bool signs = false;
            foreach (PenSign s in PenSign.Near(at))
            {
                signs = true;
                if (s.Contains(at))
                    return true;
            }
            return !signs && Enclosed(at);
        }

        // Out of the pen it was in: seen penned before and not now. One never seen penned (free range) never counts.
        internal static bool Escaped(ZDOID id, Vector3 at)
        {
            if (Penned(at))
            {
                Home[id] = PenSign.At(at) is PenSign sign ? sign.transform.position : at;
                return false;
            }
            if (!Home.TryGetValue(id, out Vector3 home))
                return false;
            VfhLog.D(LogCat.Work, "ranch.out_of_pen", ("at", at), ("ground", ZoneSystem.instance != null ? ZoneSystem.instance.GetGroundHeight(at) : 0f),
                ("home", home), ("open", string.Join(",", OpenSides(at))));
            return true;
        }

        /// <summary>Where an animal's pen is (its sign, or where it was last walled in), if it's been seen in one.</summary>
        public static Vector3? HomeOf(ZDOID id) => Home.TryGetValue(id, out Vector3 p) ? p : null;
        public sealed class Found
        {
            public HerdAnimal Model = new();
            public Character Animal = null!;
        }

        public static List<Found> Scan(Vector3 centre, float radius, Func<Vector3, bool> mayUse)
        {
            var found = new List<Found>();
            foreach (Character c in Character.GetAllCharacters())
            {
                if (c == null || c.IsPlayer() || !c.IsTamed() || c.IsDead() || c.GetComponent<Hireling>() != null || c.m_nview == null || !c.m_nview.IsValid())
                    continue;
                string prefab = Utils.GetPrefabName(c.gameObject);
                string? kind = AnimalCatalog.KindOf(prefab);
                if (kind == null || Utils.DistanceXZ(c.transform.position, centre) > radius || !mayUse(c.transform.position))
                    continue;
                ZDO zdo = c.m_nview.GetZDO();
                Vector3 p = c.transform.position;
                found.Add(new Found
                {
                    Animal = c,
                    Model = new HerdAnimal
                    {
                        Id = zdo.m_uid.ToString(), Kind = kind, Stars = Mathf.Max(0, c.GetLevel() - 1), Adult = prefab == kind,
                        Named = NamedByPlayer(c, zdo), X = p.x, Z = p.z, Hungry = c.GetComponent<Tameable>()?.IsHungry() ?? false,
                        Escaped = Escaped(zdo.m_uid, p),
                    },
                });
            }
            return found.OrderBy(f => f.Model.Id, StringComparer.Ordinal).ToList();
        }

        /// <summary>
        /// Named by a player (a pet): some kinds get a random name when tamed, which doesn't count; a name a player gave
        /// carries its author.
        /// </summary>
        public static bool NamedByPlayer(Character c, ZDO zdo)
        {
            string name = zdo.GetString(ZDOVars.s_tamedName);
            if (name.Length == 0)
                return false;
            if (zdo.GetString(ZDOVars.s_tamedNameAuthor).Length > 0)
                return true;
            Tameable? t = c.GetComponent<Tameable>();
            return t == null || t.m_randomStartingName == null || !t.m_randomStartingName.Contains(name);
        }
    }
}

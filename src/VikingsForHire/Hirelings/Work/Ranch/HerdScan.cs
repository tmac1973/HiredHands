using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Core.Orders;

namespace VikingsForHire.Hirelings.Work.Ranch
{
    /// <summary>The herd in an area: every tame of a catalog kind (or its young), as the Rancher's rules see them.</summary>
    internal static class HerdScan
    {
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

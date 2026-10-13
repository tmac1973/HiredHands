using System.Collections.Generic;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>
    /// Creatures a hireling calls up for a while (Raise dead's skeleton): tamed, following it, counted against no cap,
    /// and removed when their time is up, when the hireling is gone or dead, or when it's left them far behind.
    /// Only the summoner's owner keeps the list; a summon left over after a reload is removed by its unsummon distance.
    /// </summary>
    internal static class SummonedAllies
    {
        private const float LeftBehind = 40f;

        private sealed class Summon
        {
            public Character Creature = null!;
            public Hireling Owner = null!;
            public float Until;
        }

        private static readonly List<Summon> Summons = new();

        public static bool Raise(Hireling owner, string prefab, float seconds)
        {
            GameObject? go = ZNetScene.instance?.GetPrefab(prefab);
            if (go == null)
            {
                VfhLog.W(LogCat.Combat, "summon.missing", ("prefab", prefab));
                return false;
            }
            Vector3 at = owner.transform.position + owner.transform.right * 1.5f;
            if (ZoneSystem.instance.GetSolidHeight(at, out float y, 1000))
                at.y = y + 0.2f;
            GameObject made = Object.Instantiate(go, at, owner.transform.rotation);
            Character? c = made.GetComponent<Character>();
            if (c == null)
            {
                ZNetScene.instance.Destroy(made);
                return false;
            }
            c.SetTamed(true);
            if (c.GetBaseAI() is MonsterAI ai)
                ai.SetFollowTarget(owner.gameObject);
            if (made.GetComponent<Tameable>() is Tameable t)
                t.m_unsummonDistance = LeftBehind;
            Summons.Add(new Summon { Creature = c, Owner = owner, Until = Time.time + seconds });
            VfhLog.D(LogCat.Combat, "enh.raise_dead", ("hid", owner.Hid), ("prefab", prefab), ("seconds", seconds));
            return true;
        }

        /// <summary>Living summons within a radius of a point (tests).</summary>
        public static int Near(Vector3 at, float radius)
        {
            int n = 0;
            foreach (Summon s in Summons)
                if (s.Creature != null && !s.Creature.IsDead() && Vector3.Distance(s.Creature.transform.position, at) <= radius)
                    n++;
            return n;
        }

        /// <summary>Once a frame or so (from the plugin's update): removes summons whose time is up.</summary>
        public static void Tick()
        {
            for (int i = Summons.Count - 1; i >= 0; i--)
            {
                Summon s = Summons[i];
                bool gone = s.Creature == null || s.Creature.IsDead();
                bool expire = !gone && (Time.time >= s.Until || s.Owner == null || s.Owner.Humanoid.IsDead() ||
                                        Vector3.Distance(s.Owner.transform.position, s.Creature.transform.position) > LeftBehind);
                if (expire && s.Creature.m_nview != null && s.Creature.m_nview.IsValid() && s.Creature.m_nview.IsOwner())
                {
                    VfhLog.D(LogCat.Combat, "enh.raise_dead_end", ("prefab", s.Creature.m_name));
                    ZNetScene.instance.Destroy(s.Creature.gameObject);
                }
                if (gone || expire)
                    Summons.RemoveAt(i);
            }
        }
    }
}

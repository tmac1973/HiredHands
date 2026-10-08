using System.Collections.Generic;
using VikingsForHire.Core;
using VikingsForHire.Hirelings;
using UnityEngine;

namespace VikingsForHire.Telemetry
{
    /// <summary>
    /// Fight summaries for the balance log, on the machine simulating the hireling: from engaging an enemy to the end of
    /// that fight (kill, death, giving up, retreat), with damage dealt and taken. One record per fight, nothing per hit.
    /// </summary>
    internal static class BalanceFights
    {
        private sealed class Fight
        {
            public Character Enemy = null!;
            public string EnemyName = "";
            public int EnemyLevel;
            public float EnemyMax;
            public float Start;
            public float Dealt;
            public float Taken;
            public int HitsDealt;
            public int HitsTaken;
            public string Mode = "";
            public Hirelings.Combat.DefenseStats Defense = null!;
        }

        private static readonly Dictionary<Hireling, Fight> Fights = new();

        public static void Start(Hireling h, Character enemy)
        {
            if (!BalanceLog.On || enemy == null)
                return;
            if (Fights.TryGetValue(h, out Fight f) && f.Enemy == enemy)
                return;
            End(h, "switched");
            Fights[h] = new Fight
            {
                Enemy = enemy,
                EnemyName = Utils.GetPrefabName(enemy.gameObject),
                EnemyLevel = enemy.GetLevel(),
                EnemyMax = enemy.GetMaxHealth(),
                Start = Time.time,
                Mode = h.Mode == HirelingMode.Following ? "follower" : h.HasPost ? "posted" : "base",
                Defense = h.Ai.Defense.Snapshot(),
            };
        }

        public static void Dealt(Hireling h, Character target, float damage)
        {
            if (Fights.TryGetValue(h, out Fight f) && f.Enemy == target)
            {
                f.Dealt += damage;
                f.HitsDealt++;
            }
        }

        public static void Taken(Hireling h, float damage)
        {
            if (damage >= 1e6f)
                return; // a test cleanup's kill, not a fight's damage
            if (Fights.TryGetValue(h, out Fight f))
            {
                f.Taken += damage;
                f.HitsTaken++;
            }
        }

        /// <summary>The fight is over; <paramref name="why"/> is the combat behaviour's reason (a dead enemy counts as a kill).</summary>
        public static void End(Hireling h, string why)
        {
            if (!Fights.TryGetValue(h, out Fight f))
                return;
            Fights.Remove(h);
            if (!BalanceLog.On)
                return;
            bool killed = f.Enemy == null || f.Enemy.IsDead();
            Humanoid me = h.Humanoid;
            var fields = new List<(string, object?)>
            {
                ("hid", Short(h.Hid)), ("owner", BalanceLog.Who(h.OwnerId)), ("job", h.Job), ("lvl", h.Level), ("stance", h.Stance), ("mode", f.Mode),
                ("weapon", GearApplier.Name(me.GetCurrentWeapon())), ("armor", h.Armor), ("hpMax", me.GetMaxHealth()), ("hpEnd", me.GetHealth()),
                ("enemy", f.EnemyName), ("elvl", f.EnemyLevel), ("ehpMax", f.EnemyMax), ("ehpEnd", f.Enemy != null ? f.Enemy.GetHealth() : 0f),
                ("biome", Biome(h.transform.position)), ("secs", Time.time - f.Start), ("dealt", f.Dealt), ("taken", f.Taken),
                ("hitsDealt", f.HitsDealt), ("hitsTaken", f.HitsTaken), ("outcome", killed ? "kill" : me.IsDead() ? "died" : why),
            };
            // Blocking and dodging during this fight (0.6.0), and the CombatSkill preset.
            fields.AddRange(h.Ai.Defense.Since(f.Defense));
            fields.Add(("defense", Config.VfhConfig.CombatSkill.Value.ToString().ToLowerInvariant())); // off, green, trained, veteran
            BalanceLog.Record("fight", fields.ToArray());
        }

        public static void Died(Hireling h, string killer, float lastHit)
        {
            End(h, "died");
            BalanceLog.Record("death", ("hid", Short(h.Hid)), ("owner", BalanceLog.Who(h.OwnerId)), ("job", h.Job), ("lvl", h.Level),
                ("mode", h.Mode == HirelingMode.Following ? "follower" : h.HasPost ? "posted" : "base"), ("hpMax", h.Humanoid.GetMaxHealth()),
                ("armor", h.Armor), ("killer", killer), ("lastHit", lastHit), ("biome", Biome(h.transform.position)));
        }

        /// <summary>Forget fights of hirelings that were unloaded mid-fight (their objects are gone).</summary>
        public static void Prune()
        {
            foreach (Hireling h in new List<Hireling>(Fights.Keys))
            {
                if (h == null)
                    Fights.Remove(h!);
            }
        }

        public static string Short(string hid) => hid.Length > 4 ? hid.Substring(0, 4) : hid;

        public static string Biome(Vector3 p) =>
            p.y > 4000f ? "Dungeon" : WorldGenerator.instance != null ? WorldGenerator.instance.GetBiome(p).ToString() : "";
    }
}

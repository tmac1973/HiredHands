using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using VikingsForHire.Board;
using VikingsForHire.Commands;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings;
using VikingsForHire.Net;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VikingsForHire.Testing
{
    internal static class FixturesCombat
    {
        public static void Register()
        {
            Fixtures.Add("enemies", "<prefab> <n> <distance> - spawn creatures that far from the last spawned hireling", Enemies);
            Fixtures.Add("tame_ally", "<prefab> <distance> - spawn a tamed creature (tagged tame) that far from the last spawned hireling", TameAlly);
            TestHarness.RegisterCheck("tame_alive", "- whether the tame from tame_ally is alive", _ =>
                FixturesWork.FindTagged("tame") is GameObject t && t.GetComponent<Character>() is Character c && !c.IsDead() ? "true" : "false");
            TestHarness.RegisterCheck("damage_blocked", "<reason> - hits between hirelings and others blocked since login (e.g. tame_on_hireling)", args =>
                Hirelings.DamagePatches.BlockedCount(args.ElementAtOrDefault(0) ?? "").ToString());
            Fixtures.Add("kill_enemies", "<radius=60> - kill every hostile creature near you", KillEnemies);
            Fixtures.Add("stance", "<last|all> <stance> - set the stance of the last spawned hireling (or all nearby)", SetStance);
            Fixtures.Add("wait", "<seconds> - pause the test run", args => Wait(float.Parse(args.ElementAtOrDefault(0) ?? "1", CultureInfo.InvariantCulture)));

            Fixtures.Add("enemy_level", "<prefab> <stars> [all] - give the nearest enemy of that kind (or all within 80 m) that many stars (level = stars + 1)", EnemyLevel);
            Fixtures.Add("ghost", "<on|off> - enemies ignore you (so they fight the hirelings, not the tester); every test starts with it on, so only `off` is needed, for a row where something must come for you", Ghost);
            Fixtures.Add("defense_chances", "<level> <read> <parry> <dodge> | reset - override a level's blocking/dodging chances (in memory, for tests); reset puts the loaded ones back", DefenseChances);
            Fixtures.Add("hireling_health", "<fraction> - set the last spawned hireling's health to that share of its max", HirelingHealth);
            TestHarness.RegisterCheck("defense", "<last|all> <reads|misses|blocks|parries|dodges|dodged_hits> - blocking/dodging counters since the hireling loaded (all: summed over hirelings within 50 m)", args =>
            {
                string counter = args.ElementAtOrDefault(1) ?? "reads";
                var targets = args.ElementAtOrDefault(0) == "all"
                    ? Hireling.Loaded.Where(h => h != null && Vector3.Distance(h.transform.position, Player.m_localPlayer.transform.position) < 50f).ToList()
                    : new[] { Last() }.ToList();
                return targets.Sum(h => h.Ai.Defense.Get(counter)).ToString();
            });

            TestHarness.RegisterCheck("fight_over", "- true when no enemy is alive within 40 m of you, or no hireling is", _ =>
                !Hostiles(40f).Any() || !Hireling.Loaded.Any(h => h != null && !h.Humanoid.IsDead()) ? "true" : "false");

            TestHarness.RegisterCheck("enemies_alive", "<radius=40> - hostile creatures alive near you", args =>
            {
                float r = args.Length > 0 ? float.Parse(args[0], CultureInfo.InvariantCulture) : 40f;
                return Hostiles(r).Count().ToString();
            });
        }

        private static Hireling Last()
        {
            // The last one spawned by a fixture if it's still here, else the one from your last contract.
            string? spawned = HirelingCommands.LastSpawned.LastOrDefault();
            return Hireling.Loaded.FirstOrDefault(h => h != null && spawned != null && h.Hid == spawned)
                   ?? Hireling.Loaded.FirstOrDefault(h => h != null && h.Hid == BoardContracts.LastPostedHid)
                   ?? throw new InvalidOperationException("the last spawned hireling isn't loaded");
        }

        private static IEnumerator Enemies(string[] args)
        {
            string prefab = args.ElementAtOrDefault(0) ?? "Greyling";
            int n = int.Parse(args.ElementAtOrDefault(1) ?? "1", CultureInfo.InvariantCulture);
            float dist = float.Parse(args.ElementAtOrDefault(2) ?? "10", CultureInfo.InvariantCulture);
            GameObject go = ZNetScene.instance.GetPrefab(prefab) ?? throw new ArgumentException($"no prefab {prefab}");
            Vector3 center = Last().transform.position;
            float start = UnityEngine.Random.Range(0f, 360f);
            for (int i = 0; i < n; i++)
            {
                Vector3 p = center + Quaternion.Euler(0f, start + i * 25f, 0f) * Vector3.forward * dist;
                p.y = ZoneSystem.instance.GetGroundHeight(p) + 0.5f;
                GameObject e = Object.Instantiate(go, p, Quaternion.LookRotation(center - p));
                e.GetComponent<ZNetView>()?.GetZDO()?.Set(BoardZdo.Fixture, true);
            }
            VfhLog.I(LogCat.Test, "fixture.enemies", ("prefab", prefab), ("n", n), ("dist", dist), ("around", Last().Hid));
            yield return new WaitForSeconds(0.5f);
        }

        private static IEnumerator TameAlly(string[] args)
        {
            string prefab = args.ElementAtOrDefault(0) ?? "Troll";
            float dist = float.Parse(args.ElementAtOrDefault(1) ?? "3", CultureInfo.InvariantCulture);
            GameObject go = ZNetScene.instance.GetPrefab(prefab) ?? throw new ArgumentException($"no prefab {prefab}");
            Vector3 p = Last().transform.position + Last().transform.right * dist;
            p.y = ZoneSystem.instance.GetGroundHeight(p) + 0.5f;
            GameObject t = Object.Instantiate(go, p, Quaternion.identity);
            ZDO zdo = t.GetComponent<ZNetView>().GetZDO();
            zdo.Set(BoardZdo.Fixture, true);
            zdo.Set(FixturesWork.TagKey, "tame");
            t.GetComponent<Character>().SetTamed(true);
            VfhLog.I(LogCat.Test, "fixture.tame_ally", ("prefab", prefab), ("pos", p));
            yield return new WaitForSeconds(0.5f);
        }

        private static IEnumerator KillEnemies(string[] args)
        {
            float r = args.Length > 0 ? float.Parse(args[0], CultureInfo.InvariantCulture) : 60f;
            int n = 0;
            foreach (Character c in Hostiles(r).ToList())
            {
                var hit = new HitData { m_point = c.transform.position };
                hit.m_damage.m_damage = 1e7f;
                c.Damage(hit);
                n++;
            }
            VfhLog.I(LogCat.Test, "fixture.kill_enemies", ("killed", n));
            yield return new WaitForSeconds(1f);
        }

        private static IEnumerator EnemyLevel(string[] args)
        {
            string prefab = args.ElementAtOrDefault(0) ?? throw new ArgumentException("usage: enemy_level <prefab> <stars>");
            int stars = int.Parse(args.ElementAtOrDefault(1) ?? "1", CultureInfo.InvariantCulture);
            Vector3 me = Player.m_localPlayer.transform.position;
            var found = Hostiles(80f).Where(x => Utils.GetPrefabName(x.gameObject) == prefab)
                .OrderBy(x => Vector3.Distance(x.transform.position, me)).ToList();
            if (found.Count == 0)
                throw new InvalidOperationException($"no {prefab} nearby");
            foreach (Character c in args.ElementAtOrDefault(2) == "all" ? found : found.Take(1).ToList())
                c.SetLevel(stars + 1);
            VfhLog.I(LogCat.Test, "fixture.enemy_level", ("prefab", prefab), ("stars", stars), ("n", args.ElementAtOrDefault(2) == "all" ? found.Count : 1));
            yield return null;
        }

        private static readonly System.Collections.Generic.Dictionary<int, (float, float, float)> SavedChances = new();

        private static IEnumerator DefenseChances(string[] args)
        {
            var levels = Config.DataStore.Current.HirelingLevels;
            if (args.ElementAtOrDefault(0) == "reset")
            {
                foreach (var l in levels)
                    if (SavedChances.TryGetValue(l.Level, out var c))
                        (l.ReadChance, l.ParryChance, l.DodgeChance) = c;
                SavedChances.Clear();
                VfhLog.I(LogCat.Test, "fixture.defense_chances", ("reset", true));
                yield break;
            }
            int level = int.Parse(args.ElementAtOrDefault(0) ?? "1", CultureInfo.InvariantCulture);
            var data = levels.First(l => l.Level == level);
            if (!SavedChances.ContainsKey(level))
                SavedChances[level] = (data.ReadChance, data.ParryChance, data.DodgeChance);
            data.ReadChance = float.Parse(args.ElementAtOrDefault(1) ?? "1", CultureInfo.InvariantCulture);
            data.ParryChance = float.Parse(args.ElementAtOrDefault(2) ?? "1", CultureInfo.InvariantCulture);
            data.DodgeChance = float.Parse(args.ElementAtOrDefault(3) ?? "1", CultureInfo.InvariantCulture);
            VfhLog.I(LogCat.Test, "fixture.defense_chances", ("level", level), ("read", data.ReadChance), ("parry", data.ParryChance), ("dodge", data.DodgeChance));
            yield return null;
        }

        private static IEnumerator HirelingHealth(string[] args)
        {
            float f = float.Parse(args.ElementAtOrDefault(0) ?? "1", CultureInfo.InvariantCulture);
            Humanoid h = Last().Humanoid;
            h.SetHealth(h.GetMaxHealth() * f);
            VfhLog.I(LogCat.Test, "fixture.hireling_health", ("health", h.GetHealth()));
            yield return null;
        }

        private static IEnumerator Ghost(string[] args)
        {
            bool on = (args.ElementAtOrDefault(0) ?? "on") != "off";
            Player.m_localPlayer.SetGhostMode(on);
            VfhLog.I(LogCat.Test, "fixture.ghost", ("on", on));
            yield return null;
        }

        private static IEnumerator SetStance(string[] args)
        {
            if (!Enum.TryParse(args.ElementAtOrDefault(1) ?? "", true, out Stance stance))
                throw new ArgumentException("usage: stance <last|all> <Flee|Defend|Passive|Defensive|Aggressive>");
            var targets = args.ElementAtOrDefault(0) == "all"
                ? Hireling.Loaded.Where(h => h != null && Vector3.Distance(h.transform.position, Player.m_localPlayer.transform.position) < 50f).ToList()
                : new[] { Last() }.ToList();
            foreach (Hireling h in targets)
                MutationService.SubmitHireling(h.Hid, new HirelingOp { Stance = stance });
            VfhLog.I(LogCat.Test, "fixture.stance", ("stance", stance), ("hirelings", targets.Count));
            yield return null;
        }

        private static System.Collections.Generic.IEnumerable<Character> Hostiles(float radius)
        {
            Vector3 me = Player.m_localPlayer.transform.position;
            return Character.GetAllCharacters().Where(c => c != null && !c.IsDead() && !(c is Player) && !c.IsTamed() && Hireling.Of(c) == null &&
                                                           BaseAI.IsEnemy(Player.m_localPlayer, c) && !(c.GetBaseAI() is AnimalAI) &&
                                                           Vector3.Distance(c.transform.position, me) <= radius);
        }

        private static IEnumerator Wait(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }
    }
}

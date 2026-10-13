using System.Collections.Generic;
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using VikingsForHire.Board;
using VikingsForHire.Commands;
using VikingsForHire.Config;
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
            TestHarness.RegisterCheck("enemy_dist", "<last|posted|nearest> - metres from that hireling to the nearest living enemy (none without one)", args =>
            {
                Hireling h = FixturesHireling.Select(args.ElementAtOrDefault(0) ?? "last");
                Character? e = NearestEnemy(h.transform.position, 60f);
                return e == null ? "none" : Vector3.Distance(e.transform.position, h.transform.position).ToString("0.0", CultureInfo.InvariantCulture);
            });
            Fixtures.Add("order_attack_nearest", "- the Command Stone's attack order from you, on the enemy nearest your followers", _ =>
            {
                Player me = Player.m_localPlayer;
                var followers = Hireling.Loaded.Where(f => f != null && f.Mode == HirelingMode.Following && f.OwnerId == me.GetPlayerID()).ToList();
                Character enemy = NearestEnemy(followers.Count > 0 ? followers[0].transform.position : me.transform.position, 60f)
                                  ?? throw new InvalidOperationException("no enemy near your followers");
                foreach (Hireling f in followers.Where(f => f.Job.IsGuard()))
                    f.Ai.Order = new Followers.FieldOrder
                    {
                        Kind = Followers.FieldOrder.OrderKind.Attack, Enemy = enemy, Position = enemy.transform.position,
                        Until = Time.time + Followers.FieldOrder.Lifetime,
                    };
                VfhLog.I(LogCat.Test, "fixture.order_attack_nearest", ("enemy", enemy.m_name), ("followers", followers.Count));
                return Pause(0.2f);
            });
            TestHarness.RegisterCheck("enemy_health_pct", "- the nearest living enemy's health / max (none without one)", _ =>
                NearestEnemy(Player.m_localPlayer.transform.position, 60f) is Character e ? e.GetHealthPercentage().ToString("0.00", CultureInfo.InvariantCulture) : "none");
            Fixtures.Add("blast_by_wizard", "<radius> at <tag> - one hit (blunt 30, fire 20) from the last spawned wizard on every creature, piece and prop within the radius of a tagged object, as a spell's area does", BlastByWizard);
            Fixtures.Add("spell_props", "- log every destructible prop (no tool tier) a spell's blunt or fire can damage", _ =>
            {
                var names = new List<string>();
                foreach (GameObject go in ZNetScene.instance.m_prefabs)
                    if (go != null && go.GetComponent<Destructible>() is Destructible d && d.m_minToolTier <= 0 &&
                        (d.m_damages.m_blunt is HitData.DamageModifier.Normal or HitData.DamageModifier.Weak or HitData.DamageModifier.VeryWeak ||
                         d.m_damages.m_fire is HitData.DamageModifier.Normal or HitData.DamageModifier.Weak or HitData.DamageModifier.VeryWeak))
                        names.Add($"{go.name}:{d.m_health}:{d.m_destructibleType}:b={d.m_damages.m_blunt}:f={d.m_damages.m_fire}");
                VfhLog.I(LogCat.Test, "fixture.spell_props", ("n", names.Count), ("props", string.Join(" ", names.OrderBy(n => n))));
                return Pause(0.1f);
            });
            Fixtures.Add("staff_info", "<item> ... - log a weapon's main attack (type, animation, projectile speed, launch angle, gravity, accuracy, burst)", args =>
            {
                foreach (string name in args)
                {
                    ItemDrop.ItemData.SharedData? sh = ObjectDB.instance.GetItemPrefab(name)?.GetComponent<ItemDrop>()?.m_itemData.m_shared;
                    Attack? a = sh?.m_attack;
                    if (a == null)
                    {
                        VfhLog.I(LogCat.Test, "fixture.staff_info", ("item", name), ("found", false));
                        continue;
                    }
                    Projectile? p = a.m_attackProjectile != null ? a.m_attackProjectile.GetComponent<Projectile>() : null;
                    VfhLog.I(LogCat.Test, "fixture.staff_info", ("item", name), ("type", a.m_attackType), ("anim", a.m_attackAnimation),
                        ("chain", a.m_attackChainLevels), ("vel", a.m_projectileVel), ("velMin", a.m_projectileVelMin),
                        ("angle", a.m_launchAngle), ("accuracy", a.m_projectileAccuracy), ("projectiles", a.m_projectiles),
                        ("bursts", a.m_projectileBursts), ("eitr", a.m_attackEitr), ("health", a.m_attackHealth), ("draw", a.m_drawDurationMin),
                        ("reload", a.m_requiresReload), ("range", a.m_attackRange), ("height", a.m_attackHeight), ("dungeon", a.m_cantUseInDungeon),
                        ("proj", a.m_attackProjectile != null ? a.m_attackProjectile.name : "none"), ("gravity", p != null ? p.m_gravity : -1f),
                        ("ttl", p != null ? p.m_ttl : -1f), ("aoe", p != null ? p.m_aoe : -1f), ("projDmg", p != null ? p.m_damage.GetTotalDamage() : -1f),
                        ("itemDmg", sh!.m_damages.GetTotalDamage()), ("spawnOnHit", p != null && p.m_spawnOnHit != null ? p.m_spawnOnHit.name : "none"));
                }
                return Pause(0.1f);
            });
            Fixtures.Add("hit_tame_by_hireling", "<tag> <job> - the nearest hireling of that job hits a tagged tame once (slash 50), as its attack would", args =>
            {
                GameObject go = FixturesWork.FindTagged(args.ElementAtOrDefault(0) ?? "") ?? throw new InvalidOperationException("no such tag");
                Character tame = go.GetComponent<Character>() ?? throw new InvalidOperationException("not a creature");
                if (!Enum.TryParse(args.ElementAtOrDefault(1) ?? "", true, out JobType job))
                    throw new InvalidOperationException("usage: hit_tame_by_hireling <tag> <job>");
                Hireling h = Hireling.Loaded.Where(x => x != null && x.Job == job).OrderBy(x => Vector3.Distance(x.transform.position, tame.transform.position)).FirstOrDefault()
                             ?? throw new InvalidOperationException($"no {job} near");
                var hit = new HitData { m_point = tame.GetCenterPoint(), m_dir = Vector3.forward };
                hit.m_damage.m_slash = 50f;
                hit.SetAttacker(h.Humanoid);
                tame.Damage(hit);
                VfhLog.I(LogCat.Test, "fixture.hit_tame_by_hireling", ("tame", tame.m_name), ("job", job));
                return Pause(1f);
            });
            Fixtures.Add("hit_me_by_hireling", "<share=0.2> - as hit_me, but the hit comes from the last spawned hireling (hirelings never hurt players)", HitMeByHireling);
            Fixtures.Add("protect", "<on|off> - the tester's protection: off lets hits reach you for the rest of this test (the next test turns it on again)", args =>
            {
                bool on = (args.ElementAtOrDefault(0) ?? "on") != "off";
                TestProtection.Set(on);
                VfhLog.I(LogCat.Test, "fixture.protect", ("on", on));
                return Pause(0.1f);
            });
            Fixtures.Add("heal_me", "- back to full health", _ =>
            {
                Player.m_localPlayer.SetHealth(Player.m_localPlayer.GetMaxHealth());
                return Pause(0.1f);
            });
            Fixtures.Add("hit_me", "<share=0.5> - heal yourself to full, then take a blunt hit worth that share of your max health after armor (to check the tester's protection)", HitMe);
            TestHarness.RegisterCheck("player_health", "- your health as a share of your max", _ =>
                (Player.m_localPlayer.GetHealth() / Player.m_localPlayer.GetMaxHealth()).ToString("0.00", CultureInfo.InvariantCulture));
            TestHarness.RegisterCheck("player_ghost", "- whether you're in ghost mode", _ => Player.m_localPlayer.InGhostMode() ? "true" : "false");
            TestHarness.RegisterCheck("tester_hits_blocked", "- hits on you dropped by the test protection since login", _ => TestProtection.Blocked.ToString());
            Fixtures.Add("enemies_immune", "<radius=40> - hostile creatures near you take no damage from anything (a target a guard can't hurt)", args =>
            {
                float r = float.Parse(args.ElementAtOrDefault(0) ?? "40", CultureInfo.InvariantCulture);
                int n = 0;
                foreach (Character c in Character.GetAllCharacters().Where(c => c != null && !c.IsPlayer() && Hireling.Of(c) == null &&
                                                                                Vector3.Distance(c.transform.position, Player.m_localPlayer.transform.position) <= r))
                {
                    object mods = c.m_damageModifiers;
                    foreach (var f in typeof(HitData.DamageModifiers).GetFields().Where(f => f.FieldType == typeof(HitData.DamageModifier)))
                        f.SetValue(mods, HitData.DamageModifier.Immune);
                    c.m_damageModifiers = (HitData.DamageModifiers)mods;
                    n++;
                }
                VfhLog.I(LogCat.Test, "fixture.enemies_immune", ("n", n), ("radius", r));
                return Pause(0.2f);
            });
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

            TestHarness.RegisterCheck("damage_mult", "[last|posted|nearest] [target tag] - a hireling's damage multiplier (level table x job factor x the job's damage setting x enhancements, against the tagged target if given)", args =>
            {
                Hireling h = args.Length > 0 ? FixturesHireling.Select(args[0]) : Last();
                Character? target = args.Length > 1 ? FixturesWork.FindTagged(args[1])?.GetComponent<Character>() ?? throw new InvalidOperationException($"no tagged creature {args[1]}") : null;
                return Hirelings.DamagePatches.DamageMultiplier(h, target).ToString("0.###", CultureInfo.InvariantCulture);
            });
            Fixtures.Add("enh_events_reset", "- start counting enhancement techniques (enh_events) from zero", _ =>
            {
                EnhancementEvents.Reset();
                foreach (Hireling h in Hireling.Loaded)
                    if (h != null && h.Ai != null)
                        h.Ai.Archer.ShotsFired = 0;
                return Pause(0.1f);
            });
            TestHarness.RegisterCheck("enh_events", "<technique> - times an enhancement technique fired since enh_events_reset (ShieldBash, Volley…)", args =>
                EnhancementEvents.Count(args.ElementAtOrDefault(0) ?? "").ToString());
            TestHarness.RegisterCheck("enemy_status", "<tag|nearest> <status> - seconds left of a status effect (Burning, Frost…) on a creature, 0 when none", args =>
            {
                Character c = Creature(args.ElementAtOrDefault(0) ?? "nearest");
                StatusEffect? se = c.GetSEMan().GetStatusEffect((args.ElementAtOrDefault(1) ?? "").GetStableHashCode());
                return (se != null ? Mathf.Max(0f, se.GetRemaningTime()) : 0f).ToString("0.#", CultureInfo.InvariantCulture);
            });
            TestHarness.RegisterCheck("enemy_staggered", "<tag|nearest> - whether a creature is staggering", args =>
                Creature(args.ElementAtOrDefault(0) ?? "nearest").IsStaggering() ? "true" : "false");
            TestHarness.RegisterCheck("enemy_target", "<tag|nearest> - what a creature is after: a hireling's job (GuardMelee…), player, a creature's name, or none", args =>
            {
                Character c = Creature(args.ElementAtOrDefault(0) ?? "nearest");
                Character? t = c.GetBaseAI() is MonsterAI m ? m.GetTargetCreature() : null;
                return t == null ? "none" : Hireling.Of(t) is Hireling th ? th.Job.ToString() : t is Player ? "player" : t.m_name;
            });
            TestHarness.RegisterCheck("enemies_damaged", "<radius=10> - enemies within the radius of you below full health", args =>
            {
                float r = args.Length > 0 ? float.Parse(args[0], CultureInfo.InvariantCulture) : 10f;
                return Hostiles(r).Count(c => c.GetHealth() < c.GetMaxHealth() - 0.01f).ToString();
            });
            Fixtures.Add("strike_hireling", "<damage> [last|posted|nearest] - a hit from the nearest enemy (or none) on a hireling, through its armour, bypassing test protection", args =>
            {
                float dmg = float.Parse(args.ElementAtOrDefault(0) ?? "50", CultureInfo.InvariantCulture);
                Hireling h = FixturesHireling.Select(args.ElementAtOrDefault(1) ?? "posted");
                Character? from = Hostiles(30f).OrderBy(c => Vector3.Distance(c.transform.position, h.transform.position)).FirstOrDefault();
                var hit = new HitData { m_point = h.transform.position + Vector3.up };
                hit.m_damage.m_blunt = dmg;
                if (from != null)
                    hit.SetAttacker(from);
                bool was = TestProtection.On;
                TestProtection.Set(false);
                h.Humanoid.Damage(hit);
                TestProtection.Set(was);
                VfhLog.I(LogCat.Test, "fixture.strike_hireling", ("hid", h.Hid), ("damage", dmg), ("from", from != null ? from.m_name : "none"));
                return Pause(0.3f);
            });
            TestHarness.RegisterCheck("shots_fired", "[last|posted|nearest] - arrows a hireling launched since enh_events_reset (volley and rain arrows count)", args =>
                FixturesHireling.Select(args.ElementAtOrDefault(0) ?? "posted").Ai.Archer.ShotsFired.ToString());
            Fixtures.Add("enemies_tough", "<mult> [radius=60] - hostile creatures near you get that many times their max health, healed to full (a still creature takes sneak-attack hits)", args =>
            {
                float mult = float.Parse(args.ElementAtOrDefault(0) ?? "10", CultureInfo.InvariantCulture);
                float r = args.Length > 1 ? float.Parse(args[1], CultureInfo.InvariantCulture) : 60f;
                int n = 0;
                foreach (Character c in Hostiles(r).ToList())
                {
                    c.SetMaxHealth(c.GetMaxHealth() * mult);
                    c.SetHealth(c.GetMaxHealth());
                    n++;
                }
                VfhLog.I(LogCat.Test, "fixture.enemies_tough", ("n", n), ("mult", mult));
                return Pause(0.2f);
            });
            TestHarness.RegisterCheck("summons_near", "<radius=20> - living summoned allies (Raise dead's skeletons) within the radius of you", args =>
                Hirelings.Combat.SummonedAllies.Near(Player.m_localPlayer.transform.position, args.Length > 0 ? float.Parse(args[0], CultureInfo.InvariantCulture) : 20f).ToString());
            TestHarness.RegisterCheck("player_status", "<status> - seconds left of a status effect on you (Staff_shield…), 0 when none, 999 when it has no time limit", args =>
            {
                StatusEffect? se = Player.m_localPlayer.GetSEMan().GetStatusEffect((args.ElementAtOrDefault(0) ?? "").GetStableHashCode());
                // An effect with no time limit (the Staff of Protection's bubble lasts until it breaks) reads 999.
                return (se == null ? 0f : se.m_ttl <= 0f ? 999f : Mathf.Max(0f, se.GetRemaningTime())).ToString("0.#", CultureInfo.InvariantCulture);
            });
            Fixtures.Add("player_status_clear", "<status> - remove a status effect from you", args =>
            {
                Player.m_localPlayer.GetSEMan().RemoveStatusEffect((args.ElementAtOrDefault(0) ?? "").GetStableHashCode(), true);
                return Pause(0.1f);
            });
            TestHarness.RegisterCheck("prefabs", "<text> - names of prefabs, items and status effects containing the text (up to 20)", args =>
            {
                string q = args.ElementAtOrDefault(0) ?? "";
                var names = ZNetScene.instance.m_prefabs.Select(p => p.name)
                    .Concat(ObjectDB.instance.m_StatusEffects.Select(s => "se:" + s.name))
                    .Where(n => n.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0).Distinct().Take(20);
                return string.Join(",", names);
            });
            Fixtures.Add("data_set", "<job> <Name.number> <value> - set one enhancement number in the loaded data (this game only, until data_reset)", args =>
            {
                if (!Enum.TryParse(args.ElementAtOrDefault(0) ?? "", true, out JobType job))
                    throw new ArgumentException("usage: data_set <job> <Name.number> <value>");
                DataStore.Current.Enhancements[job][args[1]] = float.Parse(args[2], CultureInfo.InvariantCulture);
                VfhLog.I(LogCat.Test, "fixture.data_set", ("job", job), ("key", args[1]), ("value", args[2]));
                return Pause(0.1f);
            });
            Fixtures.Add("data_reset", "- reload the data file (undoes data_set)", _ =>
            {
                DataStore.LoadLocal("test data_reset");
                return Pause(0.2f);
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

        private static IEnumerator Pause(float s)
        {
            yield return new WaitForSeconds(s);
        }

        private static IEnumerator Enemies(string[] args)
        {
            // "at TAG": right beside a tagged object (within 0.5 m), standing still.
            Vector3? beside = FixturesWork.AtTag(ref args, 0.5f);
            string prefab = args.ElementAtOrDefault(0) ?? "Greyling";
            int n = int.Parse(args.ElementAtOrDefault(1) ?? "1", CultureInfo.InvariantCulture);
            float dist = beside != null ? 0f : float.Parse(args.ElementAtOrDefault(2) ?? "10", CultureInfo.InvariantCulture);
            // still: its AI off for as long as it lives (it stands there and doesn't fight back).
            bool still = args.Contains("still") || beside != null;
            // ahead: in front of the hireling (in its view), not at a random angle round it.
            bool ahead = args.Contains("ahead");
            // An optional tag (after the distance) for every creature spawned, for checks that name one.
            string tag = args.Skip(3).FirstOrDefault(a => a != "still" && a != "ahead") ?? "";
            GameObject go = ZNetScene.instance.GetPrefab(prefab) ?? throw new ArgumentException($"no prefab {prefab}");
            Vector3 center = beside ?? Last().transform.position;
            float start = ahead ? Quaternion.LookRotation(Vector3.ProjectOnPlane(Last().transform.forward, Vector3.up)).eulerAngles.y
                : UnityEngine.Random.Range(0f, 360f);
            for (int i = 0; i < n; i++)
            {
                Vector3 p = center + Quaternion.Euler(0f, start + i * 25f, 0f) * Vector3.forward * dist;
                p.y = ZoneSystem.instance.GetGroundHeight(p) + 0.5f;
                GameObject e = Object.Instantiate(go, p, (center - p).sqrMagnitude > 0.01f ? Quaternion.LookRotation(center - p) : Quaternion.identity);
                e.GetComponent<ZNetView>()?.GetZDO()?.Set(BoardZdo.Fixture, true);
                if (tag.Length > 0)
                    e.GetComponent<ZNetView>()?.GetZDO()?.Set(FixturesWork.TagKey, tag);
                if (still && e.GetComponent<BaseAI>() is BaseAI brain)
                    brain.enabled = false;
            }
            VfhLog.I(LogCat.Test, "fixture.enemies", ("prefab", prefab), ("n", n), ("dist", dist), ("at", beside?.ToString() ?? Last().Hid), ("still", still));
            yield return new WaitForSeconds(0.5f);
        }

        // A tagged creature, or the nearest hostile one to you.
        private static Character Creature(string sel) => sel == "nearest"
            ? Hostiles(60f).OrderBy(c => Vector3.Distance(c.transform.position, Player.m_localPlayer.transform.position)).FirstOrDefault()
              ?? throw new InvalidOperationException("no enemy near")
            : FixturesWork.FindTagged(sel)?.GetComponent<Character>() ?? throw new InvalidOperationException($"no tagged creature {sel}");

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
            yield return new WaitForSeconds(0.5f);
            // Anything still standing (made immune by enemies_immune) is removed outright, so it can't wander into the next test.
            int removed = 0;
            foreach (Character c in Hostiles(r).Where(c => c != null && !c.IsDead()).ToList())
            {
                ZNetScene.instance.Destroy(c.gameObject);
                removed++;
            }
            VfhLog.I(LogCat.Test, "fixture.kill_enemies", ("killed", n), ("removed", removed));
            yield return new WaitForSeconds(0.5f);
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

        private static IEnumerator HitMe(string[] args)
        {
            Player me = Player.m_localPlayer;
            me.SetHealth(me.GetMaxHealth());
            // Armor takes its full value off a hit at least twice its size: add it on top so the hit really costs that much.
            float share = float.Parse(args.ElementAtOrDefault(0) ?? "0.5", CultureInfo.InvariantCulture);
            float damage = share * me.GetMaxHealth() + me.GetBodyArmor();
            var hit = new HitData { m_point = me.transform.position, m_dir = Vector3.forward };
            hit.m_damage.m_blunt = damage;
            me.Damage(hit);
            VfhLog.I(LogCat.Test, "fixture.hit_me", ("damage", damage), ("armor", me.GetBodyArmor()), ("protected", TestProtection.On),
                ("healthNow", me.GetHealth()), ("god", me.InGodMode()), ("ghost", me.InGhostMode()), ("flying", me.IsDebugFlying()), ("teleporting", me.IsTeleporting()));
            yield return new WaitForSeconds(0.5f);
            VfhLog.I(LogCat.Test, "fixture.hit_me_after", ("health", me.GetHealth()), ("max", me.GetMaxHealth()));
        }

        private static IEnumerator BlastByWizard(string[] args)
        {
            int at = Array.IndexOf(args, "at");
            GameObject centre = FixturesWork.FindTagged(at >= 0 ? args.ElementAtOrDefault(at + 1) ?? "" : "") ?? throw new InvalidOperationException("usage: blast_by_wizard <radius> at <tag>");
            float radius = float.Parse(args.ElementAtOrDefault(0) ?? "3", CultureInfo.InvariantCulture);
            Hireling wizard = Hireling.Loaded.Where(h => h != null && h.Job == JobType.Wizard)
                .OrderBy(h => Vector3.Distance(h.transform.position, centre.transform.position)).FirstOrDefault()
                ?? throw new InvalidOperationException("no wizard near");
            var hit = new HashSet<IDestructible>();
            foreach (Collider c in Physics.OverlapSphere(centre.transform.position, radius))
                if (c.GetComponentInParent<IDestructible>() is IDestructible d && !ReferenceEquals(d, wizard.Humanoid))
                    hit.Add(d);
            foreach (IDestructible d in hit)
            {
                var h = new HitData { m_point = centre.transform.position, m_dir = Vector3.down, m_toolTier = 10 };
                h.m_damage.m_blunt = 30f;
                h.m_damage.m_fire = 20f;
                h.SetAttacker(wizard.Humanoid);
                d.Damage(h);
            }
            VfhLog.I(LogCat.Test, "fixture.blast_by_wizard", ("hid", wizard.Hid), ("radius", radius), ("hit", string.Join(",", hit.Select(d => ((Component)d).name))));
            yield return new WaitForSeconds(1f);
        }

        private static Character? NearestEnemy(Vector3 at, float within) =>
            Character.GetAllCharacters().Where(c => c != null && !c.IsDead() && !c.IsPlayer() && !c.IsTamed() && Hireling.Of(c) == null &&
                                                    Vector3.Distance(c.transform.position, at) <= within)
                .OrderBy(c => Vector3.Distance(c.transform.position, at)).FirstOrDefault();

        private static IEnumerator HitMeByHireling(string[] args)
        {
            Player me = Player.m_localPlayer;
            Hireling from = Last();
            me.SetHealth(me.GetMaxHealth());
            float share = float.Parse(args.ElementAtOrDefault(0) ?? "0.2", CultureInfo.InvariantCulture);
            var hit = new HitData { m_point = me.transform.position, m_dir = Vector3.forward };
            hit.m_damage.m_blunt = share * me.GetMaxHealth() + me.GetBodyArmor();
            hit.SetAttacker(from.Humanoid);
            me.Damage(hit);
            VfhLog.I(LogCat.Test, "fixture.hit_me_by_hireling", ("hid", from.Hid), ("damage", hit.m_damage.m_blunt), ("protected", TestProtection.On));
            yield return new WaitForSeconds(0.5f);
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

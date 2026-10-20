using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using VikingsForHire.Board;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Testing
{
    /// <summary>Phase 09 (miner) fixtures and checks.</summary>
    internal static class FixturesMining
    {
        private const float TerrainRadius = 80f;
        private static int? _terrainBaseline;
        // Where the last buried deposit went (dug_depth still reads there once it's mined away).
        private static Vector3? _lastBuried;

        public static void Register()
        {
            Fixtures.Add("deposits", "<prefab> <n> <distance> [tag] - place rocks/deposits in a ring that far behind the nearest board", Deposits);
            Fixtures.Add("crack", "<tag> - break a tagged deposit open and chip its highest chunk, so it counts as found (miners leave buried deposits nobody has broken into alone)", Crack);
            Fixtures.Add("dig_pit", "<depth> [radius=2.5] - dig the ground where you stand down that many metres, with the pickaxe's own dig (you end up in the hole)", DigPit);
            Fixtures.Add("bury", "<tag> <depth> - move a tagged rock/deposit down so its top is that many metres under the ground", Bury);
            Fixtures.Add("terrain_baseline", "- remember how many terrain edits there are around the nearest board", TerrainBaseline);

            TestHarness.RegisterCheck("terrain_unchanged", "- no terrain edits around the nearest board since terrain_baseline", _ =>
            {
                if (_terrainBaseline is not int before)
                    return "error: run vfh_fixture terrain_baseline first";
                int now = TerrainOps();
                return now == before ? "true" : $"false ({now - before} new terrain edits)";
            });
            TestHarness.RegisterCheck("dug_depth", "<tag> [radius=4] - the deepest the ground has been dug below the world's own surface around a tagged deposit (metres)", args =>
            {
                GameObject go = FixturesWork.FindTagged(args.ElementAtOrDefault(0) ?? "");
                Vector3 c = go != null ? go.transform.position : (_lastBuried ?? throw new InvalidOperationException("no such tagged deposit"));
                float r = args.Length > 1 ? float.Parse(args[1], CultureInfo.InvariantCulture) : 4f;
                float deepest = 0f;
                for (float x = -r; x <= r; x += 0.5f)
                    for (float z = -r; z <= r; z += 0.5f)
                        deepest = Mathf.Max(deepest, Hirelings.Work.MinerProfile.DugDepth(c + new Vector3(x, 0f, z)));
                return deepest.ToString("0.00", CultureInfo.InvariantCulture);
            });
            TestHarness.RegisterCheck("deposit_intact", "<tag> - a tagged rock/deposit hasn't been damaged at all", args =>
            {
                GameObject go = FixturesWork.FindTagged(args.ElementAtOrDefault(0) ?? "") ?? throw new InvalidOperationException("no such tagged deposit (destroyed?)");
                return Intact(go) ? "true" : "false";
            });
            TestHarness.RegisterCheck("deposits_left", "<prefab> - how many of these (unbroken or chunked) are within 60 m of the nearest board", args =>
            {
                string prefab = args.ElementAtOrDefault(0) ?? "";
                Vector3 at = FixturesWork.Board().transform.position;
                return ZNetScene.instance.m_instances.Values.Count(v => v != null && v.GetZDO() != null &&
                    Utils.GetPrefabName(v.gameObject).StartsWith(prefab, StringComparison.OrdinalIgnoreCase) &&
                    Vector3.Distance(v.transform.position, at) < 60f).ToString();
            });
        }

        private static IEnumerator Deposits(string[] args)
        {
            string prefab = args.ElementAtOrDefault(0) ?? "rock4_copper";
            int n = int.Parse(args.ElementAtOrDefault(1) ?? "1", CultureInfo.InvariantCulture);
            float dist = float.Parse(args.ElementAtOrDefault(2) ?? "30", CultureInfo.InvariantCulture);
            string tag = args.ElementAtOrDefault(3) ?? "";
            HiringBoard board = FixturesWork.Board();
            Vector3 away = -board.transform.forward;
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, (i - (n - 1) / 2f) * 20f, 0f) * away;
                GameObject go = FixturesWork.Spawn(prefab, board.transform.position + dir * dist, tag);
                VfhLog.I(LogCat.Test, "fixture.deposit", ("i", i), ("prefab", prefab), ("pos", go.transform.position), ("tag", tag));
            }
            yield return new WaitForSeconds(0.5f);
        }

        private static IEnumerator DigPit(string[] args)
        {
            float depth = float.Parse(args.ElementAtOrDefault(0) ?? "3", CultureInfo.InvariantCulture);
            float radius = float.Parse(args.ElementAtOrDefault(1) ?? "2.5", CultureInfo.InvariantCulture);
            GameObject op = new[] { "digg_v3", "digg_v2", "digg" }.Select(n => ZNetScene.instance.GetPrefab(n)).FirstOrDefault(g => g != null)
                            ?? throw new InvalidOperationException("no dig terrain op prefab");
            Vector3 c = Player.m_localPlayer.transform.position;
            float target = ZoneSystem.instance.GetGroundHeight(c) - depth;
            for (int pass = 0; pass < 40; pass++)
            {
                bool deeper = false;
                for (float x = -radius; x <= radius; x += 1f)
                    for (float z = -radius; z <= radius; z += 1f)
                    {
                        Vector3 p = c + new Vector3(x, 0f, z);
                        float g = ZoneSystem.instance.GetGroundHeight(p);
                        if (g <= target + 0.1f)
                            continue;
                        UnityEngine.Object.Instantiate(op, new Vector3(p.x, g, p.z), Quaternion.identity);
                        deeper = true;
                    }
                yield return new WaitForSeconds(0.1f);
                if (!deeper)
                    break;
            }
            VfhLog.I(LogCat.Test, "fixture.dig_pit", ("at", c), ("depth", depth), ("floor", ZoneSystem.instance.GetGroundHeight(c)));
        }

        private static IEnumerator Bury(string[] args)
        {
            GameObject go = FixturesWork.FindTagged(args.ElementAtOrDefault(0) ?? "") ?? throw new InvalidOperationException("no such tagged deposit");
            float depth = float.Parse(args.ElementAtOrDefault(1) ?? "2", CultureInfo.InvariantCulture);
            Collider[] cols = go.GetComponentsInChildren<Collider>().Where(c => c.enabled).ToArray();
            if (cols.Length == 0)
                throw new InvalidOperationException("the deposit has no colliders");
            float top = cols.Max(c => c.bounds.max.y);
            float ground = ZoneSystem.instance.GetGroundHeight(go.transform.position);
            Vector3 p = go.transform.position + Vector3.down * (top - (ground - depth));
            go.transform.position = p;
            go.GetComponent<ZNetView>()?.GetZDO()?.SetPosition(p);
            _lastBuried = p;
            Physics.SyncTransforms();
            VfhLog.I(LogCat.Test, "fixture.bury", ("tag", args[0]), ("depth", depth), ("top", top), ("ground", ground), ("pos", p));
            yield return new WaitForSeconds(0.5f);
        }

        private static IEnumerator Crack(string[] args)
        {
            GameObject go = FixturesWork.FindTagged(args.ElementAtOrDefault(0) ?? "") ?? throw new InvalidOperationException("no such tagged deposit");
            Vector3 at = go.transform.position;
            MineRock5? r5 = go.GetComponent<MineRock5>();
            if (r5 == null && go.GetComponent<Destructible>() is Destructible whole)
            {
                // A whole deposit: break it, and it's replaced by its chunked version.
                var smash = new HitData { m_toolTier = 100, m_point = whole.transform.position + Vector3.up, m_dir = Vector3.down };
                smash.m_damage.m_pickaxe = 10000f;
                smash.SetAttacker(Player.m_localPlayer);
                whole.Damage(smash);
                yield return new WaitForSeconds(1f);
                r5 = UnityEngine.Object.FindObjectsByType<MineRock5>(FindObjectsSortMode.None)
                    .OrderBy(r => Vector3.Distance(r.transform.position, at)).FirstOrDefault(r => Vector3.Distance(r.transform.position, at) < 4f);
            }
            if (r5 == null)
                throw new InvalidOperationException("no chunked deposit there");
            Collider top = r5.m_hitAreas.Where(a => a.m_collider != null).OrderByDescending(a => a.m_collider.bounds.max.y).First().m_collider;
            var hit = new HitData { m_toolTier = 100, m_point = top.bounds.center, m_hitCollider = top, m_dir = Vector3.down };
            hit.m_damage.m_pickaxe = 1f;
            hit.SetAttacker(Player.m_localPlayer);
            r5.Damage(hit);
            VfhLog.I(LogCat.Test, "fixture.crack", ("tag", args[0]), ("part", top.name), ("rock", r5.name));
            yield return new WaitForSeconds(0.5f);
        }

        private static IEnumerator Wait(float seconds)
        {
            yield return new WaitForSeconds(seconds);
        }

        private static IEnumerator TerrainBaseline(string[] args)
        {
            _terrainBaseline = TerrainOps();
            VfhLog.I(LogCat.Test, "fixture.terrain_baseline", ("ops", _terrainBaseline));
            yield break;
        }

        // Every terrain edit (dig, raise, level, paint) bumps its area's TerrainComp operation count.
        private static int TerrainOps()
        {
            Vector3 at = FixturesWork.Board().transform.position;
            return TerrainComp.s_instances.Where(t => t != null && Vector3.Distance(t.transform.position, at) < TerrainRadius + 64f).Sum(t => t.m_operations);
        }

        private static bool Intact(GameObject go)
        {
            if (go.GetComponent<MineRock5>() is MineRock5 r5)
                return r5.m_hitAreas == null || r5.m_hitAreas.All(a => a.m_health >= r5.m_health);
            if (go.GetComponent<MineRock>() is MineRock r)
            {
                ZDO zdo = r.m_nview.GetZDO();
                return Enumerable.Range(0, r.m_hitAreas.Length).All(i => zdo.GetFloat("Health" + i, r.GetHealth()) >= r.GetHealth());
            }
            if (go.GetComponent<Destructible>() is Destructible d)
                return d.m_nview.GetZDO().GetFloat(ZDOVars.s_health, d.m_health) >= d.m_health;
            return true;
        }
    }
}

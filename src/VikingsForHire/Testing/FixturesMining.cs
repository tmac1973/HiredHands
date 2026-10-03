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

        public static void Register()
        {
            Fixtures.Add("deposits", "<prefab> <n> <distance> [tag] - place rocks/deposits in a ring that far behind the nearest board", Deposits);
            Fixtures.Add("terrain_baseline", "- remember how many terrain edits there are around the nearest board", TerrainBaseline);

            TestHarness.RegisterCheck("terrain_unchanged", "- no terrain edits around the nearest board since terrain_baseline", _ =>
            {
                if (_terrainBaseline is not int before)
                    return "error: run vfh_fixture terrain_baseline first";
                int now = TerrainOps();
                return now == before ? "true" : $"false ({now - before} new terrain edits)";
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

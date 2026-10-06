using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using VikingsForHire.Board;
using VikingsForHire.Compat;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings.Work.Farm;
using Object = UnityEngine.Object;

namespace VikingsForHire.Testing
{
    /// <summary>Fields, crops and bushes for the Farmer's tests, and checks on them.</summary>
    internal static class FixturesFarm
    {
        private static Vector3 _fieldCenter;
        private static Vector3 _fieldRow;   // along the rows
        private static Vector3 _fieldAcross;
        private static float _fieldW, _fieldH;
        private static int _cropsPlaced;

        public static void Register()
        {
            Fixtures.Add("field", "<w> <h> [tag] - cultivate a w x h m patch 8 m in front of the nearest board (the cultivator's paint)", Field);
            Fixtures.Add("crop", "<sapling> <n> [ripe] [tag] - plant n of a crop in a row on the field (ripe: the grown crop instead); the first gets the tag", Crop);
            Fixtures.Add("bush", "<pickable> [tag] - a ripe regrowing plant (RaspberryBush…) at the field's edge", Bush);
            Fixtures.Add("grow_all", "- every growing crop within 40 m of the board ripens now (as if its grow time had passed)", GrowAll);

            TestHarness.RegisterCheck("ripe_crops", "[radius=40] - ripe planted crops within that distance of the nearest board", args =>
                Count(args, (plants, picks) => picks.Count(p => CropCatalog.ByGrown(Utils.GetPrefabName(p.gameObject)) is Crop c && !c.Info.Regrowing && PickableYield.Ripe(p))));
            TestHarness.RegisterCheck("plants", "<sapling> [radius=40] - growing plants of that sapling within that distance of the nearest board", args =>
            {
                string sapling = args.ElementAtOrDefault(0) ?? "";
                return Count(args.Skip(1).ToArray(), (plants, picks) => plants.Count(p => Utils.GetPrefabName(p.gameObject) == sapling));
            });
            TestHarness.RegisterCheck("picked", "<tag> - true when a tagged pickable has been picked", args =>
                ((FixturesWork.FindTagged(args.ElementAtOrDefault(0) ?? "")?.GetComponent<Pickable>() ?? throw new InvalidOperationException("no such tagged pickable"))
                    .m_picked).ToString().ToLowerInvariant());
            TestHarness.RegisterCheck("harvested", "<item> - how many of an item Farmers have harvested since login", args =>
                (HarvestChore.Harvested.TryGetValue(args.ElementAtOrDefault(0) ?? "", out int n) ? n : 0).ToString(CultureInfo.InvariantCulture));
        }

        private static string Count(string[] args, Func<List<Plant>, List<Pickable>, int> count)
        {
            float radius = float.TryParse(args.ElementAtOrDefault(0), NumberStyles.Float, CultureInfo.InvariantCulture, out float r) ? r : 40f;
            var plants = new List<Plant>();
            var picks = new List<Pickable>();
            FarmScan.Nearby(FixturesWork.Board().transform.position, radius, plants, picks);
            return count(plants, picks).ToString(CultureInfo.InvariantCulture);
        }

        private static IEnumerator Field(string[] args)
        {
            float w = float.Parse(args.ElementAtOrDefault(0) ?? "8", CultureInfo.InvariantCulture);
            float h = float.Parse(args.ElementAtOrDefault(1) ?? "8", CultureInfo.InvariantCulture);
            HiringBoard board = FixturesWork.Board();
            _fieldRow = board.transform.right;
            _fieldAcross = board.transform.forward;
            _fieldCenter = board.transform.position + board.transform.forward * (8f + h / 2f);
            _fieldCenter.y = ZoneSystem.instance.GetGroundHeight(_fieldCenter);
            _fieldW = w;
            _fieldH = h;
            _cropsPlaced = 0;
            // Paint it cultivated the way the cultivator does, one terrain patch at a time.
            var settings = new TerrainOp.Settings
            {
                m_level = false, m_raise = false, m_smooth = false, m_square = true,
                m_paintCleared = true, m_paintType = TerrainModifier.PaintType.Cultivate, m_paintRadius = Mathf.Max(w, h) / 2f + 0.5f,
            };
            var maps = new List<Heightmap>();
            Heightmap.FindHeightmap(_fieldCenter, settings.m_paintRadius + 2f, maps);
            System.Reflection.MethodInfo doOp = HarmonyLib.AccessTools.Method(typeof(TerrainComp), "DoOperation",
                new[] { typeof(Vector3), typeof(Vector3), typeof(TerrainOp.Settings) });
            foreach (Heightmap hm in maps)
            {
                TerrainComp comp = hm.GetAndCreateTerrainCompiler();
                if (comp == null || comp.m_nview == null || !comp.m_nview.IsValid())
                    continue;
                if (!comp.m_nview.IsOwner())
                    comp.m_nview.ClaimOwnership();
                doOp.Invoke(comp, new object[] { _fieldCenter, Vector3.zero, settings });
            }
            VfhLog.I(LogCat.Test, "fixture.field", ("center", _fieldCenter), ("w", w), ("h", h), ("cultivated", Heightmap.FindHeightmap(_fieldCenter)?.IsCultivated(_fieldCenter) ?? false));
            yield return new WaitForSeconds(1f);
        }

        private static IEnumerator Crop(string[] args)
        {
            string sapling = args.ElementAtOrDefault(0) ?? "sapling_carrot";
            int n = int.Parse(args.ElementAtOrDefault(1) ?? "1", CultureInfo.InvariantCulture);
            bool ripe = args.Skip(2).Contains("ripe");
            string tag = args.Skip(2).FirstOrDefault(a => a != "ripe") ?? "";
            Crop crop = CropCatalog.BySapling(sapling) ?? throw new InvalidOperationException($"{sapling} isn't a known crop");
            float spacing = PlantMods.CropSpacing(crop.GrowRadius);
            string prefab = ripe ? crop.GrownPrefab : sapling;
            for (int i = 0; i < n; i++)
            {
                // Rows along the field's width, from its near edge.
                int perRow = Mathf.Max(1, Mathf.FloorToInt(_fieldW / spacing));
                int k = _cropsPlaced++;
                Vector3 p = _fieldCenter - _fieldRow * (_fieldW / 2f - spacing / 2f) - _fieldAcross * (_fieldH / 2f - spacing / 2f)
                            + _fieldRow * ((k % perRow) * spacing) + _fieldAcross * ((k / perRow) * spacing);
                GameObject go = FixturesWork.Spawn(prefab, p, i == 0 ? tag : "");
                if (!ripe)
                    FixturesWork.OwnBuilt(go);
            }
            VfhLog.I(LogCat.Test, "fixture.crop", ("sapling", sapling), ("n", n), ("ripe", ripe), ("spacing", spacing));
            yield return null;
        }

        private static IEnumerator Bush(string[] args)
        {
            string prefab = args.ElementAtOrDefault(0) ?? "RaspberryBush";
            string tag = args.ElementAtOrDefault(1) ?? "";
            Vector3 p = _fieldCenter + _fieldRow * (_fieldW / 2f + 2f);
            GameObject go = FixturesWork.Spawn(prefab, p, tag);
            if (go.GetComponent<Pickable>() is Pickable pick)
                pick.m_nview.InvokeRPC(ZNetView.Everybody, "RPC_SetPicked", false);
            VfhLog.I(LogCat.Test, "fixture.bush", ("prefab", prefab), ("tag", tag));
            yield return null;
        }

        // Ripens every growing crop now: each is replaced by its grown pickable, as the plant itself does when it's done.
        private static IEnumerator GrowAll(string[] args)
        {
            var plants = new List<Plant>();
            var picks = new List<Pickable>();
            FarmScan.Nearby(FixturesWork.Board().transform.position, 40f, plants, picks);
            int grown = 0;
            foreach (Plant p in plants)
            {
                if (CropCatalog.BySapling(Utils.GetPrefabName(p.gameObject)) is not Crop crop)
                    continue;
                Vector3 at = p.transform.position;
                if (!p.m_nview.IsOwner())
                    p.m_nview.ClaimOwnership();
                ZNetScene.instance.Destroy(p.gameObject);
                FixturesWork.Spawn(crop.GrownPrefab, at, "");
                grown++;
            }
            VfhLog.I(LogCat.Test, "fixture.grow_all", ("grown", grown));
            yield return null;
        }
    }
}

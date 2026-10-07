using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using UnityEngine;
using VikingsForHire.Board;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Core.Nav;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings.Nav;
using Object = UnityEngine.Object;

namespace VikingsForHire.Testing
{
    /// <summary>Buildings with doors and stairs for the base nav links tests, and checks on what the scan found.</summary>
    internal static class FixturesNav
    {
        public static void Register()
        {
            Fixtures.Add("house2", "<tag> [item count]… - a 6x6 m house 12 m to the board's left: door facing the board (<tag>_door), a wood stair (<tag>_stair) up to an upper floor at the back (middle piece <tag>_up), chest upstairs (<tag>)",
                args => House(args, "wood_stair"));
            Fixtures.Add("stepladder", "<tag> [item count]… - the same house with a stepladder (<tag>_stair) instead of the stair",
                args => House(args, "wood_stepladder"));
            Fixtures.Add("remove_piece", "<tag> - deconstruct a tagged piece (as a player with the hammer)", RemovePiece);
            Fixtures.Add("flatten", "<radius=22> - level the ground around you to the height under your feet and remove rocks, trees, bushes and stumps there (test worlds only)", Flatten);
            Fixtures.Add("roof_over", "<prefab> [walls] - a thatch roof (3x3 wood_roof pieces), and with walls three sides of walls stacked up to the roof (the east open), over the nearest such station, e.g. a spinning wheel, which only works under a roof", RoofOver);
            Fixtures.Add("nav_links", "<on|off> - turn BaseNavLinks on or off here (single player only; end the macro with nav_links on)", NavLinks);
            Fixtures.Add("pass_test", "<length=12> <width=1.4> - a walled corridor beside the nearest board with a woodcutter at each end, each sent to the other end", PassTest);
            Fixtures.Add("pass_clear", "- stop the pass_test walks", _ =>
            {
                Hirelings.TestWalkBehaviour.Targets.Clear();
                return ResetStats();
            });
            TestHarness.RegisterCheck("pass_done", "- true when both pass_test hirelings reached the other end", _ =>
                (Hirelings.TestWalkBehaviour.Targets.Count == 2 && Hirelings.TestWalkBehaviour.Targets.All(kv =>
                    Hirelings.Hireling.Loaded.Find(h => h != null && h.Hid == kv.Key) is Hirelings.Hireling h && Utils.DistanceXZ(h.transform.position, kv.Value) <= 1.2f))
                .ToString().ToLowerInvariant());
            Fixtures.Add("nav_stats_reset", "- start counting stair hops from zero", _ => ResetStats());

            TestHarness.RegisterCheck("navlinks_hops", "- hops to a stair's far end since nav_stats_reset", _ => (LinkNavigator.Hops - _hopsAtReset).ToString(CultureInfo.InvariantCulture));

            TestHarness.RegisterCheck("navlinks", "<doors|stairs|ladders|floorlinks|rejected|version> [tag] - links the nearest board's scan found (with a tag: only those within 8 m of it)", args =>
            {
                string kind = args.ElementAtOrDefault(0) ?? "doors";
                GameObject? at = args.Length > 1 ? FixturesWork.FindTagged(args[1]) ?? throw new InvalidOperationException($"no tagged {args[1]}") : null;
                Vector3 pos = at != null ? at.transform.position : Player.m_localPlayer.transform.position;
                BoardNav nav = NavLinkRegistry.AreaAt(pos) ?? NavLinkRegistry.Nearest(pos) ?? throw new InvalidOperationException("no board scanned");
                var links = nav.Graph.Links.Where(l => at == null || Mathf.Min(Vector3.Distance(l.A.ToUnity(), pos), Vector3.Distance(l.B.ToUnity(), pos)) <= 8f).ToList();
                return (kind switch
                {
                    "doors" => links.Count(l => l.Kind == NavLinkKind.Door),
                    "stairs" => links.Count(l => l.Kind == NavLinkKind.Stair && !l.IsLadder),
                    "ladders" => links.Count(l => l.IsLadder),
                    "floorlinks" => links.Count(l => l.Kind == NavLinkKind.Stair),
                    "rejected" => nav.Rejected,
                    "version" => nav.Graph.Version,
                    _ => throw new InvalidOperationException($"unknown kind {kind}"),
                }).ToString(CultureInfo.InvariantCulture);
            });
        }

        private static IEnumerator House(string[] args, string stairPrefab)
        {
            string tag = args.ElementAtOrDefault(0) ?? "H";
            if (ZNetScene.instance.GetPrefab(stairPrefab) == null)
                throw new InvalidOperationException($"{stairPrefab} prefab missing");
            HiringBoard board = FixturesWork.Board();
            Vector3 n = board.transform.right; // from the house towards the board
            Vector3 t = board.transform.forward;
            Vector3 c = board.transform.position - board.transform.right * 12f;
            float ground = ZoneSystem.instance.GetGroundHeight(c);

            // Walls like the room fixture's; the middle of the side facing the board is the door.
            for (int i = -1; i <= 1; i++)
            {
                Place(i == 0 ? "wood_door" : "woodwall", Ground(c + n * 3f + t * (2f * i)), n, i == 0 ? tag + "_door" : "");
                Place("woodwall", Ground(c - n * 3f + t * (2f * i)), n, "");
                Place("woodwall", Ground(c + t * 3f + n * (2f * i)), t, "");
                Place("woodwall", Ground(c - t * 3f + n * (2f * i)), t, "");
            }

            // The stair climbs towards the back (-n); turn it round if it was placed climbing the other way.
            GameObject stair = Place(stairPrefab, new Vector3(c.x, ground, c.z), -n, tag + "_stair");
            yield return null;
            // Colliders only follow a moved transform after a sync; measuring before it reads the old place.
            Physics.SyncTransforms();
            if (Climbs(stair, -n) < 0f)
                stair.transform.rotation = Quaternion.LookRotation(n);
            Physics.SyncTransforms();
            Bounds sb = Bounds(stair);
            float along = Mathf.Abs(n.x) * sb.extents.x + Mathf.Abs(n.z) * sb.extents.z;
            // High end at the front edge of the upper floor (1 m behind the centre), low end towards the door.
            Vector3 stairPos = c - n * 1f + n * along + (stair.transform.position - sb.center).With(y: 0f);
            stair.transform.position = new Vector3(stairPos.x, ground, stairPos.z);
            Physics.SyncTransforms();
            // Its origin isn't at its foot: stand it on the ground under its own foot.
            float footGround = ZoneSystem.instance.GetGroundHeight(stair.transform.position + n * along * 0.8f);
            stair.transform.position += Vector3.up * (footGround - Bounds(stair).min.y);
            Physics.SyncTransforms();
            // Slide it so its top step (as the scan measures it) ends 0.9 m behind the centre, just inside the upper
            // floor's front edge (1 m behind), so the floor is there to step onto.
            if (TopStepPoint(stair, -n) is Vector3 step)
            {
                float d = Vector3.Dot(step - c, -n);
                stair.transform.position += -n * (0.9f - d);
                Physics.SyncTransforms();
            }
            // The upper floor goes level with the top step (not the rails' top).
            float top = TopStep(stair, -n);

            // Upper floor: the back 2 m strip, three 2x2 m floor pieces, its surface level with the stair's top.
            for (int i = -1; i <= 1; i++)
            {
                GameObject floor = Place("wood_floor", c - n * 2f + t * (2f * i) + Vector3.up * (top - c.y), n, i == 0 ? tag + "_up" : "");
                floor.transform.position = new Vector3(floor.transform.position.x, top, floor.transform.position.z);
                Physics.SyncTransforms();
                float surface = Bounds(floor).max.y;
                floor.transform.position += Vector3.up * (top - surface);
                Physics.SyncTransforms();
            }
            yield return null;
            GameObject chest = Place("piece_chest_wood", new Vector3((c - n * 2f + t * 2f).x, top, (c - n * 2f + t * 2f).z), n, tag);
            yield return null;
            Inventory inv = chest.GetComponent<Container>().GetInventory();
            for (int i = 1; i + 1 < args.Length; i += 2)
                FixturesWork.AddStacks(inv, args[i], int.Parse(args[i + 1], CultureInfo.InvariantCulture));
            VfhLog.I(LogCat.Test, "fixture.house", ("tag", tag), ("stair", stairPrefab), ("center", c), ("ground", ground), ("upperFloor", top),
                ("rise", top - Bounds(stair).min.y), ("stairPos", stair.transform.position), ("climbs", Climbs(stair, -n) >= 0f ? "back" : "front"));
            yield return new WaitForSeconds(0.5f);

            Vector3 Ground(Vector3 p) => new(p.x, ZoneSystem.instance.GetGroundHeight(p), p.z);
        }

        // Where the top step is, by the scan's own shape test (null if it doesn't take the piece for a stair).
        private static Vector3? TopStepPoint(GameObject piece, Vector3 axis)
        {
            var cols = new System.Collections.Generic.List<Collider>();
            if (!StairSampler.Colliders(piece.GetComponent<Piece>(), cols, out Bounds b))
                return null;
            var points = new System.Collections.Generic.List<Vector3>();
            StairResult r = StairProfile.Classify(StairSampler.Sample(cols, b, axis, points), nameHint: true);
            return r.Accepted ? points[r.TopIndex] : null;
        }

        // The highest walkable surface along the axis, sampled the way the board's scan does it.
        private static float TopStep(GameObject piece, Vector3 axis)
        {
            var cols = new System.Collections.Generic.List<Collider>();
            if (!StairSampler.Colliders(piece.GetComponent<Piece>(), cols, out Bounds b))
                return Bounds(piece).max.y;
            float? top = StairSampler.Sample(cols, b, axis, new System.Collections.Generic.List<Vector3>()).Max(s => s.Height);
            return top ?? b.max.y;
        }

        // Which way the piece climbs along the axis (positive: up towards +axis), judged by the scan's own shape test
        // on the piece's own colliders, so the ground and floors around it can't mislead it.
        private static float Climbs(GameObject piece, Vector3 axis)
        {
            var cols = new System.Collections.Generic.List<Collider>();
            if (StairSampler.Colliders(piece.GetComponent<Piece>(), cols, out Bounds b))
            {
                var samples = StairSampler.Sample(cols, b, axis, new System.Collections.Generic.List<Vector3>());
                StairResult r = StairProfile.Classify(samples, nameHint: true);
                if (r.Accepted)
                    return r.TopIndex > r.BottomIndex ? 1f : -1f;
            }
            return HighEnd(piece, axis);
        }

        private static IEnumerator Flatten(string[] args)
        {
            float radius = args.Length > 0 ? float.Parse(args[0], CultureInfo.InvariantCulture) : 22f;
            Vector3 c = Player.m_localPlayer.transform.position;
            c.y = ZoneSystem.instance.GetGroundHeight(c);
            // Clutter first, so nothing ends up buried or floating.
            int removed = 0;
            foreach (ZNetView v in ZNetScene.instance.m_instances.Values.ToList())
            {
                if (v == null || v.GetZDO() == null || Utils.DistanceXZ(v.transform.position, c) > radius)
                    continue;
                GameObject go = v.gameObject;
                if (go.GetComponent<Piece>() != null || go.GetComponent<Character>() != null)
                    continue;
                if (go.GetComponent<TreeBase>() || go.GetComponent<TreeLog>() || go.GetComponent<MineRock>() || go.GetComponent<MineRock5>() ||
                    go.GetComponent<Destructible>() || go.GetComponent<Pickable>() || go.GetComponent<ItemDrop>())
                {
                    ZNetScene.instance.Destroy(go);
                    removed++;
                }
            }
            yield return null;
            // The hoe's level, as one big operation at your feet's height. Not through a TerrainOp object: the game only
            // accepts those as registered prefabs and then uses the prefab's own settings, so apply it to each terrain
            // patch directly (this game owns them in single player).
            var settings = new TerrainOp.Settings
            {
                m_level = true, m_levelRadius = radius, m_levelOffset = 0f, m_square = false,
                m_raise = false, m_smooth = false, m_paintCleared = false,
            };
            var maps = new System.Collections.Generic.List<Heightmap>();
            Heightmap.FindHeightmap(c, radius, maps);
            System.Reflection.MethodInfo doOp = HarmonyLib.AccessTools.Method(typeof(TerrainComp), "DoOperation",
                new[] { typeof(Vector3), typeof(Vector3), typeof(TerrainOp.Settings) });
            int patches = 0;
            foreach (Heightmap hm in maps)
            {
                TerrainComp comp = hm.GetAndCreateTerrainCompiler();
                if (comp == null || comp.m_nview == null || !comp.m_nview.IsValid())
                    continue;
                if (!comp.m_nview.IsOwner())
                    comp.m_nview.ClaimOwnership();
                doOp.Invoke(comp, new object[] { c, Vector3.zero, settings });
                patches++;
            }
            VfhLog.I(LogCat.Test, "fixture.flatten", ("center", c), ("radius", radius), ("removed", removed), ("patches", patches));
            // The game's walking map catches up with new ground a few seconds later.
            yield return new WaitForSeconds(6f);
        }

        // Which way the piece climbs along the axis: positive when its surface is higher towards +axis.
        private static float HighEnd(GameObject piece, Vector3 axis)
        {
            Bounds b = Bounds(piece);
            float reach = Mathf.Abs(axis.x) * b.extents.x + Mathf.Abs(axis.z) * b.extents.z;
            float? Surface(Vector3 p) => Physics.Raycast(new Vector3(p.x, b.max.y + 0.5f, p.z), Vector3.down, out RaycastHit hit, b.size.y + 1f,
                StairSampler.FloorMask, QueryTriggerInteraction.Ignore) ? hit.point.y : null;
            float hi = Surface(b.center + axis * reach * 0.8f) ?? b.min.y;
            float lo = Surface(b.center - axis * reach * 0.8f) ?? b.min.y;
            return hi - lo;
        }

        private static Bounds Bounds(GameObject go)
        {
            Collider[] cols = go.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger).ToArray();
            Bounds b = cols.Length > 0 ? cols[0].bounds : new Bounds(go.transform.position, Vector3.one);
            foreach (Collider c in cols.Skip(1))
                b.Encapsulate(c.bounds);
            return b;
        }

        private static GameObject Place(string prefab, Vector3 pos, Vector3 facing, string tag)
        {
            GameObject go = Object.Instantiate(ZNetScene.instance.GetPrefab(prefab) ?? throw new ArgumentException($"no prefab {prefab}"), pos,
                Quaternion.LookRotation(facing));
            ZDO zdo = go.GetComponent<ZNetView>().GetZDO();
            zdo.Set(BoardZdo.Fixture, true);
            if (tag.Length > 0)
                zdo.Set(FixturesWork.TagKey, tag);
            FixturesWork.OwnBuilt(go);
            if (go.GetComponent<WearNTear>() is WearNTear wnt)
                wnt.m_noSupportWear = false; // test buildings on uneven ground mustn't collapse
            return go;
        }

        private static IEnumerator RoofOver(string[] args)
        {
            string prefab = args.ElementAtOrDefault(0) ?? "piece_spinningwheel";
            Vector3 me = Player.m_localPlayer.transform.position;
            Piece? station = Piece.s_allPieces.Where(p => p != null && Utils.GetPrefabName(p.gameObject) == prefab)
                .OrderBy(p => Vector3.Distance(p.transform.position, me)).FirstOrDefault()
                ?? throw new InvalidOperationException($"no {prefab} near you");
            Bounds b = Bounds(station.gameObject);
            float y = b.max.y + 1.2f; // high enough that the floor check beside the wheel finds the ground, not the roof
            for (int i = -1; i <= 1; i++)
                for (int j = -1; j <= 1; j++)
                    Place("wood_roof", new Vector3(b.center.x + i * 2f, y, b.center.z + j * 2f), Vector3.forward, ""); // floors are "leaky": rain, and the roof check, go through
            // A fermenter wants 70% of the directions round it covered, sideways too, not just a roof:
            // walls on three sides, stacked up to the roof, with the east side open so hirelings can get in.
            bool walls = args.Skip(1).Contains("walls");
            if (walls)
            {
                float ground = ZoneSystem.instance.GetGroundHeight(b.center);
                float step = 0f;
                foreach (Vector3 side in new[] { Vector3.forward, Vector3.back, Vector3.left })
                {
                    Vector3 along = Vector3.Cross(Vector3.up, side);
                    for (int k = -1; k <= 1; k++)
                    {
                        Vector3 at = b.center + side * 3f + along * (2f * k);
                        float h = ground;
                        do
                        {
                            GameObject wall = Place("woodwall", new Vector3(at.x, h, at.z), side, "");
                            if (step <= 0f)
                                step = Mathf.Max(0.5f, Bounds(wall).size.y);
                            h += step;
                        }
                        while (h < y);
                    }
                }
            }
            VfhLog.I(LogCat.Test, "fixture.roof_over", ("prefab", prefab), ("height", y), ("walls", walls));
            yield return new WaitForSeconds(1f);
        }

        // Two woodcutters meet head-on in a corridor just wider than one of them: each must get to the other's end.
        private static IEnumerator PassTest(string[] args)
        {
            float length = float.Parse(args.ElementAtOrDefault(0) ?? "12", CultureInfo.InvariantCulture);
            float width = float.Parse(args.ElementAtOrDefault(1) ?? "1.4", CultureInfo.InvariantCulture);
            HiringBoard board = FixturesWork.Board();
            Vector3 Ground(Vector3 p) => new(p.x, ZoneSystem.instance.GetGroundHeight(p), p.z);
            Vector3 along = board.transform.right;
            Vector3 across = board.transform.forward;
            Vector3 mid = board.transform.position + across * 8f;
            for (float d = -length / 2f + 1f; d <= length / 2f - 1f + 0.01f; d += 2f)
                foreach (int side in new[] { -1, 1 })
                    Place("woodwall", Ground(mid + along * d + across * side * (width / 2f + 0.15f)), across, "");
            Vector3 a = Ground(mid - along * (length / 2f - 0.5f));
            Vector3 b = Ground(mid + along * (length / 2f - 0.5f));
            Hirelings.TestWalkBehaviour.Targets.Clear();
            Commands.HirelingCommands.Spawn(new[] { "Woodcutter", "1", "1" }, a);
            string hidA = Commands.HirelingCommands.LastSpawned.FirstOrDefault() ?? "";
            Commands.HirelingCommands.Spawn(new[] { "Woodcutter", "1", "1" }, b);
            string hidB = Commands.HirelingCommands.LastSpawned.FirstOrDefault() ?? "";
            yield return new WaitForSeconds(3f); // let them load in
            Hirelings.TestWalkBehaviour.Targets[hidA] = b;
            Hirelings.TestWalkBehaviour.Targets[hidB] = a;
            VfhLog.I(LogCat.Test, "fixture.pass_test", ("a", hidA), ("b", hidB), ("length", length), ("width", width));
        }

        private static int _hopsAtReset;

        private static IEnumerator ResetStats()
        {
            _hopsAtReset = LinkNavigator.Hops;
            yield return null;
        }

        private static IEnumerator NavLinks(string[] args)
        {
            if (ZNet.instance != null && !ZNet.instance.IsServer())
                throw new InvalidOperationException("nav_links only works in single player (it's a server setting)");
            bool on = args.ElementAtOrDefault(0) != "off";
            Config.VfhConfig.BaseNavLinks.Value = on;
            VfhLog.I(LogCat.Test, "fixture.nav_links", ("on", on));
            yield return null;
        }

        private static IEnumerator RemovePiece(string[] args)
        {
            GameObject go = FixturesWork.FindTagged(args.ElementAtOrDefault(0) ?? "") ?? throw new InvalidOperationException("no such tagged piece");
            if (go.GetComponent<WearNTear>() is WearNTear wnt)
                wnt.Remove();
            else
                ZNetScene.instance.Destroy(go);
            VfhLog.I(LogCat.Test, "fixture.remove_piece", ("tag", args[0]));
            yield return null;
        }
    }

    internal static class VectorWith
    {
        public static Vector3 With(this Vector3 v, float? x = null, float? y = null, float? z = null) => new(x ?? v.x, y ?? v.y, z ?? v.z);
    }
}

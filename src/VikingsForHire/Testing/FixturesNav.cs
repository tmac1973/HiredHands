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
            Fixtures.Add("house2", "<tag> [item count]… - a 6x8 m house 13 m to the board's left: door facing the board (<tag>_door), two wood stair flights (<tag>_stair, <tag>_stair2 above it) up to an upper floor 2 m up at the back (middle piece <tag>_up), chest upstairs (<tag>)",
                args => House(args, "wood_stair"));
            Fixtures.Add("stepladder", "<tag> [item count]… - the same house with one stepladder (<tag>_stair) instead of the stairs",
                args => House(args, "wood_stepladder"));
            Fixtures.Add("stair_probe", "<prefab> - place a piece 5 m ahead at turns of 0-165 degrees and log what the stair scan's sampler reads at each (then remove it)", StairProbe);
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

        // The real climb of one flight of each piece, measured with the probe below (vanilla, 2026-10-08): wood_stair's top
        // step is 1.05 m up (two flights make a 2 m storey, as in the game), the stepladder's top plank 2.05 m.
        private const float StoreyRise = 1.8f;

        private static IEnumerator House(string[] args, string stairPrefab)
        {
            string tag = args.ElementAtOrDefault(0) ?? "H";
            if (ZNetScene.instance.GetPrefab(stairPrefab) == null)
                throw new InvalidOperationException($"{stairPrefab} prefab missing");
            HiringBoard board = FixturesWork.Board();
            Vector3 n = board.transform.right; // from the house towards the board
            Vector3 t = board.transform.forward;
            // 6 m wide, 8 m deep: the upper floor is the back 2 m, two wood_stair flights take 4 m, and there's room to stand
            // at their foot inside the door.
            Vector3 c = board.transform.position - board.transform.right * 13f;
            float ground = ZoneSystem.instance.GetGroundHeight(c);

            // Walls like the room fixture's; the middle of the side facing the board is the door.
            for (int i = -1; i <= 1; i++)
            {
                Place(i == 0 ? "wood_door" : "woodwall", Ground(c + n * 4f + t * (2f * i)), n, i == 0 ? tag + "_door" : "");
                Place("woodwall", Ground(c - n * 4f + t * (2f * i)), n, "");
            }
            for (int i = -3; i <= 3; i += 2)
            {
                Place("woodwall", Ground(c + t * 3f + n * i), t, "");
                Place("woodwall", Ground(c - t * 3f + n * i), t, "");
            }

            // One flight first, to measure: it should climb towards the back (-n); turn it round if it climbs the other way.
            GameObject first = Place(stairPrefab, new Vector3(c.x, ground, c.z), -n, tag + "_stair");
            yield return null;
            // Colliders only follow a moved transform after a sync; measuring before it reads the old place.
            Physics.SyncTransforms();
            if (Flight(first, -n).TopIndex < Flight(first, -n).BottomIndex)
            {
                first.transform.rotation = Quaternion.LookRotation(n);
                Physics.SyncTransforms();
            }
            Flight(first, -n, out Vector3 lo, out Vector3 hi);
            if (Vector3.Dot(hi - lo, -n) <= 0f)
                throw new InvalidOperationException($"{stairPrefab} still climbs towards the door after turning it round");
            Vector3 origin = first.transform.position;
            float baseY = Bounds(first).min.y - origin.y; // its foot, below its origin
            float rise = hi.y - (origin.y + baseY);
            // Cross-check against its own colliders: the top step is at most a rail's height below their top.
            float colliderTop = Bounds(first).max.y - (origin.y + baseY);
            if (colliderTop - rise > 0.25f)
                VfhLog.W(LogCat.Test, "fixture.house_stair_low", ("stair", stairPrefab), ("rise", rise), ("colliderTop", colliderTop));
            // How far one flight runs along the climb (its own length, so flights meet end to end).
            Bounds lb = StairSampler.LocalBounds(Colliders(first), first.transform);
            Vector3 la = Quaternion.Inverse(first.transform.rotation) * -n;
            float run = 2f * (Mathf.Abs(la.x) * lb.extents.x + Mathf.Abs(la.z) * lb.extents.z);
            int flights = Mathf.Clamp(Mathf.CeilToInt(StoreyRise / rise), 1, 3);

            // Lay the flights end to end, each starting on the one below's top step, with the top flight's top step 0.1 m
            // inside the upper floor's front edge (2 m behind the centre) and the bottom flight's foot on the ground.
            Vector3 perFlight = -n * run + Vector3.up * (hi.y - origin.y);
            Vector3 topStep = hi + perFlight * (flights - 1);
            Vector3 shift = -n * (2.1f - Vector3.Dot(topStep - c, -n));
            first.transform.position += shift.With(y: 0f);
            Physics.SyncTransforms();
            float footGround = ZoneSystem.instance.GetGroundHeight(lo + shift.With(y: 0f) + n * 0.3f);
            first.transform.position += Vector3.up * (footGround - (first.transform.position.y + baseY));
            Physics.SyncTransforms();
            GameObject stair = first;
            for (int k = 1; k < flights; k++)
            {
                stair = Place(stairPrefab, first.transform.position + perFlight * k, first.transform.forward, $"{tag}_stair{k + 1}");
                Physics.SyncTransforms();
            }
            yield return null;
            Physics.SyncTransforms();
            // The upper floor goes level with the top flight's top step as the scan reads it (not the rails' top).
            StairResult r = Flight(stair, -n, out _, out Vector3 topPoint);
            float top = topPoint.y;
            float storey = top - footGround;
            if (storey < StoreyRise)
                VfhLog.W(LogCat.Test, "fixture.house_low", ("stair", stairPrefab), ("flights", flights), ("storey", storey), ("want", StoreyRise));

            // Upper floor: the back 2 m strip, three 2x2 m floor pieces, its surface level with the stair's top.
            for (int i = -1; i <= 1; i++)
            {
                GameObject floor = Place("wood_floor", c - n * 3f + t * (2f * i) + Vector3.up * (top - c.y), n, i == 0 ? tag + "_up" : "");
                floor.transform.position = new Vector3(floor.transform.position.x, top, floor.transform.position.z);
                Physics.SyncTransforms();
                float surface = Bounds(floor).max.y;
                floor.transform.position += Vector3.up * (top - surface);
                Physics.SyncTransforms();
            }
            yield return null;
            GameObject chest = Place("piece_chest_wood", new Vector3((c - n * 3f + t * 2f).x, top, (c - n * 3f + t * 2f).z), n, tag);
            yield return null;
            Inventory inv = chest.GetComponent<Container>().GetInventory();
            for (int i = 1; i + 1 < args.Length; i += 2)
                FixturesWork.AddStacks(inv, args[i], int.Parse(args[i + 1], CultureInfo.InvariantCulture));
            VfhLog.I(LogCat.Test, "fixture.house", ("tag", tag), ("stair", stairPrefab), ("center", c), ("ground", footGround), ("upperFloor", top),
                ("flights", flights), ("flightRise", rise), ("run", run), ("storey", storey), ("slope", r.SlopeDeg),
                ("topStepIn", Vector3.Dot(topPoint - c, -n) - 2f), ("climbs", Vector3.Dot(hi - lo, -n) > 0f ? "back" : "front"));
            yield return new WaitForSeconds(0.5f);

            Vector3 Ground(Vector3 p) => new(p.x, ZoneSystem.instance.GetGroundHeight(p), p.z);
        }

        private static System.Collections.Generic.List<Collider> Colliders(GameObject piece)
        {
            var cols = new System.Collections.Generic.List<Collider>();
            StairSampler.Colliders(piece.GetComponent<Piece>(), cols, out _);
            return cols;
        }

        // One flight by the scan's own shape test along the axis: its bottom and top step. A test house built on a piece
        // the scan doesn't take for a stair would test nothing, so that fails the fixture.
        private static StairResult Flight(GameObject piece, Vector3 axis) => Flight(piece, axis, out _, out _);

        private static StairResult Flight(GameObject piece, Vector3 axis, out Vector3 bottom, out Vector3 top)
        {
            var cols = new System.Collections.Generic.List<Collider>();
            if (!StairSampler.Colliders(piece.GetComponent<Piece>(), cols, out Bounds b))
                throw new InvalidOperationException($"{Utils.GetPrefabName(piece)} has no colliders");
            var points = new System.Collections.Generic.List<Vector3>();
            var samples = StairSampler.Sample(cols, b, piece.transform, axis, points);
            StairResult r = StairProfile.Classify(samples, nameHint: true);
            if (!r.Accepted)
                throw new InvalidOperationException($"{Utils.GetPrefabName(piece)} isn't a stair to the scan ({r.Reason}): heights " +
                                                    string.Join(" ", samples.Select(x => x.Height?.ToString("0.00", CultureInfo.InvariantCulture) ?? "-")));
            bottom = points[r.BottomIndex];
            top = points[r.TopIndex];
            return r;
        }

        // Places the piece at several turns and logs what the scan's sampler reads along its own forward each time (the
        // board faces a different way each run, so a reading that depends on the turn shows up here).
        private static IEnumerator StairProbe(string[] args)
        {
            string prefab = args.ElementAtOrDefault(0) ?? "wood_stair";
            Transform me = Player.m_localPlayer.transform;
            Vector3 at = me.position + me.forward.With(y: 0f).normalized * 5f;
            at.y = ZoneSystem.instance.GetGroundHeight(at);
            for (int yaw = 0; yaw < 180; yaw += 15)
            {
                GameObject go = Place(prefab, at, Quaternion.Euler(0f, yaw, 0f) * Vector3.forward, "");
                yield return null;
                Physics.SyncTransforms();
                var cols = new System.Collections.Generic.List<Collider>();
                StairSampler.Colliders(go.GetComponent<Piece>(), cols, out Bounds b);
                if (yaw == 0)
                    foreach (Collider c in cols)
                        VfhLog.I(LogCat.Test, "fixture.stair_probe.col", ("name", c.name), ("min", c.bounds.min - at), ("max", c.bounds.max - at));
                var samples = StairSampler.Sample(cols, b, go.transform, go.transform.forward, new System.Collections.Generic.List<Vector3>());
                StairResult r = StairProfile.Classify(samples, nameHint: true);
                VfhLog.I(LogCat.Test, "fixture.stair_probe", ("prefab", prefab), ("yaw", yaw), ("top", b.max.y - at.y),
                    ("local", StairSampler.LocalBounds(cols, go.transform)),
                    ("heights", string.Join(" ", samples.Select(x => x.Height is float h ? (h - at.y).ToString("0.00", CultureInfo.InvariantCulture) : "-"))),
                    ("accepted", r.Accepted), ("reason", r.Reason), ("climbs", r.Accepted ? (r.TopIndex > r.BottomIndex ? "fwd" : "back") : "?"), ("rise", r.Rise));
                ZNetScene.instance.Destroy(go);
                yield return null;
            }
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

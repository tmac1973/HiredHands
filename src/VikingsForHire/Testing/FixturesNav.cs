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
            Fixtures.Add("house2", "<tag> [item count]… - a 6x6 m house 12 m to the board's left: door facing the board (<tag>_door), a wood stair (<tag>_stair) up to an upper floor at the back, chest upstairs (<tag>)",
                args => House(args, "wood_stair"));
            Fixtures.Add("stepladder", "<tag> [item count]… - the same house with a stepladder (<tag>_stair) instead of the stair",
                args => House(args, "wood_stepladder"));
            Fixtures.Add("remove_piece", "<tag> - deconstruct a tagged piece (as a player with the hammer)", RemovePiece);

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
            if (HighEnd(stair, -n) < 0f)
                stair.transform.rotation = Quaternion.LookRotation(n);
            yield return null;
            Bounds sb = Bounds(stair);
            float along = Mathf.Abs(n.x) * sb.extents.x + Mathf.Abs(n.z) * sb.extents.z;
            // High end at the front edge of the upper floor (1 m behind the centre), low end towards the door.
            Vector3 stairPos = c - n * 1f + n * along + (stair.transform.position - sb.center).With(y: 0f);
            stair.transform.position = new Vector3(stairPos.x, ground, stairPos.z);
            yield return null;
            float top = Bounds(stair).max.y;

            // Upper floor: the back 2 m strip, three 2x2 m floor pieces, its surface level with the stair's top.
            for (int i = -1; i <= 1; i++)
            {
                GameObject floor = Place("wood_floor", c - n * 2f + t * (2f * i) + Vector3.up * (top - c.y), n, "");
                floor.transform.position = new Vector3(floor.transform.position.x, top, floor.transform.position.z);
                yield return null;
                float surface = Bounds(floor).max.y;
                floor.transform.position += Vector3.up * (top - surface);
            }
            yield return null;
            GameObject chest = Place("piece_chest_wood", new Vector3((c - n * 2f + t * 2f).x, top, (c - n * 2f + t * 2f).z), n, tag);
            yield return null;
            Inventory inv = chest.GetComponent<Container>().GetInventory();
            for (int i = 1; i + 1 < args.Length; i += 2)
                FixturesWork.AddStacks(inv, args[i], int.Parse(args[i + 1], CultureInfo.InvariantCulture));
            VfhLog.I(LogCat.Test, "fixture.house", ("tag", tag), ("stair", stairPrefab), ("center", c), ("ground", ground), ("upperFloor", top));
            yield return new WaitForSeconds(0.5f);

            Vector3 Ground(Vector3 p) => new(p.x, ZoneSystem.instance.GetGroundHeight(p), p.z);
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

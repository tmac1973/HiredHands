using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using VikingsForHire.Commands;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using Object = UnityEngine.Object;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// The Steward's broom: a club (same stats, the Steward's weapon at every level) wearing a broom built here from simple
    /// shapes: a wooden handle (the club's own wood), a binding, and a flat, flared bundle of bristles (the thatch roof's
    /// straw). Valheim has no broom to borrow, and the cultivator is kept for a farmer. Never crafted by players.
    /// </summary>
    internal static class BroomItem
    {
        public const string PrefabName = "VFH_Broom";
        private const string BasePrefab = "Club";
        private const string StrawPrefab = "wood_roof";
        private const string WoodPrefab = "wood_pole";

        // In metres along the broom, the hand at 0: the handle's butt behind the hand, the bristles at the far end.
        private const float Butt = -0.25f, HandleTop = 1.05f, HandleRadius = 0.022f;
        private const float BindTop = 1.12f, BindRadius = 0.04f;
        private const float BristleEnd = 1.5f;
        private const int Sides = 10;

        private static bool _created;
        private static CustomItem? _item;

        /// <summary>Which model the broom wears, for vfh_broom_info.</summary>
        public static string Source { get; private set; } = "none";

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += () => VfhLog.Guard(LogCat.Hireling, "broom.register_failed", Create);
            CommandManager.Instance.AddConsoleCommand(new VfhCommand("vfh_broom_info", "- which model the Steward's broom uses", false, _ =>
                VfhCommand.Print($"HiredHands: broom model {Source}")));
        }

        private static void Create()
        {
            if (_created)
                return;
            _created = true;
            _item = new CustomItem(PrefabName, BasePrefab, new ItemConfig { Name = "$vfh_broom", Description = "$vfh_broom_desc" });
            ItemManager.Instance.AddItem(_item);
            Dress(_item.ItemPrefab);
        }

        private static void Dress(GameObject prefab)
        {
            MeshFilter? visual = Gear.HeldModel.Visual(prefab);
            MeshRenderer? renderer = visual != null ? visual.GetComponent<MeshRenderer>() : null;
            if (visual == null || renderer == null)
            {
                Source = "club (no visual found)";
                VfhLog.W(LogCat.Hireling, "broom.model", ("source", Source));
                return;
            }
            // Building pieces' textures repeat, so they wrap a made-up shape cleanly; the club's own is laid out for the club.
            Material wood = Gear.HeldModel.PieceMaterial(WoodPrefab, "wood") ?? renderer.sharedMaterial;
            Material straw = Gear.HeldModel.PieceMaterial(StrawPrefab, "straw", "thatch") ?? wood;
            Vector3 along = Gear.HeldModel.Point(visual, Build(), new[] { wood, straw });
            Source = $"built (wood {wood.name}, bristles {straw.name})";
            VfhLog.I(LogCat.Hireling, "broom.model", ("source", Source), ("clubAxis", along));
        }

        // Submesh 0 (wood): the handle and the binding. Submesh 1 (straw): the bristles, flat and flaring, ragged at the end.
        private static Mesh Build()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var wood = new List<int>();
            var straw = new List<int>();
            Tube(verts, uvs, wood, Butt, HandleTop + 0.02f, new Vector2(HandleRadius, HandleRadius), new Vector2(HandleRadius, HandleRadius), 0f, true, false);
            Tube(verts, uvs, wood, HandleTop - 0.04f, BindTop, new Vector2(BindRadius, BindRadius * 0.8f), new Vector2(BindRadius, BindRadius * 0.8f), 0f, true, true);
            Tube(verts, uvs, straw, BindTop - 0.02f, BristleEnd, new Vector2(0.045f, 0.035f), new Vector2(0.17f, 0.05f), 0.05f, false, true);
            var mesh = new Mesh { name = "vfh_broom" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(wood, 0);
            mesh.SetTriangles(straw, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // An elliptical tube along +y from y0 (radii r0) to y1 (radii r1), capped at either end; jag makes the far rim ragged.
        private static void Tube(List<Vector3> v, List<Vector2> uv, List<int> tri, float y0, float y1, Vector2 r0, Vector2 r1, float jag, bool cap0, bool cap1)
        {
            int ring = v.Count;
            for (int i = 0; i <= Sides; i++)
            {
                float a = i * Mathf.PI * 2f / Sides;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                float end = y1 - (i % 2 == 1 ? jag : 0f);
                v.Add(new Vector3(c * r0.x, y0, s * r0.y));
                uv.Add(new Vector2((float)i / Sides, 0f));
                v.Add(new Vector3(c * r1.x, end, s * r1.y));
                uv.Add(new Vector2((float)i / Sides, 1f));
            }
            for (int i = 0; i < Sides; i++)
            {
                int a0 = ring + i * 2, b0 = a0 + 1, a1 = a0 + 2, b1 = a0 + 3;
                tri.AddRange(new[] { a0, b0, a1, a1, b0, b1 });
            }
            if (cap0)
                Cap(v, uv, tri, ring, 0, y0, false);
            if (cap1)
                Cap(v, uv, tri, ring, 1, y1 - jag * 0.5f, true);
        }

        private static void Cap(List<Vector3> v, List<Vector2> uv, List<int> tri, int ring, int offset, float y, bool up)
        {
            int centre = v.Count;
            v.Add(new Vector3(0f, y, 0f));
            uv.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i < Sides; i++)
            {
                int a = ring + i * 2 + offset, b = ring + (i + 1) * 2 + offset;
                if (up)
                    tri.AddRange(new[] { centre, b, a });
                else
                    tri.AddRange(new[] { centre, a, b });
            }
        }

    }
}

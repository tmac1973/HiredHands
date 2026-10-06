using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Gear
{
    /// <summary>
    /// The Cook's ladle: a club (same stats, the Cook's weapon at every level) wearing a ladle built from simple shapes,
    /// a wooden handle and an iron bowl at the far end. Never crafted by players.
    /// </summary>
    internal static class LadleItem
    {
        public const string PrefabName = "VFH_Ladle";
        private const string BasePrefab = "Club";
        private const float Butt = -0.2f, HandleTop = 0.85f, HandleRadius = 0.018f, BowlRadius = 0.12f;

        private static bool _created;

        public static void Register() =>
            PrefabManager.OnVanillaPrefabsAvailable += () => VfhLog.Guard(LogCat.Hireling, "ladle.register_failed", Create);

        private static void Create()
        {
            if (_created)
                return;
            _created = true;
            var item = new CustomItem(PrefabName, BasePrefab, new ItemConfig { Name = "$vfh_ladle", Description = "$vfh_ladle_desc" });
            ItemManager.Instance.AddItem(item);
            MeshFilter? visual = HeldModel.Visual(item.ItemPrefab);
            MeshRenderer? renderer = visual != null ? visual.GetComponent<MeshRenderer>() : null;
            if (visual == null || renderer == null)
            {
                VfhLog.W(LogCat.Hireling, "ladle.model", ("source", "club (no visual found)"));
                return;
            }
            Material wood = HeldModel.PieceMaterial("wood_pole", "wood") ?? renderer.sharedMaterial;
            Material iron = HeldModel.PieceMaterial("piece_cookingstation_iron", "iron", "metal") ?? wood;
            HeldModel.Point(visual, Build(), new[] { wood, iron });
            VfhLog.I(LogCat.Hireling, "ladle.model", ("wood", wood.name), ("bowl", iron.name));
        }

        // Submesh 0 (wood): the handle. Submesh 1 (iron): the bowl, hanging off the end, its rim facing forward.
        private static Mesh Build()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var wood = new List<int>();
            var iron = new List<int>();
            MeshBuilder.Tube(verts, uvs, wood, Butt, HandleTop, new Vector2(HandleRadius, HandleRadius), new Vector2(HandleRadius * 0.8f, HandleRadius * 0.8f), 0f, true, true);
            MeshBuilder.Bowl(verts, uvs, iron, new Vector3(0f, HandleTop + BowlRadius * 0.6f, BowlRadius * 0.7f), BowlRadius, Vector3.forward + Vector3.up * 0.4f);
            var mesh = new Mesh { name = "vfh_ladle" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(wood, 0);
            mesh.SetTriangles(iron, 1);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

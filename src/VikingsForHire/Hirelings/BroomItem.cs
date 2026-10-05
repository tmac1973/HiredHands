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
    /// The Steward's broom: a club (same stats, the Steward's weapon at every level) wearing the game's own broom model.
    /// Valheim has no broom item, but a broom mesh is among its assets: it's looked up by name when the vanilla prefabs are
    /// ready, and only used if it's a sensible size. Otherwise the cultivator's model is used. Never crafted by players.
    /// </summary>
    internal static class BroomItem
    {
        public const string PrefabName = "VFH_Broom";
        private const string BasePrefab = "Club";
        private const string FallbackPrefab = "Cultivator";
        private const float Length = 1.4f;

        /// <summary>Meshes named like a broom that turned out wrong in game (by mesh name).</summary>
        private static readonly HashSet<string> Rejected = new(StringComparer.OrdinalIgnoreCase);

        private static bool _created;

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
            var item = new CustomItem(PrefabName, BasePrefab, new ItemConfig { Name = "$vfh_broom", Description = "$vfh_broom_desc" });
            ItemManager.Instance.AddItem(item);
            GameObject prefab = item.ItemPrefab;
            MeshFilter? visual = Visual(prefab);
            if (visual == null)
            {
                Source = "club (no visual found)";
                VfhLog.W(LogCat.Hireling, "broom.model", ("source", Source));
                return;
            }
            (Mesh Mesh, Material[] Materials, string From)? model = FindBroom() ?? Fallback();
            if (model is not var (mesh, materials, from))
            {
                Source = "club (no broom or cultivator found)";
                VfhLog.W(LogCat.Hireling, "broom.model", ("source", Source));
                return;
            }
            Swap(visual, mesh, materials);
            Source = from;
            VfhLog.I(LogCat.Hireling, "broom.model", ("source", from), ("mesh", mesh.name), ("size", mesh.bounds.size));
        }

        // The club's in-hand visual: the biggest mesh under its "attach" transform (or anywhere in it).
        private static MeshFilter? Visual(GameObject prefab)
        {
            Transform? attach = prefab.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "attach");
            IEnumerable<MeshFilter> filters = (attach != null ? attach.GetComponentsInChildren<MeshFilter>(true) : prefab.GetComponentsInChildren<MeshFilter>(true))
                .Where(f => f.sharedMesh != null && f.GetComponent<MeshRenderer>() != null);
            return filters.OrderByDescending(f => f.sharedMesh.bounds.size.magnitude).FirstOrDefault();
        }

        // A mesh named like a broom, of a broom's size (a whole building with "broom" in a part name won't do).
        private static (Mesh, Material[], string)? FindBroom()
        {
            IEnumerable<(MeshFilter Filter, string Where)> candidates =
                ZNetScene.instance.m_prefabs.Where(p => p != null).SelectMany(p => p.GetComponentsInChildren<MeshFilter>(true).Select(f => (f, "prefab:" + p.name)))
                    .Concat(Resources.FindObjectsOfTypeAll<MeshFilter>().Select(f => (f, "loaded")));
            foreach ((MeshFilter f, string where) in candidates)
            {
                Mesh? m = f != null ? f.sharedMesh : null;
                if (f == null || m == null || m.name.IndexOf("broom", StringComparison.OrdinalIgnoreCase) < 0 || Rejected.Contains(m.name))
                    continue;
                float longest = Mathf.Max(m.bounds.size.x, Mathf.Max(m.bounds.size.y, m.bounds.size.z)) * MaxScale(f.transform);
                MeshRenderer? r = f.GetComponent<MeshRenderer>();
                if (longest < 0.8f || longest > 3f || m.vertexCount > 5000 || r == null || r.sharedMaterials.Length == 0)
                    continue;
                return (m, r.sharedMaterials, where);
            }
            return null;
        }

        private static (Mesh, Material[], string)? Fallback()
        {
            GameObject? cultivator = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(FallbackPrefab) : null;
            MeshFilter? f = cultivator != null ? Visual(cultivator) : null;
            MeshRenderer? r = f != null ? f.GetComponent<MeshRenderer>() : null;
            return f != null && r != null ? (f.sharedMesh, r.sharedMaterials, "fallback:Cultivator") : null;
        }

        private static float MaxScale(Transform t) => Mathf.Max(Mathf.Abs(t.lossyScale.x), Mathf.Max(Mathf.Abs(t.lossyScale.y), Mathf.Abs(t.lossyScale.z)));

        // The broom in the club's place: lined up with the club along its longest axis, 1.4 m long.
        private static void Swap(MeshFilter visual, Mesh mesh, Material[] materials)
        {
            int clubAxis = LongestAxis(visual.sharedMesh.bounds.size);
            int broomAxis = LongestAxis(mesh.bounds.size);
            visual.sharedMesh = mesh;
            visual.GetComponent<MeshRenderer>().sharedMaterials = materials;
            Transform t = visual.transform;
            if (broomAxis != clubAxis)
                t.localRotation *= Quaternion.FromToRotation(Axis(broomAxis), Axis(clubAxis));
            float scale = Length / Mathf.Max(0.01f, mesh.bounds.size[broomAxis]);
            t.localScale = Vector3.one * scale;
        }

        private static int LongestAxis(Vector3 size) => size.x >= size.y && size.x >= size.z ? 0 : size.y >= size.z ? 1 : 2;

        private static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
    }
}

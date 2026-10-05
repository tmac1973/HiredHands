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
        private static bool _dressed;
        private static CustomItem? _item;

        /// <summary>Which model the broom wears, for vfh_broom_info.</summary>
        public static string Source { get; private set; } = "none";

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += () => VfhLog.Guard(LogCat.Hireling, "broom.register_failed", Create);
            // The model search needs the scene's prefabs, which only exist in a world (the event above fires at the menu).
            PrefabManager.OnPrefabsRegistered += () => VfhLog.Guard(LogCat.Hireling, "broom.model_failed", Dress);
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
        }

        private static void Dress()
        {
            if (_dressed || _item == null || ZNetScene.instance == null)
                return;
            _dressed = true;
            GameObject prefab = _item.ItemPrefab;
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

        // A mesh named like a broom, of a broom's size (a whole building with "broom" in a part name won't do). Meshes
        // drawn by plain or skinned renderers both count.
        private static (Mesh, Material[], string)? FindBroom()
        {
            IEnumerable<(Renderer R, Mesh M, string Where)> candidates =
                (ZNetScene.instance != null ? ZNetScene.instance.m_prefabs : new List<GameObject>()).Where(p => p != null)
                    .SelectMany(p => Meshes(p).Select(x => (x.R, x.M, "prefab:" + p.name)))
                    .Concat(Resources.FindObjectsOfTypeAll<MeshFilter>().Select(f => ((Renderer)f.GetComponent<MeshRenderer>(), f.sharedMesh, "loaded")))
                    .Concat(Resources.FindObjectsOfTypeAll<SkinnedMeshRenderer>().Select(s => ((Renderer)s, s.sharedMesh, "loaded")));
            var seen = new List<string>();
            foreach ((Renderer r, Mesh m, string where) in candidates)
            {
                if (r == null || m == null || m.name.IndexOf("broom", System.StringComparison.OrdinalIgnoreCase) < 0 || Rejected.Contains(m.name))
                    continue;
                float longest = Mathf.Max(m.bounds.size.x, Mathf.Max(m.bounds.size.y, m.bounds.size.z)) * MaxScale(r.transform);
                if (seen.Count < 10)
                    seen.Add($"{m.name}@{where} {longest:0.00}m {m.vertexCount}v");
                if (longest < 0.8f || longest > 3f || m.vertexCount > 5000 || r.sharedMaterials.Length == 0)
                    continue;
                return (m, r.sharedMaterials, where);
            }
            VfhLog.I(LogCat.Hireling, "broom.search", ("found", seen.Count == 0 ? "no mesh named broom is loaded" : string.Join(" | ", seen)));
            return null;
        }

        private static IEnumerable<(Renderer R, Mesh M)> Meshes(GameObject go) =>
            go.GetComponentsInChildren<MeshFilter>(true).Select(f => ((Renderer)f.GetComponent<MeshRenderer>(), f.sharedMesh))
                .Concat(go.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(s => ((Renderer)s, s.sharedMesh)));

        private static (Mesh, Material[], string)? Fallback()
        {
            // The scene knows every item prefab too (the item database may not be up yet when this runs).
            GameObject? cultivator = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(FallbackPrefab) : null;
            if (cultivator == null && ObjectDB.instance != null)
                cultivator = ObjectDB.instance.GetItemPrefab(FallbackPrefab);
            if (cultivator == null)
                return null;
            (Renderer R, Mesh M) best = Meshes(cultivator).Where(x => x.R != null && x.M != null)
                .OrderByDescending(x => x.M.bounds.size.magnitude).FirstOrDefault();
            return best.R != null && best.M != null ? (best.M, best.R.sharedMaterials, "fallback:Cultivator") : null;
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

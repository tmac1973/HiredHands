using System.Collections.Generic;
using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using VikingsForHire.Config;
using VikingsForHire.Core.Data;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Board
{
    /// <summary>Builds VFH_HiringBoard from the vanilla sign: bigger, no sign text, a storage container and HiringBoard.</summary>
    internal static class BoardPiece
    {
        private const string BasePrefab = "sign";
        private const string ChestPrefab = "piece_chest_wood";
        private const int StorageWidth = 4;
        private const int StorageHeight = 2;
        private const string PolePrefab = "wood_pole2";
        private const float SignScale = 2f;
        private const float SignBottom = 1.0f;
        private const float PostSink = 0.1f;

        private static CustomPiece? _piece;

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += () => VfhLog.Guard(LogCat.Board, "board.register_failed", Create);
            DataStore.Changed += () => VfhLog.Guard(LogCat.Board, "board.cost_update_failed", UpdateCost);
        }

        private static void Create()
        {
            if (_piece != null)
                return;

            var config = new PieceConfig
            {
                Name = "$vfh_board",
                Description = "$vfh_board_desc",
                PieceTable = PieceTables.Hammer,
                Category = PieceCategories.Crafting,
                CraftingStation = CraftingStations.Workbench,
            };
            foreach (KeyValuePair<string, int> c in BuildCost())
                config.AddRequirement(c.Key, c.Value, true);

            _piece = new CustomPiece("VFH_HiringBoard", BasePrefab, config);
            GameObject prefab = _piece.PiecePrefab;

            // A notice board, not a sign: no editable text, a bigger board raised on two posts.
            Object.DestroyImmediate(prefab.GetComponent<Sign>());
            foreach (TMPro.TMP_Text text in prefab.GetComponentsInChildren<TMPro.TMP_Text>(true))
                Object.DestroyImmediate(text.gameObject);
            foreach (Canvas canvas in prefab.GetComponentsInChildren<Canvas>(true))
                Object.DestroyImmediate(canvas.gameObject);

            BuildModel(prefab);
            AddStorage(prefab);
            prefab.AddComponent<HiringBoard>();

            PieceManager.Instance.AddPiece(_piece);
            VfhLog.I(LogCat.Board, "board.registered", ("prefab", prefab.name), ("cost", CostText()));
        }

        /// <summary>
        /// Wraps the sign's meshes (including WearNTear's new/worn/broken variants, which keep working because they're the
        /// same objects), scales them to notice-board size and lifts them to head height, then stands a copy of the vanilla
        /// 2m wood pole's mesh at each edge, stretched to reach the top of the board. Sizes come from the meshes.
        /// </summary>
        private static void BuildModel(GameObject prefab)
        {
            var wrapper = new GameObject("VFH_SignVisual");
            wrapper.transform.SetParent(prefab.transform, false);
            foreach (Transform child in prefab.transform.Cast<Transform>().Where(t => t != wrapper.transform).ToList())
                child.SetParent(wrapper.transform, false);
            wrapper.transform.localScale = new Vector3(SignScale, SignScale, 1f);

            Bounds sign = LocalBounds(prefab.transform, wrapper.transform);
            wrapper.transform.localPosition += Vector3.up * (SignBottom - sign.min.y);
            sign = LocalBounds(prefab.transform, wrapper.transform);

            GameObject? pole = PrefabManager.Instance.GetPrefab(PolePrefab);
            GameObject? poleVisual = pole?.GetComponent<WearNTear>()?.m_new ?? pole;
            if (pole == null || poleVisual == null)
            {
                VfhLog.W(LogCat.Board, "board.pole_missing", ("prefab", PolePrefab));
                return;
            }

            Bounds poleBounds = LocalBounds(pole.transform, poleVisual.transform);
            float height = sign.max.y + PostSink;
            float stretch = height / Mathf.Max(0.01f, poleBounds.size.y);
            // Posts stand just outside the board's edges, overlapping it by a couple of centimetres so there's no gap.
            float offset = sign.extents.x + poleBounds.extents.x - 0.02f;
            foreach (int side in new[] { -1, 1 })
            {
                var post = new GameObject(side < 0 ? "VFH_PostLeft" : "VFH_PostRight");
                post.layer = poleVisual.layer;
                post.transform.SetParent(prefab.transform, false);
                post.transform.localScale = new Vector3(1f, stretch, 1f);
                post.transform.localPosition = new Vector3(
                    sign.center.x + side * offset - poleBounds.center.x,
                    -PostSink - poleBounds.min.y * stretch,
                    sign.center.z - poleBounds.center.z);

                GameObject mesh = Object.Instantiate(poleVisual, post.transform, false);
                mesh.name = "mesh";
                foreach (Component c in mesh.GetComponentsInChildren<Component>(true))
                {
                    if (c is not Transform && c is not MeshFilter && c is not MeshRenderer && c is not Collider)
                        Object.DestroyImmediate(c);
                }
                if (mesh.GetComponentInChildren<Collider>(true) == null)
                {
                    BoxCollider box = post.AddComponent<BoxCollider>();
                    box.center = poleBounds.center;
                    box.size = poleBounds.size;
                }
            }

            VfhLog.I(LogCat.Board, "board.model", ("signMin", sign.min), ("signMax", sign.max), ("poleSize", poleBounds.size),
                ("postHeight", height), ("stretch", stretch));
        }

        /// <summary>Bounds of every mesh under <paramref name="under"/>, in <paramref name="root"/>'s local space (works on inactive prefabs).</summary>
        private static Bounds LocalBounds(Transform root, Transform under)
        {
            bool any = false;
            var bounds = new Bounds();
            foreach (MeshFilter mf in under.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null)
                    continue;
                Bounds m = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = new Vector3((i & 1) == 0 ? m.min.x : m.max.x, (i & 2) == 0 ? m.min.y : m.max.y, (i & 4) == 0 ? m.min.z : m.max.z);
                    Vector3 p = root.InverseTransformPoint(mf.transform.TransformPoint(corner));
                    if (!any)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }
            return bounds;
        }

        /// <summary>The storage lives on a child with no collider, so hovering the board finds HiringBoard rather than the Container.</summary>
        private static void AddStorage(GameObject prefab)
        {
            var child = new GameObject("VFH_BoardStorage");
            child.transform.SetParent(prefab.transform, false);

            Container container = child.AddComponent<Container>();
            container.m_name = BoardStorage.InventoryName;
            container.m_width = StorageWidth;
            container.m_height = StorageHeight;
            container.m_privacy = Container.PrivacySetting.Public;
            container.m_checkGuardStone = true;
            container.m_rootObjectOverride = prefab.GetComponent<ZNetView>();

            Container? chest = PrefabManager.Instance.GetPrefab(ChestPrefab)?.GetComponent<Container>();
            if (chest != null)
            {
                container.m_bkg = chest.m_bkg;
                container.m_openEffects = chest.m_openEffects;
                container.m_closeEffects = chest.m_closeEffects;
            }
            else
            {
                VfhLog.W(LogCat.Board, "board.chest_template_missing", ("prefab", ChestPrefab));
            }
        }

        private static void UpdateCost()
        {
            if (_piece?.Piece == null)
                return;
            var requirements = new List<Piece.Requirement>();
            foreach (KeyValuePair<string, int> c in BuildCost())
            {
                ItemDrop? item = PrefabManager.Cache.GetPrefab<ItemDrop>(c.Key);
                if (item != null)
                    requirements.Add(new Piece.Requirement { m_resItem = item, m_amount = c.Value, m_recover = true });
            }
            _piece.Piece.m_resources = requirements.ToArray();
            VfhLog.I(LogCat.Board, "board.cost_updated", ("cost", CostText()));
        }

        private static Dictionary<string, int> BuildCost()
        {
            BoardLevelData? level1 = DataStore.Current.BoardLevels.FirstOrDefault(b => b.Level == 1);
            return level1?.Cost ?? new Dictionary<string, int>();
        }

        private static string CostText() => string.Join(" ", BuildCost().Select(c => $"{c.Key}x{c.Value}"));
    }
}

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

            // A notice board, not a sign: twice as wide and tall, and no editable text.
            prefab.transform.localScale = Vector3.Scale(prefab.transform.localScale, new Vector3(2f, 2f, 1f));
            Object.DestroyImmediate(prefab.GetComponent<Sign>());
            foreach (TMPro.TMP_Text text in prefab.GetComponentsInChildren<TMPro.TMP_Text>(true))
                Object.DestroyImmediate(text.gameObject);
            foreach (Canvas canvas in prefab.GetComponentsInChildren<Canvas>(true))
                Object.DestroyImmediate(canvas.gameObject);

            AddStorage(prefab);
            prefab.AddComponent<HiringBoard>();

            PieceManager.Instance.AddPiece(_piece);
            VfhLog.I(LogCat.Board, "board.registered", ("prefab", prefab.name), ("cost", CostText()));
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

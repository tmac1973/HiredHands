using System;
using System.Linq;
using HarmonyLib;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using VikingsForHire.Board;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using Object = UnityEngine.Object;

namespace VikingsForHire.Hirelings.Work.Trees
{
    /// <summary>
    /// The Tree patch piece: the vanilla sign's board on a single wooden stick, built with the hammer (Misc, Wood 2), only
    /// inside a hiring board's area (the woodcutters' work radius at the board's level).
    /// </summary>
    [HarmonyPatch]
    internal static class TreePatchPiece
    {
        public const string PrefabName = "VFH_TreePatch";
        private const string BasePrefab = "sign";
        private const string PolePrefab = "wood_pole2";
        private const float SignBottom = 0.9f;

        private static CustomPiece? _piece;

        public static void Register() =>
            PrefabManager.OnVanillaPrefabsAvailable += () => VfhLog.Guard(LogCat.Work, "patch.register_failed", Create);

        private static void Create()
        {
            if (_piece != null)
                return;
            var config = new PieceConfig
            {
                Name = "$vfh_patch",
                Description = "$vfh_patch_desc",
                PieceTable = PieceTables.Hammer,
                Category = PieceCategories.Misc,
            };
            config.AddRequirement("Wood", 2, true);
            _piece = new CustomPiece(PrefabName, BasePrefab, config);
            GameObject prefab = _piece.PiecePrefab;
            Object.DestroyImmediate(prefab.GetComponent<Sign>());
            foreach (TMPro.TMP_Text text in prefab.GetComponentsInChildren<TMPro.TMP_Text>(true))
                Object.DestroyImmediate(text.gameObject);
            foreach (Canvas canvas in prefab.GetComponentsInChildren<Canvas>(true))
                Object.DestroyImmediate(canvas.gameObject);
            Piece piece = prefab.GetComponent<Piece>();
            piece.m_groundPiece = true;
            piece.m_allowedInDungeons = false;
            // A sign on its own stick stands by itself: the wall sign it's made from would collapse without a wall.
            if (prefab.GetComponent<WearNTear>() is WearNTear wnt)
                wnt.m_noSupportWear = false;
            BuildModel(prefab);
            prefab.AddComponent<TreePatch>();
            PieceManager.Instance.AddPiece(_piece);
            VfhLog.I(LogCat.Work, "patch.registered", ("prefab", PrefabName));
        }

        // The sign's board raised to waist height on one stick (the 2 m pole's mesh, stretched to reach it).
        private static void BuildModel(GameObject prefab)
        {
            var wrapper = new GameObject("VFH_PatchSign");
            wrapper.transform.SetParent(prefab.transform, false);
            foreach (Transform child in prefab.transform.Cast<Transform>().Where(t => t != wrapper.transform).ToList())
                child.SetParent(wrapper.transform, false);
            Bounds sign = BoardPiece.LocalBounds(prefab.transform, wrapper.transform);
            wrapper.transform.localPosition += Vector3.up * (SignBottom - sign.min.y);
            sign = BoardPiece.LocalBounds(prefab.transform, wrapper.transform);

            GameObject? pole = PrefabManager.Instance.GetPrefab(PolePrefab);
            GameObject? poleVisual = pole?.GetComponent<WearNTear>()?.m_new ?? pole;
            if (pole == null || poleVisual == null)
                return;
            Bounds poleBounds = BoardPiece.LocalBounds(pole.transform, poleVisual.transform);
            float height = sign.min.y + 0.1f + 0.2f; // into the board a little, and a little into the ground
            float stretch = height / Mathf.Max(0.01f, poleBounds.size.y);
            var post = new GameObject("VFH_PatchPost");
            post.layer = poleVisual.layer;
            post.transform.SetParent(prefab.transform, false);
            post.transform.localScale = new Vector3(0.6f, stretch, 0.6f);
            post.transform.localPosition = new Vector3(sign.center.x - poleBounds.center.x * 0.6f, -0.2f - poleBounds.min.y * stretch, sign.center.z - poleBounds.center.z * 0.6f);
            GameObject mesh = Object.Instantiate(poleVisual, post.transform, false);
            mesh.name = "mesh";
            foreach (Component c in mesh.GetComponentsInChildren<Component>(true))
                if (c is not Transform && c is not MeshFilter && c is not MeshRenderer && c is not Collider)
                    Object.DestroyImmediate(c);
        }

        public static bool IsPatch(Piece? p) => p != null && Utils.GetPrefabName(p.gameObject) == PrefabName;

        /// <summary>Inside a hiring board's area: within the woodcutters' work radius at that board's level.</summary>
        public static bool Allowed(Vector3 pos)
        {
            var rules = new LevelRules(DataStore.Current);
            return HiringBoard.Loaded.Any(b => b != null && Utils.DistanceXZ(b.transform.position, pos) <= rules.MaxWorkRadius(b.Level, JobType.Woodcutter));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        private static void Ghost(Player __instance)
        {
            try
            {
                if (__instance != Player.m_localPlayer || __instance.m_placementGhost == null ||
                    __instance.m_placementStatus != Player.PlacementStatus.Valid || !IsPatch(__instance.GetSelectedPiece()))
                    return;
                if (Allowed(__instance.m_placementGhost.transform.position))
                    return;
                __instance.m_placementStatus = Player.PlacementStatus.Invalid;
                __instance.SetPlacementGhostValid(false);
            }
            catch (Exception e)
            {
                VfhLog.PatchFailed("TreePatchPiece.UpdatePlacementGhost", e);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
        private static bool Place(Player __instance, Piece piece, ref bool __result)
        {
            try
            {
                if (!IsPatch(piece) || __instance.m_placementGhost == null || Allowed(__instance.m_placementGhost.transform.position))
                    return true;
                __instance.Message(MessageHud.MessageType.Center, "$vfh_patch_outside");
                __result = false;
                return false;
            }
            catch (Exception e)
            {
                VfhLog.PatchFailed("TreePatchPiece.TryPlacePiece", e);
                return true;
            }
        }
    }
}

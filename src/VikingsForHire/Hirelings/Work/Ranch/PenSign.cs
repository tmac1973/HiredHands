using System;
using System.Collections.Generic;
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

namespace VikingsForHire.Hirelings.Work.Ranch
{
    /// <summary>
    /// The Pen sign: marks an animal pen (its circle, on the sign's level). Where a board has one, "in the pen" means inside
    /// a sign's circle instead of walled in all round: Tim's terrace pen has earth banks for walls and his basement has
    /// walls all round, and the wall check got both wrong. E on it widens the circle, Shift+E narrows it.
    /// </summary>
    internal sealed class PenSign : MonoBehaviour, Hoverable, Interactable
    {
        public const string RadiusKey = "vfh_pen_radius";
        public const float DefaultRadius = 8f, MinRadius = 3f, MaxRadius = 25f;
        // How far above or below the sign still counts as the pen's level (a terrace below the base isn't the base).
        private const float Level = 3f;
        private const float InteractRange = 5f;

        public static readonly List<PenSign> Loaded = new();

        private ZNetView? _nview;

        public ZDO? Zdo => _nview != null && _nview.IsValid() ? _nview.GetZDO() : null;
        public float Radius => Mathf.Clamp(Zdo?.GetFloat(RadiusKey, DefaultRadius) ?? DefaultRadius, MinRadius, MaxRadius);

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            Loaded.Add(this);
        }

        private void OnDestroy() => Loaded.Remove(this);

        public bool Contains(Vector3 p) => Utils.DistanceXZ(p, transform.position) <= Radius && Mathf.Abs(p.y - transform.position.y) <= Level;

        /// <summary>The pen signs near a spot (within 80 m: a board's area and a little more).</summary>
        public static IEnumerable<PenSign> Near(Vector3 p) => Loaded.Where(s => s != null && s.Zdo != null && Utils.DistanceXZ(s.transform.position, p) <= 80f);

        /// <summary>The sign whose pen holds the spot, if any.</summary>
        public static PenSign? At(Vector3 p) => Near(p).FirstOrDefault(s => s.Contains(p));

        private int _animals;
        private float _countedAt = -999f;

        private int Animals()
        {
            if (Time.time - _countedAt < 1f)
                return _animals;
            _countedAt = Time.time;
            _animals = Character.GetAllCharacters().Count(c => c != null && !c.IsDead() && c.IsTamed() && Hireling.Of(c) == null &&
                                                               AnimalCatalog.KindOf(Utils.GetPrefabName(c.gameObject)) != null && Contains(c.transform.position));
            return _animals;
        }

        public string GetHoverText()
        {
            ShowRing();
            Localization l = Localization.instance;
            return l.Localize("$vfh_pen") + "\n" + l.Localize("$vfh_pen_info", Radius.ToString("0"), Animals().ToString()) + "\n" +
                   l.Localize("[<color=yellow><b>$KEY_Use</b></color>] $vfh_pen_wider  [<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] $vfh_pen_narrower");
        }

        public string GetHoverName() => "$vfh_pen";

        public float GetHoverOffset() => 0f;

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || user != Player.m_localPlayer || Vector3.Distance(user.transform.position, transform.position) > InteractRange || Zdo == null)
                return false;
            if (!PrivateArea.CheckAccess(transform.position, 0f, flash: true))
                return true;
            if (!_nview!.IsOwner())
                _nview.ClaimOwnership();
            Zdo.Set(RadiusKey, Mathf.Clamp(Radius + (alt ? -1f : 1f), MinRadius, MaxRadius));
            ShowRing();
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        public void ShowRing() => UI.AreaRing.On(this, new Color(1f, 0.6f, 0.3f, 0.85f)).Show(Radius);
    }

    /// <summary>The Pen sign piece: the Tree patch sign's look, built with the hammer (Misc, Wood 2) inside a hiring board's area.</summary>
    [HarmonyPatch]
    internal static class PenSignPiece
    {
        public const string PrefabName = "VFH_PenSign";
        private static CustomPiece? _piece;

        public static void Register() =>
            PrefabManager.OnVanillaPrefabsAvailable += () => VfhLog.Guard(LogCat.Work, "pen.register_failed", Create);

        private static void Create()
        {
            if (_piece != null)
                return;
            var config = new PieceConfig
            {
                Name = "$vfh_pen",
                Description = "$vfh_pen_desc",
                PieceTable = PieceTables.Hammer,
                Category = PieceCategories.Misc,
            };
            config.AddRequirement("Wood", 2, true);
            _piece = new CustomPiece(PrefabName, "sign", config);
            GameObject prefab = _piece.PiecePrefab;
            Object.DestroyImmediate(prefab.GetComponent<Sign>());
            foreach (TMPro.TMP_Text text in prefab.GetComponentsInChildren<TMPro.TMP_Text>(true))
                Object.DestroyImmediate(text.gameObject);
            foreach (Canvas canvas in prefab.GetComponentsInChildren<Canvas>(true))
                Object.DestroyImmediate(canvas.gameObject);
            Piece piece = prefab.GetComponent<Piece>();
            piece.m_groundPiece = true;
            piece.m_allowedInDungeons = false;
            if (prefab.GetComponent<WearNTear>() is WearNTear wnt)
                wnt.m_noSupportWear = false;
            Trees.TreePatchPiece.BuildModel(prefab);
            prefab.AddComponent<PenSign>();
            PieceManager.Instance.AddPiece(_piece);
            VfhLog.I(LogCat.Work, "pen.registered", ("prefab", PrefabName));
        }

        private static bool IsPen(Piece? p) => p != null && Utils.GetPrefabName(p.gameObject) == PrefabName;

        private static bool Allowed(Vector3 pos)
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
                    __instance.m_placementStatus != Player.PlacementStatus.Valid || !IsPen(__instance.GetSelectedPiece()))
                    return;
                if (Allowed(__instance.m_placementGhost.transform.position))
                    return;
                __instance.m_placementStatus = Player.PlacementStatus.Invalid;
                __instance.SetPlacementGhostValid(false);
            }
            catch (Exception e)
            {
                VfhLog.PatchFailed("PenSignPiece.UpdatePlacementGhost", e);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
        private static bool Place(Player __instance, Piece piece, ref bool __result)
        {
            try
            {
                if (!IsPen(piece) || __instance.m_placementGhost == null || Allowed(__instance.m_placementGhost.transform.position))
                    return true;
                __instance.Message(MessageHud.MessageType.Center, "$vfh_pen_outside");
                __result = false;
                return false;
            }
            catch (Exception e)
            {
                VfhLog.PatchFailed("PenSignPiece.TryPlacePiece", e);
                return true;
            }
        }
    }
}

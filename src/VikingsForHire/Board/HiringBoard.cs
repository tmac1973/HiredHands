using System;
using System.Collections.Generic;
using System.Text;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Board
{
    /// <summary>
    /// The hiring board in the world: identity and level in its ZDO, hover/interact, and the level tint. Its storage is a
    /// Container on a child object (so this component, not the Container, is what the player hovers and interacts with).
    /// </summary>
    internal sealed class HiringBoard : MonoBehaviour, Hoverable, Interactable
    {
        private const float LevelPollSeconds = 2f;
        private static readonly Color LevelOneTint = Color.white;
        private static readonly Color LevelMaxTint = new(1f, 0.78f, 0.32f);

        /// <summary>Raised on every machine that has the board loaded when its level changes (seen by the 2s poll).</summary>
        public static event System.Action<HiringBoard, int>? LevelChanged;

        /// <summary>Boards loaded on this machine.</summary>
        public static readonly List<HiringBoard> Loaded = new();

        private ZNetView _nview = null!;
        private Container? _storage;
        private Renderer[] _renderers = Array.Empty<Renderer>();
        private MaterialPropertyBlock? _block;
        private int _shownLevel = -1;
        private float _nextPoll;

        public ZDO? Zdo => _nview != null && _nview.IsValid() ? _nview.GetZDO() : null;
        public string Id => Zdo != null ? BoardZdo.GetId(Zdo) : "";
        public int Level => Zdo != null ? BoardZdo.GetLevel(Zdo) : 1;
        public Container? Storage => _storage;
        public Inventory? Inventory => _storage != null ? _storage.GetInventory() : null;

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            // Placement ghosts have no ZDO: they only need to look like a board.
            if (_nview == null || _nview.GetZDO() == null)
            {
                enabled = false;
                return;
            }

            _storage = GetComponentInChildren<Container>(true);
            _renderers = GetComponentsInChildren<Renderer>(true);
            Loaded.Add(this);
            BoardUpgrade.RegisterRpcs(this, _nview);
            EnsureId();
            ApplyLevelVisual();
            VfhLog.D(LogCat.Board, "board.loaded", ("board", Id), ("level", Level), ("pos", transform.position),
                ("owner", _nview.GetZDO().GetOwner()));
        }

        private void OnDestroy()
        {
            if (Loaded.Remove(this))
                VfhLog.D(LogCat.Board, "board.unloaded", ("board", Id));
        }

        private void Update()
        {
            if (Time.time < _nextPoll)
                return;
            _nextPoll = Time.time + LevelPollSeconds;
            VfhLog.Guard(LogCat.Board, "board.tick_failed", () =>
            {
                EnsureId();
                if (Level != _shownLevel)
                    ApplyLevelVisual();
            }, ("board", Id));
        }

        private void EnsureId()
        {
            ZDO? zdo = Zdo;
            if (zdo == null || !_nview.IsOwner() || BoardZdo.GetId(zdo).Length > 0)
                return;
            string id = Guid.NewGuid().ToString("N");
            zdo.Set(BoardZdo.Id, id);
            zdo.Set(BoardZdo.Level, 1);
            VfhLog.I(LogCat.Board, "board.created", ("board", id), ("pos", transform.position), ("zdo", zdo.m_uid.ToString()));
        }

        /// <summary>Plain wood at level 1, warming toward gold at the top level.</summary>
        public void ApplyLevelVisual()
        {
            int level = Level;
            float t = Mathf.Clamp01((level - 1) / 7f);
            Color tint = Color.Lerp(LevelOneTint, LevelMaxTint, t);
            _block ??= new MaterialPropertyBlock();
            foreach (Renderer r in _renderers)
            {
                if (r == null)
                    continue;
                r.GetPropertyBlock(_block);
                _block.SetColor("_Color", tint);
                r.SetPropertyBlock(_block);
            }
            bool changed = _shownLevel != -1 && _shownLevel != level;
            if (changed)
                VfhLog.I(LogCat.Board, "board.level_visual", ("board", Id), ("from", _shownLevel), ("to", level));
            _shownLevel = level;
            if (changed)
                VfhLog.Guard(LogCat.Board, "board.level_changed_handler", () => LevelChanged?.Invoke(this, level), ("board", Id));
        }

        public string GetHoverName() => "$vfh_board";

        public float GetHoverOffset() => 0f;

        public string GetHoverText()
        {
            var sb = new StringBuilder();
            sb.Append("$vfh_board ($vfh_level ").Append(Level).Append(')');
            if (!PrivateArea.CheckAccess(transform.position, 0f, flash: false))
                return Localization.instance.Localize(sb.Append("\n$piece_noaccess").ToString());

            Inventory? inv = Inventory;
            Cost funds = inv != null ? BoardStorage.Totals(inv) : Cost.Zero;
            sb.Append("\n$vfh_board_funds ").Append(funds.FoodPoints).Append(" $vfh_food_points · ").Append(funds.Coins).Append(" $vfh_coins");
            sb.Append("\n[<color=yellow><b>$KEY_Use</b></color>] $vfh_board_open_storage");
            sb.Append("\n[<color=yellow><b>$KEY_AltPlace + $KEY_Use</b></color>] $vfh_board_manage");
            return Localization.instance.Localize(sb.ToString());
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || _storage == null)
                return false;
            if (alt)
            {
                UI.BoardPanel.Open(this);
                return true;
            }
            VfhLog.D(LogCat.Board, "storage.open", ("board", Id), ("player", Player.m_localPlayer?.GetPlayerName()));
            return _storage.Interact(user, hold, false);
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        /// <summary>The nearest loaded board within <paramref name="maxDistance"/> of a point.</summary>
        public static HiringBoard? Nearest(Vector3 point, float maxDistance)
        {
            HiringBoard? best = null;
            float bestSq = maxDistance * maxDistance;
            foreach (HiringBoard b in Loaded)
            {
                if (b == null)
                    continue;
                float d = (b.transform.position - point).sqrMagnitude;
                if (d <= bestSq)
                {
                    bestSq = d;
                    best = b;
                }
            }
            return best;
        }
    }
}

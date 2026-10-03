using System.Collections.Generic;
using System.Linq;
using System.Text;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Data;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// The hireling's identity and state, on every client. The ZDO is the source of truth: job, level, looks, home and
    /// mode live there and travel with snapshots. Gear and stats follow the ZDO, so a promotion re-dresses the hireling
    /// everywhere within a second.
    /// </summary>
    internal sealed class Hireling : MonoBehaviour, Hoverable, Interactable
    {
        private const float PollSeconds = 1f;
        private const float InteractRange = 4f;

        public static readonly List<Hireling> Loaded = new();
        private static readonly Dictionary<Inventory, Hireling> ByCargo = new();

        private ZNetView _nview = null!;
        private Humanoid _humanoid = null!;
        private HirelingAI _ai = null!;
        private VisEquipment _vis = null!;
        private Container? _cargo;
        private JobType _gearJob;
        private int _gearLevel = -1;
        private float _nextPoll;
        private float _nextBoardCheck;
        private const float BoardCheckSeconds = 60f;

        public ZDO? Zdo => _nview != null && _nview.IsValid() ? _nview.GetZDO() : null;
        public bool IsOwner => _nview != null && _nview.IsValid() && _nview.IsOwner();
        public Humanoid Humanoid => _humanoid;
        public HirelingAI Ai => _ai;
        public VisEquipment Visuals => _vis;
        public Container? Cargo => _cargo;
        public Inventory? CargoInventory => _cargo != null ? _cargo.GetInventory() : null;

        public string Hid => Zdo?.GetString(HirelingZdo.Hid) ?? "";
        public string BoardId => Zdo?.GetString(HirelingZdo.BoardId) ?? "";
        public JobType Job => (JobType)(Zdo?.GetInt(HirelingZdo.Job) ?? 0);
        public int Level => Mathf.Max(1, Zdo?.GetInt(HirelingZdo.Level, 1) ?? 1);
        public HirelingMode Mode => (HirelingMode)(Zdo?.GetInt(HirelingZdo.Mode, (int)HirelingMode.Idle) ?? (int)HirelingMode.Idle);
        public string DisplayName => Zdo?.GetString(HirelingZdo.Name) ?? "";
        public float Radius => Zdo?.GetFloat(HirelingZdo.Radius, 20f) ?? 20f;
        public Vector3 Home => Zdo?.GetVec3(HirelingZdo.Home, transform.position) ?? transform.position;

        public HirelingLevelData LevelData =>
            DataStore.Current.HirelingLevels.FirstOrDefault(l => l.Level == Level) ?? DataStore.Current.HirelingLevels[0];

        /// <summary>Someone has this hireling's cargo open (vanilla syncs the flag through the ZDO).</summary>
        public bool CargoInUse => _cargo != null && (_cargo.IsInUse() || (Zdo != null && Zdo.GetInt(ZDOVars.s_inUse) == 1));

        public int CargoSlots => Mathf.Clamp(LevelData.CargoSlots, 1, HirelingPrefab.CargoWidth * HirelingPrefab.CargoHeight);

        public static Hireling? Of(Component? c) => c == null ? null : c.GetComponent<Hireling>();

        public static Hireling? ForCargo(Inventory? inventory) =>
            inventory != null && ByCargo.TryGetValue(inventory, out Hireling h) && h != null ? h : null;

        private void Awake()
        {
            _nview = GetComponent<ZNetView>();
            if (_nview == null || _nview.GetZDO() == null)
            {
                enabled = false;
                return;
            }
            _humanoid = GetComponent<Humanoid>();
            _ai = GetComponent<HirelingAI>();
            _ai.Init(this);
            Net.MutationService.RegisterApply(_nview);
            _humanoid.m_onDeath += OnDeath;
            _vis = GetComponent<VisEquipment>();
            _cargo = GetComponentInChildren<Container>(true);

            // Runs before Humanoid.Start, which equips m_defaultItems.
            _gearJob = Job;
            _gearLevel = Level;
            GearApplier.Prepare(_humanoid, _gearJob, _gearLevel);
            Loaded.Add(this);
        }

        private void Start()
        {
            if (Zdo == null)
                return;
            if (CargoInventory != null)
                ByCargo[CargoInventory] = this;
            // Humanoid.Start has equipped the default gear by now; the sidearm is carried, not worn.
            GearApplier.AddSidearm(_humanoid, Job, Level);

            // The name shown on the health bar (EnemyHud) and anywhere else that reads Character.m_name.
            if (DisplayName.Length > 0)
                _humanoid.m_name = DisplayName;

            if (IsOwner)
            {
                Appearance.Apply(this);
                ApplyLevelStats();
                if (!Zdo.GetBool(HirelingZdo.Initialized))
                {
                    _humanoid.SetHealth(_humanoid.GetMaxHealth());
                    Zdo.Set(HirelingZdo.Initialized, true);
                }
            }
            VfhLog.D(LogCat.Hireling, "hireling.loaded", ("hid", Hid), ("board", BoardId), ("name", DisplayName), ("job", Job), ("level", Level),
                ("owner", Zdo.GetOwner()), ("gear", GearApplier.Describe(_humanoid)), ("health", _humanoid.GetHealth()), ("max", _humanoid.GetMaxHealth()));
        }

        private void OnDestroy()
        {
            Loaded.Remove(this);
            if (CargoInventory != null)
                ByCargo.Remove(CargoInventory);
        }

        private void Update()
        {
            if (Time.time < _nextPoll || Zdo == null)
                return;
            _nextPoll = Time.time + PollSeconds;
            VfhLog.Guard(LogCat.Hireling, "hireling.tick_failed", () =>
            {
                if (Job != _gearJob || Level != _gearLevel)
                {
                    VfhLog.I(LogCat.Hireling, "hireling.regear", ("hid", Hid), ("job", Job), ("level", Level), ("fromLevel", _gearLevel));
                    _gearJob = Job;
                    _gearLevel = Level;
                    GearApplier.Regear(_humanoid, _gearJob, _gearLevel);
                    if (IsOwner)
                        ApplyLevelStats();
                }
                if (IsOwner && Job == JobType.GuardRanged)
                    GearApplier.RefillAmmo(_humanoid);
                if (IsOwner && BoardId.Length > 0 && Mode != HirelingMode.Leaving && Time.time >= _nextBoardCheck)
                {
                    _nextBoardCheck = Time.time + BoardCheckSeconds;
                    Net.BoardServer.CheckBoardExists(BoardId, exists =>
                    {
                        if (exists || this == null || Zdo == null || Mode == HirelingMode.Leaving)
                            return;
                        VfhLog.I(LogCat.Hireling, "hireling.board_gone", ("hid", Hid), ("board", BoardId));
                        Net.MutationService.SubmitHireling(Hid, new HirelingOp { Mode = HirelingMode.Leaving, LeavingSince = (long)ZNet.instance.GetTimeSeconds(), Status = "$vfh_status_board_gone" });
                    });
                }
            }, ("hid", Hid));
        }

        /// <summary>
        /// Owner, as it dies: cargo drops where it fell (gear never does), and the board learns of the death, which
        /// either ends the contract (permadeath) or schedules the return.
        /// </summary>
        private void OnDeath()
        {
            if (!IsOwner)
                return;
            VfhLog.Guard(LogCat.Hireling, "hireling.death_failed", () =>
            {
                int dropped = CargoInventory != null ? Work.DropPile.DropAll(CargoInventory, transform.position + Vector3.up * 0.5f, "died", Hid) : 0;
                VfhLog.I(LogCat.Hireling, "hireling.died", ("hid", Hid), ("board", BoardId), ("name", DisplayName), ("job", Job), ("level", Level), ("cargoStacks", dropped));
                if (BoardId.Length > 0 && Mode != HirelingMode.Leaving)
                    Net.MutationService.SubmitBoard(BoardId, new RosterOp { Type = RosterOpType.MarkDied, Hid = Hid, Name = DisplayName });
            }, ("hid", Hid));
        }

        /// <summary>Owner: max health from the level table (current health is kept, capped to the new max).</summary>
        public void ApplyLevelStats()
        {
            float max = LevelData.Health;
            if (Mathf.Abs(_humanoid.GetMaxHealth() - max) > 0.01f)
            {
                _humanoid.SetMaxHealth(max);
                VfhLog.D(LogCat.Hireling, "hireling.stats", ("hid", Hid), ("level", Level), ("maxHealth", max), ("armor", LevelData.Armor));
            }
        }

        public string GetHoverName() => DisplayName;

        public float GetHoverOffset() => 0f;

        public string GetHoverText()
        {
            var sb = new StringBuilder();
            sb.Append(DisplayName).Append(" — $vfh_job_").Append(Job.ToString().ToLowerInvariant())
                .Append(" ($vfh_level ").Append(Level).Append(')');
            string status = Zdo?.GetString(HirelingZdo.Status) ?? "";
            if (status.Length == 0)
                status = Mode switch
                {
                    HirelingMode.Working => "$vfh_status_working",
                    HirelingMode.Leaving => "$vfh_status_leaving",
                    _ => "",
                };
            if (status.Length > 0)
                sb.Append('\n').Append(status);
            sb.Append("\n$vfh_health ").Append(Mathf.CeilToInt(_humanoid.GetHealth())).Append('/').Append(Mathf.CeilToInt(_humanoid.GetMaxHealth()));
            Inventory? cargo = CargoInventory;
            if (cargo != null)
                sb.Append("\n$vfh_cargo ").Append(cargo.NrOfItems()).Append('/').Append(CargoSlots);
            sb.Append("\n[<color=yellow><b>$KEY_Use</b></color>] $vfh_open_cargo");
            return Localization.instance.Localize(sb.ToString());
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || _cargo == null)
                return false;
            if (Vector3.Distance(user.transform.position, transform.position) > InteractRange)
                return false;
            // Base workers: whoever may use the base. Followers (phase 13) will add an owner check.
            if (!PrivateArea.CheckAccess(transform.position))
                return true;
            VfhLog.D(LogCat.Hireling, "cargo.open", ("hid", Hid), ("by", Player.m_localPlayer?.GetPlayerName()));
            return _cargo.Interact(user, false, false);
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        /// <summary>
        /// Character is Hoverable too and sits earlier on the object, so the hover raycast finds it first. Hand its hover
        /// text and name to the Hireling for hirelings.
        /// </summary>
        [HarmonyLib.HarmonyPatch(typeof(Character), nameof(Character.GetHoverText))]
        private static class CharacterHoverTextPatch
        {
            private static void Postfix(Character __instance, ref string __result)
            {
                Hireling? h = Of(__instance);
                if (h != null && h.enabled)
                    __result = h.GetHoverText();
            }
        }

        [HarmonyLib.HarmonyPatch(typeof(Character), nameof(Character.GetHoverName))]
        private static class CharacterHoverNamePatch
        {
            private static void Postfix(Character __instance, ref string __result)
            {
                Hireling? h = Of(__instance);
                if (h != null && h.enabled && h.DisplayName.Length > 0)
                    __result = h.DisplayName;
            }
        }

        public static Hireling? Nearest(Vector3 point, float range) =>
            Loaded.Where(h => h != null && h.Zdo != null && Vector3.Distance(h.transform.position, point) <= range)
                .OrderBy(h => Vector3.Distance(h.transform.position, point)).FirstOrDefault();
    }
}

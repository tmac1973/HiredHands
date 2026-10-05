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
        public bool HasPost => Zdo?.GetBool(HirelingZdo.Posted) ?? false;
        public bool IsParked => Zdo?.GetBool(HirelingZdo.Parked) ?? false;
        /// <summary>The ship it's aboard as a passenger (its ZDOID as "user:id"), or empty.</summary>
        public string StowedOn => Zdo?.GetString(HirelingZdo.Stowed) ?? "";
        public bool IsStowed => StowedOn.Length > 0;

        /// <summary>Crouched and sneaking with its owner (owner side; others see the crouch through the animator).</summary>
        public bool Sneaking { get; private set; }

        /// <summary>The hirelings sneaking right now, for the crouch and stealth hooks (characters aren't hirelings by type).</summary>
        public static readonly HashSet<Character> SneakingNow = new();

        private static readonly int CrouchingAnim = ZSyncAnimation.GetHash("crouching");

        public void SetSneaking(bool on)
        {
            if (on == Sneaking)
                return;
            Sneaking = on;
            if (on)
                SneakingNow.Add(_humanoid);
            else
                SneakingNow.Remove(_humanoid);
            _humanoid.m_zanim?.SetBool(CrouchingAnim, on);
            VfhLog.T(LogCat.Follow, "follow.sneak", ("hid", Hid), ("on", on));
        }

        /// <summary>Gatherers: items switched off in the Shift+E panel (parsed once per change of the ZDO value).</summary>
        public HashSet<string> SkipItems
        {
            get
            {
                string raw = Zdo?.GetString(HirelingZdo.SkipItems) ?? "";
                if (raw != _skipRaw)
                {
                    _skipRaw = raw;
                    _skip = GatherRules.ParseSkip(raw);
                }
                return _skip;
            }
        }
        private string _skipRaw = "";
        private HashSet<string> _skip = new();

        /// <summary>Gatherers: whether it harvests inside its board's area (off for decorative bases).</summary>
        public bool WorksAtHome => !(Zdo?.GetBool(HirelingZdo.NoHomeWork) ?? false);
        public Vector3 PostPos => Zdo?.GetVec3(HirelingZdo.Post, transform.position) ?? transform.position;
        public float PostYaw => Zdo?.GetFloat(HirelingZdo.PostYaw) ?? 0f;

        public long OwnerId => Zdo?.GetLong(HirelingZdo.Owner) ?? 0L;
        public string OwnerName => Zdo?.GetString(HirelingZdo.OwnerName) ?? "";
        public FollowMode FollowMode => (FollowMode)(Zdo?.GetInt(HirelingZdo.FollowMode) ?? 0);
        public Vector3 StayPos => Zdo?.GetVec3(HirelingZdo.StayPos, transform.position) ?? transform.position;

        public HirelingMode Mode => (HirelingMode)(Zdo?.GetInt(HirelingZdo.Mode, (int)HirelingMode.Idle) ?? (int)HirelingMode.Idle);
        public string DisplayName => Zdo?.GetString(HirelingZdo.Name) ?? "";
        // Work state (owner side).
        public bool NoTargets { get; set; }
        public float CarryingSince { get; private set; }

        /// <summary>Smelter: when it started holding ore/fuel that no station needed (0 = not idle with leftovers).</summary>
        public float LeftoverSince { get; set; }

        /// <summary>Set while a Steward collects from several producers in one trip: deliver once at the end, not after each.</summary>
        public bool HoldDeliveries { get; set; }

        /// <summary>
        /// Armor applied to incoming hits: the real armor of the worn set (as a player in that gear would have) plus the
        /// level's bonus. Vanilla only applies body armor to players, so DamagePatches applies this itself.
        /// </summary>
        public float Armor
        {
            get
            {
                Humanoid h = _humanoid;
                float gear = (h.m_chestItem?.GetArmor() ?? 0f) + (h.m_legItem?.GetArmor() ?? 0f) +
                             (h.m_helmetItem?.GetArmor() ?? 0f) + (h.m_shoulderItem?.GetArmor() ?? 0f);
                return gear + LevelData.ArmorBonus;
            }
        }

        /// <summary>The last thing that damaged this hireling, for the death log.</summary>
        public string LastHitBy { get; set; } = "";
        public float LastHitDamage { get; set; }
        public bool DeliverPending => Zdo?.GetBool(HirelingZdo.DeliverPending) ?? false;
        public ItemDrop.ItemData? Tool => _humanoid.GetRightItem();
        public int ToolTier => Tool?.m_shared.m_toolTier ?? 0;
        public HitData.DamageTypes ToolDamage => Tool?.GetDamage() ?? new HitData.DamageTypes();

        /// <summary>Full: every usable slot holds a full stack, or there's no weight left for even one more heavy item.</summary>
        public bool CargoFull
        {
            get
            {
                Inventory? cargo = CargoInventory;
                if (cargo == null)
                    return false;
                if (CargoWeightLimit > 0f && cargo.GetTotalWeight() > CargoWeightLimit - FullWeightMargin)
                    return true;
                return cargo.NrOfItems() >= CargoSlots && cargo.GetAllItems().All(i => i.m_stack >= i.m_shared.m_maxStackSize);
            }
        }

        // About one piece of ore: less room than this counts as full, so a gatherer goes to deliver rather than
        // trying (and failing) to pick up one more heavy item.
        private const float FullWeightMargin = 12f;

        /// <summary>The most its cargo may weigh (0: no limit, only slots).</summary>
        public float CargoWeightLimit => Mathf.Max(0f, LevelData.CargoWeight);

        /// <summary>How much more weight its cargo takes (float.MaxValue with no limit).</summary>
        public float CargoWeightRoom => CargoWeightLimit <= 0f ? float.MaxValue
            : Mathf.Max(0f, CargoWeightLimit - (CargoInventory?.GetTotalWeight() ?? 0f));

        private string _activity = "";

        /// <summary>Owner: what it's doing, for the hover (written only when it changes).</summary>
        public void SetActivity(string token)
        {
            if (token == _activity || !IsOwner || Zdo == null)
                return;
            _activity = token;
            Zdo.Set(HirelingZdo.Activity, token);
        }

        public void OnPickedUp()
        {
            if (CarryingSince <= 0f)
                CarryingSince = Time.time;
        }

        public void OnDelivered()
        {
            CarryingSince = 0f;
            LeftoverSince = 0f;
            NoTargets = false;
            if (IsOwner && Zdo != null && Zdo.GetBool(HirelingZdo.DeliverPending))
                Zdo.Set(HirelingZdo.DeliverPending, false);
            SetActivity("");
        }

        public Core.Stance Stance => (Core.Stance)(Zdo?.GetInt(HirelingZdo.Stance) ?? 0);

        private bool _sidearmOut;

        /// <summary>Archers switch between bow and club; equipping is local on every machine, visuals sync from the owner.</summary>
        public void UseSidearm(bool on)
        {
            if (Job != JobType.GuardRanged || on == _sidearmOut)
                return;
            ItemDrop.ItemData? club = GearApplier.Sidearm(_humanoid, Job, Level);
            ItemDrop.ItemData? bow = _humanoid.GetInventory().GetAllItems().FirstOrDefault(i => i.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow);
            if (club == null || bow == null)
                return;
            _sidearmOut = on;
            if (on)
            {
                _humanoid.UnequipItem(bow, false);
                _humanoid.EquipItem(club, false);
            }
            else
            {
                _humanoid.UnequipItem(club, false);
                _humanoid.EquipItem(bow, false);
            }
            VfhLog.D(LogCat.Combat, "weapon.swap", ("hid", Hid), ("to", on ? GearApplier.Name(club) : GearApplier.Name(bow)));
        }

        /// <summary>
        /// Owner: a hireling always has its weapon or tool in hand (the bow for an archer unless the club is out; the axe
        /// or pickaxe for a gatherer). If it's ever found empty-handed (the item can show in its hand while the game
        /// counts the hand as empty: punching, and gatherer strikes at bare-hand tier that never break ore), the item
        /// goes back in its hand and what was there is logged.
        /// </summary>
        private void EnsureArmed()
        {
            // The weapon the game would use: right hand, else a bow in the left hand (bows are held left).
            ItemDrop.ItemData? current = _humanoid.GetCurrentWeapon();
            bool unarmed = current == null || _humanoid.m_unarmedWeapon != null && current == _humanoid.m_unarmedWeapon.m_itemData;
            bool ok = Job == JobType.GuardRanged && !_sidearmOut
                ? !unarmed && current!.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow
                : !unarmed;
            if (ok || _humanoid.InAttack())
                return;
            ItemDrop.ItemData? weapon = Job == JobType.GuardRanged && !_sidearmOut
                ? _humanoid.GetInventory().GetAllItems().FirstOrDefault(i => i.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Bow)
                : _humanoid.GetInventory().GetAllItems().FirstOrDefault(i => i.IsWeapon() && i.m_shared.m_itemType != ItemDrop.ItemData.ItemType.Bow);
            if (weapon == null && !Job.IsGuard())
                return; // a job without a tool (smelters) is fine empty-handed
            string before = GearApplier.Describe(_humanoid);
            bool equipped = weapon != null && _humanoid.EquipItem(weapon, false);
            if (!equipped)
                GearApplier.Regear(_humanoid, Job, Level);
            VfhLog.W(LogCat.Combat, "weapon.restored", ("hid", Hid), ("had", unarmed ? "nothing" : GearApplier.Name(current)), ("gearBefore", before),
                ("now", GearApplier.Describe(_humanoid)), ("sidearmOut", _sidearmOut), ("weaponDurability", weapon?.m_durability ?? -1f),
                ("how", equipped ? "re-equipped" : "regeared"));
        }

        private static readonly int AnimStateHash = Animator.StringToHash("statei");

        /// <summary>
        /// Owner: the animator's weapon state ("statei": unarmed, one-handed, bow, pickaxe…) matches what's in hand.
        /// The game only sets it when the gear changes, so after the hireling's object is rebuilt (dungeon trips) or
        /// changes hands between machines it could stay at "unarmed": pickaxe in hand, punching animation, and swings
        /// that never connect.
        /// </summary>
        private void EnsureWeaponAnimation()
        {
            Animator? anim = _humanoid.m_animator;
            if (anim == null)
                return;
            ItemDrop.ItemData? left = _humanoid.GetLeftItem(), right = _humanoid.GetRightItem();
            ItemDrop.ItemData.AnimationState want = left != null
                ? left.m_shared.m_itemType == ItemDrop.ItemData.ItemType.Torch ? ItemDrop.ItemData.AnimationState.LeftTorch : left.m_shared.m_animationState
                : right != null ? right.m_shared.m_animationState
                : _humanoid.m_unarmedWeapon != null ? _humanoid.m_unarmedWeapon.m_itemData.m_shared.m_animationState
                : ItemDrop.ItemData.AnimationState.Unarmed;
            int have = anim.GetInteger(AnimStateHash);
            if (have == (int)want)
                return;
            _humanoid.SetupAnimationState();
            VfhLog.D(LogCat.Hireling, "hireling.anim_state_fixed", ("hid", Hid), ("had", (ItemDrop.ItemData.AnimationState)have), ("now", want));
        }

        public float Radius => Zdo?.GetFloat(HirelingZdo.Radius, 20f) ?? 20f;
        public Vector3 Home => Zdo?.GetVec3(HirelingZdo.Home, transform.position) ?? transform.position;

        public HirelingLevelData LevelData =>
            DataStore.Current.HirelingLevels.FirstOrDefault(l => l.Level == Level) ?? DataStore.Current.HirelingLevels[0];

        private const string PanelHoldRpc = "VFH_PanelHold";
        private const float PanelHoldSeconds = 3f;
        private float _panelHoldUntil;

        /// <summary>Someone has this hireling's Shift+E panel open (renewed every second by their game, so it lapses on its own).</summary>
        public bool PanelHeld => Time.time < _panelHoldUntil;

        /// <summary>From the game with the panel open: hold still (or carry on) on whichever game runs this hireling.</summary>
        public void HoldForPanel(bool hold)
        {
            if (_nview != null && _nview.IsValid())
                _nview.InvokeRPC(_nview.GetZDO().GetOwner(), PanelHoldRpc, hold);
        }

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
            _nview.Register<bool>(PanelHoldRpc, (_, hold) => _panelHoldUntil = hold ? Time.time + PanelHoldSeconds : 0f);
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
            ApplyCargoRows();
            PlayerCollision.IgnoreAllPlayers(this);

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
            SneakingNow.Remove(_humanoid);
            if (CargoInventory != null)
                ByCargo.Remove(CargoInventory);
        }

        private void Update()
        {
            if (Zdo != null && Time.time >= _nextHideCheck)
            {
                _nextHideCheck = Time.time + 0.25f;
                bool hide = IsStowed || Mode == HirelingMode.Returning;
                if (hide != _hidden)
                    SetHidden(hide);
            }
            if (Time.time < _nextPoll || Zdo == null)
                return;
            _nextPoll = Time.time + PollSeconds;
            // A rename reaches everyone through the ZDO; the health bar and hover read Character.m_name.
            if (DisplayName.Length > 0 && _humanoid.m_name != DisplayName)
                _humanoid.m_name = DisplayName;
            VfhLog.Guard(LogCat.Hireling, "hireling.tick_failed", () =>
            {
                if (Job != _gearJob || Level != _gearLevel)
                {
                    VfhLog.I(LogCat.Hireling, "hireling.regear", ("hid", Hid), ("job", Job), ("level", Level), ("fromLevel", _gearLevel));
                    _gearJob = Job;
                    _gearLevel = Level;
                    GearApplier.Regear(_humanoid, _gearJob, _gearLevel);
                    ApplyCargoRows();
                    if (IsOwner)
                        ApplyLevelStats();
                }
                if (IsOwner && Job == JobType.GuardRanged)
                    GearApplier.RefillAmmo(_humanoid);
                if (IsOwner && !_hidden)
                {
                    EnsureArmed();
                    EnsureWeaponAnimation();
                }
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

        private bool _hidden;
        private float _nextHideCheck;
        private readonly List<Renderer> _hiddenRenderers = new();
        private readonly List<Collider> _hiddenColliders = new();

        // A passenger is out of sight and out of reach on every client (read from the ZDO, so everyone agrees): its
        // renderers and colliders are switched off, and back on when it steps ashore.
        private void SetHidden(bool hide)
        {
            _hidden = hide;
            // The floating name and health bar too: no new one while hidden, and the one already up goes now (it would
            // otherwise hang over the mast until its hover timer ran out).
            _humanoid.m_hideHud = hide;
            if (hide && EnemyHud.instance != null && EnemyHud.instance.m_huds.TryGetValue(_humanoid, out EnemyHud.HudData hud))
            {
                Destroy(hud.m_gui);
                EnemyHud.instance.m_huds.Remove(_humanoid);
            }
            if (hide)
            {
                _hiddenRenderers.Clear();
                _hiddenColliders.Clear();
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                {
                    if (r.enabled)
                    {
                        r.enabled = false;
                        _hiddenRenderers.Add(r);
                    }
                }
                foreach (Collider c in GetComponentsInChildren<Collider>(true))
                {
                    if (c.enabled)
                    {
                        c.enabled = false;
                        _hiddenColliders.Add(c);
                    }
                }
            }
            else
            {
                foreach (Renderer r in _hiddenRenderers)
                    if (r != null)
                        r.enabled = true;
                foreach (Collider c in _hiddenColliders)
                    if (c != null)
                        c.enabled = true;
                _hiddenRenderers.Clear();
                _hiddenColliders.Clear();
            }
            VfhLog.D(LogCat.Follow, hide ? "follow.hidden" : "follow.shown", ("hid", Hid), ("ship", StowedOn));
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
                // Into a grave named after it (HirelingTombstones), or a loose pile.
                int dropped = CargoInventory == null ? 0 : Graves.Bury(this, CargoInventory, transform.position) is int buried && buried >= 0
                    ? buried : Work.DropPile.DropAll(CargoInventory, transform.position + Vector3.up * 0.5f, "died", Hid);
                Telemetry.BalanceFights.Died(this, LastHitBy, LastHitDamage);
                VfhLog.I(LogCat.Hireling, "hireling.died", ("hid", Hid), ("board", BoardId), ("name", DisplayName), ("job", Job), ("level", Level), ("cargoStacks", dropped),
                    ("lastHitBy", LastHitBy), ("lastHitDamage", LastHitDamage));
                if (BoardId.Length > 0 && Mode != HirelingMode.Leaving)
                {
                    Net.MutationService.SubmitBoard(BoardId, new RosterOp { Type = RosterOpType.MarkDied, Hid = Hid, Name = DisplayName });
                    AnnounceDeath();
                }
            }, ("hid", Hid));
        }

        // Said here, on the machine simulating the hireling (where it died), not by the board: the board's change may be
        // applied on a dedicated server with nobody to show it to, and a follower dies far from its board. Followers run
        // on their owner's machine, so the owner always hears; at the base, whoever is within 50 m does.
        private void AnnounceDeath()
        {
            Player me = Player.m_localPlayer;
            if (me == null)
                return;
            bool mine = Mode == HirelingMode.Following && OwnerId == me.GetPlayerID();
            if (!mine && Vector3.Distance(me.transform.position, transform.position) > 50f)
                return;
            string text = $"{DisplayName} {(Config.VfhConfig.PermadeathEnabled.Value ? "$vfh_msg_died" : "$vfh_msg_died_respawn")}";
            me.Message(MessageHud.MessageType.Center, Localization.instance.Localize(text));
            if (mine)
                me.Message(MessageHud.MessageType.TopLeft, Localization.instance.Localize(text));
        }

        /// <summary>
        /// Show only the cargo rows this level can use (8 slots per row); the unusable part of a partly-used last row is
        /// greyed by CargoGate. Vanilla still grows the grid to fit any items already in it, so nothing is ever hidden.
        /// </summary>
        private void ApplyCargoRows()
        {
            if (_cargo == null || CargoInventory == null)
                return;
            int rows = Mathf.Clamp(Mathf.CeilToInt(CargoSlots / (float)HirelingPrefab.CargoWidth), 1, HirelingPrefab.CargoHeight);
            _cargo.m_height = rows;
            _cargo.UpdateRows();
        }

        /// <summary>Owner: max health from the level table (current health is kept, capped to the new max).</summary>
        public void ApplyLevelStats()
        {
            float max = LevelData.Health;
            if (Mathf.Abs(_humanoid.GetMaxHealth() - max) > 0.01f)
            {
                _humanoid.SetMaxHealth(max);
                VfhLog.D(LogCat.Hireling, "hireling.stats", ("hid", Hid), ("level", Level), ("maxHealth", max), ("armor", Armor));
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
            // Steward status lines carry arguments ("token|arg|arg"): expand them here, in this game's language.
            string activity = Work.Steward.ActivityText.Show(Zdo?.GetString(HirelingZdo.Activity) ?? "");
            if (Mode == HirelingMode.Following)
            {
                // "Following Tim (Stay)", plus what it's doing when that says more (chopping, cargo full…).
                sb.Append('\n').Append(Localization.instance.Localize("$vfh_roster_following", OwnerName))
                    .Append(" ($vfh_mode_").Append(FollowMode.ToString().ToLowerInvariant()).Append(')');
                if (activity.Length > 0 && activity != "$vfh_status_following" && activity != "$vfh_status_staying")
                    sb.Append(" — ").Append(activity);
            }
            else
            {
                if (HasPost)
                    sb.Append(" — $vfh_roster_posted");
                if (activity.Length > 0 && Mode != HirelingMode.Leaving)
                    sb.Append(" — ").Append(activity);
            }
            sb.Append("\n$vfh_stance: $vfh_stance_").Append(Stance.ToString().ToLowerInvariant());
            sb.Append("\n$vfh_health ").Append(Mathf.CeilToInt(_humanoid.GetHealth())).Append('/').Append(Mathf.CeilToInt(_humanoid.GetMaxHealth()));
            Inventory? cargo = CargoInventory;
            if (cargo != null)
            {
                sb.Append("\n$vfh_cargo ").Append(cargo.NrOfItems()).Append('/').Append(CargoSlots);
                if (CargoWeightLimit > 0f)
                    sb.Append("  $vfh_weight ").Append(Mathf.CeilToInt(cargo.GetTotalWeight())).Append('/').Append(Mathf.RoundToInt(CargoWeightLimit));
            }
            sb.Append("\n[<color=yellow><b>$KEY_Use</b></color>] $vfh_open_cargo   [<color=yellow><b>Shift + $KEY_Use</b></color>] $vfh_orders");
            return Localization.instance.Localize(sb.ToString());
        }

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || _cargo == null)
                return false;
            if (Vector3.Distance(user.transform.position, transform.position) > InteractRange)
                return false;
            if (alt)
            {
                UI.HirelingPanel.Open(this);
                return true;
            }
            // A follower's cargo is its owner's; base workers' is for whoever may use the base.
            if (Mode == HirelingMode.Following)
            {
                if (user is Player p && p.GetPlayerID() != OwnerId)
                {
                    p.Message(MessageHud.MessageType.Center, "$vfh_not_your_follower");
                    return true;
                }
            }
            else if (!PrivateArea.CheckAccess(transform.position))
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

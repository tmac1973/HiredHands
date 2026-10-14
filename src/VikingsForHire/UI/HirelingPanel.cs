using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using VikingsForHire.Board;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Chores;
using VikingsForHire.Core.Data;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Followers;
using VikingsForHire.Hirelings;
using VikingsForHire.Hirelings.Work;
using VikingsForHire.Net;
using UnityEngine;
using UnityEngine.UI;

namespace VikingsForHire.UI
{
    /// <summary>
    /// Shift+E on a hireling: its orders. Followers: follow mode (Follow / Stay / Gather Here). Everyone you may give
    /// orders to: stance (saved on its contract). Release a follower at home, send one home from the field, or clear a
    /// posted guard's post. With
    /// "Apply to all my followers nearby" a change goes to every follower of yours within 30 m. Rename opens the vanilla
    /// text box. Gatherers also get what to gather and whether to work at home.
    /// </summary>
    internal sealed class HirelingPanel : MonoBehaviour
    {
        private const float OpenRange = 6f;
        private const float CloseRange = 8f;
        private const float GroupRange = 30f;

        private static HirelingPanel? _instance;
        private Hireling? _hireling;
        private float _nextHold;
        private RectTransform _content = null!;
        private bool _all;
        private bool _limits;
        private int _limitsPage;
        private bool _orders;
        private bool _ordersPicking;
        private int _ordersPage;
        private bool _why;
        private int _whyPage;
        private bool _enh;
        private int _pendingLevel;
        private char _pendingChoice;
        private string _shown = "";
        private float _nextRefresh;

        public static bool IsOpen => _instance != null && _instance.gameObject.activeSelf;

        public static void Open(Hireling h)
        {
            Player me = Player.m_localPlayer;
            if (me == null || GUIManager.IsHeadless() || Vector3.Distance(me.transform.position, h.transform.position) > OpenRange)
                return;
            bool follower = h.Mode == HirelingMode.Following;
            if (follower && h.OwnerId != me.GetPlayerID())
            {
                me.Message(MessageHud.MessageType.Center, Localization.instance.Localize("$vfh_not_your_follower"));
                return;
            }
            if (!follower && !PrivateArea.CheckAccess(h.Home, 0f, flash: true))
                return;
            if (_instance == null)
                _instance = Create();
            _instance._hireling = h;
            _instance._shown = "";
            _instance._limits = false;
            _instance._limitsPage = 0;
            _instance._orders = false;
            _instance._ordersPicking = false;
            _instance._ordersPage = 0;
            _instance._why = false;
            _instance._whyPage = 0;
            _instance._enh = false;
            _instance._pendingLevel = 0;
            _instance._nextRefresh = 0f;
            _instance._nextHold = 0f;
            _instance.gameObject.SetActive(true);
            GUIManager.BlockInput(true);
            VfhLog.D(LogCat.UI, "orders.open", ("hid", h.Hid));
        }

        /// <summary>Tests: switch the open panel to its Enhancements page.</summary>
        /// <summary>Tests: the open panel's Orders or Problems page.</summary>
        public static void ShowPage(string page)
        {
            if (_instance == null)
                return;
            _instance._orders = page is "orders" or "orders_add";
            _instance._ordersPicking = page == "orders_add";
            _instance._why = page == "why";
            _instance._shown = "";
        }

        public static void ShowEnhancements()
        {
            if (_instance == null)
                return;
            _instance._enh = true;
            _instance._shown = "";
        }

        public static void Close(string reason)
        {
            if (!IsOpen)
                return;
            _instance!.gameObject.SetActive(false);
            GUIManager.BlockInput(false);
            VfhLog.D(LogCat.UI, "orders.close", ("reason", reason));
            if (_instance._hireling != null)
                _instance._hireling.HoldForPanel(false);
            _instance._hireling = null;
        }

        private static HirelingPanel Create()
        {
            GameObject root = GUIManager.Instance.CreateWoodpanel(GUIManager.CustomGUIFront.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 520f, 420f, false);
            root.name = "VFH_HirelingPanel";
            HirelingPanel panel = root.AddComponent<HirelingPanel>();
            panel._content = PanelUi.Fill(root.transform, "content");
            PanelUi.Button(root.transform, "$vfh_close", 200f, -30f, 80f, 34f, () => Close("button"));
            root.SetActive(false);
            PanelUi.Clicked += () =>
            {
                if (_instance != null)
                    _instance._nextRefresh = 0f;
            };
            return panel;
        }

        private void Update()
        {
            Hireling? h = _hireling;
            Player me = Player.m_localPlayer;
            if (h == null || h.Zdo == null || me == null || me.IsDead() ||
                Vector3.Distance(me.transform.position, h.transform.position) > CloseRange || ZInput.GetKeyDown(KeyCode.Escape))
            {
                Close(h == null ? "gone" : "walked away");
                return;
            }
            // Keep it standing still while the panel is open (the hold lapses on its own if this game goes away).
            if (Time.unscaledTime >= _nextHold)
            {
                _nextHold = Time.unscaledTime + 1f;
                h.HoldForPanel(true);
            }
            if (Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + 0.3f;
            string signature = $"{h.Mode}|{h.FollowMode}|{h.Stance}|{h.HasPost}|{_all}|{h.DisplayName}|{h.Zdo.GetString(HirelingZdo.SkipItems)}|{h.WorksAtHome}|{h.IsParked}|{h.Level}|{(ChoreRules.ChoresFor(h.Job).Count > 0 || h.Job is JobType.Woodcutter or JobType.Miner ? h.Zdo.GetString(HirelingZdo.Activity) + "|" + h.Zdo.GetString(HirelingZdo.Why) : "")}";
            if (_limits && BoardOrders.BoardOf(h.BoardId) is HiringBoard lb)
            {
                Core.Orders.OrderList ol = BoardOrders.For(lb);
                List<Container> chests = Hirelings.Work.ChestModes.Stock(ChestFinder.Find(h.Home, h.Radius)).ToList();
                signature += $"|{_limits}|{_limitsPage}|{ol.Serialize()}|" +
                             string.Join(",", ol.Orders.Where(o => o.Kind == Core.Orders.OrderKind.Station).Select(o => Hirelings.Work.Steward.StewardLimits.Held(chests, o.Item)));
            }
            if (_orders && BoardOrders.BoardOf(h.BoardId) is HiringBoard ob)
            {
                Core.Orders.OrderList ol = BoardOrders.For(ob);
                Stock os = BoardOrders.Stock(ob);
                signature += $"|orders|{_ordersPicking}|{_ordersPage}|{ol.Serialize()}|" +
                             string.Join(",", OrderPicks.For(ol, h.Job).Select(o => OrderPicks.Have(ob, os, o)));
            }
            if (_why)
            {
                signature += $"|why|{_whyPage}|{h.Zdo.GetString(HirelingZdo.Why)}";
                if (BoardOrders.BoardOf(h.BoardId) is HiringBoard wb)
                {
                    Stock ws = BoardOrders.Stock(wb);
                    signature += "|" + string.Join(",", WhyOrders(h, wb).Select(o => $"{o.Item}:{o.Target}:{o.Paused}:{ws.Have(o.Item)}"));
                }
            }
            if (_enh)
                signature += $"|enh|{h.Zdo.GetString(HirelingZdo.Enhancements)}|{_pendingLevel}{_pendingChoice}|{VfhConfig.Enhancements.Value}";
            else
                signature += $"|{h.Zdo.GetString(HirelingZdo.Enhancements)}";
            if (signature == _shown)
                return;
            _shown = signature;
            Build(h);
        }

        private void Build(Hireling h)
        {
            PanelUi.Clear(_content);
            Transform t = _content;
            if (transform is RectTransform wide)
                wide.sizeDelta = new Vector2(_enh || _orders ? EnhWidth : _limits || _why ? LimitsWidth : Width, wide.sizeDelta.y);
            if (_enh)
            {
                BuildEnhancements(h);
                return;
            }
            if (_limits)
            {
                BuildLimits(h);
                return;
            }
            if (_orders)
            {
                BuildOrders(h);
                return;
            }
            if (_why)
            {
                BuildWhy(h);
                return;
            }
            bool follower = h.Mode == HirelingMode.Following;
            // Rename sits at the left: the panel's Close button takes the top right corner.
            PanelUi.Button(t, "$vfh_orders_rename", -190f, -45f, 90f, 30f, () => Rename(h));
            PanelUi.Text(t, $"{h.DisplayName} — $vfh_job_{h.Job.ToString().ToLowerInvariant()} $vfh_level {h.Level}", 15f, -45f, 290f, 22, bold: true);
            float y = -100f;

            if (follower)
            {
                PanelUi.Text(t, "$vfh_orders_mode", -170f, y, 140f, 18, TextAnchor.MiddleLeft);
                var modes = new[] { FollowMode.Follow, FollowMode.Stay, FollowMode.GatherHere };
                for (int i = 0; i < modes.Length; i++)
                {
                    FollowMode m = modes[i];
                    Button b = PanelUi.Button(t, $"$vfh_mode_{m.ToString().ToLowerInvariant()}", -40f + i * 110f, y, 104f, 34f, () => SetMode(m));
                    Highlight(b, h.FollowMode == m);
                }
                y -= 50f;
            }

            PanelUi.Text(t, "$vfh_orders_stance", -170f, y, 140f, 18, TextAnchor.MiddleLeft);
            IReadOnlyList<Stance> stances = StanceRules.Allowed(h.Job, h.Enh.Has(Core.Enhancements.EnhancementId.Watchman));
            for (int i = 0; i < stances.Count; i++)
            {
                Stance s = stances[i];
                Button b = PanelUi.Button(t, $"$vfh_stance_{s.ToString().ToLowerInvariant()}", -40f + i * 110f, y, 104f, 34f, () => SetStance(s));
                Highlight(b, h.Stance == s);
            }
            y -= 50f;

            // Enhancements (plan/enhancements): owed choices flagged on the button.
            if (VfhConfig.Enhancements.Value && h.BoardId.Length > 0)
            {
                int owed = Core.Enhancements.EnhancementRules.Owed(Core.Enhancements.EnhancementPicks.Parse(h.Zdo?.GetString(HirelingZdo.Enhancements)), h.Level).Count;
                Button eb = PanelUi.Button(t, owed > 0 ? Localization.instance.Localize("$vfh_enh_open", owed.ToString()) : "$vfh_enh_open_none",
                    0f, y, 300f, 32f, () => { _enh = true; _pendingLevel = 0; _shown = ""; });
                Highlight(eb, owed > 0);
                y -= 50f;
            }

            // Gatherers: what to gather (each tree or rock counts as its best drop), and whether to work at home at all.
            if (h.Job is JobType.Woodcutter or JobType.Miner && DataStore.Current.Jobs.TryGetValue(h.Job, out JobData? job) && job.GatherToggles.Count > 0)
            {
                PanelUi.Text(t, "$vfh_orders_gather", -170f, y, 140f, 18, TextAnchor.MiddleLeft);
                HashSet<string> skip = h.SkipItems;
                for (int i = 0; i < job.GatherToggles.Count; i++)
                {
                    string item = job.GatherToggles[i];
                    bool on = !skip.Contains(item);
                    string label = Localization.instance.Localize(ItemName(item)) + ": " + Localization.instance.Localize(on ? "$vfh_on" : "$vfh_off");
                    Button b = PanelUi.Button(t, label, -5f + i % 2 * 170f, y - i / 2 * 40f, 164f, 34f, () => ToggleItem(item));
                    ShowToggle(b, on);
                }
                y -= (job.GatherToggles.Count + 1) / 2 * 40f + 10f;
                Button home = PanelUi.Button(t, h.WorksAtHome ? "$vfh_orders_home_work_on" : "$vfh_orders_home_work_off", 0f, y, 360f, 34f, ToggleHome);
                ShowToggle(home, h.WorksAtHome);
                y -= 44f;
                // Why it isn't working, from its last look round (what it skipped, and why).
                PanelUi.Button(t, Localization.instance.Localize("$vfh_why_open", WhyLines(h).Count.ToString()), 0f, y, 300f, 32f, OpenWhy);
                y -= 50f;
            }

            // Stewards, Farmers, Cooks: each chore with its toggle, or why it can't be switched on; then what it's doing.
            if (ChoreRules.ChoresFor(h.Job).Count > 0 && DataStore.Current.Jobs.TryGetValue(h.Job, out JobData? steward))
            {
                PanelUi.Text(t, "$vfh_orders_chores", -170f, y, 140f, 18, TextAnchor.MiddleLeft);
                HashSet<ChoreKind> off = ChoreRules.ChoresOff(h.Zdo?.GetString(HirelingZdo.SkipItems));
                y -= 30f;
                foreach (ChoreKind kind in ChoreRules.ChoresFor(h.Job))
                {
                    string name = Localization.instance.Localize("$vfh_chore_" + ChoreKeys.Key(kind));
                    string? state = LockedState(kind, steward, h.Level, h.BoardId);
                    if (state != null)
                    {
                        PanelUi.Text(t, name + ": " + state, 0f, y, 400f, 15, TextAnchor.MiddleCenter, PanelUi.Dim);
                        y -= 30f;
                        continue;
                    }
                    bool on = !off.Contains(kind);
                    Button b = PanelUi.Button(t, name + ": " + Localization.instance.Localize(on ? "$vfh_chore_state_on" : "$vfh_chore_state_off"),
                        0f, y, 400f, 30f, () => ToggleChore(kind));
                    ShowToggle(b, on);
                    y -= 34f;
                    string later = StillLocked(kind, steward, h.Level);
                    if (later.Length > 0)
                    {
                        PanelUi.Text(t, later, 0f, y + 4f, 400f, 13, TextAnchor.MiddleCenter, PanelUi.Dim);
                        y -= 22f;
                    }
                }
                int problems = WhyLines(h).Count;
                if (h.Job == JobType.Smelter)
                {
                    int set = BoardOrders.For(BoardOrders.BoardOf(h.BoardId)?.Zdo).Orders.Count(o => o.Kind == Core.Orders.OrderKind.Station);
                    PanelUi.Button(t, Localization.instance.Localize("$vfh_limits_open", set.ToString()), -105f, y - 6f, 200f, 32f, () => { _limits = true; _limitsPage = 0; _shown = ""; });
                    PanelUi.Button(t, Localization.instance.Localize("$vfh_why_open", problems.ToString()), 105f, y - 6f, 200f, 32f, OpenWhy);
                }
                else if (OrderPicks.TakesOrders(h.Job))
                {
                    int set = OrderPicks.For(BoardOrders.For(BoardOrders.BoardOf(h.BoardId)?.Zdo), h.Job).Count;
                    PanelUi.Button(t, Localization.instance.Localize("$vfh_orders_open", set.ToString()), -105f, y - 6f, 200f, 32f,
                        () => { _orders = true; _ordersPicking = false; _ordersPage = 0; _shown = ""; });
                    PanelUi.Button(t, Localization.instance.Localize("$vfh_why_open", problems.ToString()), 105f, y - 6f, 200f, 32f, OpenWhy);
                }
                else
                    PanelUi.Button(t, Localization.instance.Localize("$vfh_why_open", problems.ToString()), 0f, y - 6f, 300f, 32f, OpenWhy);
                y -= 44f;
                y -= 10f;
                string doing = Hirelings.Work.Chores.ActivityText.Show(h.Zdo?.GetString(HirelingZdo.Activity) ?? "");
                if (doing.Length > 0)
                {
                    // Two lines' room: a long reason ("no station here can make it now (fire, roof, free slot)") was cut off.
                    Text line = PanelUi.Text(t, doing, 0f, y - 8f, 440f, 15, color: PanelUi.Dim);
                    line.horizontalOverflow = HorizontalWrapMode.Wrap;
                    if (line.transform is RectTransform lr)
                        lr.sizeDelta = new Vector2(440f, 40f);
                    y -= 52f;
                }
            }

            if (follower)
            {
                Button all = PanelUi.Button(t, _all ? "$vfh_orders_all_on" : "$vfh_orders_all_off", 0f, y, 360f, 34f, () => { _all = !_all; _shown = ""; });
                Highlight(all, _all);
                y -= 55f;
                // At home: back to work now. In the field: it heads home on its own (a walking-pace trip) and works there.
                bool home = Utils.DistanceXZ(h.transform.position, h.Home) <= h.Radius;
                if (home)
                    PanelUi.Button(t, "$vfh_orders_release", 0f, y, 240f, 38f, () => Act(FollowerServer.Kind.Release));
                else
                {
                    // With ReturnHomeWithNonTeleportable off it leaves ore and the like behind: say so on the button.
                    List<string> left = h.CargoInventory?.GetAllItems().Where(Followers.HomeReturn.LeavesBehind)
                        .Select(i => Localization.instance.Localize(i.m_shared.m_name)).Distinct().ToList() ?? new List<string>();
                    PanelUi.Button(t, left.Count > 0 ? "$vfh_orders_send_home_drop" : "$vfh_orders_send_home", 0f, y, 300f, 38f, () => Act(FollowerServer.Kind.SendHome));
                    PanelUi.Text(t, left.Count > 0 ? Localization.instance.Localize("$vfh_orders_send_home_drop_hint", string.Join(", ", left))
                        : "$vfh_orders_send_home_hint", 0f, y - 35f, 440f, 15, color: left.Count > 0 ? GUIManager.Instance.ValheimOrange : PanelUi.Dim);
                }
            }
            else if (h.IsParked)
            {
                PanelUi.Button(t, "$vfh_roster_back_to_work", 0f, y, 240f, 38f, () => Board.BoardContracts.Park(h.Hid, false));
            }
            else if (h.HasPost)
            {
                PanelUi.Button(t, "$vfh_orders_clear_post", 0f, y, 240f, 38f, () => Act(FollowerServer.Kind.ClearPost));
            }
            // Grow the panel to fit (everything is pinned to its top).
            if (transform is RectTransform rt)
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, Mathf.Max(MinHeight, -y + 70f));
        }

        private const float MinHeight = 420f;
        private const float EnhWidth = 760f;
        private const float EnhRowHeight = 74f;

        // Each level's two options: chosen (highlighted), the other at a chosen level (greyed), owed (click, then click
        // again to confirm: undoing costs a respec), or not yet reached (greyed, "at level N").
        private void BuildEnhancements(Hireling h)
        {
            Transform t = _content;
            PanelUi.Button(t, "$vfh_orders_back", -320f, -45f, 100f, 30f, () => { _enh = false; _pendingLevel = 0; _shown = ""; });
            PanelUi.Text(t, Localization.instance.Localize("$vfh_enh_title", h.DisplayName), -10f, -45f, 420f, 22, bold: true);
            float y = -85f;
            if (!VfhConfig.Enhancements.Value)
            {
                PanelUi.Text(t, "$vfh_enh_off", 0f, y, 600f, 16, color: PanelUi.Dim);
                return;
            }
            PanelUi.Text(t, "$vfh_enh_hint", 0f, y, 660f, 14, color: PanelUi.Dim);
            y -= 45f;
            var picks = Core.Enhancements.EnhancementPicks.Parse(h.Zdo?.GetString(HirelingZdo.Enhancements));
            for (int level = Core.Enhancements.EnhancementCatalog.FirstLevel; level <= Core.Enhancements.EnhancementCatalog.LastLevel; level++)
            {
                char chosen = picks.Get(level);
                // Owed levels stand out; levels not reached yet are dimmed.
                Color levelColor = level > h.Level ? PanelUi.Dim : chosen == '-' ? GUIManager.Instance.ValheimOrange : GUIManager.Instance.ValheimBeige;
                PanelUi.Text(t, Localization.instance.Localize("$vfh_enh_level", level.ToString()), -330f, y, 80f, 16, TextAnchor.MiddleLeft, levelColor, bold: true);
                (Core.Enhancements.EnhancementId a, Core.Enhancements.EnhancementId b) = Core.Enhancements.EnhancementCatalog.Pair(h.Job, level);
                Option(h, level, 'A', a, -125f, y, chosen);
                Option(h, level, 'B', b, 200f, y, chosen);
                y -= EnhRowHeight;
            }
            if (transform is RectTransform rt)
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, Mathf.Max(MinHeight, -y + 40f));
        }

        private void Option(Hireling h, int level, char choice, Core.Enhancements.EnhancementId id, float x, float y, char chosen)
        {
            Transform t = _content;
            string name = Localization.instance.Localize(Core.Enhancements.EnhancementText.NameKey(h.Job, id));
            string desc = Localization.instance.Localize(Core.Enhancements.EnhancementText.DescKey(h.Job, id),
                Core.Enhancements.EnhancementText.Args(DataStore.Current, h.Job, id));
            bool future = level > h.Level;
            bool pending = _pendingLevel == level && _pendingChoice == choice;
            string label = pending ? Localization.instance.Localize("$vfh_enh_confirm", name) : chosen == choice ? "✓ " + name : name;
            Button btn = PanelUi.Button(t, label, x, y, 310f, 30f, () => ClickOption(h, level, choice));
            if (chosen == choice)
                Highlight(btn, true);
            else if (chosen != '-' || future)
                btn.interactable = false;
            else if (pending)
                Highlight(btn, true);
            // Two lines of description under the option.
            Text d = PanelUi.Text(t, desc, x, y - 33f, 310f, 12, TextAnchor.UpperCenter, chosen == choice ? GUIManager.Instance.ValheimBeige : PanelUi.Dim);
            d.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 32f);
        }

        private void ClickOption(Hireling h, int level, char choice)
        {
            if (_pendingLevel == level && _pendingChoice == choice)
            {
                _pendingLevel = 0;
                Board.BoardContracts.ChooseEnhancement(h.BoardId, h.Hid, level, choice);
                VfhLog.I(LogCat.UI, "enh.choose", ("hid", h.Hid), ("level", level), ("choice", choice));
            }
            else
            {
                _pendingLevel = level;
                _pendingChoice = choice;
            }
            _shown = "";
        }

        private const float Width = 520f;
        private const float LimitsWidth = 660f;
        private const int LimitPicksPerPage = 12;

        // The Steward's limits (kept on its board as Station orders): what's limited, with - / + (Shift: steps of 1),
        // pause and remove; below, what else it makes, to add a limit for.
        private void BuildLimits(Hireling h)
        {
            Transform t = _content;
            PanelUi.Button(t, "$vfh_orders_back", -250f, -45f, 100f, 30f, () => { _limits = false; _shown = ""; });
            PanelUi.Text(t, Localization.instance.Localize("$vfh_limits_title", h.DisplayName), 20f, -45f, 380f, 22, bold: true);
            float y = -85f;
            HiringBoard? board = BoardOrders.BoardOf(h.BoardId);
            if (board == null)
            {
                PanelUi.Text(t, "$vfh_limits_no_board", 0f, y, 560f, 16, color: PanelUi.Dim);
                Fit(-y + 60f);
                return;
            }
            bool edit = PrivateArea.CheckAccess(board.transform.position, 0f, false, false);
            PanelUi.Text(t, "$vfh_limits_hint", 0f, y, 600f, 14, color: PanelUi.Dim);
            y -= 40f;
            Core.Orders.OrderList list = BoardOrders.For(board);
            // Counted as the Steward counts: every chest in its area, reserves included.
            List<Container> chests = Hirelings.Work.ChestModes.Stock(ChestFinder.Find(h.Home, h.Radius)).ToList();
            List<Core.Orders.ProductionOrder> limits = list.Orders.Where(o => o.Kind == Core.Orders.OrderKind.Station).ToList();
            if (limits.Count == 0)
            {
                PanelUi.Text(t, "$vfh_limits_none", 0f, y, 560f, 16, color: PanelUi.Dim);
                y -= 36f;
            }
            foreach (Core.Orders.ProductionOrder o in limits)
            {
                int have = Hirelings.Work.Steward.StewardLimits.Held(chests, o.Item);
                PanelUi.Icon(t, ObjectDB.instance.GetItemPrefab(o.Item)?.GetComponent<ItemDrop>()?.m_itemData.GetIcon(), -290f, y, 30f);
                PanelUi.Text(t, ItemName(o.Item), -165f, y, 200f, 16, TextAnchor.MiddleLeft, o.Paused ? PanelUi.Dim : (Color?)null);
                PanelUi.Text(t, o.Paused ? Localization.instance.Localize("$vfh_limits_paused") : $"{have} / {o.Target}", 10f, y, 110f, 16,
                    color: have >= o.Target || o.Paused ? PanelUi.Good : PanelUi.Dim);
                string item = o.Item;
                var buttons = new[]
                {
                    PanelUi.Button(t, "-", 90f, y, 34f, 30f, () => BoardOrders.Submit(board, Core.OrderEdit.Target, item, target: o.Target - LimitStep())),
                    PanelUi.Button(t, "+", 128f, y, 34f, 30f, () => BoardOrders.Submit(board, Core.OrderEdit.Target, item, target: o.Target + LimitStep())),
                    PanelUi.Button(t, o.Paused ? "$vfh_orders_resume" : "$vfh_orders_pause", 205f, y, 100f, 30f,
                        () => BoardOrders.Submit(board, Core.OrderEdit.Pause, item, paused: !o.Paused)),
                    PanelUi.Button(t, "X", 280f, y, 34f, 30f, () => BoardOrders.Submit(board, Core.OrderEdit.Remove, item)),
                };
                foreach (Button b in buttons)
                    b.interactable = edit;
                y -= 40f;
            }

            y -= 10f;
            PanelUi.Text(t, "$vfh_limits_add", 0f, y, 560f, 17, bold: true);
            y -= 36f;
            JobData steward = DataStore.Current.Jobs.TryGetValue(JobType.Smelter, out JobData? j) ? j : new JobData();
            List<Hirelings.Work.Steward.StewardLimits.Product> picks = Hirelings.Work.Steward.StewardLimits.Products().Where(p => list.Find(p.Item) == null).ToList();
            int pages = Mathf.Max(1, (picks.Count + LimitPicksPerPage - 1) / LimitPicksPerPage);
            _limitsPage = Mathf.Clamp(_limitsPage, 0, pages - 1);
            int i = 0;
            foreach (Hirelings.Work.Steward.StewardLimits.Product p in picks.Skip(_limitsPage * LimitPicksPerPage).Take(LimitPicksPerPage))
            {
                int level = ChoreRules.MinLevel(steward, p.Gate);
                string label = Localization.instance.Localize(ItemName(p.Item)) +
                               (level > h.Level ? " (" + Localization.instance.Localize("$vfh_orders_level", level.ToString()) + ")" : "");
                string item = p.Item;
                Button b = PanelUi.Button(t, label, i % 2 == 0 ? -150f : 150f, y - (i / 2) * 38f, 290f, 32f,
                    () => BoardOrders.Submit(board, Core.OrderEdit.Add, item, Core.Orders.OrderKind.Station, 100));
                b.interactable = edit;
                i++;
            }
            if (picks.Count == 0)
                PanelUi.Text(t, "$vfh_orders_pick_none", 0f, y, 560f, 16, color: PanelUi.Dim);
            y -= Mathf.Max(1, (i + 1) / 2) * 38f + 8f;
            if (pages > 1)
            {
                PanelUi.Button(t, "<", -60f, y, 40f, 30f, () => { _limitsPage = Mathf.Max(0, _limitsPage - 1); _shown = ""; });
                PanelUi.Text(t, $"{_limitsPage + 1} / {pages}", 0f, y, 70f, 16);
                PanelUi.Button(t, ">", 60f, y, 40f, 30f, () => { _limitsPage = Mathf.Min(pages - 1, _limitsPage + 1); _shown = ""; });
                y -= 40f;
            }
            Fit(-y + 50f);
        }

        private const int OrderPicksPerPage = 14;

        // A Farmer's, Cook's or Rancher's orders (kept on its board): each with have / target, - / + (Shift: steps of
        // 1), up and down (the order they're worked in), pause and remove; and a picker for adding one.
        private void BuildOrders(Hireling h)
        {
            Transform t = _content;
            PanelUi.Button(t, "$vfh_orders_back", -310f, -45f, 100f, 30f, () =>
            {
                if (_ordersPicking)
                    _ordersPicking = false;
                else
                    _orders = false;
                _shown = "";
            });
            PanelUi.Text(t, Localization.instance.Localize(_ordersPicking ? "$vfh_orders_pick_title" : "$vfh_orders_title", h.DisplayName), 30f, -45f, 480f, 22, bold: true);
            float y = -85f;
            HiringBoard? board = BoardOrders.BoardOf(h.BoardId);
            if (board == null)
            {
                PanelUi.Text(t, "$vfh_orders_no_board", 0f, y, 600f, 16, color: PanelUi.Dim);
                Fit(-y + 60f);
                return;
            }
            bool edit = PrivateArea.CheckAccess(board.transform.position, 0f, false, false);
            Core.Orders.OrderList list = BoardOrders.For(board);
            if (_ordersPicking)
            {
                y -= 12f; // clear of the title
                List<OrderPicks.Pick> picks = OrderPicks.Picks(h.Job).Where(p => list.Find(p.Item) == null).ToList();
                int pages = Mathf.Max(1, (picks.Count + OrderPicksPerPage - 1) / OrderPicksPerPage);
                _ordersPage = Mathf.Clamp(_ordersPage, 0, pages - 1);
                int i = 0;
                foreach (OrderPicks.Pick p in picks.Skip(_ordersPage * OrderPicksPerPage).Take(OrderPicksPerPage))
                {
                    float x = i % 2 == 0 ? -180f : 180f;
                    float py = y - (i / 2) * 48f;
                    bool canDo = h.Level >= p.Level;
                    OrderPicks.Pick chosen = p;
                    Button b = PanelUi.Button(t, OrderPicks.PickLabel(p), x, py, 340f, 30f, () =>
                    {
                        BoardOrders.Submit(board, Core.OrderEdit.Add, chosen.Item, chosen.Kind, OrderPicks.StartTarget(chosen.Kind));
                        _ordersPicking = false;
                        _ordersPage = 0;
                        _shown = "";
                    });
                    b.interactable = edit;
                    PanelUi.Text(t, Localization.instance.Localize(canDo ? "$vfh_orders_level" : "$vfh_orders_needs_level", p.Level.ToString()),
                        x, py - 20f, 340f, 12, color: canDo ? PanelUi.Dim : PanelUi.Bad);
                    i++;
                }
                if (picks.Count == 0)
                    PanelUi.Text(t, "$vfh_orders_pick_none", 0f, y, 600f, 16, color: PanelUi.Dim);
                y -= Mathf.Max(1, (i + 1) / 2) * 48f + 6f;
                y = OrdersPager(t, y, pages);
                Fit(-y + 50f);
                return;
            }
            PanelUi.Text(t, "$vfh_orders_hint", 0f, y, 700f, 14, color: PanelUi.Dim);
            y -= 38f;
            Stock stock = BoardOrders.Stock(board);
            List<Core.Orders.ProductionOrder> orders = OrderPicks.For(list, h.Job);
            const int rows = 10;
            int listPages = Mathf.Max(1, (orders.Count + rows - 1) / rows);
            _ordersPage = Mathf.Clamp(_ordersPage, 0, listPages - 1);
            if (orders.Count == 0)
            {
                PanelUi.Text(t, "$vfh_orders_none", 0f, y, 600f, 16, color: PanelUi.Dim);
                y -= 36f;
            }
            foreach (Core.Orders.ProductionOrder o in orders.Skip(_ordersPage * rows).Take(rows))
            {
                int have = OrderPicks.Have(board, stock, o);
                if (OrderPicks.Icon(o.Item) is Sprite icon)
                    PanelUi.Icon(t, icon, -345f, y, 32f);
                PanelUi.Text(t, OrderPicks.Name(o.Item), -195f, y + 8f, 240f, 16, TextAnchor.MiddleLeft, o.Paused ? PanelUi.Dim : (Color?)null);
                PanelUi.Text(t, OrderPicks.KindLabel(o.Kind), -195f, y - 10f, 240f, 12, TextAnchor.MiddleLeft, PanelUi.Dim);
                PanelUi.Text(t, o.Paused ? Localization.instance.Localize("$vfh_limits_paused") : $"{have} / {o.Target}", 15f, y, 120f, 16,
                    color: o.Paused ? PanelUi.Dim : have >= o.Target ? PanelUi.Good : PanelUi.Bad);
                string item = o.Item;
                var buttons = new[]
                {
                    PanelUi.Button(t, "-", 80f, y, 34f, 30f, () => BoardOrders.Submit(board, Core.OrderEdit.Target, item, target: o.Target - OrderStep())),
                    PanelUi.Button(t, "+", 118f, y, 34f, 30f, () => BoardOrders.Submit(board, Core.OrderEdit.Target, item, target: o.Target + OrderStep())),
                    PanelUi.Button(t, "^", 165f, y, 34f, 30f, () => BoardOrders.Submit(board, Core.OrderEdit.Up, item)),
                    PanelUi.Button(t, "v", 203f, y, 34f, 30f, () => BoardOrders.Submit(board, Core.OrderEdit.Down, item)),
                    PanelUi.Button(t, o.Paused ? "$vfh_orders_resume" : "$vfh_orders_pause", 268f, y, 90f, 30f,
                        () => BoardOrders.Submit(board, Core.OrderEdit.Pause, item, paused: !o.Paused)),
                    PanelUi.Button(t, "X", 335f, y, 34f, 30f, () => BoardOrders.Submit(board, Core.OrderEdit.Remove, item)),
                };
                foreach (Button b in buttons)
                    b.interactable = edit;
                y -= 44f;
            }
            y = OrdersPager(t, y, listPages);
            y -= 6f;
            Button add = PanelUi.Button(t, "$vfh_orders_add", 0f, y, 260f, 34f, () => { _ordersPicking = true; _ordersPage = 0; _shown = ""; });
            add.interactable = edit;
            y -= 40f;
            Fit(-y + 50f);
        }

        private float OrdersPager(Transform t, float y, int pages)
        {
            if (pages <= 1)
                return y;
            PanelUi.Button(t, "<", -60f, y, 40f, 30f, () => { _ordersPage = Mathf.Max(0, _ordersPage - 1); _shown = ""; });
            PanelUi.Text(t, $"{_ordersPage + 1} / {pages}", 0f, y, 70f, 16);
            PanelUi.Button(t, ">", 60f, y, 40f, 30f, () => { _ordersPage = Mathf.Min(pages - 1, _ordersPage + 1); _shown = ""; });
            return y - 40f;
        }

        // Shift for single steps.
        private static int OrderStep() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 1 : 5;

        private void OpenWhy()
        {
            _why = true;
            _whyPage = 0;
            _shown = "";
        }

        // The hireling's problems as of its last work survey (HirelingZdo.Why): (order item or "", activity text).
        private static List<(string Item, string Why)> WhyLines(Hireling h) =>
            (h.Zdo?.GetString(HirelingZdo.Why) ?? "").Split('\n').Where(l => l.Length > 0).Select(l =>
            {
                int tab = l.IndexOf('\t');
                return tab < 0 ? ("", l) : (l.Substring(0, tab), l.Substring(tab + 1));
            }).ToList();

        // The board orders this hireling works: seed and crop for a Farmer, kitchen for a Cook (Station limits have their own view).
        private static List<Core.Orders.ProductionOrder> WhyOrders(Hireling h, HiringBoard board)
        {
            Core.Orders.OrderList list = BoardOrders.For(board);
            return h.Job switch
            {
                JobType.Farmer => list.Orders.Where(o => o.Kind == Core.Orders.OrderKind.Seed)
                    .Concat(list.Orders.Where(o => o.Kind == Core.Orders.OrderKind.Crop)).ToList(),
                JobType.Cook => list.Orders.Where(o => o.Kind == Core.Orders.OrderKind.Kitchen).ToList(),
                JobType.Rancher => list.Orders.Where(o => o.IsRanch).ToList(),
                _ => new List<Core.Orders.ProductionOrder>(),
            };
        }

        private const int WhyRowsPerPage = 10;

        // Everything in its way: each of its orders (done, on it, or why not), then chore problems not tied to an order.
        private void BuildWhy(Hireling h)
        {
            Transform t = _content;
            PanelUi.Button(t, "$vfh_orders_back", -250f, -45f, 100f, 30f, () => { _why = false; _shown = ""; });
            PanelUi.Text(t, Localization.instance.Localize("$vfh_why_title", h.DisplayName), 20f, -45f, 380f, 22, bold: true);
            float y = -90f;
            List<(string Item, string Why)> lines = WhyLines(h);
            var byItem = new Dictionary<string, string>();
            foreach ((string item, string why) in lines.Where(l => l.Item.Length > 0))
                byItem[item] = why;
            HiringBoard? board = BoardOrders.BoardOf(h.BoardId);
            if (OrderPicks.TakesOrders(h.Job))
            {
                PanelUi.Text(t, "$vfh_why_orders", 0f, y, 560f, 17, bold: true);
                y -= 34f;
                List<Core.Orders.ProductionOrder> orders = board != null ? WhyOrders(h, board) : new List<Core.Orders.ProductionOrder>();
                if (orders.Count == 0)
                {
                    PanelUi.Text(t, board == null ? "$vfh_limits_no_board" : "$vfh_why_no_orders", 0f, y, 560f, 15, color: PanelUi.Dim);
                    y -= 32f;
                }
                int pages = Mathf.Max(1, (orders.Count + WhyRowsPerPage - 1) / WhyRowsPerPage);
                _whyPage = Mathf.Clamp(_whyPage, 0, pages - 1);
                Stock stock = board != null ? BoardOrders.Stock(board) : null!;
                foreach (Core.Orders.ProductionOrder o in orders.Skip(_whyPage * WhyRowsPerPage).Take(WhyRowsPerPage))
                {
                    int have = o.Kind == Core.Orders.OrderKind.Herd
                        ? (Hirelings.Work.Ranch.RanchState.For(board!).Adults.TryGetValue(o.Item, out int adults) ? adults : 0)
                        : stock.Have(o.Item);
                    PanelUi.Icon(t, ObjectDB.instance.GetItemPrefab(o.Item)?.GetComponent<ItemDrop>()?.m_itemData.GetIcon(), -295f, y, 28f);
                    PanelUi.Text(t, ItemName(o.Item), -195f, y, 170f, 15, TextAnchor.MiddleLeft, o.Paused ? PanelUi.Dim : (Color?)null);
                    PanelUi.Text(t, $"{have} / {o.Target}", -75f, y, 80f, 15, color: have >= o.Target ? PanelUi.Good : PanelUi.Dim);
                    (string text, Color color) = o.Paused ? (Localization.instance.Localize("$vfh_limits_paused"), PanelUi.Dim)
                        : have >= o.Target ? (Localization.instance.Localize("$vfh_why_done"), PanelUi.Good)
                        : byItem.TryGetValue(o.Item, out string why) ? (Hirelings.Work.Chores.ActivityText.Show(why), PanelUi.Bad)
                        : (Localization.instance.Localize("$vfh_why_on_it"), PanelUi.Good);
                    PanelUi.Text(t, text, 155f, y, 340f, 13, TextAnchor.MiddleLeft, color);
                    y -= 34f;
                }
                if (pages > 1)
                {
                    PanelUi.Button(t, "<", -60f, y, 40f, 30f, () => { _whyPage = Mathf.Max(0, _whyPage - 1); _shown = ""; });
                    PanelUi.Text(t, $"{_whyPage + 1} / {pages}", 0f, y, 70f, 16);
                    PanelUi.Button(t, ">", 60f, y, 40f, 30f, () => { _whyPage = Mathf.Min(pages - 1, _whyPage + 1); _shown = ""; });
                    y -= 40f;
                }
                y -= 10f;
            }
            PanelUi.Text(t, h.Job is JobType.Woodcutter or JobType.Miner ? "$vfh_why_found" : "$vfh_why_other", 0f, y, 560f, 17, bold: true);
            y -= 32f;
            List<string> notes = lines.Where(l => l.Item.Length == 0).Select(l => l.Why).ToList();
            if (notes.Count == 0)
            {
                PanelUi.Text(t, "$vfh_why_none", 0f, y, 560f, 15, color: PanelUi.Dim);
                y -= 30f;
            }
            foreach (string note in notes)
            {
                PanelUi.Text(t, Hirelings.Work.Chores.ActivityText.Show(note), 0f, y, 600f, 14, color: PanelUi.Bad);
                y -= 28f;
            }
            Fit(-y + 50f);
        }

        // Shift for single steps.
        private static int LimitStep() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? 1 : 10;

        private void Fit(float height)
        {
            if (transform is RectTransform rt)
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, Mathf.Max(MinHeight, height));
        }

        private static string ItemName(string prefab)
        {
            GameObject? go = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefab) : null;
            ItemDrop? drop = go != null ? go.GetComponent<ItemDrop>() : null;
            if (drop != null)
                return drop.m_itemData.m_shared.m_name;
            // A breeding pool order is for an animal kind.
            return ZNetScene.instance?.GetPrefab(prefab)?.GetComponent<Character>() is Character c ? c.m_name : prefab;
        }

        // Switch one item on or off for this gatherer (with "all": every follower of yours with the same job nearby).
        private void ToggleItem(string item)
        {
            Hireling h = _hireling!;
            bool turnOff = !h.SkipItems.Contains(item);
            foreach (Hireling f in Targets().Where(f => f.Job == h.Job))
            {
                var skip = new HashSet<string>(f.SkipItems);
                if (turnOff)
                    skip.Add(item);
                else
                    skip.Remove(item);
                SubmitGather(f, skip, !f.WorksAtHome);
            }
        }

        // A chore that can't be switched on for this Steward: off on the server, done by another mod, or not unlocked yet.
        private static string? LockedState(ChoreKind kind, JobData steward, int level, string boardId)
        {
            if (!Hirelings.Work.Chores.ChoreLoop.ServerAllows(kind))
                return Localization.instance.Localize("$vfh_chore_state_disabled");
            if (kind == ChoreKind.Animals && Hirelings.Work.Chores.ChoreLoop.RancherOnBoard(boardId))
                return Localization.instance.Localize("$vfh_chore_state_rancher");
            if (Compat.StewardCompat.HandledBy(kind) is string mod)
                return Localization.instance.Localize("$vfh_chore_state_handled", mod);
            int first = ChoreRules.FirstUnlock(steward, kind);
            if (first == int.MaxValue)
                return Localization.instance.Localize("$vfh_chore_state_disabled");
            return level < first ? Localization.instance.Localize("$vfh_chore_state_locked", first.ToString()) : null;
        }

        // For a partly unlocked Stations or Mills chore: the stations still to come, e.g. "Blast furnace: locked until level 5".
        private static string StillLocked(ChoreKind kind, JobData steward, int level)
        {
            if (kind is not (ChoreKind.Stations or ChoreKind.Kilns or ChoreKind.Mills))
                return "";
            return string.Join(", ", ChoreRules.GateKeys(steward, kind).Where(k => ChoreRules.MinLevel(steward, k) > level)
                .Select(k => StationName(k) + ": " + Localization.instance.Localize("$vfh_chore_state_locked", ChoreRules.MinLevel(steward, k).ToString())));
        }

        private static string StationName(string prefab)
        {
            Piece? piece = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(prefab)?.GetComponent<Piece>() : null;
            return piece != null ? Localization.instance.Localize(piece.m_name) : prefab;
        }

        // Switch a chore on or off for this Steward (with "all": every Steward of yours nearby, as for gather toggles).
        private void ToggleChore(ChoreKind kind)
        {
            Hireling h = _hireling!;
            bool turnOn = ChoreRules.ChoresOff(h.Zdo?.GetString(HirelingZdo.SkipItems)).Contains(kind);
            foreach (Hireling f in Targets().Where(f => f.Job == h.Job))
            {
                string skip = ChoreRules.WithChore(f.Zdo?.GetString(HirelingZdo.SkipItems), kind, turnOn);
                SubmitGather(f, GatherRules.ParseSkip(skip), !f.WorksAtHome);
            }
            _shown = "";
        }

        private void ToggleHome()
        {
            Hireling h = _hireling!;
            bool noHome = h.WorksAtHome;
            foreach (Hireling f in Targets().Where(f => f.Job == h.Job))
                SubmitGather(f, f.SkipItems, noHome);
        }

        // On the contract (so it survives respawns), which sets the hireling too; a hireling without a board directly.
        private static void SubmitGather(Hireling f, HashSet<string> skip, bool noHomeWork)
        {
            string list = GatherRules.FormatSkip(skip);
            if (f.BoardId.Length > 0)
                MutationService.SubmitBoard(f.BoardId, new RosterOp { Type = RosterOpType.SetGather, Hid = f.Hid, SkipItems = list, NoHomeWork = noHomeWork });
            else
                MutationService.SubmitHireling(f.Hid, new HirelingOp { SkipItems = list, NoHomeWork = noHomeWork });
            VfhLog.I(LogCat.UI, "hireling.gather_set", ("hid", f.Hid), ("skip", list), ("noHomeWork", noHomeWork));
        }

        // An on/off button that reads at a glance: bright orange text on a normal button when on, grey text on a
        // dimmed button when off (the label says which too).
        private static void ShowToggle(Button b, bool on)
        {
            Text label = b.GetComponentInChildren<Text>();
            if (label != null)
                label.color = on ? GUIManager.Instance.ValheimOrange : new Color(0.55f, 0.55f, 0.55f);
            if (b.targetGraphic is Image image)
                image.color = on ? Color.white : new Color(0.45f, 0.45f, 0.45f, 0.8f);
        }

        private static void Highlight(Button b, bool on)
        {
            Text label = b.GetComponentInChildren<Text>();
            if (label != null)
                label.color = on ? GUIManager.Instance.ValheimOrange : GUIManager.Instance.ValheimBeige;
        }

        // The hireling, or with "all" every follower of yours within 30 m.
        private IEnumerable<Hireling> Targets()
        {
            Hireling h = _hireling!;
            if (!_all || h.Mode != HirelingMode.Following)
                return new[] { h };
            return StoneInput.MyFollowers(Player.m_localPlayer, GroupRange).Append(h).Distinct();
        }

        private void SetMode(FollowMode mode)
        {
            foreach (Hireling f in Targets())
            {
                Vector3 p = f.transform.position;
                f.Ai.Order = null;
                MutationService.SubmitHireling(f.Hid, mode == FollowMode.Follow
                    ? new HirelingOp { FollowMode = mode }
                    : new HirelingOp { FollowMode = mode, StayPos = (p.x, p.y, p.z) });
                VfhLog.I(LogCat.Follow, "follow.mode", ("hid", f.Hid), ("mode", mode), ("via", "panel"));
            }
        }

        // Stance lives on the contract too, so it's an Edit on the board (which also updates the hireling).
        private void SetStance(Stance stance)
        {
            foreach (Hireling f in Targets())
            {
                if (!StanceRules.IsAllowed(f.Job, stance, f.Enh.Has(Core.Enhancements.EnhancementId.Watchman)) || f.BoardId.Length == 0)
                    continue;
                MutationService.SubmitBoard(f.BoardId, new RosterOp { Type = RosterOpType.Edit, Hid = f.Hid, Radius = f.ContractRadius, Stance = stance });
                VfhLog.I(LogCat.Follow, "follow.stance", ("hid", f.Hid), ("stance", stance), ("via", "panel"));
            }
        }

        // The vanilla text box (as for signs): the name goes on the contract, which renames the hireling too, so it
        // keeps the name when it comes back after dying. A hireling without a board is renamed directly.
        private void Rename(Hireling h)
        {
            Close("rename");
            TextInput.instance.RequestText(new RenameReceiver(h), "$vfh_rename_topic", HirelingNames.MaxLength);
        }

        private sealed class RenameReceiver : TextReceiver
        {
            private readonly Hireling _h;
            private readonly string _hid;
            private readonly string _boardId;

            public RenameReceiver(Hireling h)
            {
                _h = h;
                _hid = h.Hid;
                _boardId = h.BoardId;
            }

            public string GetText() => _h != null ? _h.DisplayName : "";

            public void SetText(string text)
            {
                string name = HirelingNames.Clean(text);
                if (name.Length == 0)
                    return;
                if (_boardId.Length > 0)
                    MutationService.SubmitBoard(_boardId, new RosterOp { Type = RosterOpType.Rename, Hid = _hid, Name = name });
                else
                    MutationService.SubmitHireling(_hid, new HirelingOp { Name = name });
                VfhLog.I(LogCat.UI, "hireling.rename", ("hid", _hid), ("name", name));
            }
        }

        private void Act(FollowerServer.Kind kind)
        {
            Hireling h = _hireling!;
            int quality = Player.m_localPlayer.GetInventory().GetAllItems().Where(CommandStoneItem.IsStone).Select(i => i.m_quality).DefaultIfEmpty(0).Max();
            FollowerServer.Send(kind, h.Hid, quality);
            Close("action");
        }
    }
}

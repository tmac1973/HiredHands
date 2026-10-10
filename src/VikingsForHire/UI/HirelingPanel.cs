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
            _instance._nextRefresh = 0f;
            _instance._nextHold = 0f;
            _instance.gameObject.SetActive(true);
            GUIManager.BlockInput(true);
            VfhLog.D(LogCat.UI, "orders.open", ("hid", h.Hid));
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
            string signature = $"{h.Mode}|{h.FollowMode}|{h.Stance}|{h.HasPost}|{_all}|{h.DisplayName}|{h.Zdo.GetString(HirelingZdo.SkipItems)}|{h.WorksAtHome}|{h.IsParked}|{h.Level}|{(ChoreRules.ChoresFor(h.Job).Count > 0 ? h.Zdo.GetString(HirelingZdo.Activity) : "")}";
            if (_limits && BoardOrders.BoardOf(h.BoardId) is HiringBoard lb)
            {
                Core.Orders.OrderList ol = BoardOrders.For(lb);
                List<Container> chests = ChestFinder.Find(h.Home, h.Radius).ToList();
                signature += $"|{_limits}|{_limitsPage}|{ol.Serialize()}|" +
                             string.Join(",", ol.Orders.Where(o => o.Kind == Core.Orders.OrderKind.Station).Select(o => Hirelings.Work.Steward.StewardLimits.Held(chests, o.Item)));
            }
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
                wide.sizeDelta = new Vector2(_limits ? LimitsWidth : Width, wide.sizeDelta.y);
            if (_limits)
            {
                BuildLimits(h);
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
            IReadOnlyList<Stance> stances = StanceRules.Allowed(h.Job);
            for (int i = 0; i < stances.Count; i++)
            {
                Stance s = stances[i];
                Button b = PanelUi.Button(t, $"$vfh_stance_{s.ToString().ToLowerInvariant()}", -40f + i * 110f, y, 104f, 34f, () => SetStance(s));
                Highlight(b, h.Stance == s);
            }
            y -= 60f;

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
                y -= 55f;
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
                    string? state = LockedState(kind, steward, h.Level);
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
                if (h.Job == JobType.Smelter)
                {
                    int set = BoardOrders.For(BoardOrders.BoardOf(h.BoardId)?.Zdo).Orders.Count(o => o.Kind == Core.Orders.OrderKind.Station);
                    PanelUi.Button(t, Localization.instance.Localize("$vfh_limits_open", set.ToString()), 0f, y - 6f, 300f, 32f, () => { _limits = true; _limitsPage = 0; _shown = ""; });
                    y -= 44f;
                }
                y -= 10f;
                string doing = Hirelings.Work.Chores.ActivityText.Show(h.Zdo?.GetString(HirelingZdo.Activity) ?? "");
                if (doing.Length > 0)
                {
                    PanelUi.Text(t, doing, 0f, y, 440f, 15, color: PanelUi.Dim);
                    y -= 35f;
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
            List<Container> chests = ChestFinder.Find(h.Home, h.Radius).ToList();
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
            return drop != null ? drop.m_itemData.m_shared.m_name : prefab;
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
        private static string? LockedState(ChoreKind kind, JobData steward, int level)
        {
            if (!Hirelings.Work.Chores.ChoreLoop.ServerAllows(kind))
                return Localization.instance.Localize("$vfh_chore_state_disabled");
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
                if (!StanceRules.IsAllowed(f.Job, stance) || f.BoardId.Length == 0)
                    continue;
                MutationService.SubmitBoard(f.BoardId, new RosterOp { Type = RosterOpType.Edit, Hid = f.Hid, Radius = f.Radius, Stance = stance });
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

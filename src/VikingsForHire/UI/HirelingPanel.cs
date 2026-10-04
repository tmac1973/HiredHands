using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Data;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Followers;
using VikingsForHire.Hirelings;
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
        private RectTransform _content = null!;
        private bool _all;
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
            _instance._nextRefresh = 0f;
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
            if (Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + 0.3f;
            string signature = $"{h.Mode}|{h.FollowMode}|{h.Stance}|{h.HasPost}|{_all}|{h.DisplayName}|{h.Zdo.GetString(HirelingZdo.SkipItems)}|{h.WorksAtHome}";
            if (signature == _shown)
                return;
            _shown = signature;
            Build(h);
        }

        private void Build(Hireling h)
        {
            PanelUi.Clear(_content);
            Transform t = _content;
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
            else if (h.HasPost)
            {
                PanelUi.Button(t, "$vfh_orders_clear_post", 0f, y, 240f, 38f, () => Act(FollowerServer.Kind.ClearPost));
            }
            // Grow the panel to fit (everything is pinned to its top).
            if (transform is RectTransform rt)
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, Mathf.Max(MinHeight, -y + 70f));
        }

        private const float MinHeight = 420f;

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

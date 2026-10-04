using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using VikingsForHire.Core;
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
    /// orders to: stance (saved on its contract). Release a follower at home, or clear a posted guard's post. With
    /// "Apply to all my followers nearby" a change goes to every follower of yours within 30 m.
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
            string signature = $"{h.Mode}|{h.FollowMode}|{h.Stance}|{h.HasPost}|{_all}";
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
            PanelUi.Text(t, $"{h.DisplayName} — $vfh_job_{h.Job.ToString().ToLowerInvariant()} $vfh_level {h.Level}", 0f, -45f, 440f, 22, bold: true);
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

            if (follower)
            {
                Button all = PanelUi.Button(t, _all ? "$vfh_orders_all_on" : "$vfh_orders_all_off", 0f, y, 360f, 34f, () => { _all = !_all; _shown = ""; });
                Highlight(all, _all);
                y -= 55f;
                bool home = Utils.DistanceXZ(h.transform.position, h.Home) <= h.Radius;
                Button release = PanelUi.Button(t, "$vfh_orders_release", 0f, y, 240f, 38f, () => Act(FollowerServer.Kind.Release));
                release.interactable = home;
                if (!home)
                    PanelUi.Text(t, "$vfh_follow_too_far", 0f, y - 35f, 440f, 15, color: PanelUi.Dim);
            }
            else if (h.HasPost)
            {
                PanelUi.Button(t, "$vfh_orders_clear_post", 0f, y, 240f, 38f, () => Act(FollowerServer.Kind.ClearPost));
            }
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

        private void Act(FollowerServer.Kind kind)
        {
            Hireling h = _hireling!;
            int quality = Player.m_localPlayer.GetInventory().GetAllItems().Where(CommandStoneItem.IsStone).Select(i => i.m_quality).DefaultIfEmpty(0).Max();
            FollowerServer.Send(kind, h.Hid, quality);
            Close("action");
        }
    }
}

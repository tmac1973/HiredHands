using System;
using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Managers;
using VikingsForHire.Board;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

namespace VikingsForHire.UI
{
    /// <summary>
    /// The board's management panel (Shift+E): Contracts, Roster and Upgrade tabs. One instance, created on first use.
    /// It follows its board live (re-checking twice a second) and closes on Esc, the close button, walking away or the
    /// board disappearing.
    /// </summary>
    internal sealed class BoardPanel : MonoBehaviour
    {
        private const float OpenRange = 5f;
        private const float CloseRange = 6f;
        private const float RefreshSeconds = 0.5f;

        private static BoardPanel? _instance;

        private readonly List<IBoardTab> _tabs = new() { new ContractsTab(), new RosterTab(), new UpgradeTab() };
        private readonly List<Button> _tabButtons = new();
        private HiringBoard? _board;
        private RectTransform _content = null!;
        private Text _title = null!;
        private int _tab;
        private string _shown = "";
        private float _nextRefresh;

        public static bool IsOpen => _instance != null && _instance.gameObject.activeSelf;

        public static void Open(HiringBoard board, int tab = 0)
        {
            Player player = Player.m_localPlayer;
            if (player == null || GUIManager.IsHeadless())
                return;
            if (Vector3.Distance(player.transform.position, board.transform.position) > OpenRange)
                return;
            if (!PrivateArea.CheckAccess(board.transform.position))
                return;

            if (_instance == null)
                _instance = Create();
            _instance._board = board;
            _instance._tab = tab;
            _instance._shown = "";
            _instance._nextRefresh = 0f;
            _instance.gameObject.SetActive(true);
            GUIManager.BlockInput(true);
            VfhLog.D(LogCat.UI, "panel.open", ("board", board.Id), ("tab", tab));
        }

        public static void Close(string reason)
        {
            if (!IsOpen)
                return;
            _instance!.gameObject.SetActive(false);
            GUIManager.BlockInput(false);
            VfhLog.D(LogCat.UI, "panel.close", ("board", _instance._board != null ? _instance._board.Id : ""), ("reason", reason));
            _instance._board = null;
        }

        private static BoardPanel Create()
        {
            GameObject root = GUIManager.Instance.CreateWoodpanel(GUIManager.CustomGUIFront.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 720f, 560f, false);
            root.name = "VFH_BoardPanel";
            BoardPanel panel = root.AddComponent<BoardPanel>();

            panel._title = PanelUi.Text(root.transform, "$vfh_board", 0f, -40f, 640f, 28, bold: true);
            for (int i = 0; i < panel._tabs.Count; i++)
            {
                int index = i;
                panel._tabButtons.Add(PanelUi.Button(root.transform, panel._tabs[i].Title, -220f + i * 220f, -95f, 200f, 40f,
                    () => panel.SelectTab(index)));
            }
            panel._content = PanelUi.Fill(root.transform, "content");
            PanelUi.Button(root.transform, "$vfh_close", 300f, -30f, 80f, 34f, () => Close("button"));
            root.SetActive(false);
            PanelUi.Clicked += () =>
            {
                // Redraw on the next frame (the op has usually applied by then) instead of up to half a second later.
                if (_instance != null)
                    _instance._nextRefresh = 0f;
            };
            return panel;
        }

        private void SelectTab(int index)
        {
            _tab = index;
            _shown = "";
            _nextRefresh = 0f;
            VfhLog.D(LogCat.UI, "panel.tab", ("tab", _tabs[index].Title));
        }

        private void Update()
        {
            if (_board == null || _board.Zdo == null)
            {
                Close("board gone");
                return;
            }
            Player player = Player.m_localPlayer;
            if (player == null || player.IsDead() || Vector3.Distance(player.transform.position, _board.transform.position) > CloseRange)
            {
                Close("walked away");
                return;
            }
            if (Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + RefreshSeconds;

            VfhLog.Guard(LogCat.UI, "panel.refresh_failed", () =>
            {
                IBoardTab tab = _tabs[_tab];
                string signature = _tab + "|" + tab.Signature(_board);
                if (signature == _shown)
                    return;
                _shown = signature;
                _title.text = Localization.instance.Localize("$vfh_board — $vfh_level ") + _board.Level;
                for (int i = 0; i < _tabButtons.Count; i++)
                    _tabButtons[i].GetComponentInChildren<Text>().color = i == _tab ? GUIManager.Instance.ValheimOrange : GUIManager.Instance.ValheimBeige;
                PanelUi.Clear(_content);
                tab.Build(_content, _board);
            }, ("board", _board.Id));
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                if (gameObject.activeSelf)
                    GUIManager.BlockInput(false);
                _instance = null;
            }
        }

        /// <summary>Esc (or gamepad B) closes the panel instead of opening the game menu.</summary>
        [HarmonyPatch(typeof(Menu), "Update")]
        private static class MenuPatch
        {
            private static bool Prefix()
            {
                long vfhStarted = System.Diagnostics.Stopwatch.GetTimestamp();
                try
                {
                    if (!IsOpen)
                        return true;
                    if (ZInput.GetKeyDown(KeyCode.Escape) || ZInput.GetButtonDown("JoyButtonB"))
                    {
                        Close("escape");
                        return false;
                    }
                    return true;
                }
                catch (Exception e)
                {
                    VfhLog.PatchFailed("BoardPanel.MenuUpdate", e);
                    return true;
                }
                finally
                {
                    VikingsForHire.Diagnostics.PerfCounters.Patch("BoardPanel.MenuUpdate", vfhStarted);
                }
            }
        }
    }
}

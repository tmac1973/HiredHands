using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings.Work.Trees;

namespace VikingsForHire.UI
{
    /// <summary>A tree patch sign's settings (Shift+E): its radius and what to plant there.</summary>
    internal sealed class TreePatchPanel : MonoBehaviour
    {
        private const float CloseRange = 8f;

        private static TreePatchPanel? _instance;
        private TreePatch? _patch;
        private RectTransform _content = null!;
        private string _shown = "";
        private float _nextRefresh;

        public static bool IsOpen => _instance != null && _instance.gameObject.activeSelf;

        public static void Open(TreePatch patch)
        {
            if (Player.m_localPlayer == null || GUIManager.IsHeadless())
                return;
            if (_instance == null)
                _instance = Create();
            _instance._patch = patch;
            _instance._shown = "";
            _instance._nextRefresh = 0f;
            _instance.gameObject.SetActive(true);
            GUIManager.BlockInput(true);
            VfhLog.D(LogCat.UI, "patch.open", ("pos", patch.transform.position));
        }

        public static void Close(string reason)
        {
            if (!IsOpen)
                return;
            _instance!.gameObject.SetActive(false);
            GUIManager.BlockInput(false);
            _instance._patch = null;
            VfhLog.D(LogCat.UI, "patch.close", ("reason", reason));
        }

        private static TreePatchPanel Create()
        {
            GameObject root = GUIManager.Instance.CreateWoodpanel(GUIManager.CustomGUIFront.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 460f, 300f, false);
            root.name = "VFH_TreePatchPanel";
            TreePatchPanel panel = root.AddComponent<TreePatchPanel>();
            panel._content = PanelUi.Fill(root.transform, "content");
            PanelUi.Button(root.transform, "$vfh_close", 170f, -30f, 80f, 34f, () => Close("button"));
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
            TreePatch? p = _patch;
            Player me = Player.m_localPlayer;
            if (p == null || p.Zdo == null || me == null || me.IsDead() ||
                Vector3.Distance(me.transform.position, p.transform.position) > CloseRange || ZInput.GetKeyDown(KeyCode.Escape))
            {
                Close(p == null ? "gone" : "walked away");
                return;
            }
            p.ShowRing();
            if (Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + 0.3f;
            (int saplings, int trees) = p.Count();
            string signature = $"{p.Radius}|{p.Kind}|{saplings}|{trees}";
            if (signature == _shown)
                return;
            _shown = signature;
            Build(p, saplings, trees);
        }

        // "Any", then each tree kind the cultivator can plant.
        private static List<string> Kinds() => new[] { "" }.Concat(TreeCatalog.All.Select(t => t.SaplingName)).ToList();

        private void Build(TreePatch p, int saplings, int trees)
        {
            PanelUi.Clear(_content);
            PanelUi.Text(_content, "$vfh_patch", 0f, -45f, 400f, 20, bold: true);
            PanelUi.Text(_content, "$vfh_patch_radius", -130f, -105f, 140f, 16, TextAnchor.MiddleLeft);
            PanelUi.Button(_content, "-", 20f, -105f, 40f, 32f, () => p.Set(p.Radius - 1f, p.Kind));
            PanelUi.Text(_content, $"{p.Radius:0} m", 85f, -105f, 80f, 17);
            PanelUi.Button(_content, "+", 150f, -105f, 40f, 32f, () => p.Set(p.Radius + 1f, p.Kind));

            PanelUi.Text(_content, "$vfh_patch_kind", -130f, -155f, 140f, 16, TextAnchor.MiddleLeft);
            List<string> kinds = Kinds();
            int i = Mathf.Max(0, kinds.IndexOf(p.Kind));
            PanelUi.Button(_content, TreePatch.KindName(p.Kind), 85f, -155f, 200f, 34f, () => p.Set(p.Radius, kinds[(i + 1) % kinds.Count]));

            PanelUi.Text(_content, Localization.instance.Localize("$vfh_patch_count", saplings.ToString(), trees.ToString()), 0f, -210f, 420f, 15, color: PanelUi.Dim);
            PanelUi.Text(_content, "$vfh_patch_hint", 0f, -245f, 420f, 13, color: PanelUi.Dim);
        }
    }
}

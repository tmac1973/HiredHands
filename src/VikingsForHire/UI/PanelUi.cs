using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace VikingsForHire.UI
{
    /// <summary>Small helpers over Jotunn's GUIManager so panel code reads as layout, not plumbing.</summary>
    internal static class PanelUi
    {
        public static readonly Color Good = new(0.6f, 1f, 0.6f);
        public static readonly Color Bad = new(1f, 0.45f, 0.45f);
        public static readonly Color Dim = new(0.75f, 0.75f, 0.75f);

        private static readonly Vector2 TopCenter = new(0.5f, 1f);

        /// <summary>Text anchored to the top centre of its parent; <paramref name="y"/> is negative downward.</summary>
        public static Text Text(Transform parent, string text, float x, float y, float width, int size = 18,
            TextAnchor align = TextAnchor.MiddleCenter, Color? color = null, bool bold = false)
        {
            GameObject go = GUIManager.Instance.CreateText(Localization.instance.Localize(text), parent, TopCenter, TopCenter,
                new Vector2(x, y), bold ? GUIManager.Instance.AveriaSerifBold : GUIManager.Instance.AveriaSerif, size,
                color ?? GUIManager.Instance.ValheimBeige, true, Color.black, width, size + 12, false);
            Text t = go.GetComponent<Text>();
            t.alignment = align;
            return t;
        }

        private const float ClickDebounceSeconds = 0.25f;
        private static float _lastClick = -1f;

        /// <summary>Fires after any panel button's action, so the panel can redraw straight away.</summary>
        public static event System.Action? Clicked;

        /// <summary>
        /// A panel button. Clicks within 0.25 s of the previous one are ignored (no double submits), and the hover/select
        /// sounds are off: the panel redraws after a click, and a fresh button appearing under the pointer would
        /// otherwise play its hover sound on top of the click.
        /// </summary>
        public static Button Button(Transform parent, string text, float x, float y, float width, float height, System.Action onClick)
        {
            GameObject go = GUIManager.Instance.CreateButton(Localization.instance.Localize(text), parent, TopCenter, TopCenter,
                new Vector2(x, y), width, height);
            ButtonSfx? sfx = go.GetComponent<ButtonSfx>();
            if (sfx != null)
            {
                sfx.m_enterSfxPrefab = null;
                sfx.m_enterSfxPrefabVibrationOnly = null;
                sfx.m_selectSfxPrefab = null;
                sfx.m_selectSfxPrefabVibrationOnly = null;
            }
            Button b = go.GetComponent<Button>();
            b.onClick.AddListener(() =>
            {
                if (Time.unscaledTime - _lastClick < ClickDebounceSeconds)
                    return;
                _lastClick = Time.unscaledTime;
                onClick();
                Clicked?.Invoke();
            });
            return b;
        }

        public static Image Icon(Transform parent, Sprite? sprite, float x, float y, float size)
        {
            var go = new GameObject("icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = TopCenter;
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(size, size);
            Image img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            return img;
        }

        /// <summary>An empty, full-size child used as a tab's content root.</summary>
        public static RectTransform Fill(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        /// <summary>Removes children now, not at the end of the frame, so old and new widgets never overlap.</summary>
        public static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(t.GetChild(i).gameObject);
        }
    }
}

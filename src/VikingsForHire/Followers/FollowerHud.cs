using System.Linq;
using System.Text;
using Jotunn.Managers;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Hirelings;
using VikingsForHire.Net;
using UnityEngine;
using UnityEngine.UI;

namespace VikingsForHire.Followers
{
    /// <summary>
    /// While the Command Stone is in hand: a small list on the left of the screen with your follower count (the
    /// server's, so far-away followers count too) against the stone's limit, and each follower near you with its
    /// follow mode and health.
    /// </summary>
    internal static class FollowerHud
    {
        private const float RefreshSeconds = 0.5f;
        private const float CountSeconds = 3f;
        private const float ListRange = 60f;

        private static Text? _text;
        private static float _nextRefresh;
        private static float _nextCount;

        public static void Tick()
        {
            bool show = StoneInput.StoneInHand && !GUIManager.IsHeadless() && Hud.instance != null;
            if (!show)
            {
                if (_text != null && _text.gameObject.activeSelf)
                    _text.gameObject.SetActive(false);
                return;
            }
            if (Time.unscaledTime >= _nextCount)
            {
                _nextCount = Time.unscaledTime + CountSeconds;
                FollowerServer.RequestCount();
            }
            if (Time.unscaledTime < _nextRefresh)
                return;
            _nextRefresh = Time.unscaledTime + RefreshSeconds;
            if (_text == null)
                _text = Create();
            _text.gameObject.SetActive(true);
            _text.text = Build();
        }

        private static Text Create()
        {
            GameObject go = GUIManager.Instance.CreateText("", GUIManager.CustomGUIFront.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                new Vector2(170f, 60f), GUIManager.Instance.AveriaSerifBold, 16, GUIManager.Instance.ValheimBeige, true, Color.black, 320f, 260f, false);
            go.name = "VFH_FollowerHud";
            Text t = go.GetComponent<Text>();
            t.alignment = TextAnchor.UpperLeft;
            t.raycastTarget = false;
            return t;
        }

        private static string Build()
        {
            Player me = Player.m_localPlayer;
            int quality = me.GetRightItem()?.m_quality ?? 1;
            int cap;
            try
            {
                cap = new LevelRules(DataStore.Current).StoneFollowerCap(quality);
            }
            catch (System.ArgumentOutOfRangeException)
            {
                cap = 0;
            }
            var sb = new StringBuilder();
            sb.Append(Localization.instance.Localize("$vfh_hud_header", quality.ToString(), FollowerServer.LastCount.ToString(), cap.ToString()));
            foreach (Hireling f in StoneInput.MyFollowers(me, ListRange).OrderBy(f => f.DisplayName))
            {
                sb.Append('\n').Append(f.DisplayName).Append(" — ")
                    .Append(Localization.instance.Localize($"$vfh_mode_{f.FollowMode.ToString().ToLowerInvariant()}"))
                    .Append(" — ").Append(Mathf.CeilToInt(f.Humanoid.GetHealth())).Append('/').Append(Mathf.CeilToInt(f.Humanoid.GetMaxHealth()));
                if (f.CargoInventory != null && f.Job is JobType.Woodcutter or JobType.Miner)
                    sb.Append(" — ").Append(f.CargoInventory.NrOfItems()).Append('/').Append(f.CargoSlots);
            }
            return sb.ToString();
        }
    }
}

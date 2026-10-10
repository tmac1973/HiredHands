using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Board;
using VikingsForHire.Config;
using VikingsForHire.Core;
using UnityEngine;
using UnityEngine.UI;

namespace VikingsForHire.UI
{
    /// <summary>Current → next level, what it unlocks, the cost with have/need per item, and the Upgrade button.</summary>
    internal sealed class UpgradeTab : IBoardTab
    {
        private const float RowHeight = 36f;

        public string Title => "$vfh_tab_upgrade";

        public string Signature(HiringBoard board)
        {
            int level = board.Level;
            Inventory? inv = Player.m_localPlayer?.GetInventory();
            string have = inv == null ? "" : string.Join(",", BoardUpgrade.Requirements(level).Select(r => r.Have(inv, board)));
            return $"{level}|{DataStore.Hash}|{have}|{BoardUpgrade.InProgress}";
        }

        public void Build(RectTransform root, HiringBoard board)
        {
            int level = board.Level;
            var rules = new LevelRules(DataStore.Current);
            if (level >= BoardUpgrade.MaxLevel)
            {
                PanelUi.Text(root, Localization.instance.Localize("$vfh_upgrade_level", level.ToString()), 0f, -150f, 560f, 24, bold: true);
                PanelUi.Text(root, "$vfh_upgrade_max", 0f, -200f, 560f, 20, color: PanelUi.Good);
                Benefits(root, rules, level, level, -250f);
                return;
            }

            int next = level + 1;
            PanelUi.Text(root, Localization.instance.Localize("$vfh_upgrade_to", level.ToString(), next.ToString()), 0f, -150f, 560f, 24, bold: true);
            float y = Benefits(root, rules, level, next, -195f);

            PanelUi.Text(root, Compat.CraftyBoxesCompat.Active ? "$vfh_upgrade_requires_nearby" : "$vfh_upgrade_requires", 0f, y - 10f, 560f, 20, bold: true);
            y -= 50f;
            Inventory? inv = Player.m_localPlayer?.GetInventory();
            List<UpgradeRequirement> reqs = BoardUpgrade.Requirements(level);
            foreach (UpgradeRequirement r in reqs)
            {
                int have = inv != null ? r.Have(inv, board) : 0;
                PanelUi.Icon(root, r.Item.m_itemData.GetIcon(), -200f, y, 32f);
                PanelUi.Text(root, r.Item.m_itemData.m_shared.m_name, -40f, y, 260f, 18, TextAnchor.MiddleLeft);
                PanelUi.Text(root, $"{have}/{r.Need}", 190f, y, 120f, 18, TextAnchor.MiddleRight, have >= r.Need ? PanelUi.Good : PanelUi.Bad);
                y -= RowHeight;
            }

            PanelUi.Text(root, "$vfh_upgrade_nonrefundable", 0f, y - 6f, 600f, 14, color: PanelUi.Dim);
            bool canAfford = inv != null && BoardUpgrade.CanAfford(inv, board, reqs);
            Button button = PanelUi.Button(root, BoardUpgrade.InProgress ? "$vfh_upgrade_waiting" : "$vfh_upgrade_button",
                0f, -455f, 220f, 44f, () => BoardUpgrade.TryUpgrade(board));
            button.interactable = canAfford && !BoardUpgrade.InProgress;
        }

        /// <summary>The things a level changes, "now → next". Returns the y below the last line.</summary>
        private static float Benefits(RectTransform root, LevelRules rules, int level, int next, float y)
        {
            // Both caps on one line: the tab has no room for another (five materials already reach the button).
            int wNow = rules.WorkerCap(level), wNext = rules.WorkerCap(next), cNow = rules.CombatCap(level), cNext = rules.CombatCap(next);
            string caps = VfhConfig.CombatHirelings.Value
                ? Localization.instance.Localize("$vfh_upgrade_caps", Change(wNow, wNext), Change(cNow, cNext))
                : Localization.instance.Localize("$vfh_upgrade_caps_workers", Change(wNow, wNext));
            PanelUi.Text(root, caps, 0f, y, 560f, 18, color: wNow == wNext && cNow == cNext ? PanelUi.Dim : PanelUi.Good);
            y -= 28f;
            Line(root, "$vfh_upgrade_maxlevel", rules.MaxHirelingLevel(level), rules.MaxHirelingLevel(next), ref y);
            Line(root, "$vfh_upgrade_radius", rules.MaxWorkRadius(level), rules.MaxWorkRadius(next), ref y);
            return y;
        }

        private static string Change(int now, int next) => now == next ? $"{now}" : $"{now} → {next}";

        private static void Line(RectTransform root, string label, float now, float next, ref float y)
        {
            string value = now == next ? $"{now:0}" : $"{now:0} → {next:0}";
            PanelUi.Text(root, Localization.instance.Localize(label) + "  " + value, 0f, y, 560f, 18,
                color: now == next ? PanelUi.Dim : PanelUi.Good);
            y -= 28f;
        }
    }
}

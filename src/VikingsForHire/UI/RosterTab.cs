using VikingsForHire.Board;
using UnityEngine;

namespace VikingsForHire.UI
{
    /// <summary>The hireling roster arrives in phase 06.</summary>
    internal sealed class RosterTab : IBoardTab
    {
        public string Title => "$vfh_tab_roster";

        public string Signature(HiringBoard board) => "stub";

        public void Build(RectTransform root, HiringBoard board) =>
            PanelUi.Text(root, "$vfh_tab_roster_empty", 0f, -200f, 560f, 18, color: PanelUi.Dim);
    }
}

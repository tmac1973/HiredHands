using VikingsForHire.Board;
using UnityEngine;

namespace VikingsForHire.UI
{
    /// <summary>Posting contracts arrives in phase 06.</summary>
    internal sealed class ContractsTab : IBoardTab
    {
        public string Title => "$vfh_tab_contracts";

        public string Signature(HiringBoard board) => "stub";

        public void Build(RectTransform root, HiringBoard board) =>
            PanelUi.Text(root, "$vfh_tab_contracts_soon", 0f, -200f, 560f, 18, color: PanelUi.Dim);
    }
}

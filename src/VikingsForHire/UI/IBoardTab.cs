using VikingsForHire.Board;
using UnityEngine;

namespace VikingsForHire.UI
{
    /// <summary>One tab of the board panel. Build is called whenever <see cref="Signature"/> changes (checked twice a second).</summary>
    internal interface IBoardTab
    {
        string Title { get; }

        /// <summary>A string that changes whenever what the tab shows would change.</summary>
        string Signature(HiringBoard board);

        void Build(RectTransform root, HiringBoard board);
    }
}

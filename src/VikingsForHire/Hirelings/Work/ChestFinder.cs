using System.Collections.Generic;
using VikingsForHire.Board;
using VikingsForHire.Compat;
using UnityEngine;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>
    /// Chests a hireling may deliver to: player-built containers within the radius, excluding the hiring board, carts,
    /// ships, private chests, anything excluded for compat, and chests someone has open right now.
    /// </summary>
    internal static class ChestFinder
    {
        private static readonly List<Piece> Pieces = new();

        public static List<Container> Find(Vector3 center, float radius)
        {
            var result = new List<Container>();
            Pieces.Clear();
            Piece.GetAllPiecesInRadius(center, radius, Pieces);
            foreach (Piece p in Pieces)
            {
                if (p == null || p.GetCreator() == 0L || p.GetComponent<HiringBoard>() != null || p.GetComponent<Vagon>() != null || p.GetComponent<Ship>() != null)
                    continue;
                Container c = p.GetComponentInChildren<Container>();
                if (c == null || c.GetInventory() == null || c.m_privacy == Container.PrivacySetting.Private || c.IsInUse() || ExcludedContainers.IsExcluded(c))
                    continue;
                result.Add(c);
            }
            return result;
        }
    }
}

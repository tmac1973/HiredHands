using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Board
{
    /// <summary>Result of the base check at a position; Pending while a client waits for the server's board registry.</summary>
    internal readonly struct PlacementVerdict
    {
        public readonly bool Pending;
        public readonly BaseCheckResult? Result;
        public readonly BaseCounts Counts;

        public PlacementVerdict(bool pending, BaseCheckResult? result, BaseCounts counts)
        {
            Pending = pending;
            Result = result;
            Counts = counts;
        }

        public bool Ok => !Pending && Result != null && Result.Ok;

        /// <summary>The message shown for the first unmet requirement.</summary>
        public string Message()
        {
            if (Pending)
                return Localization.instance.Localize("$vfh_base_checking");
            if (Result == null || Result.Ok)
                return "";
            BaseMissing m = Result.Missing[0];
            return Localization.instance.Localize(m.Token, Mathf.FloorToInt(m.Have).ToString(), Mathf.CeilToInt(m.Need).ToString(),
                Mathf.RoundToInt(VfhConfig.BaseCheckRadius.Value).ToString());
        }

        public string MissingTokens() => Pending ? "pending" : Result == null ? "" : string.Join(",", Result.Missing.Select(m => m.Kind));
    }

    internal static class PlacementCheck
    {
        private static readonly List<Piece> PieceBuffer = new();

        public static BaseRules Rules() => new(VfhConfig.RequiredWorkbenches.Value, VfhConfig.RequiredBeds.Value,
            VfhConfig.RequiredPieces.Value, VfhConfig.MinDistanceBetweenBoards.Value, VfhConfig.MaxBoardsPerWorld.Value);

        public static PlacementVerdict Evaluate(Vector3 position)
        {
            PieceBuffer.Clear();
            Piece.GetAllPiecesInRadius(position, VfhConfig.BaseCheckRadius.Value, PieceBuffer);
            int workbenches = 0, beds = 0, pieces = 0;
            foreach (Piece p in PieceBuffer)
            {
                if (p == null || p.GetCreator() == 0L)
                    continue;
                pieces++;
                CraftingStation station = p.GetComponent<CraftingStation>();
                if (station != null && station.m_name == "$piece_workbench")
                    workbenches++;
                if (p.GetComponent<Bed>() != null)
                    beds++;
            }

            bool known = BoardRegistry.TryQuery(position, out BoardRegistry.Answer answer);
            var counts = new BaseCounts(workbenches, beds, pieces, known ? answer.NearestDistance : null, known ? answer.Count : 0);
            return known
                ? new PlacementVerdict(false, BaseRequirement.Evaluate(counts, Rules()), counts)
                : new PlacementVerdict(true, null, counts);
        }

        public static bool IsBoard(Piece? piece) => piece != null && piece.gameObject.name.StartsWith(BoardZdo.PrefabName);
    }

    internal static class PlacementPatches
    {
        private const float HintSeconds = 4f;
        private const float CacheSeconds = 0.5f;

        private static Vector3 _cachedPos = new(float.MaxValue, 0, 0);
        private static float _cachedAt = -999f;
        private static PlacementVerdict _cached;
        private static float _lastHint;
        private static string _lastHintText = "";
        private static string _lastLogged = "";

        /// <summary>Turns the ghost red, with a hint, while the spot doesn't qualify as a base.</summary>
        [HarmonyPatch(typeof(Player), "UpdatePlacementGhost")]
        private static class GhostPatch
        {
            private static void Postfix(Player __instance)
            {
                if (__instance != Player.m_localPlayer || __instance.m_placementGhost == null ||
                    __instance.m_placementStatus != Player.PlacementStatus.Valid || !PlacementCheck.IsBoard(__instance.GetSelectedPiece()))
                    return;

                VfhLog.Guard(LogCat.Placement, "placement.ghost_failed", () =>
                {
                    Vector3 pos = __instance.m_placementGhost.transform.position;
                    if ((pos - _cachedPos).sqrMagnitude > 1f || Time.time - _cachedAt > CacheSeconds)
                    {
                        _cached = PlacementCheck.Evaluate(pos);
                        _cachedPos = pos;
                        _cachedAt = Time.time;
                        LogChange(_cached, pos);
                    }
                    if (_cached.Ok)
                        return;

                    __instance.m_placementStatus = Player.PlacementStatus.Invalid;
                    __instance.SetPlacementGhostValid(false);
                    // Repeat the hint only when it changes or every few seconds, so the message feed doesn't fill up.
                    string hint = _cached.Message();
                    if (hint != _lastHintText || Time.time - _lastHint > HintSeconds)
                    {
                        _lastHint = Time.time;
                        _lastHintText = hint;
                        __instance.Message(MessageHud.MessageType.TopLeft, hint);
                    }
                });
            }
        }

        /// <summary>Re-checks on the actual click, so a stale ghost status can never place a board.</summary>
        [HarmonyPatch(typeof(Player), nameof(Player.TryPlacePiece))]
        private static class PlacePatch
        {
            private static bool Prefix(Player __instance, Piece piece, ref bool __result)
            {
                if (!PlacementCheck.IsBoard(piece) || __instance.m_placementGhost == null)
                    return true;

                Vector3 pos = __instance.m_placementGhost.transform.position;
                PlacementVerdict verdict = PlacementCheck.Evaluate(pos);
                if (verdict.Ok)
                {
                    VfhLog.I(LogCat.Placement, "placement.allowed", ("pos", pos), ("workbenches", verdict.Counts.Workbenches),
                        ("beds", verdict.Counts.Beds), ("pieces", verdict.Counts.Pieces), ("nearestBoard", NearestOrNone(verdict.Counts)),
                        ("worldBoards", verdict.Counts.WorldBoardCount));
                    return true;
                }

                VfhLog.I(LogCat.Placement, "placement.blocked", ("pos", pos), ("missing", verdict.MissingTokens()),
                    ("workbenches", verdict.Counts.Workbenches), ("beds", verdict.Counts.Beds), ("pieces", verdict.Counts.Pieces),
                    ("nearestBoard", NearestOrNone(verdict.Counts)), ("worldBoards", verdict.Counts.WorldBoardCount));
                __instance.Message(MessageHud.MessageType.Center, verdict.Message());
                __result = false;
                return false;
            }
        }

        private static void LogChange(PlacementVerdict v, Vector3 pos)
        {
            string key = v.Ok ? "ok" : v.MissingTokens();
            if (key == _lastLogged)
                return;
            _lastLogged = key;
            VfhLog.D(LogCat.Placement, "placement.ghost", ("pos", pos), ("ok", v.Ok), ("missing", key),
                ("workbenches", v.Counts.Workbenches), ("beds", v.Counts.Beds), ("pieces", v.Counts.Pieces),
                ("nearestBoard", NearestOrNone(v.Counts)), ("worldBoards", v.Counts.WorldBoardCount));
        }

        private static object NearestOrNone(BaseCounts c) => c.NearestBoardDistance.HasValue ? c.NearestBoardDistance.Value : "none";
    }
}

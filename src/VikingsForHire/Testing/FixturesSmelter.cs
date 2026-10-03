using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using VikingsForHire.Board;
using VikingsForHire.Compat;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using VikingsForHire.Hirelings.Work;
using UnityEngine;

namespace VikingsForHire.Testing
{
    /// <summary>Phase 10 (smelter) fixtures and checks.</summary>
    internal static class FixturesSmelter
    {
        public static void Register()
        {
            Fixtures.Add("stations", "<prefab> [prefab]… - build these stations in a ring 8 m around the nearest board (e.g. smelter smelter charcoal_kiln)", Stations);

            TestHarness.RegisterCheck("station", "<prefab> <ore_ratio|fuel_ratio|queue|fuel|processed> - the lowest value among those stations within 30 m of the nearest board", args =>
            {
                string prefab = args.ElementAtOrDefault(0) ?? "smelter";
                string field = args.ElementAtOrDefault(1) ?? "ore_ratio";
                HiringBoard board = FixturesWork.Board();
                var found = StationSurvey.Find(board.transform.position, 30f).Where(s => Utils.GetPrefabName(s.gameObject) == prefab).ToList();
                if (found.Count == 0)
                    return "error: no " + prefab + " near the board";
                float value = found.Min(s =>
                {
                    var st = StationSurvey.State(s, board.transform.position);
                    return field switch
                    {
                        "ore_ratio" => st.OreRatio,
                        "fuel_ratio" => st.FuelRatio,
                        "queue" => st.Queue,
                        "fuel" => st.Fuel,
                        "processed" => StationSurvey.ProcessedWaiting(s),
                        _ => throw new ArgumentException("unknown field " + field),
                    };
                });
                return value.ToString("0.##", CultureInfo.InvariantCulture);
            });
            TestHarness.RegisterCheck("azu_loaded", "- whether AzuAutoStore is installed", _ => AzuAutoStoreCompat.IsLoaded ? "true" : "false");
        }

        private static IEnumerator Stations(string[] args)
        {
            HiringBoard board = FixturesWork.Board();
            for (int i = 0; i < args.Length; i++)
            {
                // Round the sides and back of the board, away from the drop pile in front.
                float step = args.Length > 1 ? 180f / (args.Length - 1) : 0f;
                float angle = 90f + i * step;
                Vector3 pos = board.transform.position + Quaternion.Euler(0f, angle, 0f) * board.transform.forward * 8f;
                GameObject go = FixturesWork.Spawn(args[i], pos, "");
                go.transform.rotation = Quaternion.LookRotation(board.transform.position - pos);
                Piece piece = go.GetComponent<Piece>();
                if (piece != null && Player.m_localPlayer != null)
                {
                    piece.m_creator = Player.m_localPlayer.GetPlayerID();
                    go.GetComponent<ZNetView>().GetZDO().Set(ZDOVars.s_creator, piece.m_creator);
                }
                VfhLog.I(LogCat.Test, "fixture.station", ("prefab", args[i]), ("pos", go.transform.position));
            }
            yield return new WaitForSeconds(0.5f);
        }
    }
}

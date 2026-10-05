using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Config;
using VikingsForHire.Core;
using UnityEngine;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>
    /// The processing stations a smelter hireling looks after: Smelter components on player-built pieces in its work
    /// radius whose prefab is in jobs.Smelter.stations. Their state is read live from each station's ZDO.
    /// </summary>
    internal static class StationSurvey
    {
        private static readonly List<Piece> Pieces = new();

        public static List<Smelter> Find(Vector3 center, float radius)
        {
            var allowed = new HashSet<string>(DataStore.Current.Jobs.TryGetValue(JobType.Smelter, out var j) ? j.Stations : new List<string>());
            var result = new List<Smelter>();
            Pieces.Clear();
            Piece.GetAllPiecesInRadius(center, radius, Pieces);
            foreach (Piece p in Pieces)
            {
                if (p == null || p.GetCreator() == 0L || !allowed.Contains(Utils.GetPrefabName(p.gameObject)))
                    continue;
                Smelter s = p.GetComponentInChildren<Smelter>();
                if (s != null && s.m_nview != null && s.m_nview.IsValid())
                    result.Add(s);
            }
            return result;
        }

        public static string Id(Smelter s) => s.GetInstanceID().ToString();

        public static StationState State(Smelter s, Vector3 from)
        {
            ZDO zdo = s.m_nview.GetZDO();
            return new StationState
            {
                Id = Id(s),
                Prefab = Utils.GetPrefabName(s.gameObject),
                Distance = Vector3.Distance(from, s.transform.position),
                Queue = zdo.GetInt(ZDOVars.s_queued),
                MaxOre = s.m_maxOre,
                Fuel = zdo.GetFloat(ZDOVars.s_fuel),
                MaxFuel = s.m_fuelItem != null ? s.m_maxFuel : 0,
                FuelItem = s.m_fuelItem != null ? s.m_fuelItem.gameObject.name : "",
                Inputs = s.m_conversion.Where(c => c.m_from != null).Select(c => c.m_from.gameObject.name).Distinct().ToList(),
            };
        }

        /// <summary>Finished items waiting inside the station (stacking stations hold them until a stack is full).</summary>
        public static int ProcessedWaiting(Smelter s) => s.m_nview.GetZDO().GetInt(ZDOVars.s_spawnAmount);

        public static HashSet<string> Outputs(Smelter s) =>
            new(s.m_conversion.Where(c => c.m_to != null).Select(c => c.m_to.gameObject.name));

        /// <summary>
        /// Finished products a smelter hands in: outputs of any configured station type (bars, eitr…) that no
        /// configured station also burns or processes. Coal is a kiln's output but a smelter's fuel, so it's kept as
        /// supply, not delivered.
        /// </summary>
        public static HashSet<string> Products()
        {
            if (_productsHash == DataStore.Hash && _products != null)
                return _products;
            var outputs = new HashSet<string>();
            var consumed = new HashSet<string>();
            if (DataStore.Current.Jobs.TryGetValue(JobType.Smelter, out var job) && ZNetScene.instance != null)
                foreach (string name in job.Stations)
                {
                    Smelter? s = ZNetScene.instance.GetPrefab(name)?.GetComponentInChildren<Smelter>();
                    if (s == null)
                        continue;
                    outputs.UnionWith(Outputs(s));
                    consumed.UnionWith(s.m_conversion.Where(c => c.m_from != null).Select(c => c.m_from.gameObject.name));
                    if (s.m_fuelItem != null)
                        consumed.Add(s.m_fuelItem.gameObject.name);
                }
            outputs.ExceptWith(consumed);
            _products = outputs;
            _productsHash = DataStore.Hash;
            return outputs;
        }

        /// <summary>What any configured station burns or processes (so it's supply, never delivered as a product).</summary>
        public static HashSet<string> Consumed()
        {
            if (_consumedHash == DataStore.Hash && _consumed != null)
                return _consumed;
            var consumed = new HashSet<string>();
            if (DataStore.Current.Jobs.TryGetValue(JobType.Smelter, out var job) && ZNetScene.instance != null)
                foreach (string name in job.Stations)
                {
                    Smelter? s = ZNetScene.instance.GetPrefab(name)?.GetComponentInChildren<Smelter>();
                    if (s == null)
                        continue;
                    consumed.UnionWith(s.m_conversion.Where(c => c.m_from != null).Select(c => c.m_from.gameObject.name));
                    if (s.m_fuelItem != null)
                        consumed.Add(s.m_fuelItem.gameObject.name);
                }
            _consumed = consumed;
            _consumedHash = DataStore.Hash;
            return consumed;
        }

        private static HashSet<string>? _consumed;
        private static string _consumedHash = "";

        private static HashSet<string>? _products;
        private static string _productsHash = "";
    }
}

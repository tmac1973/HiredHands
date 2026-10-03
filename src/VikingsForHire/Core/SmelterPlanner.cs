using System;
using System.Collections.Generic;
using System.Linq;

namespace VikingsForHire.Core
{
    /// <summary>A processing station's live state, read fresh every cycle.</summary>
    public sealed class StationState
    {
        public string Id { get; set; } = "";
        public string Prefab { get; set; } = "";
        public float Distance { get; set; }
        public int Queue { get; set; }
        public int MaxOre { get; set; }
        public float Fuel { get; set; }
        public int MaxFuel { get; set; }

        /// <summary>Fuel item prefab, or empty when the station needs no fuel (charcoal kiln).</summary>
        public string FuelItem { get; set; } = "";

        /// <summary>Input prefabs the station converts.</summary>
        public IReadOnlyList<string> Inputs { get; set; } = Array.Empty<string>();

        public float OreRatio => MaxOre <= 0 ? 1f : (float)Queue / MaxOre;
        public float FuelRatio => MaxFuel <= 0 || FuelItem.Length == 0 ? 1f : Fuel / MaxFuel;
        public int OreSpace => Math.Max(0, MaxOre - Queue);

        // Vanilla refuses fuel once it's above max - 1, so whole units only.
        public int FuelSpace => FuelItem.Length == 0 ? 0 : Math.Max(0, MaxFuel - (int)Math.Ceiling(Fuel));
    }

    public readonly struct LoadTask
    {
        public readonly string StationId;
        public readonly string Prefab;
        public readonly int Amount;
        public readonly bool IsFuel;

        public LoadTask(string stationId, string prefab, int amount, bool isFuel)
        {
            StationId = stationId;
            Prefab = prefab;
            Amount = amount;
            IsFuel = isFuel;
        }

        public override string ToString() => $"{StationId}:{Prefab}x{Amount}{(IsFuel ? "(fuel)" : "")}";
    }

    public sealed class SmelterPlan
    {
        /// <summary>Stations to load, most urgent first, with what goes in each.</summary>
        public IReadOnlyList<LoadTask> Loads { get; }

        /// <summary>What to take from chests first (prefab → count); the rest comes from cargo.</summary>
        public IReadOnlyDictionary<string, int> Fetch { get; }

        /// <summary>Stations that need service but have nothing available to load.</summary>
        public IReadOnlyList<string> Starved { get; }

        public SmelterPlan(IReadOnlyList<LoadTask> loads, IReadOnlyDictionary<string, int> fetch, IReadOnlyList<string> starved)
        {
            Loads = loads;
            Fetch = fetch;
            Starved = starved;
        }

        public bool Idle => Loads.Count == 0;
    }

    /// <summary>
    /// Decides which stations a smelter hireling tops up and what it must fetch for that. A station needs service when
    /// its ore or fuel is below the threshold fraction of max; then both are filled up towards max. The most empty
    /// station goes first. Cargo is used before chest stock, and fetching is limited by free cargo slots, shared
    /// between ore and fuel.
    /// </summary>
    public static class SmelterPlanner
    {
        public static SmelterPlan Plan(IReadOnlyList<StationState> stations, IReadOnlyDictionary<string, int> chestStock,
            IReadOnlyDictionary<string, int> cargo, float threshold, int freeSlots, Func<string, int> maxStack)
        {
            var stock = new Dictionary<string, int>(chestStock.Where(s => s.Value > 0).ToDictionary(s => s.Key, s => s.Value));
            var carried = new Dictionary<string, int>(cargo.Where(s => s.Value > 0).ToDictionary(s => s.Key, s => s.Value));
            var fetch = new Dictionary<string, int>();
            var loads = new List<LoadTask>();
            var starved = new List<string>();
            int slotsLeft = Math.Max(0, freeSlots);

            int Available(string prefab) => Get(carried, prefab) + Get(stock, prefab);

            // Takes up to n of a prefab, cargo first, then chests while cargo slots allow; returns how many.
            int Take(string prefab, int n)
            {
                int fromCargo = Math.Min(n, Get(carried, prefab));
                carried[prefab] = Get(carried, prefab) - fromCargo;
                int want = n - fromCargo;
                if (want <= 0)
                    return fromCargo;
                int stack = Math.Max(1, maxStack(prefab));
                int already = Get(fetch, prefab);
                // Room in the partial stack this plan already fetches, plus whole new slots.
                int partialRoom = already % stack == 0 ? 0 : stack - already % stack;
                int capacity = partialRoom + slotsLeft * stack;
                int fromChest = Math.Min(want, Math.Min(Get(stock, prefab), capacity));
                if (fromChest > 0)
                {
                    stock[prefab] = Get(stock, prefab) - fromChest;
                    fetch[prefab] = already + fromChest;
                    int newSlots = Math.Max(0, (int)Math.Ceiling((fromChest - partialRoom) / (double)stack));
                    slotsLeft -= Math.Min(slotsLeft, newSlots);
                }
                return fromCargo + fromChest;
            }

            foreach (StationState s in stations
                         .Where(s => s.OreRatio < threshold || s.FuelRatio < threshold)
                         .OrderBy(s => Math.Min(s.OreRatio, s.FuelRatio)).ThenBy(s => s.Distance).ThenBy(s => s.Id, StringComparer.Ordinal))
            {
                // The input with the most on hand; a station takes one kind at a time from us.
                string input = s.Inputs.OrderByDescending(Available).ThenBy(i => i, StringComparer.Ordinal).FirstOrDefault() ?? "";
                int oreWant = input.Length > 0 ? Math.Min(s.OreSpace, Available(input)) : 0;
                int fuelWant = s.FuelItem.Length > 0 ? Math.Min(s.FuelSpace, Available(s.FuelItem)) : 0;

                // Ore without fuel is useless, and so is fuel without ore, unless the station already has the other.
                bool hasFuel = s.FuelItem.Length == 0 || s.Fuel >= 1f || fuelWant > 0;
                bool hasOre = s.Queue > 0 || oreWant > 0;
                if (!hasFuel || !hasOre || (oreWant == 0 && fuelWant == 0))
                {
                    starved.Add(s.Id);
                    continue;
                }

                // Take ore and fuel a stack at a time, alternating, so limited cargo space is shared between them
                // instead of filling up with ore and leaving no room for the fuel to burn it.
                int ore = 0, fuel = 0;
                bool progress = true;
                while (progress && (ore < oreWant || fuel < fuelWant))
                {
                    progress = false;
                    if (ore < oreWant)
                    {
                        int t = Take(input, Math.Min(Math.Max(1, maxStack(input)), oreWant - ore));
                        ore += t;
                        progress |= t > 0;
                    }
                    if (fuel < fuelWant)
                    {
                        int t = Take(s.FuelItem, Math.Min(Math.Max(1, maxStack(s.FuelItem)), fuelWant - fuel));
                        fuel += t;
                        progress |= t > 0;
                    }
                }
                if (ore > 0)
                    loads.Add(new LoadTask(s.Id, input, ore, false));
                if (fuel > 0)
                    loads.Add(new LoadTask(s.Id, s.FuelItem, fuel, true));
                if (ore == 0 && fuel == 0)
                    starved.Add(s.Id);
            }
            return new SmelterPlan(loads, fetch, starved);
        }

        private static int Get(Dictionary<string, int> d, string key) => d.TryGetValue(key, out int v) ? v : 0;
    }
}

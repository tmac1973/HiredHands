using System.Collections.Generic;
using System.Linq;
using BepInEx.Bootstrap;
using VikingsForHire.Config;
using VikingsForHire.Core.Chores;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Compat
{
    /// <summary>
    /// Chores another installed mod already does, which the Steward then leaves alone: PetPantry (animals eat straight
    /// from chests) and Torches Eternal (every fireplace, torch and hot tub stays full). AzuAreaRepair and RepairStation
    /// don't count: the first only widens a repair a player makes, the second repairs items.
    /// </summary>
    internal static class StewardCompat
    {
        private static readonly (ChoreKind Kind, string Guid, string Name)[] Covering =
        {
            (ChoreKind.Animals, "Azumatt.PetPantry", "PetPantry"),
            (ChoreKind.Fires, "Xenofell.TorchesEternal", "TorchesEternal"),
        };

        private static Dictionary<ChoreKind, string>? _handled;

        /// <summary>The installed mod that covers this chore, or null.</summary>
        public static string? HandledBy(ChoreKind kind)
        {
            if (VfhConfig.StewardIgnoreOtherMods.Value)
                return null;
            if (_handled == null)
            {
                _handled = Covering.Where(c => Chainloader.PluginInfos.ContainsKey(c.Guid)).ToDictionary(c => c.Kind, c => c.Name);
                VfhLog.I(LogCat.Smelter, "steward.compat",
                    ("covered", _handled.Count == 0 ? "none" : string.Join(",", _handled.Select(h => $"{ChoreKeys.Key(h.Key)}={h.Value}"))));
            }
            return _handled.TryGetValue(kind, out string name) ? name : null;
        }
    }
}

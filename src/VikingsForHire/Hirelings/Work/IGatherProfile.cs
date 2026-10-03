using System.Collections.Generic;
using UnityEngine;

namespace VikingsForHire.Hirelings.Work
{
    /// <summary>What a gathering job harvests and picks up. The shared GatherBehaviour does the walking and swinging.</summary>
    internal interface IGatherProfile
    {
        string Status { get; }

        /// <summary>Harvestable things within <paramref name="radius"/> of <paramref name="center"/> (the board).</summary>
        IEnumerable<Component> Candidates(Vector3 center, float radius);

        /// <summary>Still worth harvesting: alive, within the tool's tier, and allowed (e.g. not next to buildings).</summary>
        bool IsValid(Component target, Hireling hireling, out string reason);

        /// <summary>How close to stand while working on it.</summary>
        float StandOff(Component target);

        /// <summary>Damage for one swing of the hireling's tool (chop or pickaxe).</summary>
        HitData.DamageTypes SwingDamage(HitData.DamageTypes toolDamage, float gatherMult);

        HashSet<string> PickupItems { get; }
    }
}

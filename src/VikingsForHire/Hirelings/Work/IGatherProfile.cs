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

        /// <summary>Still worth harvesting: alive and within the tool's tier. Cheap; checked every tick.</summary>
        bool IsValid(Component target, Hireling hireling, out string reason);

        /// <summary>
        /// Checked once when picking a target: is it safe to harvest (e.g. a tree can't fall on buildings)? For a tree,
        /// <paramref name="fellDir"/> is the direction it must be felled; the hireling stands on the opposite side.
        /// </summary>
        bool Plan(Component target, out Vector3? fellDir, out string reason);

        /// <summary>Work order: lower ranks are always done first, nearest first within a rank.</summary>
        int Rank(Component target);

        /// <summary>
        /// How far past the work radius this may still be worked (e.g. a log that rolled out of the area after its tree
        /// was felled inside it). 0 for things that must be inside the radius.
        /// </summary>
        float ExtraReach(Component target);

        /// <summary>
        /// The collider a swing from <paramref name="from"/> hits (a rock's nearest intact chunk, a tree's trunk) and
        /// the point to walk to and face. Null when nothing hittable is left.
        /// </summary>
        Collider? Aim(Component target, Vector3 from, out Vector3 point);

        /// <summary>How close to stand while working on it.</summary>
        float StandOff(Component target);

        /// <summary>Damage for one swing of the hireling's tool (chop or pickaxe).</summary>
        HitData.DamageTypes SwingDamage(HitData.DamageTypes toolDamage, float gatherMult);

        HashSet<string> PickupItems { get; }
    }
}

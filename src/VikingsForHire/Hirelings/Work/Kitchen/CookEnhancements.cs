using System.Collections.Generic;
using VikingsForHire.Core.Diagnostics;
using UnityEngine;
using static VikingsForHire.Core.Enhancements.EnhancementId;
using Random = UnityEngine.Random;

namespace VikingsForHire.Hirelings.Work.Kitchen
{
    /// <summary>
    /// The Cook's enhancements (plan/enhancements phase 10), used by CraftChore, StovesChore and WorkContext: quicker
    /// crafting and loading (Quick hands, Head chef), ingredients saved (Frugal, Head chef), extra output (Double batch,
    /// Head chef; Chef's touch and Banquet make more food from the same ingredients), the oven kept fuelled (Keeper of
    /// stoves), two stations at once (Two stations), chests further out (Pantry sense).
    /// </summary>
    internal static class CookEnhancements
    {
        /// <summary>Its crafting and loading pace multiplier.</summary>
        public static float StepMult(Hireling h) =>
            (1f - h.Enh.Value(QuickHands, "speed")) * (1f - h.Enh.Value(HeadChef, "speed"));

        /// <summary>The ingredients a craft actually uses: each unit has Frugal's (plus Head chef's) chance of being saved.</summary>
        public static Dictionary<string, int> Uses(Hireling h, IReadOnlyDictionary<string, int> inputs)
        {
            var uses = new Dictionary<string, int>();
            float save = h.Enh.Value(Frugal, "chance") + h.Enh.Value(HeadChef, "saveChance");
            int saved = 0;
            foreach (KeyValuePair<string, int> kv in inputs)
            {
                int n = kv.Value;
                if (save > 0f)
                    for (int i = 0; i < kv.Value; i++)
                        if (Random.value < save)
                            n--;
                saved += kv.Value - n;
                uses[kv.Key] = n;
            }
            if (saved > 0)
                EnhancementEvents.Fire(h, "Frugal", LogCat.Work, ("saved", saved));
            return uses;
        }

        /// <summary>
        /// How many a craft makes: one batch, maybe one more batch (Double batch, Head chef), and for food a share more
        /// (Chef's touch, Banquet: each unit has that chance of one more).
        /// </summary>
        public static int Makes(Hireling h, int amount, bool food)
        {
            int n = amount;
            float extra = h.Enh.Value(DoubleBatch, "chance") + h.Enh.Value(HeadChef, "extraChance");
            if (extra > 0f && Random.value < extra)
            {
                n += amount;
                EnhancementEvents.Fire(h, "DoubleBatch", LogCat.Work, ("extra", amount));
            }
            float share = food ? h.Enh.Value(ChefsTouch, "share") + h.Enh.Value(Banquet, "share") : 0f;
            if (share > 0f)
            {
                int more = 0;
                for (int i = 0; i < amount; i++)
                    if (Random.value < share)
                        more++;
                if (more > 0)
                    EnhancementEvents.Fire(h, "ChefsTouch", LogCat.Work, ("extra", more));
                n += more;
            }
            return n;
        }

        /// <summary>Keeper of stoves: the oven is fuelled whenever it isn't full (from half otherwise).</summary>
        public static float FuelBelow(Hireling h) => h.Enh.Has(KeeperOfStoves) ? 0.999f : 0.5f;

        /// <summary>Two stations: it may start at another stove while one is busy.</summary>
        public static bool TwoAtOnce(Hireling h) => h.Enh.Has(Core.Enhancements.EnhancementId.TwoStations);

        /// <summary>Pantry sense: how far it takes ingredients from (a multiple of its radius).</summary>
        public static float PantryMult(Hireling h) => h.Job == Core.JobType.Cook && h.Enh.Has(PantrySense) ? h.Enh.Value(PantrySense, "rangeMult") : 1f;
    }
}

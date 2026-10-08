using System.Collections.Generic;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>
    /// When attacks really land, learnt from watching them: the hit time read from an attack clip's events is right for
    /// some creatures (Greydwarf Brutes) and up to a second off for others (a Troll's slap), and modded creatures could be
    /// anything. Each hit that lands (or is blocked) corrects the estimate for that creature and attack, shared by all
    /// hirelings on this machine.
    /// </summary>
    internal static class HitTiming
    {
        private const float Rate = 0.5f;
        private const float MaxCorrection = 2f;
        private static readonly Dictionary<string, float> Offsets = new();

        public static string Key(Character attacker, string weapon) => Utils.GetPrefabName(attacker.gameObject) + "/" + weapon;

        public static float Offset(string key) => Offsets.TryGetValue(key, out float o) ? o : 0f;

        /// <summary>A hit from this attack landed <paramref name="error"/> seconds after the (corrected) estimate.</summary>
        public static void Learn(string key, float error)
        {
            float o = Offset(key) + error * Rate;
            o = o < -MaxCorrection ? -MaxCorrection : o > MaxCorrection ? MaxCorrection : o;
            Offsets[key] = o;
            VfhLog.D(LogCat.Combat, "defense.timing", ("attack", key), ("error", error), ("offset", o));
        }
    }
}

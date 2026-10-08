using HarmonyLib;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Testing
{
    /// <summary>
    /// While tests run, the tester is out of the fight: in ghost mode (enemies ignore you) and taking no hits at all.
    /// Vanilla's god and ghost modes only stop you dying: a stray area attack still lands, staggers and knocks you
    /// about, and a death sends you back to your bed, away from the test (and ghost mode ends with it).
    /// </summary>
    internal static class TestProtection
    {
        public static bool On { get; private set; }

        /// <summary>On at each test's start (again, in case a death reset ghost mode); off once the test queue is done.</summary>
        public static void Set(bool on)
        {
            Player? player = Player.m_localPlayer;
            if (player != null)
                player.SetGhostMode(on);
            if (On != on)
                VfhLog.I(LogCat.Test, "test.protect", ("on", on));
            On = on;
        }

        // Runs on the target's owner (your own game for you), before anything else looks at the hit.
        [HarmonyPatch(typeof(Character), "RPC_Damage")]
        private static class NoHitsOnTester
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(Character __instance) => !(On && __instance == Player.m_localPlayer);
        }
    }
}

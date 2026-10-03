using System.Linq;
using HarmonyLib;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// Hirelings and players don't physically collide, so a hireling wandering past doesn't shove you around (and you
    /// can't shove them). Raycasts still hit them, so hover, E and combat work as before. Applied on every client for
    /// every player/hireling pair, from whichever of the two appears second.
    /// </summary>
    internal static class PlayerCollision
    {
        public static void IgnoreAllPlayers(Hireling hireling)
        {
            foreach (Player p in Player.GetAllPlayers())
                Ignore(p, hireling);
        }

        private static void Ignore(Player player, Hireling hireling)
        {
            if (player == null || hireling == null)
                return;
            var mine = hireling.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger).ToArray();
            foreach (Collider pc in player.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger))
                foreach (Collider hc in mine)
                    Physics.IgnoreCollision(pc, hc, true);
        }

        [HarmonyPatch(typeof(Player), "Awake")]
        private static class PlayerAwakePatch
        {
            private static void Postfix(Player __instance) =>
                VfhLog.Guard(LogCat.Hireling, "collision.player_failed", () =>
                {
                    foreach (Hireling h in Hireling.Loaded)
                        Ignore(__instance, h);
                });
        }
    }
}

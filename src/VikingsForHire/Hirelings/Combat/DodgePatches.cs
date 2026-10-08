using HarmonyLib;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>
    /// Hirelings roll like players: InDodge is true while the animator plays the roll (so vanilla applies its root motion
    /// and refuses blocking and attacking meanwhile), and IsDodgeInvincible while the roll's invincibility lasts, synced
    /// through the ZDO (the key players use) so the attacker's machine, the projectile owner's and ours all let the hit
    /// miss. Other characters are left to vanilla.
    /// </summary>
    [HarmonyPatch]
    internal static class DodgePatches
    {
        private static readonly int DodgeTag = ZSyncAnimation.GetHash("dodge");

        public static bool AnimInDodge(Character c) =>
            c.m_animator != null && (c.m_animator.GetBool(DodgeTag) || c.GetNextOrCurrentAnimHash() == DodgeTag);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), nameof(Character.InDodge))]
        private static void InDodge(Character __instance, ref bool __result)
        {
            if (!__result && __instance.m_baseAI is HirelingAI)
                __result = AnimInDodge(__instance);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Character), nameof(Character.IsDodgeInvincible))]
        private static void IsDodgeInvincible(Character __instance, ref bool __result)
        {
            if (__result || __instance.m_baseAI is not HirelingAI ai || __instance.m_nview == null || !__instance.m_nview.IsValid())
                return;
            __result = __instance.m_nview.IsOwner() ? ai.Dodge.Invincible : __instance.m_nview.GetZDO().GetBool(ZDOVars.s_dodgeinv);
        }
    }
}

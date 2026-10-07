using UnityEngine;

namespace VikingsForHire.Hirelings.Combat
{
    /// <summary>
    /// How hard and when an enemy's attack will land, from what every machine has: the weapon in its right hand (synced
    /// through its ZDO, and monsters switch to the weapon of the attack they're making) and its animator (the attack
    /// clip's hit event). Never Humanoid.m_currentAttack, which only exists on the enemy owner's machine.
    /// </summary>
    internal static class AttackEstimate
    {
        /// <summary>Assumed time to the hit when the clip has no hit event.</summary>
        public const float UntimedSeconds = 0.5f;

        private static readonly int RightItemHash = ZDOVars.s_rightItem;

        public static ItemDrop.ItemData.SharedData? Weapon(Character attacker)
        {
            if (attacker is Humanoid h)
            {
                int hash = h.m_nview != null && h.m_nview.IsValid() ? h.m_nview.GetZDO().GetInt(RightItemHash) : 0;
                if (hash != 0 && ObjectDB.instance != null && ObjectDB.instance.GetItemPrefab(hash) is GameObject go &&
                    go.GetComponent<ItemDrop>() is ItemDrop drop)
                    return drop.m_itemData.m_shared;
                if (h.GetCurrentWeapon() is ItemDrop.ItemData w)
                    return w.m_shared;
            }
            return null;
        }

        /// <summary>Damage after <paramref name="armor"/>, as vanilla works it out (stars, the world's enemy damage rate, the attack's multiplier).</summary>
        public static float Damage(Character attacker, ItemDrop.ItemData.SharedData weapon, float armor)
        {
            HitData.DamageTypes d = weapon.m_damages.Clone();
            float levelFactor = 1f + Mathf.Max(0, attacker.GetLevel() - 1) * 0.5f; // Attack.GetLevelDamageFactor
            float mult = levelFactor * Game.m_enemyDamageRate * (weapon.m_attack != null ? weapon.m_attack.m_damageMultiplier : 1f);
            d.Modify(mult);
            d.ApplyArmor(armor);
            return d.GetTotalDamage();
        }

        public static bool Area(ItemDrop.ItemData.SharedData weapon) =>
            weapon.m_attack != null && (weapon.m_attack.m_attackType == Attack.AttackType.Area || weapon.m_attack.m_spawnOnTrigger != null);

        /// <summary>
        /// Seconds from now to the hit, from the attack clip's hit event ("Hit" or "OnAttackTrigger") and how far the
        /// state has played; null when there's no such event ahead.
        /// </summary>
        public static float? SecondsToHit(Animator animator)
        {
            bool next = animator.IsInTransition(0);
            AnimatorStateInfo state = next ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            AnimatorClipInfo[] clips = next ? animator.GetNextAnimatorClipInfo(0) : animator.GetCurrentAnimatorClipInfo(0);
            if (clips.Length == 0 || state.length <= 0f)
                return null;
            AnimationClip clip = clips[0].clip;
            if (clip == null || clip.length <= 0f)
                return null;
            float played = state.normalizedTime % 1f;
            float speed = Mathf.Max(0.05f, animator.speed);
            foreach (AnimationEvent e in clip.events)
            {
                if (e.functionName != "Hit" && e.functionName != "OnAttackTrigger")
                    continue;
                float at = e.time / clip.length;
                if (at >= played)
                    return (at - played) * state.length / speed;
            }
            return null;
        }
    }
}

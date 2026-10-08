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
        public static float Damage(Character attacker, ItemDrop.ItemData.SharedData weapon, float armor) =>
            Types(attacker, weapon, armor).GetTotalDamage();

        /// <summary>As <see cref="Damage"/>, by damage type (a shield blocks some types and not others).</summary>
        public static HitData.DamageTypes Types(Character attacker, ItemDrop.ItemData.SharedData weapon, float armor)
        {
            HitData.DamageTypes d = weapon.m_damages.Clone();
            float levelFactor = 1f + Mathf.Max(0, attacker.GetLevel() - 1) * 0.5f; // Attack.GetLevelDamageFactor
            d.Modify(levelFactor * (weapon.m_attack != null ? weapon.m_attack.m_damageMultiplier : 1f));
            return Taken(d, armor, attacker.transform.position);
        }

        /// <summary>
        /// What reaches a hireling of <paramref name="armor"/>, in vanilla's order for a non-player: our armor first
        /// (DamagePatches), then the world's scaling (Character.RPC_Damage: difficulty and enemy damage rate; and as the
        /// hireling isn't a player, the enemy difficulty scale and player damage rate too).
        /// </summary>
        private static HitData.DamageTypes Taken(HitData.DamageTypes d, float armor, Vector3 at)
        {
            d.ApplyArmor(armor);
            float scale = Game.m_enemyDamageRate * Game.m_playerDamageRate;
            if (Game.instance != null)
                scale *= Game.instance.GetDifficultyDamageScalePlayer(at) * Game.instance.GetDifficultyDamageScaleEnemy(at);
            d.Modify(scale);
            // Chop and pickaxe damage (a troll's slap fells trees) don't hurt characters: their resistances ignore it.
            d.m_chop = 0f;
            d.m_pickaxe = 0f;
            return d;
        }

        /// <summary>
        /// Damage of a projectile after <paramref name="armor"/>: its own damage where this machine launched it, else the
        /// thrower's weapon that fires this projectile, else a fifth of the hireling's max health (a middling hit).
        /// </summary>
        public static HitData.DamageTypes ProjectileTypes(Projectile p, Character? shooter, float armor, float maxHealth)
        {
            if (p.m_damage.GetTotalDamage() > 0f)
            {
                HitData.DamageTypes d = p.m_damage.Clone(); // already scaled for stars at launch
                return Taken(d, armor, p.transform.position);
            }
            if (shooter != null && ProjectileWeapon(shooter, Utils.GetPrefabName(p.gameObject)) is ItemDrop.ItemData.SharedData w)
                return Types(shooter, w, armor);
            return new HitData.DamageTypes { m_blunt = maxHealth * 0.2f };
        }

        private static ItemDrop.ItemData.SharedData? ProjectileWeapon(Character shooter, string projectile)
        {
            if (Weapon(shooter) is ItemDrop.ItemData.SharedData current && Fires(current, projectile))
                return current;
            if (shooter is Humanoid h && ZNetScene.instance?.GetPrefab(Utils.GetPrefabName(h.gameObject)) is GameObject prefab &&
                prefab.GetComponent<Humanoid>() is Humanoid ph)
            {
                foreach (GameObject item in ph.m_defaultItems ?? new GameObject[0])
                    if (item != null && item.GetComponent<ItemDrop>() is ItemDrop d && Fires(d.m_itemData.m_shared, projectile))
                        return d.m_itemData.m_shared;
                foreach (GameObject item in ph.m_randomWeapon ?? new GameObject[0])
                    if (item != null && item.GetComponent<ItemDrop>() is ItemDrop d && Fires(d.m_itemData.m_shared, projectile))
                        return d.m_itemData.m_shared;
            }
            return null;
        }

        private static bool Fires(ItemDrop.ItemData.SharedData w, string projectile) =>
            w.m_attack?.m_attackProjectile != null && w.m_attack.m_attackProjectile.name == projectile;

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

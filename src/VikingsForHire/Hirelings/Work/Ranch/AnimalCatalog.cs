using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Core.Orders;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Hirelings.Work.Ranch
{
    /// <summary>
    /// Every animal the Rancher can manage, read from the game: a creature that can be tamed, breeds and drops meat
    /// (vanilla boars, wolves, lox, hens, asksvin, and modded ones), with its meats, young, egg, food and vanilla's
    /// breeding limits. Built when first asked, again when the prefabs change (mods adding creatures late).
    /// </summary>
    internal static class AnimalCatalog
    {
        private static List<AnimalKind> _kinds = new();
        private static Dictionary<string, string> _kindOf = new();
        private static int _prefabs = -1;

        public static IReadOnlyList<AnimalKind> Kinds
        {
            get
            {
                Refresh();
                return _kinds;
            }
        }

        /// <summary>The adult kind a prefab belongs to (itself, or the kind its young or egg grows into), or null.</summary>
        public static string? KindOf(string prefab)
        {
            Refresh();
            return _kindOf.TryGetValue(prefab, out string kind) ? kind : null;
        }

        public static AnimalKind? Find(string kind) => Kinds.FirstOrDefault(k => k.Prefab == kind);

        private static void Refresh()
        {
            if (ZNetScene.instance == null || ObjectDB.instance == null || ZNetScene.instance.m_prefabs.Count == _prefabs)
                return;
            _prefabs = ZNetScene.instance.m_prefabs.Count;
            var kinds = new List<AnimalKind>();
            var kindOf = new Dictionary<string, string>();
            foreach (GameObject go in ZNetScene.instance.m_prefabs)
            {
                if (go == null || go.GetComponent<Tameable>() == null || go.GetComponent<Procreation>() is not Procreation proc ||
                    go.GetComponent<Character>() == null || go.GetComponent<CharacterDrop>() is not CharacterDrop drop)
                    continue;
                var kind = new AnimalKind
                {
                    Prefab = go.name,
                    MaxCreatures = proc.m_maxCreatures,
                    TotalCheckRange = proc.m_totalCheckRange,
                    PartnerCheckRange = proc.m_partnerCheckRange,
                };
                foreach (CharacterDrop.Drop d in drop.m_drops)
                {
                    ItemDrop.ItemData.SharedData? s = d?.m_prefab != null ? d.m_prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
                    if (s == null || s.m_itemType != ItemDrop.ItemData.ItemType.Consumable || s.m_food <= 0f && s.m_foodStamina <= 0f && s.m_foodEitr <= 0f)
                        continue;
                    kind.Meats[d!.m_prefab.name] = (d.m_amountMin + d.m_amountMax) / 2f * d.m_chance;
                }
                if (kind.Meats.Count == 0)
                    continue;
                if (go.GetComponent<MonsterAI>() is MonsterAI ai && ai.m_consumeItems != null)
                    kind.Foods = ai.m_consumeItems.Where(i => i != null).Select(i => i.gameObject.name).Distinct().ToList();
                GameObject? offspring = proc.m_offspring;
                if (offspring != null && offspring.GetComponent<EggGrow>() is EggGrow egg)
                {
                    kind.EggItem = offspring.name;
                    kindOf[offspring.name] = go.name;
                    if (egg.m_grownPrefab != null)
                        AddYoung(kind, egg.m_grownPrefab, kindOf);
                }
                else if (offspring != null && offspring.GetComponent<Growup>() != null)
                    AddYoung(kind, offspring, kindOf);
                kindOf[go.name] = go.name;
                kinds.Add(kind);
            }
            _kinds = kinds.OrderBy(k => k.Prefab).ToList();
            _kindOf = kindOf;
            VfhLog.I(LogCat.Work, "ranch.catalog", ("kinds", string.Join(" ", _kinds.Select(k =>
                $"{k.Prefab}[{string.Join(",", k.Meats.Keys)}{(k.EggItem.Length > 0 ? " egg " + k.EggItem : "")}{(k.YoungPrefabs.Count > 0 ? " young " + string.Join(",", k.YoungPrefabs) : "")}]"))));
        }

        private static void AddYoung(AnimalKind kind, GameObject young, Dictionary<string, string> kindOf)
        {
            if (young.name == kind.Prefab || kind.YoungPrefabs.Contains(young.name))
                return;
            kind.YoungPrefabs.Add(young.name);
            kindOf[young.name] = kind.Prefab;
        }
    }
}

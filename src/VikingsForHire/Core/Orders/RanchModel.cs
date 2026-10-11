using System.Collections.Generic;

namespace VikingsForHire.Core.Orders
{
    /// <summary>An animal the Rancher can manage, as the game defines it (read at run time, modded animals included).</summary>
    public sealed class AnimalKind
    {
        /// <summary>The adult's prefab ("Boar").</summary>
        public string Prefab { get; set; } = "";

        /// <summary>The young's prefabs ("Boar_piggy"); they count as this kind but are never slaughtered.</summary>
        public List<string> YoungPrefabs { get; set; } = new();

        /// <summary>Meat item -> about how many a no-star adult drops (each star doubles it).</summary>
        public Dictionary<string, float> Meats { get; set; } = new();

        /// <summary>The egg it lays instead of giving birth ("ChickenEgg"), or "".</summary>
        public string EggItem { get; set; } = "";

        /// <summary>What it eats.</summary>
        public List<string> Foods { get; set; } = new();

        /// <summary>Vanilla's breeding limits: no more breeding with this many of its kind within TotalCheckRange.</summary>
        public int MaxCreatures { get; set; } = 4;

        public float TotalCheckRange { get; set; } = 10f;

        /// <summary>A partner must be this close to breed.</summary>
        public float PartnerCheckRange { get; set; } = 3f;
    }

    /// <summary>One animal of the herd, as the Rancher sees it.</summary>
    public sealed class HerdAnimal
    {
        /// <summary>A stable key (its ZDO id): ties go to the first in id order.</summary>
        public string Id { get; set; } = "";

        /// <summary>The adult kind's prefab, also for its young.</summary>
        public string Kind { get; set; } = "";

        /// <summary>0 for no star (level 1).</summary>
        public int Stars { get; set; }

        public bool Adult { get; set; } = true;

        /// <summary>Named by a player: a pet, never slaughtered, not counted in the breeding pool.</summary>
        public bool Named { get; set; }

        public float X { get; set; }
        public float Z { get; set; }
        public bool Hungry { get; set; }

        /// <summary>Was in a pen (walled in all round) and isn't now: out, and boars can't be led back.</summary>
        public bool Escaped { get; set; }
    }
}

using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace VikingsForHire.Core.Data
{
    /// <summary>Root of Spronglehump.VikingsForHire.yml. Plain data only: no Unity types.</summary>
    public class VfhData
    {
        [YamlMember(Description = "Hiring board levels 1-8. Level 1 cost is the build cost (needs a Workbench); levels 2-8 are upgrade costs.")]
        public List<BoardLevelData> BoardLevels { get; set; } = new();

        [YamlMember(Description = "Hireling levels 1-8: stats and prices. Food is in food points (item health + stamina + eitr). Level 1 never costs coins.")]
        public List<HirelingLevelData> HirelingLevels { get; set; } = new();

        [YamlMember(Description = "Cosmetic armor by hireling level, shared by every job. Empty = no item in that slot.")]
        public List<ArmorSetData> ArmorSets { get; set; } = new();

        [YamlMember(Description = "Per-job settings. Keys: Woodcutter, Miner, Smelter, GuardMelee, GuardRanged.")]
        public Dictionary<JobType, JobData> Jobs { get; set; } = new();

        [YamlMember(Description = "Command Stone qualities 1-4: the board level needed near the workbench, follower cap and crafting cost.")]
        public List<StoneLevelData> CommandStone { get; set; } = new();

        [YamlMember(Description = "Food rules for hiring and upkeep.")]
        public FoodData Food { get; set; } = new();

        [YamlMember(Description = "Names given to hirelings, picked to match the generated body.")]
        public NameData Names { get; set; } = new();
    }

    public class BoardLevelData
    {
        public int Level { get; set; }

        [YamlMember(Description = "Item prefab name -> amount.")]
        public Dictionary<string, int> Cost { get; set; } = new();

        public int HirelingCap { get; set; }

        [YamlMember(Description = "Largest work radius (m) a contract may use; also how far hirelings roam from the board.")]
        public float MaxWorkRadius { get; set; }
    }

    public class HirelingLevelData
    {
        public int Level { get; set; }
        public float Health { get; set; }
        public float Armor { get; set; }

        [YamlMember(Description = "Damage multiplier for guards. Workers use this times their job's WorkerCombatFactor.")]
        public float GuardDamageMult { get; set; }

        [YamlMember(Description = "Chop/mine damage multiplier.")]
        public float GatherMult { get; set; }

        [YamlMember(Description = "Usable cargo slots (max 32).")]
        public int CargoSlots { get; set; }

        public int HireFood { get; set; }
        public int HireCoins { get; set; }
        public int UpkeepFood { get; set; }
        public int UpkeepCoins { get; set; }
    }

    public class ArmorSetData
    {
        public int Level { get; set; }
        public string Helmet { get; set; } = "";
        public string Chest { get; set; } = "";
        public string Legs { get; set; } = "";
    }

    public class JobData
    {
        [YamlMember(Description = "Multiplier on the level's hire and upkeep prices.")]
        public float CostMult { get; set; } = 1f;

        [YamlMember(Description = "Fraction of GuardDamageMult this job fights with (guards use 1).")]
        public float WorkerCombatFactor { get; set; } = 1f;

        [YamlMember(Description = "Lowest board level that can post this job (e.g. 2 for miners: no pickaxe before Eikthyr).")]
        public int MinBoardLevel { get; set; } = 1;

        [YamlMember(Description = "Multiplier on the board level's maxWorkRadius for this job. Gatherers use a bigger area so they don't strip it in a few days. Ground much more than 100-120 m from the nearest player isn't loaded, so radii past that have no effect.")]
        public float WorkRadiusMultiplier { get; set; } = 1f;

        [YamlMember(Description = "Items this job picks up while working.")]
        public List<string> PickupItems { get; set; } = new();

        [YamlMember(Description = "Cosmetic weapons by level. The main item's real tool tier decides what a gatherer can harvest.")]
        public List<WeaponSetData> Gear { get; set; } = new();

        [YamlMember(Description = "Smelter only: station prefab names this job keeps stocked.")]
        public List<string> Stations { get; set; } = new();
    }

    public class WeaponSetData
    {
        public int Level { get; set; }
        public string Main { get; set; } = "";
        public string Offhand { get; set; } = "";
        public string Ammo { get; set; } = "";
        public string Sidearm { get; set; } = "";
    }

    public class StoneLevelData
    {
        public int Quality { get; set; }
        public int RequiredBoardLevel { get; set; }
        public int FollowerCap { get; set; }
        public Dictionary<string, int> Cost { get; set; } = new();
    }

    public class FoodData
    {
        [YamlMember(Description = "Edible items that don't count as food unless AllowRawFood is on.")]
        public List<string> RawFoods { get; set; } = new();
    }

    public class NameData
    {
        public List<string> Male { get; set; } = new();
        public List<string> Female { get; set; } = new();
    }
}

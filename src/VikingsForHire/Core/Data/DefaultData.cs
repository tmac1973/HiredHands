using System.Collections.Generic;
using System.Linq;

namespace VikingsForHire.Core.Data
{
    /// <summary>Default tables. Every item name was checked against the game's prefab list (Valheim l-1.0.16).</summary>
    public static class DefaultData
    {
        public static VfhData Create() => new()
        {
            BoardLevels = new List<BoardLevelData>
            {
                Board(1, 2, 20, ("Wood", 40), ("Stone", 20), ("DeerHide", 10), ("LeatherScraps", 10), ("Resin", 10)),
                Board(2, 3, 25, ("TrophyEikthyr", 1), ("HardAntler", 3), ("DeerHide", 20), ("Flint", 20), ("Wood", 50)),
                Board(3, 4, 30, ("TrophyTheElder", 1), ("Bronze", 10), ("RoundLog", 40), ("TrollHide", 5), ("GreydwarfEye", 20)),
                Board(4, 5, 35, ("TrophyBonemass", 1), ("Iron", 20), ("ElderBark", 40), ("Guck", 10), ("WitheredBone", 10)),
                Board(5, 6, 40, ("TrophyDragonQueen", 1), ("Silver", 20), ("DragonTear", 5), ("WolfPelt", 10), ("Obsidian", 20)),
                Board(6, 7, 45, ("TrophyGoblinKing", 1), ("BlackMetal", 20), ("LinenThread", 20), ("Needle", 20), ("LoxPelt", 5)),
                Board(7, 8, 50, ("TrophySeekerQueen", 1), ("BlackCore", 3), ("Eitr", 15), ("YggdrasilWood", 40), ("Carapace", 20)),
                Board(8, 10, 60, ("TrophyFader", 1), ("FlametalNew", 20), ("Blackwood", 40), ("AskHide", 10), ("MoltenCore", 3)),
            },
            HirelingLevels = new List<HirelingLevelData>
            {
                Hireling(1, 80, 4, 1.0f, 1.0f, 8, 150, 0, 40, 0),
                Hireling(2, 120, 8, 1.3f, 1.2f, 10, 250, 50, 60, 5),
                Hireling(3, 180, 14, 1.7f, 1.4f, 12, 400, 150, 90, 10),
                Hireling(4, 250, 20, 2.2f, 1.6f, 16, 600, 300, 120, 20),
                Hireling(5, 330, 26, 2.8f, 1.8f, 20, 800, 500, 160, 30),
                Hireling(6, 420, 32, 3.5f, 2.0f, 24, 1000, 800, 200, 45),
                Hireling(7, 520, 38, 4.3f, 2.2f, 28, 1300, 1200, 250, 60),
                Hireling(8, 650, 44, 5.2f, 2.4f, 32, 1600, 1800, 300, 80),
            },
            ArmorSets = new List<ArmorSetData>
            {
                Armor(1, "", "ArmorRagsChest", "ArmorRagsLegs"),
                Armor(2, "HelmetLeather", "ArmorLeatherChest", "ArmorLeatherLegs"),
                Armor(3, "HelmetBronze", "ArmorBronzeChest", "ArmorBronzeLegs"),
                Armor(4, "HelmetIron", "ArmorIronChest", "ArmorIronLegs"),
                Armor(5, "HelmetDrake", "ArmorWolfChest", "ArmorWolfLegs"),
                Armor(6, "HelmetPadded", "ArmorPaddedCuirass", "ArmorPaddedGreaves"),
                Armor(7, "HelmetCarapace", "ArmorCarapaceChest", "ArmorCarapaceLegs"),
                Armor(8, "HelmetFlametal", "ArmorFlametalChest", "ArmorFlametalLegs"),
            },
            Jobs = new Dictionary<JobType, JobData>
            {
                [JobType.Woodcutter] = new()
                {
                    CostMult = 1.0f,
                    WorkerCombatFactor = 0.4f,
                    PickupItems = List("Wood", "FineWood", "RoundLog", "ElderBark", "YggdrasilWood", "Blackwood", "Resin",
                        "BeechSeeds", "FirCone", "PineCone", "BirchSeeds", "Acorn"),
                    Gear = Mains("AxeStone", "AxeFlint", "AxeBronze", "AxeIron", "AxeIron", "AxeBlackMetal", "AxeJotunBane", "AxeJotunBane"),
                },
                [JobType.Miner] = new()
                {
                    CostMult = 1.1f,
                    WorkerCombatFactor = 0.4f,
                    PickupItems = List("Stone", "CopperOre", "TinOre", "IronScrap", "SilverOre", "BlackMetalScrap", "CopperScrap",
                        "Obsidian", "FlametalOreNew", "Grausten"),
                    Gear = Mains("PickaxeAntler", "PickaxeAntler", "PickaxeBronze", "PickaxeIron", "PickaxeIron",
                        "PickaxeBlackMetal", "PickaxeBlackMetal", "PickaxeBlackMetal"),
                },
                [JobType.Smelter] = new()
                {
                    CostMult = 0.9f,
                    WorkerCombatFactor = 0.3f,
                    Gear = Mains("Club", "Club", "Club", "Club", "Club", "Club", "Club", "Club"),
                    Stations = List("smelter", "blastfurnace", "charcoal_kiln", "eitrrefinery"),
                },
                [JobType.GuardMelee] = new()
                {
                    CostMult = 1.3f,
                    WorkerCombatFactor = 1.0f,
                    Gear = new List<WeaponSetData>
                    {
                        Weapon(1, "Club", "ShieldWood"),
                        Weapon(2, "KnifeFlint", "ShieldWood"),
                        Weapon(3, "SwordBronze", "ShieldBronzeBuckler"),
                        Weapon(4, "SwordIron", "ShieldBanded"),
                        Weapon(5, "SwordSilver", "ShieldSilver"),
                        Weapon(6, "SwordBlackmetal", "ShieldBlackmetal"),
                        Weapon(7, "SwordMistwalker", "ShieldCarapace"),
                        Weapon(8, "SwordNiedhogg", "ShieldFlametal"),
                    },
                },
                [JobType.GuardRanged] = new()
                {
                    CostMult = 1.3f,
                    WorkerCombatFactor = 1.0f,
                    Gear = new List<WeaponSetData>
                    {
                        Weapon(1, "Bow", ammo: "ArrowWood"),
                        Weapon(2, "Bow", ammo: "ArrowFlint"),
                        Weapon(3, "BowFineWood", ammo: "ArrowBronze"),
                        Weapon(4, "BowHuntsman", ammo: "ArrowIron"),
                        Weapon(5, "BowDraugrFang", ammo: "ArrowSilver"),
                        Weapon(6, "BowDraugrFang", ammo: "ArrowNeedle"),
                        Weapon(7, "BowSpineSnap", ammo: "ArrowCarapace"),
                        Weapon(8, "BowAshlands", ammo: "ArrowCharred"),
                    },
                },
            },
            CommandStone = new List<StoneLevelData>
            {
                Stone(1, 3, 1, ("SurtlingCore", 5), ("Bronze", 10), ("GreydwarfEye", 20), ("FineWood", 10)),
                Stone(2, 4, 2, ("Iron", 15), ("Guck", 10), ("Ooze", 10), ("WitheredBone", 10)),
                Stone(3, 6, 3, ("BlackMetal", 15), ("Silver", 10), ("Needle", 10), ("LinenThread", 10)),
                Stone(4, 8, 4, ("FlametalNew", 15), ("Eitr", 10), ("BlackCore", 2), ("MoltenCore", 2)),
            },
            Food = new FoodData
            {
                RawFoods = List("RawMeat", "DeerMeat", "NeckTail", "WolfMeat", "LoxMeat", "SerpentMeat", "FishRaw", "ChickenMeat",
                    "HareMeat", "BugMeat", "AsksvinMeat", "VoltureMeat", "BoneMawSerpentMeat", "Raspberry", "Blueberries", "Cloudberry",
                    "Mushroom", "MushroomYellow", "MushroomBlue", "MushroomJotunPuffs", "MushroomMagecap", "Carrot", "Turnip", "Onion",
                    "Honey", "Fiddleheadfern", "Vineberry"),
            },
            Names = new NameData
            {
                Male = List("Agnar", "Alf", "Arne", "Asbjorn", "Bard", "Bjarni", "Bjorn", "Bodvar", "Brand", "Dag", "Egil", "Einar",
                    "Eirik", "Eyvind", "Finn", "Floki", "Frode", "Geir", "Gisli", "Gorm", "Grim", "Gudmund", "Gunnar", "Hakon",
                    "Halfdan", "Hallvard", "Harald", "Hauk", "Hjalmar", "Hrafn", "Ingvar", "Ivar", "Kare", "Ketil", "Knut", "Leif",
                    "Magnus", "Njal", "Odd", "Olaf", "Orm", "Ragnar", "Rolf", "Runolf", "Sigurd", "Skeggi", "Snorri", "Stein",
                    "Sten", "Svein", "Thorbjorn", "Thorgrim", "Thorkell", "Thorvald", "Toke", "Ulf", "Vali", "Vemund", "Vidar", "Yngvar"),
                Female = List("Alfhild", "Asa", "Asgerd", "Aslaug", "Astrid", "Bera", "Bergljot", "Bodil", "Borghild", "Brynhild",
                    "Dagny", "Eir", "Embla", "Estrid", "Frida", "Freydis", "Gerd", "Grima", "Gudrun", "Gunnhild", "Gyda", "Halla",
                    "Helga", "Herdis", "Hild", "Hildigunn", "Hlif", "Inga", "Ingrid", "Jorunn", "Katla", "Kolfinna", "Liv", "Ragna",
                    "Ragnhild", "Rannveig", "Runa", "Saga", "Sigrid", "Sigrun", "Signy", "Siv", "Solveig", "Steinunn", "Svala",
                    "Svanhild", "Thora", "Thordis", "Thorgerd", "Thorunn", "Thyra", "Tofa", "Turid", "Ulfhild", "Una", "Vigdis",
                    "Yrsa", "Groa", "Hervor", "Ylva"),
            },
        };

        private static BoardLevelData Board(int level, int cap, float radius, params (string Item, int Amount)[] cost) =>
            new() { Level = level, HirelingCap = cap, MaxWorkRadius = radius, Cost = cost.ToDictionary(c => c.Item, c => c.Amount) };

        private static HirelingLevelData Hireling(int level, float health, float armor, float guardMult, float gatherMult, int slots,
            int hireFood, int hireCoins, int upkeepFood, int upkeepCoins) => new()
        {
            Level = level, Health = health, Armor = armor, GuardDamageMult = guardMult, GatherMult = gatherMult, CargoSlots = slots,
            HireFood = hireFood, HireCoins = hireCoins, UpkeepFood = upkeepFood, UpkeepCoins = upkeepCoins,
        };

        private static ArmorSetData Armor(int level, string helmet, string chest, string legs) =>
            new() { Level = level, Helmet = helmet, Chest = chest, Legs = legs };

        private static WeaponSetData Weapon(int level, string main, string offhand = "", string ammo = "") =>
            new() { Level = level, Main = main, Offhand = offhand, Ammo = ammo, Sidearm = ammo == "" ? "" : "Club" };

        private static List<WeaponSetData> Mains(params string[] mains) =>
            mains.Select((m, i) => Weapon(i + 1, m)).ToList();

        private static StoneLevelData Stone(int quality, int boardLevel, int cap, params (string Item, int Amount)[] cost) =>
            new() { Quality = quality, RequiredBoardLevel = boardLevel, FollowerCap = cap, Cost = cost.ToDictionary(c => c.Item, c => c.Amount) };

        private static List<string> List(params string[] items) => items.ToList();
    }
}

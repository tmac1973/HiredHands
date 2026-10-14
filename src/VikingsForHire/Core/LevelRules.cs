using System;
using System.Linq;
using VikingsForHire.Core.Data;

namespace VikingsForHire.Core
{
    public sealed class LevelRules
    {
        public const float MinWorkRadius = 10f;

        private readonly VfhData _data;

        public LevelRules(VfhData data) => _data = data;

        public int MaxBoardLevel => _data.BoardLevels.Count;

        /// <summary>A board hires up to its own level.</summary>
        public int MaxHirelingLevel(int boardLevel) => Math.Max(1, Math.Min(boardLevel, _data.HirelingLevels.Count));

        /// <summary>Combat hirelings (guards, any mix) a board of this level can have.</summary>
        public int CombatCap(int boardLevel) => Board(boardLevel).CombatCap;

        /// <summary>Workers (everyone else) a board of this level can have.</summary>
        public int WorkerCap(int boardLevel) => Board(boardLevel).WorkerCap;

        /// <summary>The cap of the job's kind.</summary>
        public int Cap(int boardLevel, JobType job) => job.IsCombat() ? CombatCap(boardLevel) : WorkerCap(boardLevel);

        /// <summary>Most of this job per board; 0 = no limit beyond the cap.</summary>
        public int MaxPerBoard(JobType job) => Job(job).MaxPerBoard;

        /// <summary>Largest contract work radius, which is also how far the board's hirelings roam.</summary>
        public float MaxWorkRadius(int boardLevel) => Board(boardLevel).MaxWorkRadius;

        public float ClampRadius(int boardLevel, float radius) =>
            Math.Max(MinWorkRadius, Math.Min(radius, MaxWorkRadius(boardLevel)));

        /// <summary>The board level's radius times the job's multiplier (gatherers work a bigger area).</summary>
        public float MaxWorkRadius(int boardLevel, JobType job) => MaxWorkRadius(boardLevel) * Job(job).WorkRadiusMultiplier;

        public float ClampRadius(int boardLevel, JobType job, float radius) =>
            Math.Max(MinWorkRadius, Math.Min(radius, MaxWorkRadius(boardLevel, job)));

        public int MinBoardLevel(JobType job) => Effective(job).MinBoardLevel;

        /// <summary>The job's weapons by level, as changed by a loaded mod (withMods).</summary>
        public System.Collections.Generic.List<WeaponSetData> Gear(JobType job) => Effective(job).Gear;

        /// <summary>The job's own weapons by level, ignoring mods (what a mod's missing items fall back to).</summary>
        public System.Collections.Generic.List<WeaponSetData> GearWithoutMods(JobType job) => Job(job).Gear;

        /// <summary>The job's outfit by level, as changed by a loaded mod (empty: the shared armour sets).</summary>
        public System.Collections.Generic.List<ArmorSetData> Outfit(JobType job) => Effective(job).Outfit;

        /// <summary>The job's hat, worn instead of the helmet (null: none; fighting jobs never wear one).</summary>
        public string? Hat(JobType job) => job.IsCombat() || string.IsNullOrEmpty(Job(job).Hat) ? null : Job(job).Hat;

        /// <summary>The job's data with the first loaded mod's overrides applied (withMods, in order).</summary>
        public JobData Effective(JobType job)
        {
            JobData own = Job(job);
            JobModOverride? mod = own.WithMods.FirstOrDefault(m => m != null && m.Guid.Length > 0 && ModPresence.IsLoaded(m.Guid));
            if (mod == null)
                return own;
            return new JobData
            {
                CostMult = own.CostMult, WorkerCombatFactor = own.WorkerCombatFactor, MaxPerBoard = own.MaxPerBoard,
                WorkRadiusMultiplier = own.WorkRadiusMultiplier, PickupItems = own.PickupItems, GatherToggles = own.GatherToggles,
                Stations = own.Stations, ChoreLevels = own.ChoreLevels, CropLevels = own.CropLevels, StationLevels = own.StationLevels,
                RecipeLevels = own.RecipeLevels, KeepInStorage = own.KeepInStorage, Hat = own.Hat, WithMods = own.WithMods,
                MinBoardLevel = mod.MinBoardLevel > 0 ? mod.MinBoardLevel : own.MinBoardLevel,
                Gear = mod.Gear.Count > 0 ? mod.Gear : own.Gear,
                Outfit = mod.Outfit.Count > 0 ? mod.Outfit : own.Outfit,
            };
        }

        public bool JobUnlocked(int boardLevel, JobType job) => boardLevel >= MinBoardLevel(job);

        private JobData Job(JobType job) => _data.Jobs.TryGetValue(job, out JobData? j) ? j : new JobData();

        public int StoneFollowerCap(int quality) => Stone(quality).FollowerCap;

        public int RequiredBoardLevelForStone(int quality) => Stone(quality).RequiredBoardLevel;

        private BoardLevelData Board(int level) =>
            _data.BoardLevels.FirstOrDefault(b => b.Level == level)
            ?? throw new ArgumentOutOfRangeException(nameof(level), level, "no such board level");

        private StoneLevelData Stone(int quality) =>
            _data.CommandStone.FirstOrDefault(s => s.Quality == quality)
            ?? throw new ArgumentOutOfRangeException(nameof(quality), quality, "no such stone quality");
    }
}

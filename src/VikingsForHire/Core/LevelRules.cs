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

        public int MinBoardLevel(JobType job) => Job(job).MinBoardLevel;

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

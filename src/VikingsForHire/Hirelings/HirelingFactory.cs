using System;
using VikingsForHire.Board;
using VikingsForHire.Config;
using VikingsForHire.Core;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Hirelings
{
    /// <summary>Builds new hirelings (a fresh identity, looks and job) as snapshots, ready to spawn.</summary>
    internal static class HirelingFactory
    {
        private static readonly System.Random Rng = new();

        public static HirelingRecord NewRecord(JobType job, int level, HiringBoard? board, Vector3 fallbackHome)
        {
            Vector3 home = board != null ? board.transform.position : fallbackHome;
            var record = new HirelingRecord
            {
                Hid = Guid.NewGuid().ToString("N"),
                BoardId = board != null ? board.Id : "",
                Job = job,
                Level = Mathf.Clamp(level, 1, DataStore.Current.HirelingLevels.Count),
                Stance = StanceRules.Default(job),
                Radius = board != null ? new LevelRules(DataStore.Current).MaxWorkRadius(board.Level) : 20f,
                HomeX = home.x, HomeY = home.y, HomeZ = home.z,
                Mode = HirelingMode.Idle,
            };
            Appearance.Fill(record, Rng);
            return record;
        }

        public static ZDO Spawn(HirelingRecord record, Vector3 position, Quaternion rotation, string reason)
        {
            ZDO zdo = HirelingSnapshot.Create(record).Spawn(position, rotation);
            VfhLog.I(LogCat.Hireling, "hireling.spawned", ("hid", record.Hid), ("board", record.BoardId), ("name", record.Name),
                ("job", record.Job), ("level", record.Level), ("model", record.Model), ("hair", record.Hair), ("beard", record.Beard),
                ("pos", position), ("zdo", zdo.m_uid.ToString()), ("reason", reason));
            return zdo;
        }
    }
}

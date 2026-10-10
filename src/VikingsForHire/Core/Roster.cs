using System;
using System.Collections.Generic;
using System.Linq;

namespace VikingsForHire.Core
{
    public enum ContractState
    {
        Pending = 0,
        Active = 1,
        Leaving = 2,
    }

    public enum OpOutcome
    {
        Ok,
        /// <summary>Not used since 0.7.0 (CombatCapReached and WorkerCapReached replace it).</summary>
        CapReached,
        NotFound,
        WrongState,
        InsufficientFunds,
        BadLevel,
        BadValue,
        /// <summary>Not used since 0.7.0 (JobLimitReached replaces it).</summary>
        JobTaken,
        CombatCapReached,
        WorkerCapReached,
        JobLimitReached,
        /// <summary>The server has combat hirelings turned off (CombatHirelings).</summary>
        CombatOff,
    }

    /// <summary>One contract on a board: a hireling that's coming, working or on its way out.</summary>
    /// <summary>Where a posted guard stands, and which way it faces (yaw, degrees).</summary>
    public sealed class GuardPost
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Yaw { get; set; }
    }

    public sealed class ContractEntry
    {
        public string ContractId { get; set; } = "";
        public string Hid { get; set; } = "";
        public string Name { get; set; } = "";
        public JobType Job { get; set; }
        public int Level { get; set; } = 1;
        public float Radius { get; set; }
        public Stance Stance { get; set; }
        public ContractState State { get; set; }
        public int UnpaidDays { get; set; }
        /// <summary>ZNet time (s) a pending hireling arrives.</summary>
        public double ArriveAt { get; set; }
        /// <summary>The hireling as created at posting (looks, name); job/level/stance/radius above override it on spawn.</summary>
        public byte[] Snapshot { get; set; } = Array.Empty<byte>();
        /// <summary>Pending because it died (permadeath off) and is coming back; it pays the respawn fee on arrival.</summary>
        public bool RespawnPending { get; set; }
        /// <summary>What was taken from the board for the hire fee ("Prefab:count;…"), so a cancel refunds exactly that.</summary>
        public string Paid { get; set; } = "";
        /// <summary>A guard's post (phase 13): it stands guard here instead of patrolling. Null = patrol.</summary>
        public GuardPost? Post { get; set; }
        /// <summary>Gatherers: items switched off in the Shift+E panel ("CopperOre,Stone"), left unharvested and on the ground.</summary>
        public string SkipItems { get; set; } = "";
        /// <summary>Gatherers: don't harvest inside the board's own area (decorative trees and rocks in a base).</summary>
        public bool NoHomeWork { get; set; }

        public ContractEntry Clone()
        {
            var c = (ContractEntry)MemberwiseClone();
            c.Snapshot = (byte[])Snapshot.Clone();
            return c;
        }
    }

    /// <summary>Gatherers' "what to gather" settings (phase 15 follow-up).</summary>
    public static class GatherRules
    {
        /// <summary>
        /// What a tree or rock counts as: its best drop on the toggle list (the first one that isn't the plain first
        /// entry), else the plain one if it drops that, else null (not on the list: never filtered).
        /// </summary>
        public static string? Primary(IEnumerable<string> drops, IList<string> toggles)
        {
            if (toggles.Count == 0)
                return null;
            var set = new HashSet<string>(drops);
            foreach (string t in toggles.Skip(1))
                if (set.Contains(t))
                    return t;
            return set.Contains(toggles[0]) ? toggles[0] : null;
        }

        public static HashSet<string> ParseSkip(string? skip) =>
            new((skip ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0));

        public static string FormatSkip(IEnumerable<string> items) => string.Join(",", items.Where(i => i.Length > 0).Distinct().OrderBy(i => i));
    }

    /// <summary>What a player may name a hireling.</summary>
    public static class HirelingNames
    {
        public const int MaxLength = 24;

        /// <summary>
        /// Trimmed, inner whitespace collapsed, no markup brackets (names show in rich-text UI), at most MaxLength
        /// characters. Empty when nothing usable is left.
        /// </summary>
        public static string Clean(string? raw)
        {
            if (raw == null)
                return "";
            var sb = new System.Text.StringBuilder();
            bool space = false;
            foreach (char c in raw.Trim())
            {
                if (char.IsWhiteSpace(c))
                {
                    space = true;
                    continue;
                }
                if (c == '<' || c == '>' || char.IsControl(c))
                    continue;
                if (space && sb.Length > 0)
                    sb.Append(' ');
                space = false;
                sb.Append(c);
            }
            string name = sb.ToString();
            return name.Length > MaxLength ? name.Substring(0, MaxLength).TrimEnd() : name;
        }
    }

    public sealed class UpkeepResult
    {
        public List<string> Paid { get; } = new();
        public List<(string Hid, int UnpaidDays)> Unpaid { get; } = new();
        public List<string> NowLeaving { get; } = new();
    }

    /// <summary>
    /// A board's contracts. Pure data and rules: the game side does payment, spawning and networking, and stores this as
    /// bytes in the board's ZDO.
    /// </summary>
    public sealed class Roster
    {
        // 2: adds the guard post. 3: adds the gather settings. Older rosters still read.
        public const byte FormatVersion = 3;

        public List<ContractEntry> Entries { get; } = new();

        public int Active => Entries.Count(e => e.State == ContractState.Active);
        public int Pending => Entries.Count(e => e.State == ContractState.Pending);
        public int Leaving => Entries.Count(e => e.State == ContractState.Leaving);

        /// <summary>Everyone counts against the cap until they've actually gone.</summary>
        public int Count => Entries.Count;


        public ContractEntry? ByHid(string hid) => hid.Length == 0 ? null : Entries.FirstOrDefault(e => e.Hid == hid);

        public ContractEntry? ByContract(string id) => Entries.FirstOrDefault(e => e.ContractId == id);

        /// <summary>Contracts of a kind (combat or worker): all of them count until they've actually gone.</summary>
        public int KindCount(bool combat) => Entries.Count(e => e.Job.IsCombat() == combat);

        /// <summary>Contracts of a job that aren't on their way out (a replacement can be hired while one walks off).</summary>
        public int JobCount(JobType job) => Entries.Count(e => e.Job == job && e.State != ContractState.Leaving);

        /// <summary>
        /// Whether this board may post the job: its kind's cap first, then the job's own limit. A board already over a cap
        /// (an older world, or a lowered setting) keeps its hirelings; only new contracts of that kind are refused.
        /// </summary>
        public OpOutcome CanPost(JobType job, LevelRules rules, int boardLevel)
        {
            bool combat = job.IsCombat();
            if (KindCount(combat) >= rules.Cap(boardLevel, job))
                return combat ? OpOutcome.CombatCapReached : OpOutcome.WorkerCapReached;
            int max = rules.MaxPerBoard(job);
            if (max > 0 && JobCount(job) >= max)
                return OpOutcome.JobLimitReached;
            return OpOutcome.Ok;
        }

        public OpOutcome Post(ContractEntry entry, LevelRules rules, int boardLevel)
        {
            OpOutcome can = CanPost(entry.Job, rules, boardLevel);
            if (can != OpOutcome.Ok)
                return can;
            Add(entry);
            return OpOutcome.Ok;
        }

        /// <summary>Adds a pending contract with no checks (callers check with <see cref="CanPost"/> first).</summary>
        public void Add(ContractEntry entry)
        {
            entry.State = ContractState.Pending;
            Entries.Add(entry);
        }

        public OpOutcome Activate(string contractId, string hid)
        {
            ContractEntry? e = ByContract(contractId);
            if (e == null)
                return OpOutcome.NotFound;
            if (e.State != ContractState.Pending)
                return OpOutcome.WrongState;
            e.State = ContractState.Active;
            e.Hid = hid;
            e.RespawnPending = false;
            e.ArriveAt = 0;
            e.UnpaidDays = 0;
            return OpOutcome.Ok;
        }

        public OpOutcome Edit(string hid, float radius, Stance stance)
        {
            ContractEntry? e = ByHid(hid);
            if (e == null)
                return OpOutcome.NotFound;
            if (!StanceRules.IsAllowed(e.Job, stance))
                return OpOutcome.BadValue;
            e.Radius = radius;
            e.Stance = stance;
            return OpOutcome.Ok;
        }

        /// <summary>Sets a gatherer's "what to gather" and "work at home" settings.</summary>
        public OpOutcome SetGather(string hid, string skipItems, bool noHomeWork)
        {
            ContractEntry? e = ByHid(hid);
            if (e == null)
                return OpOutcome.NotFound;
            e.SkipItems = GatherRules.FormatSkip(GatherRules.ParseSkip(skipItems));
            e.NoHomeWork = noHomeWork;
            return OpOutcome.Ok;
        }

        /// <summary>Gives a contract's hireling a new name (already cleaned with <see cref="HirelingNames.Clean"/>).</summary>
        public OpOutcome Rename(string hid, string name)
        {
            ContractEntry? e = ByHid(hid);
            if (e == null)
                return OpOutcome.NotFound;
            if (name.Length == 0)
                return OpOutcome.BadValue;
            e.Name = name;
            return OpOutcome.Ok;
        }

        /// <summary>Sets (or with null clears) a hireling's post: where a guard stands guard, or a worker waits.</summary>
        public OpOutcome SetPost(string hid, GuardPost? post)
        {
            ContractEntry? e = ByHid(hid);
            if (e == null)
                return OpOutcome.NotFound;
            e.Post = post;
            return OpOutcome.Ok;
        }

        public OpOutcome Promote(string hid, int level, int maxLevel)
        {
            ContractEntry? e = ByHid(hid);
            if (e == null)
                return OpOutcome.NotFound;
            if (e.State != ContractState.Active)
                return OpOutcome.WrongState;
            if (level <= e.Level || level > maxLevel)
                return OpOutcome.BadLevel;
            e.Level = level;
            return OpOutcome.Ok;
        }

        public OpOutcome Dismiss(string hid)
        {
            ContractEntry? e = ByHid(hid);
            if (e == null)
                return OpOutcome.NotFound;
            if (e.State != ContractState.Active)
                return OpOutcome.WrongState;
            e.State = ContractState.Leaving;
            return OpOutcome.Ok;
        }

        /// <summary>Removes a not-yet-arrived contract; the caller refunds <see cref="ContractEntry.Paid"/>.</summary>
        public ContractEntry? CancelPending(string contractId)
        {
            ContractEntry? e = ByContract(contractId);
            if (e == null || e.State != ContractState.Pending)
                return null;
            Entries.Remove(e);
            return e;
        }

        /// <summary>Permadeath removes the contract; otherwise the hireling comes back after the cooldown.</summary>
        public OpOutcome MarkDied(string hid, bool permadeath, double now, double cooldownSeconds)
        {
            ContractEntry? e = ByHid(hid);
            if (e == null)
                return OpOutcome.NotFound;
            if (permadeath || e.State == ContractState.Leaving)
            {
                Entries.Remove(e);
                return OpOutcome.Ok;
            }
            e.State = ContractState.Pending;
            e.RespawnPending = true;
            e.ArriveAt = now + cooldownSeconds;
            e.UnpaidDays = 0;
            // It comes back to patrol, not to an old post that may be anywhere (it walked in from the edge and stood
            // 26 m off a post it couldn't reach).
            e.Post = null;
            return OpOutcome.Ok;
        }

        public bool RemoveEntry(string hid)
        {
            ContractEntry? e = ByHid(hid);
            return e != null && Entries.Remove(e);
        }

        public List<ContractEntry> VoidBoard()
        {
            var all = Entries.ToList();
            Entries.Clear();
            return all;
        }

        /// <summary>
        /// One day of upkeep for every working hireling, in hiring order, each paid in full or not at all. Three
        /// unpaid days in a row (configurable) and they leave; a paid day clears the count.
        /// </summary>
        public UpkeepResult ApplyUpkeepDay(Func<ContractEntry, bool> tryPay, int unpaidDaysBeforeLeaving)
        {
            var result = new UpkeepResult();
            foreach (ContractEntry e in Entries.Where(e => e.State == ContractState.Active))
            {
                if (tryPay(e))
                {
                    e.UnpaidDays = 0;
                    result.Paid.Add(e.Hid);
                    continue;
                }
                e.UnpaidDays++;
                result.Unpaid.Add((e.Hid, e.UnpaidDays));
                if (e.UnpaidDays > unpaidDaysBeforeLeaving)
                {
                    e.State = ContractState.Leaving;
                    result.NowLeaving.Add(e.Hid);
                }
            }
            return result;
        }

        public void Write(IPackageWriter w)
        {
            w.Write(FormatVersion);
            w.Write(Entries.Count);
            foreach (ContractEntry e in Entries)
            {
                w.Write(e.ContractId);
                w.Write(e.Hid);
                w.Write(e.Name);
                w.Write((int)e.Job);
                w.Write(e.Level);
                w.Write(e.Radius);
                w.Write((int)e.Stance);
                w.Write((int)e.State);
                w.Write(e.UnpaidDays);
                w.Write(e.ArriveAt);
                w.Write(e.Snapshot);
                w.Write(e.RespawnPending ? 1 : 0);
                w.Write(e.Paid);
                w.Write(e.Post != null ? 1 : 0);
                if (e.Post != null)
                {
                    w.Write(e.Post.X);
                    w.Write(e.Post.Y);
                    w.Write(e.Post.Z);
                    w.Write(e.Post.Yaw);
                }
                w.Write(e.SkipItems);
                w.Write(e.NoHomeWork ? 1 : 0);
            }
        }

        public static Roster Read(IPackageReader r)
        {
            byte version = r.ReadByte();
            if (version < 1 || version > FormatVersion)
                throw new NotSupportedException($"roster version {version} (this build reads {FormatVersion})");
            var roster = new Roster();
            int n = r.ReadInt();
            for (int i = 0; i < n; i++)
            {
                var entry = new ContractEntry
                {
                    ContractId = r.ReadString(),
                    Hid = r.ReadString(),
                    Name = r.ReadString(),
                    Job = (JobType)r.ReadInt(),
                    Level = r.ReadInt(),
                    Radius = r.ReadSingle(),
                    Stance = (Stance)r.ReadInt(),
                    State = (ContractState)r.ReadInt(),
                    UnpaidDays = r.ReadInt(),
                    ArriveAt = r.ReadDouble(),
                    Snapshot = r.ReadBytes(),
                    RespawnPending = r.ReadInt() == 1,
                    Paid = r.ReadString(),
                };
                if (version >= 2 && r.ReadInt() == 1)
                    entry.Post = new GuardPost { X = r.ReadSingle(), Y = r.ReadSingle(), Z = r.ReadSingle(), Yaw = r.ReadSingle() };
                if (version >= 3)
                {
                    entry.SkipItems = r.ReadString();
                    entry.NoHomeWork = r.ReadInt() == 1;
                }
                roster.Entries.Add(entry);
            }
            return roster;
        }

        /// <summary>"Wood:3;Coins:50" → pairs. Malformed parts are skipped.</summary>
        public static List<(string Prefab, int Count)> ParsePaid(string paid) =>
            paid.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split(':'))
                .Where(p => p.Length == 2 && int.TryParse(p[1], out _))
                .Select(p => (p[0], int.Parse(p[1])))
                .ToList();

        public static string FormatPaid(IEnumerable<(string Prefab, int Count)> items) =>
            string.Join(";", items.Where(i => i.Count > 0).Select(i => $"{i.Prefab}:{i.Count}"));
    }
}

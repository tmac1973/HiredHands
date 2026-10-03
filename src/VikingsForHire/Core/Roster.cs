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
        CapReached,
        NotFound,
        WrongState,
        InsufficientFunds,
        BadLevel,
        BadValue,
    }

    /// <summary>One contract on a board: a hireling that's coming, working or on its way out.</summary>
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

        public ContractEntry Clone()
        {
            var c = (ContractEntry)MemberwiseClone();
            c.Snapshot = (byte[])Snapshot.Clone();
            return c;
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
        public const byte FormatVersion = 1;

        public List<ContractEntry> Entries { get; } = new();

        public int Active => Entries.Count(e => e.State == ContractState.Active);
        public int Pending => Entries.Count(e => e.State == ContractState.Pending);
        public int Leaving => Entries.Count(e => e.State == ContractState.Leaving);

        /// <summary>Everyone counts against the cap until they've actually gone.</summary>
        public int Count => Entries.Count;

        public bool HasRoom(int cap) => Count < cap;

        public ContractEntry? ByHid(string hid) => hid.Length == 0 ? null : Entries.FirstOrDefault(e => e.Hid == hid);

        public ContractEntry? ByContract(string id) => Entries.FirstOrDefault(e => e.ContractId == id);

        public OpOutcome Post(ContractEntry entry, int cap)
        {
            if (!HasRoom(cap))
                return OpOutcome.CapReached;
            entry.State = ContractState.Pending;
            Entries.Add(entry);
            return OpOutcome.Ok;
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
            }
        }

        public static Roster Read(IPackageReader r)
        {
            byte version = r.ReadByte();
            if (version != FormatVersion)
                throw new NotSupportedException($"roster version {version} (this build reads {FormatVersion})");
            var roster = new Roster();
            int n = r.ReadInt();
            for (int i = 0; i < n; i++)
            {
                roster.Entries.Add(new ContractEntry
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
                });
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

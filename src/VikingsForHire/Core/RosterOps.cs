using System;

namespace VikingsForHire.Core
{
    public enum RosterOpType
    {
        Post = 1,
        Cancel = 2,
        Edit = 3,
        Promote = 4,
        Dismiss = 5,
        MarkDied = 6,
        Remove = 7,
    }

    /// <summary>A change to a board's roster, as sent over the network to whoever owns the board.</summary>
    public sealed class RosterOp
    {
        public RosterOpType Type { get; set; }
        public string ContractId { get; set; } = "";
        public string Hid { get; set; } = "";
        public string Name { get; set; } = "";
        public JobType Job { get; set; }
        public int Level { get; set; }
        public float Radius { get; set; }
        public Stance Stance { get; set; }
        public byte[] Snapshot { get; set; } = Array.Empty<byte>();
        /// <summary>Test cheat (vfh_spawn_contract): no fee and immediate arrival.</summary>
        public bool Free { get; set; }

        public void Write(IPackageWriter w)
        {
            w.Write((int)Type);
            w.Write(ContractId);
            w.Write(Hid);
            w.Write(Name);
            w.Write((int)Job);
            w.Write(Level);
            w.Write(Radius);
            w.Write((int)Stance);
            w.Write(Snapshot);
            w.Write(Free ? 1 : 0);
        }

        public static RosterOp Read(IPackageReader r) => new()
        {
            Type = (RosterOpType)r.ReadInt(),
            ContractId = r.ReadString(),
            Hid = r.ReadString(),
            Name = r.ReadString(),
            Job = (JobType)r.ReadInt(),
            Level = r.ReadInt(),
            Radius = r.ReadSingle(),
            Stance = (Stance)r.ReadInt(),
            Snapshot = r.ReadBytes(),
            Free = r.ReadInt() == 1,
        };

        public override string ToString() => $"{Type}(contract={ContractId} hid={Hid} job={Job} level={Level})";
    }

    /// <summary>A change to a hireling's ZDO fields; only the fields that are set get written.</summary>
    public sealed class HirelingOp
    {
        public HirelingMode? Mode { get; set; }
        public Stance? Stance { get; set; }
        public float? Radius { get; set; }
        public int? Level { get; set; }
        public long? LeavingSince { get; set; }
        public string? Status { get; set; }

        public void Write(IPackageWriter w)
        {
            int flags = (Mode.HasValue ? 1 : 0) | (Stance.HasValue ? 2 : 0) | (Radius.HasValue ? 4 : 0) | (Level.HasValue ? 8 : 0)
                        | (LeavingSince.HasValue ? 16 : 0) | (Status != null ? 32 : 0);
            w.Write(flags);
            if (Mode.HasValue) w.Write((int)Mode.Value);
            if (Stance.HasValue) w.Write((int)Stance.Value);
            if (Radius.HasValue) w.Write(Radius.Value);
            if (Level.HasValue) w.Write(Level.Value);
            if (LeavingSince.HasValue) w.Write(LeavingSince.Value);
            if (Status != null) w.Write(Status);
        }

        public static HirelingOp Read(IPackageReader r)
        {
            int flags = r.ReadInt();
            var op = new HirelingOp();
            if ((flags & 1) != 0) op.Mode = (HirelingMode)r.ReadInt();
            if ((flags & 2) != 0) op.Stance = (Stance)r.ReadInt();
            if ((flags & 4) != 0) op.Radius = r.ReadSingle();
            if ((flags & 8) != 0) op.Level = r.ReadInt();
            if ((flags & 16) != 0) op.LeavingSince = r.ReadLong();
            if ((flags & 32) != 0) op.Status = r.ReadString();
            return op;
        }

        public override string ToString() =>
            $"SetFields({(Mode.HasValue ? $"mode={Mode} " : "")}{(Stance.HasValue ? $"stance={Stance} " : "")}{(Radius.HasValue ? $"radius={Radius} " : "")}{(Level.HasValue ? $"level={Level} " : "")}{(LeavingSince.HasValue ? "leaving " : "")}{(Status != null ? $"status={Status}" : "")})".Replace(" )", ")");
    }
}

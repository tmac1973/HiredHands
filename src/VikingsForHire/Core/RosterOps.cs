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
        SetPost = 8,
        Rename = 9,
        SetGather = 10,
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
        /// <summary>SetPost: the post to set, or null to clear it.</summary>
        public GuardPost? Post { get; set; }
        /// <summary>SetGather: items switched off, and whether it stays idle at home.</summary>
        public string SkipItems { get; set; } = "";
        public bool NoHomeWork { get; set; }

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
            w.Write(Post != null ? 1 : 0);
            if (Post != null)
            {
                w.Write(Post.X);
                w.Write(Post.Y);
                w.Write(Post.Z);
                w.Write(Post.Yaw);
            }
            w.Write(SkipItems);
            w.Write(NoHomeWork ? 1 : 0);
        }

        public static RosterOp Read(IPackageReader r)
        {
            RosterOp op = ReadFields(r);
            if (r.ReadInt() == 1)
                op.Post = new GuardPost { X = r.ReadSingle(), Y = r.ReadSingle(), Z = r.ReadSingle(), Yaw = r.ReadSingle() };
            op.SkipItems = r.ReadString();
            op.NoHomeWork = r.ReadInt() == 1;
            return op;
        }

        private static RosterOp ReadFields(IPackageReader r) => new()
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

        /// <summary>Follower fields (phase 12). Owner 0 = no owner.</summary>
        public long? Owner { get; set; }
        public string? OwnerName { get; set; }
        public FollowMode? FollowMode { get; set; }
        public (float X, float Y, float Z)? StayPos { get; set; }
        /// <summary>A guard post: position and facing (yaw). ClearPost removes it.</summary>
        public (float X, float Y, float Z, float Yaw)? Post { get; set; }
        public bool? ClearPost { get; set; }
        public bool? DeliverPending { get; set; }
        public string? Name { get; set; }
        public string? SkipItems { get; set; }
        public bool? NoHomeWork { get; set; }
        /// <summary>Called to the board: stands there, off work, until sent back to work.</summary>
        public bool? Parked { get; set; }

        public void Write(IPackageWriter w)
        {
            int flags = (Mode.HasValue ? 1 : 0) | (Stance.HasValue ? 2 : 0) | (Radius.HasValue ? 4 : 0) | (Level.HasValue ? 8 : 0)
                        | (LeavingSince.HasValue ? 16 : 0) | (Status != null ? 32 : 0) | (Owner.HasValue ? 64 : 0) | (OwnerName != null ? 128 : 0)
                        | (FollowMode.HasValue ? 256 : 0) | (StayPos.HasValue ? 512 : 0) | (DeliverPending.HasValue ? 1024 : 0)
                        | (Post.HasValue ? 2048 : 0) | (ClearPost == true ? 4096 : 0) | (Name != null ? 8192 : 0)
                        | (SkipItems != null ? 16384 : 0) | (NoHomeWork.HasValue ? 32768 : 0) | (Parked.HasValue ? 65536 : 0);
            w.Write(flags);
            if (Mode.HasValue) w.Write((int)Mode.Value);
            if (Stance.HasValue) w.Write((int)Stance.Value);
            if (Radius.HasValue) w.Write(Radius.Value);
            if (Level.HasValue) w.Write(Level.Value);
            if (LeavingSince.HasValue) w.Write(LeavingSince.Value);
            if (Status != null) w.Write(Status);
            if (Owner.HasValue) w.Write(Owner.Value);
            if (OwnerName != null) w.Write(OwnerName);
            if (FollowMode.HasValue) w.Write((int)FollowMode.Value);
            if (StayPos is (float x, float y, float z))
            {
                w.Write(x);
                w.Write(y);
                w.Write(z);
            }
            if (DeliverPending.HasValue) w.Write(DeliverPending.Value ? 1 : 0);
            if (Post is (float px, float py, float pz, float yaw))
            {
                w.Write(px);
                w.Write(py);
                w.Write(pz);
                w.Write(yaw);
            }
            if (Name != null) w.Write(Name);
            if (SkipItems != null) w.Write(SkipItems);
            if (NoHomeWork.HasValue) w.Write(NoHomeWork.Value ? 1 : 0);
            if (Parked.HasValue) w.Write(Parked.Value ? 1 : 0);
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
            if ((flags & 64) != 0) op.Owner = r.ReadLong();
            if ((flags & 128) != 0) op.OwnerName = r.ReadString();
            if ((flags & 256) != 0) op.FollowMode = (FollowMode)r.ReadInt();
            if ((flags & 512) != 0) op.StayPos = (r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            if ((flags & 1024) != 0) op.DeliverPending = r.ReadInt() != 0;
            if ((flags & 2048) != 0) op.Post = (r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            if ((flags & 4096) != 0) op.ClearPost = true;
            if ((flags & 8192) != 0) op.Name = r.ReadString();
            if ((flags & 16384) != 0) op.SkipItems = r.ReadString();
            if ((flags & 32768) != 0) op.NoHomeWork = r.ReadInt() != 0;
            if ((flags & 65536) != 0) op.Parked = r.ReadInt() != 0;
            return op;
        }

        public override string ToString() =>
            $"SetFields({(Mode.HasValue ? $"mode={Mode} " : "")}{(Stance.HasValue ? $"stance={Stance} " : "")}{(Radius.HasValue ? $"radius={Radius} " : "")}{(Level.HasValue ? $"level={Level} " : "")}{(LeavingSince.HasValue ? "leaving " : "")}{(Status != null ? $"status={Status} " : "")}{(Owner.HasValue ? $"owner={Owner} " : "")}{(FollowMode.HasValue ? $"follow={FollowMode} " : "")}{(StayPos.HasValue ? "stay " : "")}{(DeliverPending == true ? "deliver " : "")}{(Name != null ? $"name={Name} " : "")}{(Parked.HasValue ? $"parked={Parked} " : "")})".Replace(" )", ")");
    }
}

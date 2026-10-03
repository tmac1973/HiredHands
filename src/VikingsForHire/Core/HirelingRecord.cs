using System;

namespace VikingsForHire.Core
{
    /// <summary>Minimal binary writer/reader so the record can be tested without Valheim's ZPackage.</summary>
    public interface IPackageWriter
    {
        void Write(byte value);
        void Write(int value);
        void Write(long value);
        void Write(float value);
        void Write(string value);
    }

    public interface IPackageReader
    {
        byte ReadByte();
        int ReadInt();
        long ReadLong();
        float ReadSingle();
        string ReadString();
    }

    /// <summary>
    /// The logical fields of a hireling that server-side code needs without spawning it (which board, what job, where
    /// home is…). A snapshot stores this header followed by every raw ZDO value.
    /// </summary>
    public sealed class HirelingRecord
    {
        public const byte FormatVersion = 1;

        public string Hid { get; set; } = "";
        public string BoardId { get; set; } = "";
        public JobType Job { get; set; }
        public int Level { get; set; } = 1;
        public Stance Stance { get; set; }
        public float Radius { get; set; }
        public float HomeX { get; set; }
        public float HomeY { get; set; }
        public float HomeZ { get; set; }
        public HirelingMode Mode { get; set; } = HirelingMode.Idle;
        public long Owner { get; set; }
        public string Name { get; set; } = "";
        public int Model { get; set; }
        public string Hair { get; set; } = "";
        public string Beard { get; set; } = "";
        public float SkinR { get; set; } = 1f;
        public float SkinG { get; set; } = 1f;
        public float SkinB { get; set; } = 1f;
        public float HairR { get; set; } = 1f;
        public float HairG { get; set; } = 1f;
        public float HairB { get; set; } = 1f;

        public void Write(IPackageWriter w)
        {
            w.Write(FormatVersion);
            w.Write(Hid);
            w.Write(BoardId);
            w.Write((int)Job);
            w.Write(Level);
            w.Write((int)Stance);
            w.Write(Radius);
            w.Write(HomeX);
            w.Write(HomeY);
            w.Write(HomeZ);
            w.Write((int)Mode);
            w.Write(Owner);
            w.Write(Name);
            w.Write(Model);
            w.Write(Hair);
            w.Write(Beard);
            w.Write(SkinR);
            w.Write(SkinG);
            w.Write(SkinB);
            w.Write(HairR);
            w.Write(HairG);
            w.Write(HairB);
        }

        public static HirelingRecord Read(IPackageReader r)
        {
            byte version = r.ReadByte();
            if (version != FormatVersion)
                throw new NotSupportedException($"hireling record version {version} (this build reads {FormatVersion})");
            return new HirelingRecord
            {
                Hid = r.ReadString(),
                BoardId = r.ReadString(),
                Job = (JobType)r.ReadInt(),
                Level = r.ReadInt(),
                Stance = (Stance)r.ReadInt(),
                Radius = r.ReadSingle(),
                HomeX = r.ReadSingle(),
                HomeY = r.ReadSingle(),
                HomeZ = r.ReadSingle(),
                Mode = (HirelingMode)r.ReadInt(),
                Owner = r.ReadLong(),
                Name = r.ReadString(),
                Model = r.ReadInt(),
                Hair = r.ReadString(),
                Beard = r.ReadString(),
                SkinR = r.ReadSingle(),
                SkinG = r.ReadSingle(),
                SkinB = r.ReadSingle(),
                HairR = r.ReadSingle(),
                HairG = r.ReadSingle(),
                HairB = r.ReadSingle(),
            };
        }

        public HirelingRecord Clone() => (HirelingRecord)MemberwiseClone();
    }
}

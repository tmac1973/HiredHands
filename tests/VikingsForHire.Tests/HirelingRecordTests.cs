using System;
using System.IO;
using VikingsForHire.Core;
using Xunit;

namespace VikingsForHire.Tests
{
    public class HirelingRecordTests
    {
        private sealed class Writer : IPackageWriter
        {
            public readonly MemoryStream Stream = new();
            private readonly BinaryWriter _w;
            public Writer() => _w = new BinaryWriter(Stream);
            public void Write(byte value) => _w.Write(value);
            public void Write(int value) => _w.Write(value);
            public void Write(long value) => _w.Write(value);
            public void Write(float value) => _w.Write(value);
            public void Write(string value) => _w.Write(value);
        }

        private sealed class Reader : IPackageReader
        {
            private readonly BinaryReader _r;
            public Reader(byte[] bytes) => _r = new BinaryReader(new MemoryStream(bytes));
            public byte ReadByte() => _r.ReadByte();
            public int ReadInt() => _r.ReadInt32();
            public long ReadLong() => _r.ReadInt64();
            public float ReadSingle() => _r.ReadSingle();
            public string ReadString() => _r.ReadString();
        }

        private static HirelingRecord Sample() => new()
        {
            Hid = "3f2a91c0", BoardId = "b0a7", Job = JobType.GuardRanged, Level = 6, Stance = Stance.Aggressive, Radius = 35.5f,
            HomeX = 12.5f, HomeY = 40f, HomeZ = -300.25f, Mode = HirelingMode.Following, Owner = 1234567890123L, Name = "Sigrun",
            Model = 1, Hair = "Hair14", Beard = "BeardNone", SkinR = 0.9f, SkinG = 0.8f, SkinB = 0.7f, HairR = 0.3f, HairG = 0.2f, HairB = 0.1f,
        };

        [Fact]
        public void RoundTripsEveryField()
        {
            var w = new Writer();
            Sample().Write(w);
            HirelingRecord back = HirelingRecord.Read(new Reader(w.Stream.ToArray()));
            Assert.Equivalent(Sample(), back);
        }

        [Fact]
        public void RejectsUnknownVersion()
        {
            var w = new Writer();
            Sample().Write(w);
            byte[] bytes = w.Stream.ToArray();
            bytes[0] = 99;
            Assert.Throws<NotSupportedException>(() => HirelingRecord.Read(new Reader(bytes)));
        }

        [Fact]
        public void CloneIsIndependent()
        {
            HirelingRecord a = Sample();
            HirelingRecord b = a.Clone();
            b.Level = 2;
            Assert.Equal(6, a.Level);
        }
    }
}

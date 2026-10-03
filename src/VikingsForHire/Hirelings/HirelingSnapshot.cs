using System;
using System.Collections.Generic;
using System.Linq;
using VikingsForHire.Core;
using UnityEngine;

namespace VikingsForHire.Hirelings
{
    /// <summary>
    /// A hireling frozen into bytes: a <see cref="HirelingRecord"/> header followed by every value stored in its ZDO
    /// (all seven ZDO data types, by key hash). Because the copy is generic, keys added by later phases, the cargo
    /// (Container "items") and current health travel through portals, ships and return-home without changes here.
    /// </summary>
    internal sealed class HirelingSnapshot
    {
        private const byte FormatVersion = 1;

        public HirelingRecord Record { get; }
        public readonly Dictionary<int, float> Floats = new();
        public readonly Dictionary<int, Vector3> Vec3s = new();
        public readonly Dictionary<int, Quaternion> Quats = new();
        public readonly Dictionary<int, int> Ints = new();
        public readonly Dictionary<int, long> Longs = new();
        public readonly Dictionary<int, string> Strings = new();
        public readonly Dictionary<int, byte[]> Bytes = new();

        private HirelingSnapshot(HirelingRecord record) => Record = record;

        /// <summary>Everything stored on a live or unloaded hireling ZDO.</summary>
        public static HirelingSnapshot FromZdo(ZDO zdo)
        {
            var snap = new HirelingSnapshot(RecordFrom(zdo));
            ZDOID id = zdo.m_uid;
            Copy(ZDOExtraData.s_floats, id, snap.Floats);
            Copy(ZDOExtraData.s_vec3, id, snap.Vec3s);
            Copy(ZDOExtraData.s_quats, id, snap.Quats);
            Copy(ZDOExtraData.s_ints, id, snap.Ints);
            Copy(ZDOExtraData.s_longs, id, snap.Longs);
            Copy(ZDOExtraData.s_strings, id, snap.Strings);
            Copy(ZDOExtraData.s_byteArrays, id, snap.Bytes);
            return snap;
        }

        /// <summary>A hireling that doesn't exist yet (e.g. a new contract): the record's fields become its ZDO values.</summary>
        public static HirelingSnapshot Create(HirelingRecord record, IDictionary<string, object>? extra = null)
        {
            var snap = new HirelingSnapshot(record.Clone());
            snap.Strings[HirelingZdo.Hid.GetStableHashCode()] = record.Hid;
            snap.Strings[HirelingZdo.BoardId.GetStableHashCode()] = record.BoardId;
            snap.Ints[HirelingZdo.Job.GetStableHashCode()] = (int)record.Job;
            snap.Ints[HirelingZdo.Level.GetStableHashCode()] = record.Level;
            snap.Ints[HirelingZdo.Stance.GetStableHashCode()] = (int)record.Stance;
            snap.Floats[HirelingZdo.Radius.GetStableHashCode()] = record.Radius;
            snap.Vec3s[HirelingZdo.Home.GetStableHashCode()] = new Vector3(record.HomeX, record.HomeY, record.HomeZ);
            snap.Ints[HirelingZdo.Mode.GetStableHashCode()] = (int)record.Mode;
            snap.Longs[HirelingZdo.Owner.GetStableHashCode()] = record.Owner;
            snap.Strings[HirelingZdo.Name.GetStableHashCode()] = record.Name;
            snap.Ints[HirelingZdo.Model.GetStableHashCode()] = record.Model;
            snap.Strings[HirelingZdo.Hair.GetStableHashCode()] = record.Hair;
            snap.Strings[HirelingZdo.Beard.GetStableHashCode()] = record.Beard;
            snap.Vec3s[HirelingZdo.Skin.GetStableHashCode()] = new Vector3(record.SkinR, record.SkinG, record.SkinB);
            snap.Vec3s[HirelingZdo.HairColor.GetStableHashCode()] = new Vector3(record.HairR, record.HairG, record.HairB);
            if (extra != null)
            {
                foreach (KeyValuePair<string, object> e in extra)
                    snap.Put(e.Key.GetStableHashCode(), e.Value);
            }
            return snap;
        }

        /// <summary>
        /// Creates a new ZDO holding every value. ZNetScene instantiates it like any other object once it's in a loaded
        /// area, so this works whether or not anyone is nearby. Returns the new ZDO.
        /// </summary>
        public ZDO Spawn(Vector3 position, Quaternion rotation)
        {
            ZDO zdo = ZDOMan.instance.CreateNewZDO(position, HirelingZdo.PrefabHash);
            zdo.Persistent = true;
            zdo.SetPrefab(HirelingZdo.PrefabHash);
            zdo.SetRotation(rotation);
            foreach (var kv in Floats) zdo.Set(kv.Key, kv.Value);
            foreach (var kv in Vec3s) zdo.Set(kv.Key, kv.Value);
            foreach (var kv in Quats) zdo.Set(kv.Key, kv.Value);
            foreach (var kv in Ints) zdo.Set(kv.Key, kv.Value);
            foreach (var kv in Longs) zdo.Set(kv.Key, kv.Value);
            foreach (var kv in Strings) zdo.Set(kv.Key, kv.Value);
            foreach (var kv in Bytes) zdo.Set(kv.Key, kv.Value);
            return zdo;
        }

        public byte[] ToBytes()
        {
            var pkg = new ZPackage();
            pkg.Write(FormatVersion);
            Record.Write(new PackageWriter(pkg));
            WriteSection(pkg, Floats, pkg.Write);
            WriteSection(pkg, Vec3s, pkg.Write);
            WriteSection(pkg, Quats, pkg.Write);
            WriteSection(pkg, Ints, pkg.Write);
            WriteSection(pkg, Longs, pkg.Write);
            WriteSection(pkg, Strings, pkg.Write);
            WriteSection(pkg, Bytes, pkg.Write);
            return pkg.GetArray();
        }

        public static HirelingSnapshot FromBytes(byte[] bytes)
        {
            var pkg = new ZPackage(bytes);
            byte version = pkg.ReadByte();
            if (version != FormatVersion)
                throw new NotSupportedException($"hireling snapshot version {version} (this build reads {FormatVersion})");
            var snap = new HirelingSnapshot(HirelingRecord.Read(new PackageReader(pkg)));
            ReadSection(pkg, snap.Floats, pkg.ReadSingle);
            ReadSection(pkg, snap.Vec3s, pkg.ReadVector3);
            ReadSection(pkg, snap.Quats, pkg.ReadQuaternion);
            ReadSection(pkg, snap.Ints, pkg.ReadInt);
            ReadSection(pkg, snap.Longs, pkg.ReadLong);
            ReadSection(pkg, snap.Strings, pkg.ReadString);
            ReadSection(pkg, snap.Bytes, pkg.ReadByteArray);
            return snap;
        }

        public int ValueCount => Floats.Count + Vec3s.Count + Quats.Count + Ints.Count + Longs.Count + Strings.Count + Bytes.Count;

        /// <summary>Saved values (keys in this snapshot) that are missing or different in <paramref name="other"/>. Floats within 0.5 match.</summary>
        public List<string> Differences(HirelingSnapshot other, ISet<int> ignore)
        {
            var diffs = new List<string>();
            Diff(Floats, other.Floats, (a, b) => Mathf.Abs(a - b) < 0.5f, "float", ignore, diffs);
            Diff(Vec3s, other.Vec3s, (a, b) => Vector3.Distance(a, b) < 0.01f, "vec3", ignore, diffs);
            Diff(Quats, other.Quats, (a, b) => Quaternion.Angle(a, b) < 0.5f, "quat", ignore, diffs);
            Diff(Ints, other.Ints, (a, b) => a == b, "int", ignore, diffs);
            Diff(Longs, other.Longs, (a, b) => a == b, "long", ignore, diffs);
            Diff(Strings, other.Strings, (a, b) => a == b, "string", ignore, diffs);
            Diff(Bytes, other.Bytes, (a, b) => a.SequenceEqual(b), "bytes", ignore, diffs);
            return diffs;
        }

        public static HirelingRecord RecordFrom(ZDO zdo)
        {
            Vector3 home = zdo.GetVec3(HirelingZdo.Home, zdo.GetPosition());
            Vector3 skin = zdo.GetVec3(HirelingZdo.Skin, Vector3.one);
            Vector3 hair = zdo.GetVec3(HirelingZdo.HairColor, Vector3.one);
            return new HirelingRecord
            {
                Hid = zdo.GetString(HirelingZdo.Hid),
                BoardId = zdo.GetString(HirelingZdo.BoardId),
                Job = (JobType)zdo.GetInt(HirelingZdo.Job),
                Level = Math.Max(1, zdo.GetInt(HirelingZdo.Level, 1)),
                Stance = (Stance)zdo.GetInt(HirelingZdo.Stance),
                Radius = zdo.GetFloat(HirelingZdo.Radius),
                HomeX = home.x, HomeY = home.y, HomeZ = home.z,
                Mode = (HirelingMode)zdo.GetInt(HirelingZdo.Mode, (int)HirelingMode.Idle),
                Owner = zdo.GetLong(HirelingZdo.Owner),
                Name = zdo.GetString(HirelingZdo.Name),
                Model = zdo.GetInt(HirelingZdo.Model),
                Hair = zdo.GetString(HirelingZdo.Hair),
                Beard = zdo.GetString(HirelingZdo.Beard),
                SkinR = skin.x, SkinG = skin.y, SkinB = skin.z,
                HairR = hair.x, HairG = hair.y, HairB = hair.z,
            };
        }

        private void Put(int hash, object value)
        {
            switch (value)
            {
                case float f: Floats[hash] = f; break;
                case Vector3 v: Vec3s[hash] = v; break;
                case Quaternion q: Quats[hash] = q; break;
                case int i: Ints[hash] = i; break;
                case bool b: Ints[hash] = b ? 1 : 0; break;
                case long l: Longs[hash] = l; break;
                case string s: Strings[hash] = s; break;
                case byte[] bytes: Bytes[hash] = bytes; break;
                default: throw new ArgumentException($"unsupported snapshot value type {value?.GetType().Name}");
            }
        }

        private static void Copy<T>(Dictionary<ZDOID, BinarySearchDictionary<int, T>> source, ZDOID id, Dictionary<int, T> target)
        {
            if (!source.TryGetValue(id, out BinarySearchDictionary<int, T> values))
                return;
            foreach (KeyValuePair<int, T> kv in values)
                target[kv.Key] = kv.Value;
        }

        private static void WriteSection<T>(ZPackage pkg, Dictionary<int, T> values, Action<T> write)
        {
            pkg.Write(values.Count);
            foreach (KeyValuePair<int, T> kv in values)
            {
                pkg.Write(kv.Key);
                write(kv.Value);
            }
        }

        private static void ReadSection<T>(ZPackage pkg, Dictionary<int, T> values, Func<T> read)
        {
            int count = pkg.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int key = pkg.ReadInt();
                values[key] = read();
            }
        }

        private static void Diff<T>(Dictionary<int, T> a, Dictionary<int, T> b, Func<T, T, bool> same, string type, ISet<int> ignore, List<string> diffs)
        {
            foreach (int key in a.Keys.Union(b.Keys))
            {
                if (ignore.Contains(key))
                    continue;
                bool inA = a.TryGetValue(key, out T va), inB = b.TryGetValue(key, out T vb);
                // Values a fresh instance creates for itself (physics/animation runtime state) weren't in the snapshot,
                // so they can't have been lost. The test is that everything saved came back unchanged.
                if (!inA)
                    continue;
                if (!inA || !inB || !same(va, vb))
                    diffs.Add($"{type}:{HirelingKeyNames.Name(key)}={(inA ? Show(va) : "missing")}/{(inB ? Show(vb) : "missing")}");
            }
        }

        private static string Show<T>(T value) => value is byte[] bytes ? $"{bytes.Length}b" : value?.ToString() ?? "null";

        private sealed class PackageWriter : IPackageWriter
        {
            private readonly ZPackage _pkg;
            public PackageWriter(ZPackage pkg) => _pkg = pkg;
            public void Write(byte value) => _pkg.Write(value);
            public void Write(int value) => _pkg.Write(value);
            public void Write(long value) => _pkg.Write(value);
            public void Write(float value) => _pkg.Write(value);
            public void Write(string value) => _pkg.Write(value);
        }

        private sealed class PackageReader : IPackageReader
        {
            private readonly ZPackage _pkg;
            public PackageReader(ZPackage pkg) => _pkg = pkg;
            public byte ReadByte() => _pkg.ReadByte();
            public int ReadInt() => _pkg.ReadInt();
            public long ReadLong() => _pkg.ReadLong();
            public float ReadSingle() => _pkg.ReadSingle();
            public string ReadString() => _pkg.ReadString();
        }
    }

    /// <summary>Turns key hashes back into names in logs, for our keys and the vanilla ones a hireling uses.</summary>
    internal static class HirelingKeyNames
    {
        private static readonly Dictionary<int, string> Names = new[]
        {
            HirelingZdo.Hid, HirelingZdo.BoardId, HirelingZdo.Job, HirelingZdo.Level, HirelingZdo.Stance, HirelingZdo.Mode,
            HirelingZdo.FollowMode, HirelingZdo.Owner, HirelingZdo.OwnerName, HirelingZdo.Radius, HirelingZdo.LeavingSince,
            HirelingZdo.Order, HirelingZdo.DeliverPending, HirelingZdo.Name, HirelingZdo.Model, HirelingZdo.Hair, HirelingZdo.Beard,
            HirelingZdo.Skin, HirelingZdo.HairColor, HirelingZdo.Home, HirelingZdo.Status, HirelingZdo.Initialized,
            "health", "max_health", "items", "ModelIndex", "HairItem", "BeardItem", "SkinColor", "HairColor", "seed", "spawntime",
            "spawnpoint", "level", "tamed", "RightItem", "LeftItem", "ChestItem", "LegItem", "HelmetItem", "ShoulderItem", "UtilityItem",
        }.Distinct().ToDictionary(n => n.GetStableHashCode(), n => n);

        public static string Name(int hash) => Names.TryGetValue(hash, out string name) ? name : hash.ToString();
    }
}

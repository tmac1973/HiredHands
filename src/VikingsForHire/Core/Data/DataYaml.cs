using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace VikingsForHire.Core.Data
{
    public static class DataYaml
    {
        private const string Header =
            "# VikingsForHire data tables. Edit and save: the server (or single-player game) reloads within a second\n" +
            "# and sends the new values to connected clients. Item names are prefab names (e.g. Wood, TrophyEikthyr).\n" +
            "# A file that fails validation is ignored and the built-in defaults are used; check BepInEx/VikingsForHire.log.\n";

        private static readonly ISerializer Serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .DisableAliases()
            .Build();

        private static readonly IDeserializer Deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        public static string Serialize(VfhData data) => Header + Serializer.Serialize(data);

        /// <summary>Throws YamlDotNet exceptions on malformed input or unknown keys, so typos aren't silently ignored.</summary>
        public static VfhData Deserialize(string yaml) => Deserialize(yaml, out _);

        /// <summary>As above, with settings the file doesn't mention taken from the defaults (listed in <paramref name="filled"/>).</summary>
        public static VfhData Deserialize(string yaml, out List<string> filled)
        {
            VfhData data = Deserializer.Deserialize<VfhData>(yaml) ?? new VfhData();
            object? raw = Deserializer.Deserialize<object>(yaml);
            filled = DataDefaults.FillMissing(data, DefaultData.Create(), raw);
            return data;
        }

        public static string Hash(string text)
        {
            using SHA1 sha = SHA1.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
            return BitConverter.ToString(bytes, 0, 4).Replace("-", "").ToLowerInvariant();
        }
    }
}

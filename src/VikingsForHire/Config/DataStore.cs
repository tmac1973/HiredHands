using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Jotunn.Entities;
using Jotunn.Managers;
using VikingsForHire.Core.Data;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;
using UnityEngine;

namespace VikingsForHire.Config
{
    /// <summary>
    /// Owns Spronglehump.HiredHands.yml: writes defaults on first run, validates, hot-reloads on the server or in
    /// single-player, and pushes the server's tables to clients (on join and after every reload).
    /// </summary>
    internal static class DataStore
    {
        private const float ReloadDebounceSeconds = 1f;

        public static string FilePath { get; private set; } = "";
        public static VfhData Current { get; private set; } = DefaultData.Create();

        /// <summary>local = this machine's file, server = received from the server, defaults = the file was rejected.</summary>
        public static string Source { get; private set; } = "defaults";
        public static string Hash { get; private set; } = "";
        public static int Reloads { get; private set; }
        public static event Action? Changed;

        private static string _effectiveYaml = "";
        private static CustomRPC _syncRpc = null!;
        private static FileSystemWatcher? _watcher;
        private static volatile bool _fileDirty;
        private static float _dirtySince;
        private static bool _prefabsReady;

        public static void Init()
        {
            FilePath = Path.Combine(Paths.ConfigPath, "Spronglehump.HiredHands.yml");
            LoadLocal("startup");

            _syncRpc = NetworkManager.Instance.AddRPC("VFH_DataSync", OnServerReceive, OnClientReceive);
            SynchronizationManager.Instance.AddInitialSynchronization(_syncRpc, BuildPackage);

            PrefabManager.OnVanillaPrefabsAvailable += () =>
            {
                _prefabsReady = true;
                Sanitize();
            };

            try
            {
                _watcher = new FileSystemWatcher(Paths.ConfigPath, Path.GetFileName(FilePath))
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.FileName,
                    EnableRaisingEvents = true,
                };
                // Raised on a worker thread: only flag it, Tick() reloads on the main thread.
                _watcher.Changed += (_, _) => _fileDirty = true;
                _watcher.Created += (_, _) => _fileDirty = true;
                _watcher.Renamed += (_, _) => _fileDirty = true;
            }
            catch (Exception ex)
            {
                VfhLog.Exception(LogCat.Data, "data.watch_failed", ex, ("path", FilePath));
            }
        }

        /// <summary>Called every frame from Plugin.Update.</summary>
        public static void Tick()
        {
            if (!_fileDirty)
                return;
            if (_dirtySince == 0f)
            {
                _dirtySince = Time.realtimeSinceStartup;
                return;
            }
            if (Time.realtimeSinceStartup - _dirtySince < ReloadDebounceSeconds)
                return;
            _fileDirty = false;
            _dirtySince = 0f;

            // A connected client runs on the server's tables; its local file only matters again after disconnecting.
            if (Source == "server")
            {
                VfhLog.D(LogCat.Data, "data.reload_skipped", ("reason", "using server data"));
                return;
            }

            LoadLocal("file changed");
            if (ZNet.instance != null && ZNet.instance.IsServer())
                Broadcast();
        }

        /// <summary>Back to this machine's file after leaving a server.</summary>
        public static void OnDisconnected()
        {
            if (Source == "server")
                LoadLocal("left server");
        }

        public static void LoadLocal(string reason)
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    File.WriteAllText(FilePath, DataYaml.Serialize(DefaultData.Create()));
                    VfhLog.I(LogCat.Data, "data.defaults_written", ("path", FilePath));
                }
                Apply(File.ReadAllText(FilePath), "local", reason);
            }
            catch (Exception ex)
            {
                VfhLog.Exception(LogCat.Data, "data.read_failed", ex, ("path", FilePath));
                UseDefaults(reason);
            }
        }

        private static void Apply(string yaml, string source, string reason)
        {
            VfhData data;
            List<string> filled;
            try
            {
                data = DataYaml.Deserialize(yaml, out filled);
            }
            catch (Exception ex)
            {
                var yex = ex as YamlDotNet.Core.YamlException;
                VfhLog.E(LogCat.Data, "data.parse_error", ("source", source), ("line", yex?.Start.Line), ("col", yex?.Start.Column),
                    ("msg", (ex.InnerException ?? ex).Message.Replace('\n', ' ')), ("fallback", "defaults"));
                UseDefaults(reason);
                return;
            }

            if (filled.Count > 0)
                VfhLog.I(LogCat.Data, "data.defaults_filled", ("source", source), ("n", filled.Count),
                    ("keys", string.Join(",", filled.Take(20))), ("note", "settings missing from the file (older version?) use the shipped defaults"));

            List<string> errors = DataValidator.Validate(data);
            if (errors.Count > 0)
            {
                foreach (string e in errors)
                    VfhLog.E(LogCat.Data, "data.invalid", ("source", source), ("error", e));
                UseDefaults(reason);
                return;
            }

            Set(data, yaml, source, reason);
        }

        private static void UseDefaults(string reason)
        {
            VfhData defaults = DefaultData.Create();
            Set(defaults, DataYaml.Serialize(defaults), "defaults", reason);
        }

        private static void Set(VfhData data, string yaml, string source, string reason)
        {
            Current = data;
            _effectiveYaml = yaml;
            Source = source;
            Hash = DataYaml.Hash(yaml);
            Reloads++;
            if (_prefabsReady)
                Sanitize();
            VfhLog.I(LogCat.Data, "data.reload", ("source", source), ("reason", reason), ("hash", Hash), ("reloads", Reloads));
            VfhLog.Guard(LogCat.Data, "data.changed_handler", () => Changed?.Invoke());
        }

        private static void Sanitize()
        {
            List<string> warnings = DataValidator.Sanitize(Current, ItemExists);
            foreach (string w in warnings)
                VfhLog.W(LogCat.Data, "data.unknown_item", ("detail", w));
            VfhLog.I(LogCat.Data, "data.prefabs_checked", ("unknownItems", warnings.Count));
        }

        // The data is checked when vanilla prefabs appear, before our own items (and other mods' Jotunn items) may be registered.
        public static bool ItemExists(string prefab) =>
            PrefabManager.Cache.GetPrefab<ItemDrop>(prefab) != null
            || prefab == Hirelings.BroomItem.PrefabName
            || prefab == Followers.CommandStoneItem.PrefabName
            || prefab == Board.HiringCharter.PrefabName
            || ItemManager.Instance.GetItem(prefab) != null;

        private static ZPackage BuildPackage()
        {
            var pkg = new ZPackage();
            pkg.Write(_effectiveYaml);
            return pkg;
        }

        private static void Broadcast()
        {
            if (ZNet.instance == null || ZNet.instance.GetPeers().Count == 0)
                return;
            _syncRpc.SendPackage(ZNet.instance.GetPeers(), BuildPackage());
            VfhLog.I(LogCat.Net, "data.broadcast", ("peers", ZNet.instance.GetPeers().Count), ("hash", Hash));
        }

        private static IEnumerator OnServerReceive(long sender, ZPackage package)
        {
            // Clients never send data tables.
            yield break;
        }

        private static IEnumerator OnClientReceive(long sender, ZPackage package)
        {
            string yaml = package.ReadString();
            VfhLog.Guard(LogCat.Data, "data.receive", () => Apply(yaml, "server", "server sync"));
            yield break;
        }
    }
}

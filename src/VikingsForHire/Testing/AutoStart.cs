using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using VikingsForHire.Core.Diagnostics;
using VikingsForHire.Diagnostics;

namespace VikingsForHire.Testing
{
    /// <summary>
    /// Launch options for quick test runs, set as a Gale profile's custom arguments (only on the dev profile):
    ///   -vfh-world &lt;name&gt;           skip the menus and start this single-player world
    ///   -vfh-character &lt;name&gt;       with this character (default: the last one played)
    ///   -vfh-test &lt;a,b,c&gt;           once in the world, run vfh_test_chain a b c
    /// Only on the first visit to the main menu: going back to it later stays there.
    /// </summary>
    [HarmonyPatch]
    internal static class AutoStart
    {
        private static bool _menuDone;
        private static bool _testsDone;

        private static string? Arg(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int i = Array.FindIndex(args, a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
            return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : null;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Start))]
        private static void OnMenu(FejdStartup __instance)
        {
            if (_menuDone || Arg("-vfh-world") is not string world)
                return;
            _menuDone = true;
            __instance.StartCoroutine(StartWorld(__instance, world, Arg("-vfh-character")));
        }

        private static IEnumerator StartWorld(FejdStartup menu, string world, string? character)
        {
            yield return new WaitForSeconds(1f); // let the menu and its character preview settle
            try
            {
                if (character != null)
                {
                    PlayerProfile? p = SaveSystem.GetAllPlayerProfiles().FirstOrDefault(x => string.Equals(x.GetName(), character, StringComparison.OrdinalIgnoreCase));
                    if (p == null)
                        throw new InvalidOperationException($"no character named {character}");
                    menu.SetSelectedProfile(p.GetFilename());
                }
                if (menu.m_profiles == null || menu.m_profiles.Count == 0)
                    throw new InvalidOperationException("no characters");
                menu.m_mainMenu.SetActive(false);
                menu.UpdateWorldList(true);
                menu.m_world = menu.FindWorld(world) ?? throw new InvalidOperationException($"no world named {world}");
                menu.m_openServerToggle.isOn = false; // single player
                menu.m_publicServerToggle.isOn = false;
                VfhLog.I(LogCat.Test, "autostart.world", ("world", world), ("character", menu.m_profiles[menu.m_profileIndex].GetName()));
                menu.OnWorldStart();
            }
            catch (Exception e)
            {
                VfhLog.W(LogCat.Test, "autostart.failed", ("why", e.Message));
                menu.m_mainMenu.SetActive(true);
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        private static void OnSpawned(Player __instance)
        {
            if (_testsDone || __instance != Player.m_localPlayer || Arg("-vfh-test") is not string tests)
                return;
            _testsDone = true;
            __instance.StartCoroutine(RunTests(tests));
        }

        private static IEnumerator RunTests(string tests)
        {
            yield return new WaitForSeconds(10f); // the area around the player loads, devcommands switch on
            string command = "vfh_test_chain " + string.Join(" ", tests.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()));
            VfhLog.I(LogCat.Test, "autostart.tests", ("command", command));
            Console.instance.TryRunCommand(command);
        }
    }
}

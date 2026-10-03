# Phase 01 — Repo, build scaffolding & loading plugin

**Depends on:** nothing · **Enables:** every later phase (a project that builds, deploys to the dev profile, runs unit tests, and loads in-game)

## Goal
Set up an empty but real mod that copies the proven `vh_pull_mats` toolchain: a git repo, an SDK-style net472 plugin project compiled against the local Valheim install, Jotunn and YamlDotNet, an xUnit test project for pure logic, a dedicated Gale dev profile, a deploy-to-Gale post-build step, a Thunderstore Package target, and a local dedicated-server script. The plugin only logs that it loaded. This shows the whole toolchain works on Linux/Gale before any feature code is written.

## Files touched
- `.gitignore`: same as PullMats (`bin/`, `obj/`, `dist/`, `*.user`, `VikingsForHire.user.props`, `.idea/`, `.vs/`).
- `Directory.Build.props`: copied from PullMats. `GaleProfileDir` defaults to `.../profiles/vikingsforhire-dev`. Imports `VikingsForHire.user.props` if it exists.
- `VikingsForHire.user.props.example`: sample override file (`ValheimDir`, `GaleProfileDir`).
- `VikingsForHire.sln`: contains `src/VikingsForHire/VikingsForHire.csproj` and `tests/VikingsForHire.Tests/VikingsForHire.Tests.csproj`.
- `src/VikingsForHire/VikingsForHire.csproj`: net472 plugin project, references, `DeployToGale` and `Package` targets.
- `src/VikingsForHire/Plugin.cs`: `BaseUnityPlugin` with attributes, static `Log`, loaded log line.
- `src/VikingsForHire/Core/IsExternalInit.cs`: polyfill for `init`/records on net472 (as in PullMats).
- `src/VikingsForHire/Core/Placeholder.cs`: `public static class CoreInfo { public const string Name = "VikingsForHire.Core"; }` so the test project has something to link (removed in phase 02).
- `tests/VikingsForHire.Tests/VikingsForHire.Tests.csproj`: net10.0 xUnit project linking `../../src/VikingsForHire/Core/**/*.cs`.
- `tests/VikingsForHire.Tests/SmokeTests.cs`: one test asserting `CoreInfo.Name` (removed together with `Placeholder.cs` in phase 02).
- `package/manifest.json`: name `VikingsForHire`, version `0.1.0`, dependencies `denikson-BepInExPack_Valheim-5.4.2350`, `ValheimModding-Jotunn-2.30.1`, `ValheimModding-YamlDotNet-16.3.1`.
- `package/icon.png`: 256×256 placeholder icon (a coin over a rune-carved board, made in any image editor).
- `scripts/run-dedicated-server.sh`: copy of the PullMats script, renamed to `--no-vfh` / `--vfh-dll`, profile `vikingsforhire-dev`, save dir `vfh-save`, world/server name `VikingsForHireTest`, no password (LAN-only test server).
- `test/alias_vfh.yaml`: the ServerDevcommands alias file holding every test macro. It starts with one alias, `vfh_t_smoke`, which runs `vfh_log_mark smoke;broadcast center VikingsForHire loaded` once phase 02 exists and plain `broadcast center VikingsForHire loaded` before then (vanilla has no `echo`; ServerDevcommands' `broadcast` shows a centre message). Later phases add one alias per checklist row.
- `README.md`, `CHANGELOG.md`: stubs (`## 0.1.0 — unreleased`).
- `docs/test-checklist.md`: header and Setup section copied and adapted from PullMats. The matrix starts empty, and each later phase appends rows. Modes are `SP` (single-player, always tested first) and `D` (local dedicated server via `scripts/run-dedicated-server.sh`, tested second). `L` (listen host) is only used where a row says so. The Setup section says where each log file lives in each mode: the client `.../profiles/vikingsforhire-dev/BepInEx/{LogOutput.log,VikingsForHire.log}` and the server `.../Valheim dedicated server/BepInEx/{LogOutput.log,VikingsForHire.log}`. It also says that every bug report needs both files from the run, plus the `vfh_log_mark` text used.

## Steps
1. **Gale dev profile (done by the user):** `vikingsforhire-dev`, duplicated from `pullmats-dev`. Enabled: `ValheimModding-Jotunn`, `ValheimModding-YamlDotNet`, `ValheimModding-JsonDotNET`, `shudnal-ConfigurationManager`, `shudnal-ConditionalConfigSync`, `JereKuusela-Server_devcommands` (aliases, `;` command chaining and `wait`, used by the test macros), `Azumatt-AzuAutoStore`, `Azumatt-AzuCraftyBoxes` and `Spronglehump-PullMats`. Disabled: `Grantapher-ValheimPlus_Grantapher_Temporary`. All the enabled mods are on for normal testing. Rows that need a mod absent (e.g. Smelter without AzuAutoStore) say so, and you toggle it in Gale for that row.
2. `git init` in `/home/tim/Projects/vh_vikings_for_hire`, branch `main`. The existing `plan/` folder is committed with the scaffold.
3. Write `Directory.Build.props`, which matches PullMats exactly except the profile name and user props filename.
4. Write `src/VikingsForHire/VikingsForHire.csproj` based on `vh_pull_mats/src/PullMats/PullMats.csproj`:
   - `TargetFramework net472`, `LangVersion latest`, `Nullable enable`, `AssemblyName VikingsForHire`, `RootNamespace VikingsForHire`, `AppendTargetFrameworkToOutputPath false`, `GenerateAssemblyInfo false`, `Version 0.1.0`.
   - The same NuGet references (ReferenceAssemblies 1.0.3, AssemblyPublicizer 0.4.3, JotunnLib 2.30.1 with `ExcludeAssets="runtime;build"`).
   - The same Valheim/Unity file references, plus `UnityEngine.AnimationModule.dll`, `UnityEngine.AIModule.dll`, `UnityEngine.UIModule.dll`, `UnityEngine.TextRenderingModule.dll`, and `$(PluginsDir)/ValheimModding-YamlDotNet/YamlDotNet.dll`, all `Private=false`. Drop the AzuCraftyBoxes reference. AzuAutoStore, AzuCraftyBoxes and PullMats are **never** referenced at compile time. All compat goes through reflection.
   - `DeployToGale` copies to `$(PluginsDir)/Spronglehump-VikingsForHire/`, and also copies `test/alias_vfh.yaml` to `$(GaleProfileDir)/BepInEx/config/alias_vfh.yaml`, overwriting it. ServerDevcommands loads `alias*.yaml` from the config folder and reloads it when it changes. The `Package` target never includes it.
   - `Package` target is identical to PullMats with the zip at `dist/Spronglehump-VikingsForHire-$(Version).zip` and the same three-way version check.
5. Write `Plugin.cs`:
   - `Guid = "Spronglehump.VikingsForHire"`, `Name = "VikingsForHire"`, `Version = "0.1.0"`.
   - `[BepInDependency(Jotunn.Main.ModGuid)]`, `[BepInDependency("com.ValheimModding.YamlDotNetDetector")]`, `[BepInDependency("Azumatt.AzuAutoStore", BepInDependency.DependencyFlags.SoftDependency)]`, `[BepInDependency("Azumatt.AzuCraftyBoxes", BepInDependency.DependencyFlags.SoftDependency)]`, `[BepInDependency("Spronglehump.PullMats", BepInDependency.DependencyFlags.SoftDependency)]` (soft dependencies load first, so the compat patches find their targets).
   - `[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]`.
   - `Awake()` stores `Logger` in static `Log`, creates `new Harmony(Guid)` held in a static `Harmony` field (later phases call `PatchAll`), and logs `$"{Name} {Version} loaded"`.
6. Write the test project (Microsoft.NET.Test.Sdk 17.14.1, xunit 2.9.3, xunit.runner.visualstudio 3.1.4, same as PullMats) and the smoke test.
7. Copy and adapt `scripts/run-dedicated-server.sh` and mark it executable. After the config seeding, it always copies `test/alias_vfh.yaml` to the server's `BepInEx/config/` (overwriting).
7a. **Alias file format check:** in-game, run `alias vfh_probe echo hi` once and look at the `alias.yaml` ServerDevcommands writes. Make `test/alias_vfh.yaml` use exactly that format, then delete the probe alias.
8. Create the GitHub repo `gh repo create tmac1973/VikingsForHire --public --source . --remote origin` and push after the first commit.

## Build gate
- `dotnet build -c Release` at the repo root: 0 errors, 0 warnings from our code.
- `dotnet test`: 1 passing test.
- `src/VikingsForHire/bin/Release/VikingsForHire.dll` copied to `.../profiles/vikingsforhire-dev/BepInEx/plugins/Spronglehump-VikingsForHire/`.
- `dotnet build -c Release -t:Package` produces `dist/Spronglehump-VikingsForHire-0.1.0.zip`.

## Test plan
- Launch Valheim via Gale `vikingsforhire-dev`: `LogOutput.log` contains `VikingsForHire 0.1.0 loaded` after the Jotunn line. Typing `vfh_t_smoke` in the console shows `VikingsForHire loaded` in the centre of the screen (the alias file was deployed and loaded).
- `scripts/run-dedicated-server.sh` starts the server with the plugin, and its log has the same loaded line. Joining from the dev profile succeeds.
- Join that server from a profile **without** the mod: Jotunn's version check rejects the connection with an incompatibility message (this confirms `EveryoneMustHaveMod`).

## Commit
`chore: scaffold VikingsForHire plugin, tests, packaging and dev server script`

## Rollback
Delete the repo contents (keep `plan/`) and `.../plugins/Spronglehump-VikingsForHire`. Nothing else is touched. The Gale profile can be deleted from Gale.

# Phase 02 — Configuration, diagnostics/logging, data tables & core rules

**Depends on:** 01 · **Enables:** 03–16 (every tunable value, the logging every phase uses, and every pure rule later phases call)

## Goal
Build the configuration backbone, the **structured debug logging** that every later phase reports through, and the Unity-free rule engine, all before any gameplay. Logging comes first so that every bug found later can be traced from the log files alone. Simple tunables go in a server-synced BepInEx `.cfg`. Structured tables (recipes, level stats, costs, gear, names) go in a YAML data file that the server sends to clients. All of the math that decides costs, food points, level limits, base qualification, deposit planning, and orphan timers lives in pure `Core/` classes with full unit-test coverage, so later phases only connect them to the game.

## Files touched
- `src/VikingsForHire/Core/Placeholder.cs` and `tests/VikingsForHire.Tests/SmokeTests.cs`: deleted.
- `src/VikingsForHire/Commands/DebugCommands.cs`: created. Holds Jotunn `ConsoleCommand`s. This phase adds `vfh_dump_data`, and later phases add more.
- `src/VikingsForHire/Core/Diagnostics/LogFormat.cs`: pure formatter (unit-tested), described in step 0.
- `src/VikingsForHire/Diagnostics/VfhLog.cs`: the logging facade every other file uses (step 0).
- `src/VikingsForHire/Diagnostics/LogFileSink.cs`: writes the dedicated log file with rotation.
- `src/VikingsForHire/Testing/TestHarness.cs`: test-run state, the `vfh_test_*` commands, and the check registry (step 0b).
- `src/VikingsForHire/Testing/Fixtures.cs`: the fixture registry (`vfh_fixture <name> [args]`). Each phase adds its fixtures as separate `Fixtures<Area>.cs` files registered here.
- `src/VikingsForHire/Core/Testing/CheckExpr.cs`: pure parser and evaluator for check comparisons (`== != >= <= > <`, ints, floats, strings, bools), unit-tested.
- `docs/testing.md`: how to run macros, the list of fixtures and checks, and how to read `cat=Test` results.
- `docs/debugging.md`: how to turn on debug categories, where the log files are (single-player client vs dedicated server paths), the line format, the dump commands, and what to attach to a bug report.
- `src/VikingsForHire/Core/Data/VfhData.cs`: root YAML model (`BoardLevels`, `HirelingLevels`, `Jobs`, `CommandStone`, `Food`, `Names`). Plain C# classes with public settable properties, so the YamlDotNet-serializable classes have no Unity references.
- `src/VikingsForHire/Core/Data/DefaultData.cs`: builds the default `VfhData` (tables below).
- `src/VikingsForHire/Core/Data/DataValidator.cs`: validates a loaded `VfhData` (8 board levels, 8 hireling levels, a job entry for each `JobType`, 4 stone levels, no negative numbers) and returns a list of errors. Invalid files fall back to defaults with the errors logged.
- `src/VikingsForHire/Core/JobType.cs`: `enum JobType { Woodcutter, Miner, Smelter, GuardMelee, GuardRanged }` plus `IsGuard()`.
- `src/VikingsForHire/Core/Stance.cs`: `enum Stance { Flee, Defend, Passive, Defensive, Aggressive }`.
- `src/VikingsForHire/Core/StanceRules.cs`: `Allowed(JobType)` (workers: Flee/Defend, guards: Passive/Defensive/Aggressive) and `Default(JobType)` (workers: Defend, guards: Defensive). Phase 07 adds `Decide`.
- `src/VikingsForHire/Core/HirelingModes.cs`: `enum HirelingMode { Working = 0, Following = 1, Idle = 3, Leaving = 4 }` (value 2 is unused and reserved) and `enum FollowMode { Follow = 0, Stay = 1, GatherNearby = 2 }`. These are defined here so every phase uses the same values.
- `src/VikingsForHire/Core/Cost.cs`: `record Cost(int FoodPoints, int Coins)` with `+`, `-`, `Scale(float)`, and clamping to ≥ 0.
- `src/VikingsForHire/Core/CostCalculator.cs`: `HireCost(job, level)`, `DailyUpkeep(job, level)`, `PromotionCost(job, from, to)` = `HireCost(to) − HireCost(from)`, `RespawnCost(job, level)` = `HireCost × RespawnCostFraction`. Coins are always 0 at level 1, whatever the table says.
- `src/VikingsForHire/Core/FoodPoints.cs`: `PointsFor(health, stamina, eitr)` = `round(health + stamina + eitr)`. `IsAcceptable(prefabName, hasFoodValue, allowRaw, rawList)`. `PlanPayment(available: list of (prefab, points, count), required)` picks which stacks to take: lowest points-per-item first, to save good food, and returns the items and the overpay, which isn't refunded.
- `src/VikingsForHire/Core/LevelRules.cs`: `MaxHirelingLevel(boardLevel)` = `boardLevel` (an L5 board hires up to L5), `HirelingCap(boardLevel)`, `MaxWorkRadius(boardLevel)`, `ClampRadius(...)`. The board's `maxWorkRadius` is the hirelings' roam range: how far from the board they work and patrol.
  It also has `StoneFollowerCap(stoneQuality)`, `RequiredBoardLevelForStone(quality)`.
- `src/VikingsForHire/Core/BaseRequirement.cs`: `Evaluate(workbenches, beds, pieces, nearestBoardDistance, worldBoardCount, cfg)` returns `BaseCheckResult` with a list of missing-requirement keys (used as localization tokens).
- `src/VikingsForHire/Core/DepositPlanner.cs`: given the cargo (prefab → count) and candidate chests (id, distance, contents prefab → count, free-slot info per prefab), returns a per-item-type plan: chests that already hold that prefab and have room, nearest first, with overflow listed as "drop at board". It never assigns an item type to a chest that doesn't already contain it.
- `src/VikingsForHire/Core/OrphanRules.cs`: `ReturnSeconds(distanceMeters, secondsPer100m, minSeconds, maxSeconds)`, implemented and tested here. `ShouldOrphan` is added in phase 15.
- `src/VikingsForHire/Config/VfhConfig.cs`: all `.cfg` bindings (list below), each `AdminOnly` via Jotunn `ConfigurationManagerAttributes { IsAdminOnly = true }` except client-only keys (UI hotkeys).
- `src/VikingsForHire/Config/DataStore.cs`: loads, writes, syncs and hot-reloads `BepInEx/config/Spronglehump.VikingsForHire.yml`.
- `src/VikingsForHire/Plugin.cs`: calls `VfhConfig.Bind(Config)` and `DataStore.Init()`.
- `tests/VikingsForHire.Tests/*Tests.cs`: one test class per Core rule file (`OrphanRulesTests` covers `ReturnSeconds` here) plus `DefaultDataTests` and `DataRoundTripTests`. The test csproj links `Core/**/*.cs` and adds `YamlDotNet` 16.3.0 from NuGet for the round-trip test.

## Steps
0. **Logging (built first):**
   - **Line format** (`LogFormat.Line`): `[VFH] t=<ZNet time, 1 decimal> f=<frame> role=<SP|HOST|CLIENT|SERVER> lvl=<E|W|I|D|T> cat=<Category> <event> key=value key=value …`. Example: `[VFH] t=1834.2 f=99120 role=SP lvl=D cat=Work evt=deliver.plan hid=3f2a board=91c0 job=Woodcutter item=Wood n=40 chest=zdo:1234:56 dist=7.4`. Values containing spaces are quoted. ids are shortened to 4 hex characters in the line, and the full id is printed once per session on first sight (`evt=id.map short=3f2a full=…`), so the logs stay readable and greppable.
   - **Categories** (`enum LogCat`): `Core, Data, Board, Placement, Roster, Payment, Hireling, AI, Combat, Work, Deliver, Smelter, Follow, Orders, Travel, Orphan, Net, Compat, UI, Perf`.
   - **Levels:** Error and Warning are always on. Info is on by default. Debug and Trace are per category.
   - **Sinks:** every enabled line goes to the BepInEx `ManualLogSource` (so it shows up in `LogOutput.log`) **and** to `BepInEx/VikingsForHire.log` (the dedicated file, rotated at 10 MB with 3 backups `VikingsForHire.1.log`…, and a session header with the mod version, game version, role, world name, the loaded mod list from `Chainloader.PluginInfos`, and a dump of the effective config + YAML hash). On a dedicated server, the file is in the server's `BepInEx/` folder.
   - **Exceptions:** `VfhLog.Exception(cat, evt, ex, fields)` logs the full stack trace and the ids involved. A `VfhLog.Guard(cat, evt, Action)` helper wraps patch bodies and behaviour ticks. It logs the first 5 occurrences of each (cat, evt) per session with stack traces, then one summary every 60 s with a count (`evt=… repeat=37`), so a broken tick can't flood the log.
   - **Rate limiting:** `VfhLog.Throttled(key, seconds, …)` for per-tick spam such as "no targets".
   - **Conventions (required in every later phase):** log at Info every state change a player would care about (contract posted, arrival, payment, upkeep result, death, leave, upgrade, recruit/release, portal/stow, orphan/return). Log at Debug every AI behaviour switch (`evt=ai.switch from=Gather to=Combat reason=attacked`), target choice, delivery plan, op submitted/applied/forwarded (with op type, target ZDOID, and which peer applied it), and RPC send/receive. Log at Trace per-tick details. Every line includes the relevant ids (`hid`, `board`, `contract`, `zdo`, `owner`).
   - **Config keys** (`7 - Debug`, client-local, not synced, so each machine controls its own log volume): `LogToFile` (bool, true), `DebugCategories` (string, comma list, default empty; `All` enables everything), `TraceCategories` (string, default empty), `LogFileMaxMB` (int, 10).
   - **Console commands** (added to `DebugCommands.cs`, not cheats, so they work without `devcommands`): `vfh_debug <category|All> <on|off|trace>` changes the categories at runtime and writes them back to the cfg. `vfh_log_mark <text>` writes `evt=mark text=…`, so you can bracket a repro ("before placing board" / "after bug"). `vfh_dump_data` prints the effective data summary. Later phases extend `vfh_dump_state`, which this phase creates and which prints every loaded board and hireling with all its ZDO fields in the log format. Phase 02 just prints the session header.
0b. **Test harness** (all cheat commands, need `devcommands`, used by the ServerDevcommands macros in `test/alias_vfh.yaml`):
   - `vfh_test_begin <ROW-ID>` starts a run: logs `cat=Test evt=test.begin row=…` and writes a `vfh_log_mark`. `vfh_test_end` logs `evt=test.result row=… pass=<true|false> checks=<n> failed=<n> fails="…"`, a single line that sums up the run.
   - `vfh_assert <check> [args…] <op> <value>` evaluates a registered check and logs `evt=test.assert row=… check=… expected=… actual=… pass=…`. It also shows a green or red centre message, so you can see the result without opening the log. Checks that need server-only state (indexes, unloaded ZDOs) are forwarded to the server with CustomRPC `VFH_TestCheck`, and the result is logged on both sides.
   - `vfh_assert_eventually <timeoutSec> <check> … <op> <value>` re-checks every second until it passes or times out. This means macros don't depend on exact `wait` timing.
   - `vfh_test_summary` prints every result since login. `vfh_test_reset` clears them.
   - `vfh_fixture fast_timers <on|off>` overrides config values in memory for testing: `ArrivalDelayMin/Max` 5/10 s, `RespawnCooldownSeconds` 10, and every `Orphan*Seconds` ÷ 10. `ReturnSecondsPer100m`, `ReturnMinSeconds` and `ReturnMaxSeconds` are all ÷ 10 (2.5 / 6 / 120). The cfg file is never written. The command is sent to the server with CustomRPC `VFH_TestFastTimers` (admin-checked) and applied on the server **and** every client, so ZDO owners and server services all use the same values. The overrides end at server restart or `off`. Every override is logged on each machine (`cat=Test evt=fast_timers`).
   - Checks registered in this phase: `cfg <key>`, `data <dotted.path>` (e.g. `data BoardLevels.2.HirelingCap`), `log_errors` (count of `lvl=E` lines this session, normally asserted `== 0`).
   - Macro convention: each checklist row `VFH-XXX-N` gets an alias `vfh_t_xxxN` in `test/alias_vfh.yaml` shaped `vfh_test_begin VFH-XXX-N;<fixtures>;wait <ms>;<actions>;vfh_assert_eventually …;…;vfh_assert log_errors == 0;vfh_test_end`. Rows that need a human action in the middle (open a chest, deconstruct, take the helm) are split into `vfh_t_xxxN_a` (setup + first checks) and `vfh_t_xxxN_b` (checks after the action), and the checklist says what to do in between.
   - Phase 02 rows: `vfh_t_cfg1` (asserts synced data values on a client of the dedicated server) and `vfh_t_cfg2` (asserts a data value, then the tester edits the YAML, then `_b` asserts the new value).
1. **`.cfg` keys** (`Spronglehump.VikingsForHire.cfg`). The section and key names are final:
   - `1 - General`: `LockConfiguration` (bool, true, Jotunn's admin lock), `FriendlyFireOnHirelings` (bool, false), `PermadeathEnabled` (bool, true), `RespawnCooldownSeconds` (int, 600), `RespawnCostFraction` (float, 0.5), `AllowRawFood` (bool, false).
   - `2 - Base`: `BaseCheckRadius` (float, 20), `RequiredWorkbenches` (int, 1), `RequiredBeds` (int, 1), `RequiredPieces` (int, 40), `MinDistanceBetweenBoards` (float, 100), `MaxBoardsPerWorld` (int, 0 = unlimited).
   - `3 - Hiring`: `ArrivalDelayMinSeconds` (int, 90), `ArrivalDelayMaxSeconds` (int, 240), `ArrivalSpawnDistance` (float, 35), `UnpaidDaysBeforeLeaving` (int, 2), `DropPileOffset` (float, 2.5).
   - `4 - Work`: `AiScanIntervalSeconds` (float, 2), `TreeSafetyDistanceFromPieces` (float, 6), `MinerProtectsTerrain` (bool, true), `SmelterRefillThreshold` (float, 0.5 = refill when below half full), `KeepMinimumInChest` (int, 0).
   - `5 - Followers`: `PortalFollowRadius` (float, 20), `AllowNonTeleportableThroughPortals` (bool, false), `GatherNearbyRadius` (float, 15), `ShipStowRadius` (float, 20), `OrphanDistance` (float, 60), `OrphanDistanceSeconds` (float, 30), `OrphanStuckSeconds` (float, 20), `OrphanStayDistance` (float, 150), `OrphanStaySeconds` (float, 120), `ReturnSecondsPer100m` (float, 25), `ReturnMinSeconds` (float, 60), `ReturnMaxSeconds` (float, 1200), `StoneBoardSearchRadius` (float, 30).
   - `6 - Controls` (client-only, not synced): `BoardPanelKey` (KeyboardShortcut, LeftShift+E), `CycleFollowModeKey` (G), `CycleStanceKey` (H). They're registered as Jotunn `ButtonConfig`s in the phase that uses them.
2. **YAML default tables** (`DefaultData.cs`). Item names are vanilla prefab names:
   - **Board levels** (`level`, `buildOrUpgradeCost`, `hirelingCap`, `maxWorkRadius`):
     - L1 (build, Workbench): Wood 40, Stone 20, DeerHide 10, LeatherScraps 10, Resin 10. Cap 2, radius 20.
     - L2: TrophyEikthyr 1, HardAntler 3, DeerHide 20, Flint 20, Wood 50. Cap 3, radius 25.
     - L3: TrophyTheElder 1, Bronze 10, RoundLog 40, TrollHide 5, GreydwarfEye 20. Cap 4, radius 30.
     - L4: TrophyBonemass 1, Iron 20, ElderBark 40, Guck 10, WitheredBone 10. Cap 5, radius 35.
     - L5: TrophyDragonQueen 1, Silver 20, DragonTear 5, WolfPelt 10, Obsidian 20. Cap 6, radius 40.
     - L6: TrophyGoblinKing 1, BlackMetal 20, LinenThread 20, Needle 20, LoxPelt 5. Cap 7, radius 45.
     - L7: TrophySeekerQueen 1, BlackCore 3, Eitr 15, YggdrasilWood 40, Carapace 20. Cap 8, radius 50.
     - L8: TrophyFader 1, FlametalNew 20, Blackwood 40, AskHide 10, MoltenCore 3. Cap 10, radius 60.
   - **Hireling levels** (`health`, `armor`, `guardDamageMult`, `gatherMult`, `cargoSlots`, `cost{hireFood, hireCoins, upkeepFood, upkeepCoins}`):
     - L1: 80, 4, 1.0, 1.0, 8, {150, 0, 40, 0}
     - L2: 120, 8, 1.3, 1.2, 10, {250, 50, 60, 5}
     - L3: 180, 14, 1.7, 1.4, 12, {400, 150, 90, 10}
     - L4: 250, 20, 2.2, 1.6, 16, {600, 300, 120, 20}
     - L5: 330, 26, 2.8, 1.8, 20, {800, 500, 160, 30}
     - L6: 420, 32, 3.5, 2.0, 24, {1000, 800, 200, 45}
     - L7: 520, 38, 4.3, 2.2, 28, {1300, 1200, 250, 60}
     - L8: 650, 44, 5.2, 2.4, 32, {1600, 1800, 300, 80}
   - **Jobs** (`costMult`, `workerCombatFactor` = damage multiplier relative to `guardDamageMult`, `pickupItems`, `gear` per level, and `stations` for the Smelter only: the station prefab names it services, default `smelter`, `blastfurnace`, `charcoal_kiln`, `eitrrefinery`). Multipliers: Woodcutter 1.0/0.4, Miner 1.1/0.4, Smelter 0.9/0.3, GuardMelee 1.3/1.0, GuardRanged 1.3/1.0.
     - Armor sets, shared by all jobs, L1→L8: Rags, Leather, Bronze, Iron, Wolf, Padded, Carapace, Flametal (helmet/chest/legs prefab triples, e.g. `ArmorRagsChest`, `ArmorRagsLegs`, no helmet at L1).
     - Woodcutter tool L1→L8: AxeStone, AxeFlint, AxeBronze, AxeIron, AxeIron, AxeBlackMetal, AxeJotunBane, AxeJotunBane. Pickup: Wood, FineWood, RoundLog, ElderBark, YggdrasilWood, Blackwood, Resin, BeechSeeds, FirCone, PineCone, BirchSeeds, Acorn.
     - Miner tool: PickaxeAntler, PickaxeAntler, PickaxeBronze, PickaxeIron, PickaxeIron, PickaxeBlackMetal, PickaxeBlackMetal, PickaxeBlackMetal. Pickup: Stone, CopperOre, TinOre, IronScrap, SilverOre, BlackMetalScrap, CopperScrap, Obsidian, Flametal ore prefab `FlametalOreNew`, `Grausten`.
     - Smelter tool: none (Club as cosmetic weapon). Pickup: every conversion output of the stations in `stations` (read from the station prefabs at runtime).
     - GuardMelee weapon+shield: Club+ShieldWood, KnifeFlint+ShieldWood, SwordBronze+ShieldBronzeBuckler, SwordIron+ShieldBanded, SwordSilver+ShieldSilver, SwordBlackmetal+ShieldBlackmetal, SwordMistwalker+ShieldCarapace, SwordNiedhogg+ShieldFlametal.
     - GuardRanged bow+arrow: Bow+ArrowWood, Bow+ArrowFlint, BowFineWood+ArrowBronze, BowHuntsman+ArrowIron, BowDraugrFang+ArrowSilver, BowDraugrFang+ArrowNeedle, BowSpineSnap+ArrowCarapace, BowAshlands+ArrowCharred. The guard's side arm is a Club.
   - **Command stone** (quality → requiredBoardLevel, followerCap, cost), crafted at the Workbench:
     - Q1: board 3, cap 1. SurtlingCore 5, Bronze 10, GreydwarfEye 20, FineWood 10.
     - Q2: board 4, cap 2. Iron 15, Guck 10, Ooze 10, WitheredBone 10.
     - Q3: board 6, cap 3. BlackMetal 15, Silver 10, Needle 10, LinenThread 10.
     - Q4: board 8, cap 4. FlametalNew 15, Eitr 10, BlackCore 2, MoltenCore 2.
   - **Food**: `rawFoods` = RawMeat, DeerMeat, NeckTail, WolfMeat, LoxMeat, SerpentMeat, FishRaw, ChickenMeat, HareMeat, BugMeat, AsksvinMeat, VoltureMeat, BoneMawSerpentMeat, Raspberry, Blueberries, Cloudberry, Mushroom, MushroomYellow, MushroomBlue, MushroomJotunPuffs, MushroomMagecap, Carrot, Turnip, Onion, Honey, Fiddleheadfern, Vineberry.
   - **Names**: 60 male and 60 female common Old Norse given names (e.g. Bjorn, Ragnar, Sigurd, Ulf…; Astrid, Sigrun, Freydis, Gudrun…), in two lists matched to the generated body model and editable in the YAML.
3. **Prefab validation at runtime.** After `ObjectDB` is ready (Jotunn `PrefabManager.OnVanillaPrefabsAvailable`), check every item prefab named in the data against `ObjectDB.GetItemPrefab`. Unknown names are logged once as warnings and skipped: a missing recipe ingredient is removed, and a missing gear item falls back to the nearest lower level's item. This keeps the mod working across game updates that rename items.
4. **DataStore:**
   - On server or single-player start, if the YAML file is missing, serialize `DefaultData` with comments describing every field (YamlDotNet `CommentGatheringTypeInspector` via `[YamlMember(Description=...)]`). Otherwise deserialize, validate, and log any errors.
   - Sync: register a Jotunn `CustomRPC` `VFH_DataSync`. The server sends the raw YAML text to each client when it connects (`ZNet.OnNewConnection` → wait for the peer's `RPC_PeerInfo`, then send) and to all clients after a reload. Clients deserialize into `DataStore.Current` and ignore their local file while connected. They revert to their local file when they disconnect.
   - Hot reload: a `FileSystemWatcher` on the YAML, debounced by 1 s, on the server/host only. It reloads, re-validates and re-broadcasts. Consumers subscribe to `DataStore.Changed`.
5. **Tests:** cover every Core function, including level-1 coins forced to 0, promotion cost math, cheapest-first payment planning with overpay, the base check listing each missing requirement, `DepositPlanner` never mixing item types and sending overflow to the drop pile, return-timer clamping, the default data passing validation, and YAML round trip (serialize defaults → deserialize → equal).

## Build gate
- `dotnet build -c Release`: 0 errors.
- `dotnet test`: all tests pass.

## Test plan
- Automated: the unit tests above, plus `LogFormatTests` (field quoting, id shortening, level filtering) and `CheckExprTests`.
- Harness: run `vfh_t_cfg1`. The log contains `evt=test.begin`, the assert lines and `evt=test.result row=VFH-CFG-1 pass=true`. Deliberately assert a wrong value: the result is red and `pass=false`, with expected/actual shown.
- Logging, single-player: `vfh_debug Data on`, `vfh_log_mark test1`, hot-reload the YAML. `BepInEx/VikingsForHire.log` contains the session header, the mark, and `cat=Data evt=data.reload` lines. `LogOutput.log` contains the same lines. `vfh_debug Data off` stops the Debug lines. Force an exception in a guarded debug command (`vfh_debug_throw`, a cheat command added for this test) 10 times: 5 stack traces, then a repeat summary.
- In-game (single-player): first launch writes `Spronglehump.VikingsForHire.yml` and `.cfg` with defaults. Editing a cost in the YAML while in-game logs "VikingsForHire data reloaded".
- Dedicated server: change `RequiredPieces` in the server `.cfg` and a cost in the server YAML, then join. Without `devcommands`, run the debug command `vfh_dump_data`, a registered Jotunn `ConsoleCommand` added in this phase that prints the effective data summary. It shows the server values. Disconnect and the client's local values return.
- Prefab validation produces **zero** warnings on the current game version. Any default name that warns (the Ashlands item names are the most likely, e.g. `BowAshlands`, `ArrowCharred`, `SwordNiedhogg`, `ShieldFlametal`, `FlametalOreNew`) gets corrected in `DefaultData.cs` to the real prefab name, which you find in-game with `devcommands` + `spawn` tab-completion, before this phase is committed.
- Corrupt the YAML (bad indentation): the log shows the validation/parse error and the defaults are in use.
- Add a doc row to `docs/test-checklist.md`: **VFH-CFG-1** (server data sync) and **VFH-CFG-2** (hot reload).

## Commit
`feat(core): config, synced YAML data tables and pure rule engine with tests`

## Rollback
Revert the commit. Delete the generated `.cfg`/`.yml` from the dev profile and the dedicated server config folder. No world data is touched in this phase.

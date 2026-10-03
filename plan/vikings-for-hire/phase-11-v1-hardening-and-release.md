# Phase 11 — Milestone 1 hardening, performance & 0.1.0 release

**Depends on:** 01–10 · **Enables:** 12 (milestone 2 starts from a released, stable base)

## Goal
Ship milestone 1, base workers, as `0.1.0`. This phase adds a performance pass so a full roster doesn't hurt frame time, a balance playthrough of the default tables, complete localization and documentation, a full run of the multiplayer checklist, and packaging. No new features.

## Files touched
- `src/VikingsForHire/Hirelings/HirelingAI.cs`: scheduler improvements from profiling (below).
- `src/VikingsForHire/Diagnostics/PerfCounters.cs`: lightweight per-behaviour timing (`Stopwatch`, rolling 10 s average) shown by `vfh_perf`.
- `src/VikingsForHire/Commands/DebugCommands.cs`: every command that changes the world (`vfh_spawn*`, `vfh_kill_hirelings`, `vfh_board_setlevel`, `vfh_dismiss_contract`, `vfh_snapshot_test`, `vfh_debug_throw`) is marked `IsCheat = true` (needs `devcommands`). The diagnostics commands `vfh_debug`, `vfh_log_mark`, `vfh_dump_data`, `vfh_dump_state`, `vfh_dump_index`, `vfh_board_info` and `vfh_perf` stay non-cheat, so bug reports never need `devcommands`.
- `src/VikingsForHire/Core/Data/DefaultData.cs`: balance adjustments from step 3 (only if the playthrough calls for them, recorded in CHANGELOG).
- `src/VikingsForHire/Localization/English.json`: every `$vfh_*` token present (verified by a unit test that scans the source for `$vfh_` literals and checks the JSON has every key).
- `tests/VikingsForHire.Tests/LocalizationCoverageTests.cs`: that scan test (reads `src/**/*.cs` and the JSON from the repo root).
- `README.md`: features, requirements (BepInExPack, Jotunn, YamlDotNet, optional AzuAutoStore), the base rules, the job list, payment model, board level table, configuration overview (`.cfg` sections + YAML structure with an example), multiplayer notes, the AzuAutoStore note, and known limitations (workers only work while their zone is loaded).
- `CHANGELOG.md`: `## 0.1.0`, with the feature list.
- `docs/test-checklist.md`: all VFH rows from phases 02–10 collected in one matrix with modes SP (single-player, run first) and D (local dedicated server).
- `package/manifest.json`, `src/VikingsForHire/VikingsForHire.csproj`, `Plugin.cs`: version `0.1.0` (already set in phase 01, checked by the Package target).

## Steps
1. **Performance budget:** at an L8 board with 10 hirelings (2 of each job) working, plus `event army_bonemass` for combat, `vfh_perf` must report a combined hireling AI cost of **≤ 1.0 ms per frame on average** on the host. Measures, applied until the budget is met:
   - Stagger the behaviour `Wants()` checks across frames (round-robin by `hid` hash).
   - Cache `ChestFinder` results for 10 s per board, shared by all hirelings of that board, and invalidate when a container piece is placed or removed nearby.
   - Cache candidate lists (trees, rocks, stations) per board for 15 s, with per-target validity re-checked before use.
   - Skip AI ticks entirely for hirelings beyond 80 m from any player on the owning client. Vanilla zone loading keeps them loaded within that range, and this avoids work at the edges.
2. **Robustness and log audit:** confirm every Harmony patch body and behaviour tick goes through `VfhLog.Guard` (phase 02). Grep for `[HarmonyPatch]` and `Tick(` and fix any that don't. Review a 2-hour `vfh_debug All on` session log for lines that are noisy without being useful (demote them to Trace) and for state changes that have no log line (add them).
3. **Balance playthrough:** a fresh world with defaults, played from Meadows to after Bonemass (L4) using only intended progression. Pass criteria: the first L1 hireling is affordable on day 2–3 with cooked meat only; upkeep for 4 L3–L4 hirelings uses about 25–40% of a typical Swamp-era coin income from loot (track coins earned vs spent over 5 in-game days); and no single hireling outperforms the player at its job (a woodcutter chops slower than a player with the same tier axe). If a criterion fails, adjust the costs or multipliers in `DefaultData.cs` and record the change in the CHANGELOG.
4. **Checklist run:** run every row in `docs/test-checklist.md` in its listed modes by typing its `vfh_t_*` alias, then `vfh_test_summary`. Every result must be `pass=true`. Add `vfh_t_m1_sp` to `test/alias_vfh.yaml`: it chains the single-player rows that need no human action, each followed by `clear_area`, in a fresh test world. Fix any failures, then rerun the row plus VFH-HIRE-1 (persistence) and VFH-WORK-2 (no item loss).
5. **Package:** `dotnet build src/VikingsForHire -c Release -t:Package` produces `dist/Spronglehump-VikingsForHire-0.1.0.zip`. Install that zip into a clean Gale profile and smoke test it (place a board, hire, work).
6. **Tag and publish:** `git tag v0.1.0` and push the tag. Make a GitHub release with the zip attached. Uploading to Thunderstore is a manual step the user does through the web UI with the zip.

## Build gate
- `dotnet build -c Release` with 0 warnings from our code, `dotnet test` all green, and `dotnet build src/VikingsForHire -c Release -t:Package` succeeds with the version check passing.

## Test plan
- `vfh_perf` meets the budget under the step 1 scenario, measured on the dev machine both as host and as a client of the dedicated server.
- Every checklist row passes in each listed mode.
- A clean-profile install of the packaged zip works.
- **Logs:** every scenario above is run with `vfh_debug All on`. Afterwards `VikingsForHire.log` must show the expected events with their ids (Info for state changes, Debug for AI switches, plans and ops), and no `lvl=E` lines. Any new code path in this phase logs through `VfhLog` per the phase 02 conventions, and guarded ticks and patches use `VfhLog.Guard`.

## Commit
`chore(release): 0.1.0 — base workers milestone`

## Rollback
Only performance and balance changes land here. Revert individual commits from this phase if one regresses. The release tag can be deleted and the GitHub release withdrawn. No world data format changes.

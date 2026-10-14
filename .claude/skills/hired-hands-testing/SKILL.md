---
name: hired-hands-testing
description: Test Hired Hands in the dev game with ModTestBridge (mtb) - build and deploy, restart into testboy, run test rows and the full regression, read failures, write new rows, log results and package a release. Use when asked to test a fix in game, run the regression, add a test row, or package/release Hired Hands.
---

# Testing Hired Hands in the dev game

The general ModTestBridge procedure is the `valheim-mod-testing` skill (plugin `modtestbridge`). This is what's
specific to Hired Hands. Run everything from the repo root (`~/Projects/vh_hired_hands`), where `.mtb` (git-ignored)
names the profile `vikingsforhire-dev`, the world `testboy` and the character `Testboy`.
`mtb` is `~/Projects/vh_mod_test_bridge/scripts/mtb` if it isn't on the PATH.

## Before touching the game

- **Is the game free?** Tim plays on the same machine, and other sessions share the dev game. Only build-and-deploy,
  `mtb restart` or `mtb run` when Tim has said in this conversation that the game is free. Otherwise compile-check
  without deploying and log the in-game checks as "not yet run":
  ```
  dotnet msbuild src/VikingsForHire/VikingsForHire.csproj -p:Configuration=Release -t:ResolveReferences\;CoreCompile
  ```
- **Never** put ModTestBridge, builds or test files into a play profile (`1dotohsupermodded`, the Gale-managed
  `vikingsforhire-dev2`) or onto the live server. The server (AMP container on gameserver.spronglehump.com) is
  read-only unless Tim asks.
- **Unit tests** need no game: `dotnet test tests/VikingsForHire.Tests`.

## Build, deploy, restart

1. `dotnet build src/VikingsForHire/VikingsForHire.csproj -c Release` builds and deploys the DLL and
   `test/alias_vfh.yaml` (the test rows) into the dev profile.
2. `mtb restart` (about a minute). Rows and code load at game start, so any change to either needs a restart.
   - "timed out waiting for the world" with no `valheim.exe` running: Steam dropped the launch. Run `mtb restart`
     again.
   - A hung game: `pkill -x valheim.exe`, then end the prefix's wineserver
     (`WINEPREFIX=/games/SteamLibrary/steamapps/compatdata/892970/pfx ~/.local/share/Steam/compatibilitytools.d/GE-Proton11-6/files/bin/wineserver -k`),
     then `mtb restart`.
3. `mtb status`: Testboy must be at the test spot, about (321, -238). If not: `mtb cmd "goto 321 -238"`.

## Running rows

Rows are aliases in `test/alias_vfh.yaml`: `vfh_t_<name>: vfh_test_begin VFH-XXX-N;...;vfh_test_end`.
```
mtb run "vfh_test_reset;vfh_fixture clear_area 100;vfh_test_chain <name> <name> ...;vfh_test_summary" "test.summary" <seconds>
```
- `vfh_test_reset` also sets the start spot: a row that begins more than 30 m from it teleports the tester back
  (`test.back_home`), so one failed row can't strand the rest of a chain.
- The harness protects Testboy (ghost, no damage) from each `vfh_test_begin` until the queue ends. Rows don't add
  `ghost on`. Between runs Testboy is unprotected: don't leave him away from the test spot.
- `log_errors` counts from each row's start.
- Abort a running chain with `mtb cmd vfh_test_abort`, then return to the test spot.
- Done text: `test.summary` for a chain; for one row use `result row=VFH-X-1` (plain `row=` also matches its begin line).

**Long chains** (more than ~10 minutes) go in the background (Bash `run_in_background`), with a Monitor on the log for
failures as they happen. Use `awk` with `fflush()` (a `cut` in the pipe holds lines back):
```
tail -n0 -F ~/.local/share/com.kesomannen.gale/valheim/profiles/vikingsforhire-dev/BepInEx/LogOutput.log \
  | grep --line-buffered -E "evt=test.result .*pass=false|evt=test.summary|evt=test.step_failed" \
  | awk '{sub(/.*evt=/,"evt="); print substr($0,1,400); fflush()}'
```

### The full regression

Every row, about 3-4 hours:
```
grep -E "^vfh_t_[a-z0-9_]+: vfh_test_begin" test/alias_vfh.yaml | sed -E 's/^vfh_t_([a-z0-9_]+):.*/\1/' | grep -vE "_a$"
```
(`_a` rows are halves of two-part rows that need a relog; group macros like `vfh_t_wiz_all`, `vfh_t_ranch_all` run
subsets.)

**Expected here:** VFH-CFG-1 fails (needs a dedicated server) and VFH-WORK-5 fails (needs AzuAutoStore off).
**Known flaky:** VFH-LOOT-1/-2 (the litter fixture can drop a log somewhere awkward).

## Reading a failure

- `LogOutput.log` has Info and above: `test.assert ... pass=false` gives expected and actual.
- Detail is in `BepInEx/HiredHands.log` (Debug; rotates to `HiredHands.1.log`). Turn categories on in the dev
  profile's `Spronglehump.HiredHands.cfg` (`DebugCategories`, add `Combat,AI,Work` and the like; CRLF file; restart).
- Find a row's window by its start time and read what happened then:
  ```
  T=$(grep -h "test.begin row=VFH-X-1" HiredHands.log | tail -1 | grep -oE "t=[0-9.]+" | cut -c3-)
  awk -v s=$T -F't=' '{split($2,a," "); if (a[1]+0>=s && a[1]+0<=s+200) print}' HiredHands.log
  ```
- **Before calling it a bug, rerun the row alone** in a fresh session. In a long chain, leftovers (boards, creatures,
  the tester's position) cause failures that aren't real.
- Then decide: a real bug (fix the code), or the row assumed something that's no longer true (fix the row, and say so in
  the commit). A test fixed to pass must still test what it says.

## Writing a row

- One line in `test/alias_vfh.yaml`, a `# VFH-XXX-N (SP): what it shows` comment above it, before the group macros at
  the end. List fixtures and checks in game with `vfh_fixture list` and `vfh_checks`.
- Start from where the test spot is; set up (`base`, `board_here`, `board_level`, `stock_board`, chests), act, assert
  with `vfh_assert_eventually <seconds> <check> <op> <value>`, clean up (`kill_hirelings`, `clear_area 60`, restore any
  `cfg_set`), end with `vfh_assert log_errors == 0;vfh_test_end`.
- Hire without fees: `post <job> <level> <radius> free`. Spawn a free-standing one: `hirelings <job> <level>`.
  `enemies` spawn near the last *spawned* hireling, not a posted one.
- A wooden chest holds 10 stacks: `chest F CookedMeat 200 LoxPie 60` silently leaves the pies out.
- Ranch and feeding rows: `cfg_set StewardIgnoreOtherMods true` (the dev profile has PetPantry and Torches Eternal);
  `noazu` on chests.
- Followers arrive at the board's edge, possibly 30+ m away: wait with
  `vfh_assert_eventually 40 hireling posted owner_dist < 6` before using them.
- Something that needs a far place (the coast) uses a fixture that returns (`goto_coast` ... `goto_back`).
- A new fixture or check goes in `src/VikingsForHire/Testing/Fixtures*.cs`.
- Looks (hats, held models) can't be asserted: `vfh_fixture portrait last front` (also `side`, `back`, `top`) renders a
  close-up of the hireling's head to `BepInEx/HiredHands-<job>-<view>.png` for reading with the Read tool, and logs the
  worn helmet's size and offset from the head joint. Wait ~3 s after spawning first (`vfh_fixture wait 3`): a fresh
  hireling is still crouched.

## Logging and releasing

- Every fix and its test steps go in `docs/next-release.md` (the top section; table row: commit/branch, what, where it
  runs; then "To test" items, `[x]` once run). Tim tests by hand from that list.
- Packaging (on request):
  1. Bump the version in `package/manifest.json`, `<Version>` in `src/VikingsForHire/VikingsForHire.csproj` and
     `Plugin.Version` in `src/VikingsForHire/Plugin.cs` (the Package target checks all three).
  2. Add a player-facing `CHANGELOG.md` entry; say whether everyone must update together (the mod checks major.minor).
  3. `dotnet build src/VikingsForHire/VikingsForHire.csproj -c Release -t:Package` → `dist/Spronglehump-HiredHands-<v>.zip`.
  4. Mark the next-release section packaged with the test results, commit, merge to `main` locally.
- **Push, tag and release only on Tim's go** ("push it and release"):
  ```
  git push origin main && git tag -a v<v> -m "Hired Hands <v>" && git push origin v<v>
  gh release create v<v> dist/Spronglehump-HiredHands-<v>.zip --title "Hired Hands <v>: <theme>" --notes-file <the CHANGELOG section>
  ```
  Tim uploads to Hexium himself. Commits are signed through 1Password and fail while he's away: stage, save the
  message, don't bypass signing.
- Docs changes under `docs-site/` publish to https://tmac1973.github.io/HiredHands/ on push to `main`.

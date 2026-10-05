# Phase 05 — Live mod-set check, docs and release 0.3.0

**Depends on:** 01, 02, 03, 04 · **Enables:** the Steward indoor chores work

## Goal
Check the finished layer against the overview's success criteria on the live mod set and Tim's real base, document it, and ship it as **0.3.0** in one release. 0.3.0 rather than 0.2.x because the data file gains `navLinks`, which 0.2 builds reject, and with Minor version strictness the server and every player must move to 0.3 together.

## Files touched
- `src/VikingsForHire/Plugin.cs`: `Version = "0.3.0"`.
- `src/VikingsForHire/VikingsForHire.csproj`: `<Version>0.3.0</Version>`.
- `package/manifest.json`: `"version_number": "0.3.0"`.
- `README.md`: a new section, "Doors, stairs and ladders", placed after "Jobs".
- `CHANGELOG.md`: a `## 0.3.0` section. 0.2.4 shipped on its own before this work (overview, Constraints), so it has its own section already.
- `docs/next-release.md`: the 0.3.0 rows and the batch test list.
- `docs/test-checklist.md`: rows VFH-NAVLINK-1 to 9 with results.
- `plan/base-nav-links/*.md`: an "As built" note on any phase whose build differed from the plan, following `plan/vikings-for-hire/phase-15-orphan-return-home.md`.

## Steps
1. **Full macro run** in single player on `vikingsforhire-dev`:
   - every `vfh_t_navscan*` and `vfh_t_nav*` macro;
   - plus `vfh_t_door1`, `vfh_t_order1`, `vfh_t_gather1`, `vfh_t_deliver1`, `vfh_t_deliver2`, `vfh_t_catchup1`, `vfh_t_recruit1`, `vfh_t_portal1`, `vfh_t_home1`, `vfh_t_post1`, `vfh_t_lag1a` and `vfh_t_lag1b`.
   - Each must log `pass=true`. Record the results in `docs/test-checklist.md`.
2. **Local dedicated server** (`scripts/run-dedicated-server.sh`, LAN, no password): connect with `vikingsforhire-dev`, and run `vfh_t_nav1` and `vfh_t_nav5` from the client. This checks that the scan, the oracle and the routes run on the game simulating the hirelings, not on the server.
3. **Live mod set, read-only check:**
   - On a single-player copy of a world with Tim's `1dotohsupermodded` profile and the 0.3.0 build, build or visit a base using the modded build pieces, then `vfh_navlinks show` and `list`.
   - Every staircase and ladder from the mod set that a player can climb must show as a link. A piece wrongly found must be excluded by name.
   - Record any `navLinks.exclude` entries the mod set needs in the README's server-owner note. None is expected; that's the goal of the shape test.
4. **Performance:** compare `perf.minute` (worst frame, `aiMsPerFrame`) over 5 minutes at the copy base between 0.2.4 and 0.3.0. The worst frame may not rise beyond normal variation (±5 ms). `aiMsPerFrame` may rise by at most 0.2 ms.
5. **Docs:**
   - **README "Doors, stairs and ladders":**
     - what the layer does;
     - that it works automatically, including for modded stairs;
     - the two settings `BaseNavLinks` and `HirelingsCloseDoors`;
     - the hop on ladders;
     - `vfh_navlinks show|hide|scan|list`;
     - the optional `navLinks.include/exclude` lists, with a YAML example;
     - and that 0.3.0 needs everyone, server included, on 0.3.
   - **CHANGELOG:** the player-facing changes, the same way as earlier entries.
   - **`docs/next-release.md`:** one row per phase commit, plus the live batch test list:
     - Gerd's door and fence;
     - the upstairs chest;
     - followers into the house and upstairs;
     - a modded staircase.
6. **Release** (only with Tim's go-ahead in chat):
   - Build the package with `dotnet build -c Release -t:Package`.
   - Commit, push, tag `v0.3.0` (annotated), and run `gh release create v0.3.0 dist/Spronglehump-HiredHands-0.3.0.zip --title "Hired Hands 0.3.0" --notes-file <the 0.3.0 changelog section>`.
   - Tim uploads to Thunderstore/Hexium and updates the live server himself.
   - The live server's existing data file gets `navLinks` filled in on first load (`DataDefaults.FillMissing`); no manual edit is needed.
7. **Live batch test** with Tim, from `docs/next-release.md`, reading `nav.stuck`, `navlinks.*` and `perf.minute` from his client log and the AMP server log as in earlier releases.

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes.
- `dotnet build -c Release -t:Package` produces `dist/Spronglehump-HiredHands-0.3.0.zip`.
- Every macro in step 1 passes, with no `lvl=E`.

## Test plan
- The overview's success criteria, one by one:
  - The SP macros (step 1).
  - No door or stairs `nav.stuck` lines during a live session at Gerd's house and fence (step 7).
  - The overlay shows the expected links at Tim's base, and a modded staircase is found without a list entry (step 3).
  - No frame spikes (step 4).
  - With `BaseNavLinks` off, behaviour is identical to 0.2.4: `vfh_t_nav4`, and a by-hand session with the setting off.

## Commit
`chore: release 0.3.0 (base nav links)`

## Rollback
- **Before tagging:** revert the version and docs commit.
- **After release:** tell players to set `BaseNavLinks = false` (server setting, synced) for a quick fix without a downgrade.
- **Full downgrade to 0.2.x:** everyone downgrades together, and the server owner deletes the `navLinks:` block from `Spronglehump.HiredHands.yml`, which 0.2 builds reject.

# Phase 12 — Balance pass, documentation and the 0.10.0 release

**Depends on:** 01-11 (every enhancement implemented) · **Enables:** release

## Goal
Check the power budget (a fully enhanced level 8 about 25% stronger than an unenhanced one) with the balance log and
the game, adjust the default numbers where it's off, document the system for players and server owners, run the full
regression, and package 0.10.0.

## Files touched
- `src/VikingsForHire/Telemetry/BalanceFights.cs` — fight and death records gain `enh` (the hireling's picks string, e.g.
  `ABAB-BA`); upkeep and deliver records unchanged.
- `scripts/balance-report.py` — groups fights by job, level and enhancement picks; a table comparing each pick against
  its pair (damage per second dealt and taken, kill time, deaths).
- `src/VikingsForHire/Core/Data/DefaultData.cs` — number changes from the balance pass, if any.
- `src/VikingsForHire/Testing/FixturesCombat.cs` — fixture `duel <job> <level> <picks> <enemy> <stars> <n>`: spawns the
  hireling with those picks and n enemies, ghosted tester, and logs a `duel.result` line (time to kill, damage taken).
- `test/alias_vfh.yaml` — rows VFH-ENH-B1 … B3 (duels), and macros `vfh_t_enh_all` (every VFH-ENH row).
- `docs-site/enhancements.md` — new page: how choosing and respec work, and each job's seven pairs with their effects
  (generated text matches the in-game descriptions); `mkdocs.yml` nav entry; links from `jobs.md`, the job pages and
  `configuration.md` (the `enhancements` data section, `Enhancements` switch, `respecCostFraction`).
- `README.md` — one line under Jobs: "Each hireling picks one of two enhancements at every level from 2 to 8" with the
  page link.
- `CHANGELOG.md`, `docs/next-release.md`, `package/manifest.json`, `VikingsForHire.csproj`, `Plugin.cs` — 0.10.0.

## Steps
1. Balance log field and report.
2. Duels: for Guard, Archer and Wizard at level 8, run `duel` with no picks, with the all-A build and with the all-B build
   against each of the same three enemy sets (5 trolls, 1 star; 10 draugr; 3 lox): time to kill and damage taken, three
   runs per set, all nine runs averaged
   into one "better by" figure per build: the mean of the time-to-kill reduction and the damage-taken reduction, each as a
   percentage of the no-picks run. A build outside 20-35% has every number in its picks that sets a size (damage, health, armour, speed,
   heal, chance) scaled by the ratio 27% / its measured figure, and is measured again; this repeats until the build is
   within 20-35%. The final numbers and figures go in `docs/next-release.md`.
3. Workers: for every worker job, a level 8 all-A and all-B build against a plain one, 10 minutes each in the same fixed
   setup: Woodcutter and Miner deliveries; Steward chores completed (fires, loads, repairs); Farmer crops harvested;
   Cook meals made; Rancher animals fed and slaughtered. Outside 20-35%: the same scaling rule as step 2.
4. Documentation page and links; `mkdocs build --strict` passes.
5. Full regression (the `hired-hands-testing` skill): every row, with VFH-CFG-1 and VFH-WORK-5 the expected dev failures.
6. Version 0.10.0 everywhere; CHANGELOG entry ("Everyone updates together": new roster format, ops and data section);
   package; log the results in `docs/next-release.md`. Push, tag and release on Tim's go.

## Build gate
- `dotnet test tests/VikingsForHire.Tests`; `mkdocs build --strict`.
- In game: `vfh_t_enh_all` passes; the full regression passes (apart from the two dev rows); duel builds within 20-35%.

## Test plan
Duels and worker comparisons above; the full regression; a by-hand session on the live server after release (choosing,
respec, a few behaviours in real fights).

## Commit
`feat(enhancements): balance log picks, duel fixture and balance pass; docs page; 0.10.0`

## Rollback
Balance numbers are data: servers can tune them in the data file without a new build. The release itself is rolled back
by redeploying 0.9.3 everywhere (see phase 02 on the roster format: rosters saved by 0.10.0 need 0.10.0's reader).

# Phase 06 — Docs, full test run, release 0.8.0

**Depends on:** 01, 02, 03, 04, 05 · **Enables:** the Healer project (next), which reuses `withMods`

## Goal
Everything the earlier phases added is documented, the whole wizard chain and the combat regression pass with Wizardry
in the dev profile, and 0.8.0 is released. 0.8.0 also carries the unreleased 0.7.6 playtest fixes (Tim, 2026-10-10):
their tab in `docs/next-release.md` merges into the 0.8.0 tab and their CHANGELOG lines go under 0.8.0.

## Files touched
- `README.md`: a Wizard section (unlock levels with and without Wizardry, staffs and robes per level, the three
  settings `WizardDamage`, `WizardSpellsHit`, `WizardAttackCooldown`); the Wizard in the jobs table; the settings in the
  Combat list; "Hirelings never hurt players" (phase 01) in the combat notes.
- `CHANGELOG.md`: 0.8.0.
- `docs/next-release.md`: a 0.8.0 tab with each phase's commit and the by-hand checks below.
- `test/alias_vfh.yaml`: chain `vfh_t_wiz_all` = `wiz1 wiz2 wiz3 wiz4 wiz5 wiz6 wiz7 wiz8 wiz9 wiz10 wiz11 wiz12 combat10`.
- Version 0.8.0 in `src/VikingsForHire/Plugin.cs`, `src/VikingsForHire/VikingsForHire.csproj`, `package/manifest.json`.
- No UI code changes: the Contracts tab lists every `JobType` and already says "needs a level N board" from
  `LevelRules.MinBoardLevel`; the Shift+E panel and Roster tab take stances from `StanceRules` and posting from
  `IsGuard`, both set in phase 02.

## Steps
1. README, CHANGELOG, next-release tab.
2. Run `vfh_test_chain wiz_all`, then the regression chain `store1 orders death3 recruit1 post1 retreat1 catchup1
   combat1 stance4 stance5 block2 dodge1 noeffect move1 cap1 cap3 gather1 hire4 hire5`; fix and re-run until all pass.
3. Version bump; package with `dotnet build -c Release -t:Package src/VikingsForHire/VikingsForHire.csproj`.
4. After Tim's go-ahead: push, annotated tag `v0.8.0`, GitHub release with the zip and the CHANGELOG entry.

## Build gate
- `dotnet build -c Release`; `dotnet test tests/VikingsForHire.Tests`; the two chains above pass in the dev game.

## Test plan
- The chains (automated).
- By hand (Tim, live server with Wizardry): wizards look right (robes, cape, staff, casting animation); one on a post
  and one following; `WizardDamage 0.5` visibly weakens them.

## Commit
`docs: Wizard in the README; 0.8.0 released; version 0.8.0`

## Rollback
Revert the release commit to hold the version; the feature commits revert phase by phase in reverse order.

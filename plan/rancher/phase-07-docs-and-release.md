# Phase 07 — Docs, full regression, 0.9.0

**Depends on:** 01–06 · **Enables:** the release

## Goal
Document the Rancher, run every ranch row and the regression list together, and package 0.9.0 (released on Tim's go).

## Files touched
- `README.md` — a Rancher section: unlock, orders (meat, eggs, breeding pool, minimum 2), selective breeding, named pets,
  feeding and the Steward/PetPantry, settings.
- `CHANGELOG.md` — 0.9.0 ("Everyone updates together": new job, order kinds and data-file entries).
- `docs/next-release.md` — the 0.9.0 tab with each change, rows and by-hand checks.
- `test/alias_vfh.yaml` — chain `vfh_t_ranch_all` (VFH-RANCH-1 to -11).
- `package/manifest.json`, `src/VikingsForHire/Plugin.cs`, `src/VikingsForHire/VikingsForHire.csproj` — version 0.9.0.

## Steps
1. Write the README and CHANGELOG sections and the next-release tab.
2. Add the chain and run it, then the regression: `vfh_test_chain animals tidy orders farm_seeds cook_spit
   cook_cauldron combat1 stance4 noeffect store1 hire4 why1 why2`.
3. Bump the version and package (`dotnet build src/VikingsForHire/VikingsForHire.csproj -c Release -t:Package`).

## Verification
- `vfh_t_ranch_all` passes, and the regression chain passes.
- `dist/Spronglehump-HiredHands-0.9.0.zip` exists with the 0.9.0 manifest.
- By hand (Tim, live server, logged in `docs/next-release.md`): a Rancher works a real herd for a session (orders,
  slaughter, named pets spared, Problems readable).

## Done when
- [ ] README, CHANGELOG and next-release describe the Rancher.
- [ ] Every ranch row and the regression pass in one run.
- [ ] 0.9.0 is packaged; pushing and releasing wait for Tim.

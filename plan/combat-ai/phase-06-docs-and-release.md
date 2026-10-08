# Phase 06 — Docs, checklist and 0.6.0 release prep

**Depends on:** 01–05 (everything to document; the phase 05 report for the release notes) · **Enables:** the 0.6.0
release (tree patches, passing, block and dodge), on Tim's go-ahead

## Goal
Players, server owners and future work can all see what changed:
- **the README** explains blocking and dodging and the `BlockAndDodge` setting;
- **the CHANGELOG** has the 0.6.0 combat entry;
- **the test checklist** has the new rows;
- **futures.md** marks block and dodge as done and keeps tactics open.

The version is bumped to 0.6.0 and the package is built, ready to release when Tim says so.

## Files touched
- `README.md`: a new `## How hirelings fight` section, placed after `## Jobs` and before `## The Steward`, covering:
  - what blocking, parrying and dodging do;
  - how level affects them;
  - `BlockAndDodge`;
  - that projectiles count.
- `CHANGELOG.md`: under `## 0.6.0 (unreleased)`, a "Combat: blocking and dodging" list.
- `docs/next-release.md`: the 0.6.0 tab. Each phase has already added its commits and checks; this phase checks they're
  all there and adds the phase 05 report table.
- `docs/test-checklist.md`: the rows READ-1/2, BLOCK-1–3, PROJ-1/2, DODGE-1–4, DEF-M1–M4, the `defense_ab` chain, and
  how to read `--compare-defense`.
- `plan/vikings-for-hire/futures.md`:
  - the "Combat AI" section's block and dodge parts are marked done in 0.6.0, with a pointer to `plan/combat-ai/`;
  - Tank, DPS, Hit and run, Kite and Shoot from cover stay open, with a note that they can build on
    `AttackReader`/`Incoming`.
- The version, in `src/VikingsForHire/Plugin.cs` (`Version`), `src/VikingsForHire/VikingsForHire.csproj`
  (`<Version>`) and `package/manifest.json` (`version_number`): 0.5.0 → 0.6.0.
- `package/manifest.json` description: unchanged unless Tim asks.

## Steps
1. **README.** Write the combat section in the README's existing player-facing voice:
   - **Guards with shields** raise them in time for attacks they see coming, including arrows and thrown rocks.
     Well-timed blocks parry ("Parry!"), which staggers the attacker.
   - **Every hireling that fights** rolls out of the way of hits that would hurt a lot, and of explosions.
   - **Higher levels** read more attacks, parry more often, dodge more often, and roll again sooner. The numbers are in
     the data file's levels table: `readChance`, `parryChance`, `dodgeChance`, `dodgeCooldown`.
   - **`BlockAndDodge`** (Combat, server-synced) turns it all off and brings back the 0.5.0 fighting.
2. **CHANGELOG.** Add a 0.6.0 "Combat: blocking and dodging" entry with the same points, short, plus:
   - the balance note: health, damage and armor are unchanged;
   - an invitation to send balance logs.
3. **next-release.md.**
   - Compare `git log` since 0.5.0 with the tab, and add a row for any phase commit that's missing.
   - Make sure the by-hand checks for Tim's batch are all there, and add any that are missing:
     - a guard parrying a Brute;
     - a posted guard blocking Draugr arrows;
     - a worker rolling away from a troll;
     - no rolls off ledges or into water;
     - the dedicated-server check from phase 04.
   - Paste the `--compare-defense` table.
4. **test-checklist.md.** Add the rows and the `defense_ab` run instructions:
   - the command;
   - typically about an hour, up to about 2¼ hours if every fight runs to its limit;
   - the setting is left on at the end.
5. **futures.md.** Update as in Files touched.
6. **Version bump** to 0.6.0 in the three places. The `Package` target checks they agree.
7. **Package.** `dotnet build -c Release -t:Package` produces `dist/Spronglehump-HiredHands-0.6.0.zip`.
8. **Stop.** Don't tag, push, release or upload. Hand Tim the zip and the release notes, and wait for his go-ahead. On
   go-ahead: push, tag `v0.6.0`, create the GitHub release with the zip, and Tim uploads to Hexium.

## Build gate
- `dotnet build -c Release` and `dotnet test` pass.
- `dotnet build -c Release -t:Package` succeeds, with no version mismatch error.
- The full regression chain in single player through `mtb` passes: every row in `docs/test-checklist.md` that runs
  automatically, including the 0.6.0 tree and pass rows.

## Test plan
- Read the README and CHANGELOG sections back against the behaviour seen in phases 02–04, so no claim goes beyond what
  was tested.
- Tim's batch in `docs/next-release.md`: single player, then the local dedicated server.
- After release, run `fetch-balance.sh` and `--compare-defense` on the live server's logs from before and after the
  update. Any tuning is done as described in phase 05, step 7.

## Commit
`docs: blocking and dodging (README, CHANGELOG, checklist, futures); version 0.6.0`

## Rollback
Docs only, plus the version bump. Revert the commit. If 0.6.0 had already been released, the fix is a 0.6.1 rather than
a revert.

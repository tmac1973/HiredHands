# Phase 02 — The Wizard job and its data (per-mod overrides)

**Depends on:** nothing · **Enables:** phases 03, 04, 05, 06

## Goal
A new combat job, `JobType.Wizard`, exists in the rules and the data file: counted against the combat cap, priced
1.5x, unlocked at board level 6, or level 2 when Therzie's Wizardry is loaded. The data file gains a general way for a
job to change with a mod (`withMods`), plus a per-job outfit list. Its settings exist. No wizard fights yet; this phase
is rules, data and tests.

## Files touched
- `src/VikingsForHire/Core/JobType.cs`: `Wizard` appended to the enum (after `Cook`, so stored values keep their
  meaning); `IsGuard` includes `Wizard` (posts, attack orders, guard stances and follower posting all key on it), so
  `IsCombat` does too. The Contracts tab's job list is `Enum.GetValues(typeof(JobType))`, so the Wizard appears there
  (hidden with `CombatHirelings` off, like guards).
- `src/VikingsForHire/Hirelings/Combat/CombatBehaviour.cs`: `Wants` returns false for wizards until phase 04 adds their
  combat (so phases 02–03 never send a wizard into the guards' melee or archery code): it stands by, flees by the
  usual hurt rules, and is otherwise idle.
- `src/VikingsForHire/Core/StanceRules.cs`: the wizard's allowed stances (Passive, Defensive, Aggressive) and default
  (Defensive), as the archer's.
- `src/VikingsForHire/Core/Data/VfhData.cs`: `ArmorSetData.Cape` (string, default ""); `JobData.Outfit`
  (`List<ArmorSetData>`, default empty: use the shared `ArmorSets`); `JobData.WithMods` (`List<JobModOverride>`, an
  ordered list: the first entry whose mod is loaded wins); class `JobModOverride` with `Guid` (string), nullable
  `MinBoardLevel`, and `Gear` / `Outfit` lists (empty = keep the job's own).
- `src/VikingsForHire/Core/Data/DefaultData.cs`: the Wizard job entry (below); the shared sets keep `Cape` "".
- `src/VikingsForHire/Core/Data/DataDefaults.cs`: `FillMissing` fills a missing `wizard` job, with its `outfit` and
  `withMods`, from the defaults (as it fills other jobs and list entries).
- `src/VikingsForHire/Core/ModPresence.cs` (new, Unity-free so unit tests can drive it): `IsLoaded(string guid)`,
  `SetLoaded(IEnumerable<string> guids)` (replaces the set), and a test override `Pretend(string guid, bool? loaded)`
  (null = back to the real answer).
- `src/VikingsForHire/Compat/ModPresenceSetup.cs` (new): fills `ModPresence` with every GUID in
  `Chainloader.PluginInfos` (all loaded plugins, so a data file synced from the server later can name any of them);
  logs `compat.mods` with the GUIDs any job's `withMods` names and whether each is loaded.
- `src/VikingsForHire/Plugin.cs`: a soft dependency, `[BepInDependency("Therzie.Wizardry", BepInDependency.DependencyFlags.SoftDependency)]`,
  so BepInEx loads Wizardry first when it's there; `ModPresenceSetup.Run()` is called on `FejdStartup.Awake` (the main
  menu: every plugin is loaded by then), not in our own `Awake`.
- `src/VikingsForHire/Core/LevelRules.cs`: `Effective(JobType)` returns the job's data with the first loaded mod
  override applied; `MinBoardLevel` uses it (so `JobUnlocked`, the Post and Adopt checks in `BoardRosterOps`, and the
  Contracts tab's "needs a level N board" all follow); new `Gear(job)` and `Outfit(job)` return the effective lists.
- `src/VikingsForHire/Config/VfhConfig.cs`: settings in "8 - Combat", synced: `WizardDamage` (float, default 1),
  `WizardSpellsHit` (enum `SpellsHit { EnemiesOnly, EnemiesAndTerrain }`, default `EnemiesOnly`),
  `WizardAttackCooldown` (float, default 3 s). Not used until phases 04–05.
- `src/VikingsForHire/Localization/English.json`: `vfh_job_wizard` "Wizard".
- `src/VikingsForHire/Testing/Fixtures.cs`: fixture `mods <guid> <on|off|real>` (calls `ModPresence.Pretend`; nothing
  caches the answer, so it takes effect at once).
- `src/VikingsForHire/Testing/FixturesBoard.cs`: `board_far <dist> [level]`: sets the new board's level (free).
- `src/VikingsForHire/Testing/FixturesFollow.cs`: check `transfer_reply has <text>` (whether the last transfer
  answer, as `FollowerServer` replied it, contains the text; `transfer_posted` records it).
- `test/alias_vfh.yaml`: rows VFH-WIZ-1, VFH-WIZ-12.
- `tests/VikingsForHire.Tests/WizardRulesTests.cs` (new).

## Steps
0. **Test setup (Tim, once, in Gale):** add Therzie-Wizardry and its dependencies to the `vikingsforhire-dev` profile.
   Every in-game check from here on runs with it, using `mods Therzie.Wizardry off` for the without-Wizardry cases.
1. Append `Wizard` to `JobType`; `IsGuard` = `GuardMelee || GuardRanged || Wizard`. Stances in `StanceRules`.
2. Data classes as above; YAML keys `outfit`, `withMods` (entries with `guid`, `minBoardLevel`, `gear`, `outfit`),
   `cape`.
3. Wizard defaults in `DefaultData` (the data file is the source of truth for every level):
   - `CostMult = 1.5f`, `WorkerCombatFactor = 1.0f`, `MinBoardLevel = 6`;
   - `Gear` (without Wizardry): L1–6 `StaffFireball`, L7 `StaffIceShards`, L8 `StaffLightning`. Levels 1–5 are only
     reached by wizards hired while Wizardry was loaded, after it's removed;
   - `Outfit` (without): L1–5 none (shared armour); L6 `HelmetMage` / `ArmorMageChest` / `ArmorMageLegs`; L7–8
     `HelmetMage_Ashlands` / `ArmorMageChest_Ashlands` / `ArmorMageLegs_Ashlands`; no cape;
   - `WithMods`: one entry, `Guid = "Therzie.Wizardry"`, `MinBoardLevel = 2`;
     - `Gear`: L1–2 `StaffBlackforest_TW`, L3 `StaffSwamp_TW`, L4 `StaffMountain_TW`, L5 `StaffPlains_TW`, L6
       `StaffMistlands_TW`, L7–8 `StaffLightning`;
     - `Outfit` (helmet / chest / legs / cape), by level:
       - L1–2: `HelmetSpellslinger_BlackForest_TW` / `ArmorSpellSlingerChest_BlackForest_TW` /
         `ArmorSpellslingerLegs_BlackForest_TW` / `CapeSpellslinger_Blackforest_TW`;
       - L3: `HelmetSpellslinger_Swamp_TW` / `ArmorSpellSlingerChest_Swamp_TW` / `ArmorSpellslingerLegs_Swamp_TW` /
         `CapeSpellslinger_Swamp_TW`;
       - L4: `HelmetSpellslinger_Mountain_TW` / `ArmorSpellSlingerChest_Mountain_TW` /
         `ArmorSpellslingerLegs_Mountain_TW` / `CapeSpellslinger_Mountain_TW`;
       - L5: `HelmetSpellslinger_Plains_TW` / `ArmorSpellSlingerChest_Plains_TW` / `ArmorSpellslingerLegs_Plains_TW` /
         `CapeSpellslinger_Plains_TW`;
       - L6–8: `HelmetSpellslinger_Mistlands_TW` / `ArmorSpellSlingerChest_Mistlands_TW` /
         `ArmorSpellslingerLegs_Mistlands_TW` / `CapeSpellslinger_Mistlands_TW`.
4. `ModPresence`; `ModPresenceSetup`, run from a postfix on `FejdStartup.Awake` (the main menu, when every plugin is
   loaded); the soft dependency on `Plugin`.
5. `LevelRules.Effective(job)`: a copy of the job's `JobData`; walk `WithMods` in list order and apply the first entry
   whose `Guid` is loaded: its `MinBoardLevel` if set, its `Gear` and `Outfit` if non-empty.
6. **Existing wizards are never re-checked:** `JobUnlocked` is only asked when a contract is posted (Post) or a follower
   joins another board (Adopt). Without Wizardry a level 2–5 board refuses a *new* wizard; a wizard joining such a board
   is refused too and stays a follower on its own board's roster. Nothing dismisses a contract for being locked.
7. Unit tests: wizard is combat and a guard; price 1.5x; unlock 6 without the mod and 2 with it (`ModPresence.SetLoaded`);
   `Effective` picks the Wizardry gear and outfit only when loaded (`ModPresence.SetLoaded`); with two `withMods` entries both loaded, the first
   wins; a data file without `wizard` gets it from `FillMissing`; an entry for a mod that isn't loaded changes nothing.
8. The order of checks in `BoardRosterOps` (Post and Adopt alike, unchanged): hireling level within the board's
   maximum, then the job's unlock (`BadLevel`, "$vfh_op_job_locked"), then the caps. So a locked job is refused as
   locked even on a full board.
   Row VFH-WIZ-1 (dev game): `base; board_here; board_level 2; stock_board 2000 500; post Wizard 2 20` → `roster Active
   == 1`; `mods Therzie.Wizardry off`; `post Wizard 2 20` → `last_op == BadLevel` (the outcome
   for a locked job, as for others); `roster Active == 1` (the first wizard stays);
   `board_level 6; post Wizard 6 20` accepted → `roster Active == 2`; `mods Therzie.Wizardry real`; cleanup.
9. Row VFH-WIZ-12 (moving to another board): `base; board_here; board_level 3; stock_board 2000 500; post Wizard 3 20;
   roster Active == 1; stone 1; recruit_posted` (a level 3 wizard, hired with Wizardry loaded); `mods Therzie.Wizardry
   off`; `board_far 45 3` (a second board at level 3: `board_far` gains an optional level argument here); `wait 3;
   transfer_posted` → `transfer_reply has job_locked` (new check: the server's last answer to a transfer, recorded by
   the fixture), `posted_board == from`, `followers == 1`, `transfer_rosters == 1/0` (refused because the job is
   locked there, not for its level or a cap: still yours and on its own roster); `mods Therzie.Wizardry real`;
   cleanup.

## Build gate
- `dotnet build -c Release`.
- `dotnet test tests/VikingsForHire.Tests` (new and existing).

## Test plan
- Unit tests above.
- Rows VFH-WIZ-1 and VFH-WIZ-12 pass; `compat.mods` in the log names Therzie.Wizardry; `vfh_dump_data` shows the wizard job.

## Commit
`feat(wizard): Wizard job (combat, 1.5x, board level 6 or 2 with Therzie's Wizardry); per-mod job overrides (withMods), per-job outfits; WizardDamage, WizardSpellsHit, WizardAttackCooldown settings`

## Rollback
Revert the commit. A world that already hired wizards would then hold contracts with an unknown job value: this phase
isn't released alone; it ships with phases 03–06 as 0.8.0.

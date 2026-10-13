# Phase 03 — Choosing in the Shift+E panel, owed flags, respec at the board

**Depends on:** 01 (catalog, rules), 02 (ops, contract field, hireling copy, switch) · **Enables:** players choosing in
the game; 12 (the docs page uses the same names and descriptions)

## Goal
Players see and make the choices: an Enhancements page in the hireling's Shift+E panel with each level's two options,
a confirm step, owed choices flagged on the hover and the board's Roster tab, the picks listed on the hover, and a
Respec button on the Roster tab showing its cost. Every enhancement has a name and a one-line description that shows its
numbers from the data file.

## Files touched
- `src/VikingsForHire/UI/HirelingPanel.cs` — a `_enhancements` page flag (like `_limits` / `_why`), an "Enhancements"
  button on the main page labelled with the owed count ("Enhancements (2 to choose)"), the page builder
  `BuildEnhancements`, and the page state in the rebuild signature (line ~122: picks string, owed count, pending
  confirm).
- `src/VikingsForHire/UI/EnhancementsPage.cs` — new: lays out levels 2-8 as rows; each row has the level, and two option
  boxes (name in bold, description below in small text). States: chosen (gold border), not chosen at a chosen level
  (dimmed), owed (clickable), future ("at level N", greyed). First click selects and turns the row's button into
  "Confirm: <name>"; a second click sends `BoardContracts.ChooseEnhancement`; clicking elsewhere cancels. The panel grows
  to 760 × 560 while this page is open, and returns to its size on "back".
- `src/VikingsForHire/UI/RosterTab.cs` — in `Details`: "Enhancements: Fire arrows, Volley, … (2 to choose)" and a
  **Respec** button showing the cost (`EnhancementRules.RespecCost`), disabled when there are no picks or the board can't
  pay (as Promote is), calling `BoardContracts.Respec` after a confirm click; the list row shows "(2 to choose)" after the name when choices
  are owed: "(2 to choose)" after the name.
- `src/VikingsForHire/Hirelings/Hireling.cs` — `GetHoverText`: a line "2 enhancements to choose" (yellow) when owed, and
  the chosen enhancements' names on one line.
- `src/VikingsForHire/Core/Enhancements/EnhancementText.cs` — new: description strings with the numbers filled in from
  `EnhancementNumbers` (e.g. `$vfh_enh_guardranged_steadyaim_desc|10`).
- `src/VikingsForHire/Localization/English.json` — `vfh_enh_<job>_<name>` and `vfh_enh_<job>_<name>_desc` (lower case,
  e.g. `vfh_enh_miner_masterwork`) for all 126, plus page labels
  (`vfh_enh_title`, `vfh_enh_owed`, `vfh_enh_confirm`, `vfh_enh_at_level`, `vfh_enh_respec`, `vfh_enh_respec_cost`,
  `vfh_enh_none`, `vfh_enh_off`).
- `src/VikingsForHire/Testing/FixturesHireling.cs` — check `hireling <sel> enh_owed` (owed count); check `panel_access <sel>`
  (true when the Shift+E panel would open for you: the panel's own rule).
- `src/VikingsForHire/Testing/FixturesBoard.cs` — fixture `ward_foreign <radius>`: a ward owned by another (fake) player id
  over the test spot, enabled; removed by `clear_area`.
- `test/alias_vfh.yaml` — rows VFH-ENH-4, VFH-ENH-4b, VFH-ENH-4c.

## Steps
1. Write the names and descriptions for all 126 enhancements (the overview's wording, with `$1`-style placeholders for numbers);
   `EnhancementText` fills them from the data file so a server's tuning shows correctly.
2. Build the page and wire the confirm step; the page only offers choices when `VfhConfig.Enhancements` is on (off: a
   single line "Enhancements are turned off on this server", and no owed flags on the hover, the button or the Roster tab).
3. The panel's existing access rule decides who sees it (ward access, or the follower's owner); no new check.
4. Hover and Roster tab lines; the Respec button with its cost and confirm.
5. `LocalizationCoverageTests` already checks every `$vfh_` token used in code has an English string: extend it to cover
   `EnhancementText`'s generated keys for every catalog id.
6. Row VFH-ENH-4: post a level 3 Woodcutter: `hireling posted enh_owed == 2`; `enhance 2 B`: `enh_owed == 1`;
   `contract promote`: `enh_owed == 2`.
7. Row VFH-ENH-4b: post a level 3 Guard; `panel_access posted == true`; `ward_foreign 20`: `panel_access posted == false`
   and `board_access == false`; `clear_area`.
8. Row VFH-ENH-4c: a level 5 Archer with `enhance 2 A`: `enh_owed == 3`; `cfg_set Enhancements false`: `enh_owed == 0`
   and `vfh_enh` still `A------`; `cfg_set Enhancements true`: `enh_owed == 3` again.
9. By hand (screenshot in the dev game): open the page for a level 5 archer, choose with the confirm step, see the hover
   and Roster tab update; Respec shows the cost and works.

## Build gate
- Compile check; `dotnet test tests/VikingsForHire.Tests` (localization coverage included).
- In game: VFH-ENH-1..4, 4b, 4c pass; the panel opens, shows and chooses correctly (by hand, screenshot).

## Test plan
Row VFH-ENH-4 for the owed count through promotion; by-hand check of the page's layout and confirm step in the dev game;
the localization test for all 252 strings.

## Commit
`feat(enhancements): Enhancements page in the Shift+E panel with a confirm step, owed flags on hover and Roster tab, Respec at the board; names and descriptions for all 126`

## Rollback
UI-only on top of phase 02; reverting removes the page, flags and button. Picks already made stay on contracts.

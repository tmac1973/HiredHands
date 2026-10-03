# Phase 04 — Board management panel & board upgrades

**Depends on:** 02, 03 · **Enables:** 06 (Contracts and Roster tabs are filled in there), 12 (stone crafting checks the board level)

## Goal
Open a management panel with Shift+E on the board. It has three tabs: **Contracts**, **Roster** and **Upgrade**. This phase builds the panel framework and a fully working **Upgrade** tab. A board goes L1→L8 by spending the next boss trophy plus biome mats from the player's inventory, applied safely by the board's ZDO owner. The Contracts and Roster tabs show a placeholder "No hirelings yet" until phase 06.

## Files touched
- `src/VikingsForHire/UI/BoardPanel.cs`: the panel built with Jotunn `GUIManager` (`CreateWoodpanel`, `CreateButton`, `CreateText`, `CreateScrollView`, `CreateDropDown`). It opens, closes, switches tabs and blocks game input while open (`GUIManager.BlockInput(true)`).
- `src/VikingsForHire/UI/UpgradeTab.cs`: shows the current and next level, the next level's benefits (hireling cap, max hireling level, max work radius), a requirement list with icons and have/need counts (red when short), and an Upgrade button.
- `src/VikingsForHire/UI/ContractsTab.cs`, `src/VikingsForHire/UI/RosterTab.cs`: empty-state stubs.
- `src/VikingsForHire/Board/BoardRpc.cs`: board ZNetView RPC registration and handlers, starting with `VFH_RequestUpgrade`.
- `src/VikingsForHire/Board/HiringBoard.cs`: Shift+E opens the panel. Registers the RPCs. Fires a `LevelChanged` event.
- `src/VikingsForHire/Config/VfhConfig.cs`: registers `BoardPanelKey` as a Jotunn `ButtonConfig` (client-only).
- `src/VikingsForHire/Localization/English.json`: panel strings.

- `src/VikingsForHire/Testing/FixturesUpgrade.cs`: this phase's fixtures and checks (see the test plan).
- `test/alias_vfh.yaml`: adds this phase's row macros.

## As built (deviations from the steps below)
- **No refund patch:** deconstructing drops the piece's registered build cost (the L1 cost). Upgrades are data-only and never change it, so a patch on `Piece.DropResources` isn't needed.
- **No `BoardPanelKey`:** Shift+E is Valheim's alt-interact, so the cfg key was removed.
- **Files:** `Board/BoardUpgrade.cs` holds the RPCs and the client request/refund flow. `UI/PanelUi.cs` holds the Jotunn GUI helpers and `UI/IBoardTab.cs` the tab interface. Esc is handled by a `Menu.Update` prefix, so it closes the panel instead of opening the game menu.

## Steps
1. **Panel lifecycle:** one panel instance is created lazily on first open under `GUIManager.CustomGUIFront` and destroyed on `Game.OnDestroy`. It opens only when the player passes the ward check (`PrivateArea.CheckAccess(board.transform.position)`) and is within 5 m. It closes on Esc, the close button, moving more than 6 m away, or the board being destroyed. The panel is bound to the board's `ZDOID` and re-reads the board ZDO every 0.5 s while open, so all viewers see changes live.
2. **Upgrade request flow** (avoids races and duplication):
   - Client: the button is enabled only if the board level is below 8 and the player has every item. On click, the client removes the required items from the player inventory **first**, then sends `VFH_RequestUpgrade(expectedCurrentLevel)` with `nview.InvokeRPC("VFH_RequestUpgrade", …)`, which vanilla routes to the board's ZDO owner.
   - Owner handler: if `vfh_board_level == expectedCurrentLevel` and it's below 8, set the level +1 and reply `VFH_UpgradeResult(true, newLevel)` to the sender. Otherwise reply `(false, actualLevel)`.
   - Client on `false`: refund the exact removed items to the player inventory, or drop them at the player's feet if the inventory is full, and show `$vfh_upgrade_conflict`. On `true`: show the centre message "Hiring Board upgraded to Level N", play the vanilla `vfx_Place_workbench` effect at the board, and the panel refreshes.
   - Timeout: with no reply in 5 s, refund and show `$vfh_upgrade_timeout`.
3. **Requirements come from data:** the upgrade to level N+1 uses `DataStore.Current.BoardLevels[N].Cost`. Recipes for L2+ aren't Jotunn recipes, just data checked by the tab, so hot-reloaded YAML applies immediately.
4. **Deconstruction refund:** prefix on `Piece.DropResources` for `VFH_HiringBoard`. Drop the L1 build cost, as vanilla does, **plus nothing for upgrades**. Upgrade materials and trophies are consumed. A tooltip line on the Upgrade tab says so (`$vfh_upgrade_nonrefundable`).
5. **Effects of level** are read through `LevelRules` everywhere (cap, max hireling level, max radius). Nothing caches the level beyond the 2 s visual poll from phase 03.
6. **Debug command:** `vfh_board_setlevel <1-8>` (cheat; needs `devcommands`) sets the nearest board's level through the same owner RPC with the cost skipped.

## Build gate
- `dotnet build -c Release` and `dotnet test` pass.

## Test plan
- **Test macros** (phase 02 harness): `FixturesUpgrade.cs`. Fixtures: `upgrade_mats <toLevel>` (adds exactly that level's YAML cost to the inventory), `upgrade <n>` (clicks the Upgrade path in code through the real RPC). Checks: `inv <item>`, `upgrade_refund_ok`, and it reuses phase 03's `board_level`. Aliases added to `test/alias_vfh.yaml`: `vfh_t_upg1` (L1→L8 chain with exact-mat asserts at each step), `vfh_t_board2` (concurrent upgrade, run on two clients at once). Each row in `docs/test-checklist.md` names its alias, and a row passes when its `evt=test.result` line says `pass=true`.
- Single-player: with an L1 board, the Upgrade tab lists TrophyEikthyr 1, HardAntler 3, DeerHide 20, Flint 20, Wood 50 with have/need counts. The button is disabled until every item is present. Upgrading consumes exactly those items, the level shows 2, and the tint changes. Repeat with spawned mats up to L8. At L8 the tab shows "Maximum level".
- Hot-reload the YAML to change the L3 cost: the open panel shows the new cost within 1 s.
- Deconstruct an L4 board: only the L1 build cost drops.
- Dedicated server, two clients (row **VFH-BOARD-2**): both open the panel on the same L2 board and click Upgrade within the same second. Exactly one upgrade applies (L3). The other client gets the conflict message and a full refund. No items are lost or duplicated (count inventories before and after).
- Ward check: a non-permitted player can't open the panel (row **VFH-BOARD-1** extended).
- **Logs:** every scenario above is run with `vfh_debug All on`. Afterwards `VikingsForHire.log` must show the expected events with their ids (Info for state changes, Debug for AI switches, plans and ops), and no `lvl=E` lines. Any new code path in this phase logs through `VfhLog` per the phase 02 conventions, and guarded ticks and patches use `VfhLog.Guard`.

## Commit
`feat(board): management panel framework and trophy-gated board upgrades`

## Rollback
Revert the commit. Boards upgraded in test worlds keep their `vfh_board_level` ZDO value, which phase 03 code tolerates. It only affects the tint.

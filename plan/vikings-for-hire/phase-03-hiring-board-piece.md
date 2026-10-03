# Phase 03 — Hiring Board piece, base placement rules & storage

**Depends on:** 01, 02 · **Enables:** 04 (board UI/upgrades), 06 (contracts), every later phase that needs a board

## Goal
Add a placeable Level 1 Hiring Board to the Hammer. It can only be placed at a qualifying base, at least the minimum distance from other boards, and within the world cap, and it gives clear messages when it can't be placed. The board has a persistent identity and level in its ZDO, a food/coin storage that uses the vanilla chest UI, ward access checks, and exclusion from AzuAutoStore so Azu never deposits into it or takes from it. There are no hirelings yet. This is the anchor everything else hangs off.

## Files touched
- `src/VikingsForHire/Board/BoardPiece.cs`: registers the `VFH_HiringBoard` piece with Jotunn `PieceManager`.
- `src/VikingsForHire/Board/HiringBoard.cs`: `MonoBehaviour` on the board prefab. Handles ZDO keys, hover text, interaction, the static registry of loaded boards, and level visuals.
- `src/VikingsForHire/Board/BoardZdo.cs`: constants and typed accessors for board ZDO keys.
- `src/VikingsForHire/Board/PlacementPatches.cs`: Harmony patches enforcing `BaseRequirement` while placing.
- `src/VikingsForHire/Board/BoardRegistryServer.cs`: server-side world board count and position index (for `MaxBoardsPerWorld` and `MinDistanceBetweenBoards` across unloaded zones).
- `src/VikingsForHire/Board/BoardStorage.cs`: storage filter (only Coins and acceptable food may be placed in the board container).
- `src/VikingsForHire/Compat/AzuAutoStoreCompat.cs`: runtime detection plus exclusion of the board container.
- `src/VikingsForHire/Compat/AzuCraftyBoxesCompat.cs`: runtime detection plus exclusion of the board container from CraftyBoxes pulls. This also covers PullMats, which sources containers only through CraftyBoxes' `API`.
- `src/VikingsForHire/Compat/ExcludedContainers.cs`: one shared list of VikingsForHire prefab names that every compat patch excludes. Phase 03 adds `VFH_HiringBoard`, and phase 05 adds `VFH_Hireling`.
- `src/VikingsForHire/Localization/English.json` (embedded resource) and a `Localization.cs` loader using Jotunn `LocalizationManager`. All strings get `$vfh_*` tokens from now on.
- `src/VikingsForHire/Commands/DebugCommands.cs`: extends `vfh_dump_state` with every loaded board (id, level, position, owner peer, storage totals, raw roster bytes length) and adds `vfh_board_info` (prints the nearest board's id, level and storage totals in food points and coins).
- `src/VikingsForHire/Plugin.cs`: wires registration and patches.

- `src/VikingsForHire/Testing/FixturesBoard.cs`: this phase's fixtures and checks (see the test plan).
- `test/alias_vfh.yaml`: adds this phase's row macros.

## Steps
1. **Prefab:** in `PrefabManager.OnVanillaPrefabsAvailable`, clone the vanilla `sign` piece to `VFH_HiringBoard` with Jotunn `CustomPiece`/`PieceConfig`: Hammer, category `Crafting`, required station `piece_workbench`, cost from `DataStore.Current.BoardLevels[0].Cost`. Scale it ×2.0 on the board's local X/Y so it reads as a notice board rather than a sign. Remove the cloned `Sign` component. Add a `Container` (`m_name = "$vfh_board_storage"`, 4×2 = 8 slots, `m_privacy = Public` so ward rules come from PrivateArea, `m_checkGuardStone = true`) and a `HiringBoard` component. The icon is the vanilla `sign` piece icon tinted gold with Jotunn `RenderManager` (rendering the prefab to a sprite).
   - When the data reloads (`DataStore.Changed`), update the piece requirements in place.
2. **Board ZDO keys** (`BoardZdo`): `vfh_board_id` (string GUID, created on first `Awake` as owner if empty), `vfh_board_level` (int, default 1), `vfh_roster` (byte[] ZPackage holding active and pending contracts, used in phase 06), `vfh_last_upkeep_day` (int, phase 06).
3. **Level visuals:** `HiringBoard.ApplyLevelVisual()` tints the board mesh's material color along a gradient (L1 plain wood → L8 dark gold) using a `MaterialPropertyBlock`. It's called on `Awake` and whenever `vfh_board_level` changes (checked by polling the ZDO data revision every 2 s).
4. **Placement rules** (`PlacementPatches`):
   - Postfix on `Player.UpdatePlacementGhost`: when the selected piece is `VFH_HiringBoard`, gather counts within `BaseCheckRadius` of the ghost: `CraftingStation` named `$piece_workbench`, `Bed` components, and `Piece` components with `m_creator != 0`. Use `Piece.GetAllPiecesInRadius`. Find the nearest loaded board from the `HiringBoard` registry. Call `BaseRequirement.Evaluate`. If it fails, set `m_placementStatus = PlacementStatus.Invalid` and show the first missing requirement via `Player.Message(MessageHud.MessageType.Center, …)` at most once per second ("Needs a bed within 20m", "Needs 40 built pieces nearby (31/40)", "Another hiring board is 64m away (min 100m)").
   - The check is cached for 0.5 s while the ghost moves less than 1 m.
   - Prefix on `Player.PlacePiece` repeats the check (a defence against stale ghost status) and blocks with the same message.
   - Unloaded boards: the client asks the server through CustomRPC `VFH_BoardCheck(position)`. `BoardRegistryServer` keeps a list of every board's (id, position) by scanning `ZDOMan` for the prefab hash on server start and updating it on board ZDO creation and destruction (Harmony postfixes on `ZDOMan.CreateNewZDO` and `ZDOMan.HandleDestroyedZDO` filtered by prefab hash). It answers with the nearest board distance and world count. The client caches the answer for 2 s, and the ghost shows "Checking…" (still invalid) until the first answer arrives.
5. **Storage filter** (`BoardStorage`): prefix on `Inventory.CanAddItem`, and on `InventoryGrid`/`InventoryGui` drop handling (patch `InventoryGui.OnSelectedItem` to reject moves into the board container). Only `Coins` and items with `m_food > 0` that pass `FoodPoints.IsAcceptable` can be added. Rejected items show `$vfh_board_storage_reject`.
6. **Interaction & access:** `HiringBoard` implements `Hoverable`/`Interactable`. E opens the container (vanilla `Container.Interact`, ward-checked). Hover text shows: board name, level, "[E] Storage", "[Shift+E] Manage" (the panel arrives in phase 04, so until then Shift+E shows "coming soon" localized text), and the storage totals: "Food: 420 pts · Coins: 135".
7. **AzuAutoStore exclusion** (`AzuAutoStoreCompat`):
   - `IsLoaded` = `Chainloader.PluginInfos.ContainsKey("Azumatt.AzuAutoStore")`.
   - When loaded: take the assembly from `Chainloader.PluginInfos["Azumatt.AzuAutoStore"].Instance.GetType().Assembly`, then scan `AccessTools.GetTypesFromAssembly` for every method named exactly `IsPrefabExcluded` or `GetNearbyContainers` and patch each match. Patch with a Harmony prefix on `IsPrefabExcluded` (returns true/excluded when the container's prefab name is in `ExcludedContainers`), and a postfix on `GetNearbyContainers` that removes any container whose prefab is in `ExcludedContainers`. If either method can't be found, log a warning (`cat=Compat`) that tells the user to add `<prefab>:\n  exclude:\n    - All` for each excluded prefab to `Azumatt.AzuAutoStore.yml`.
   - All reflection is wrapped in try/catch, so an AzuAutoStore update can never stop VikingsForHire loading.
7b. **AzuCraftyBoxes / PullMats exclusion** (`AzuCraftyBoxesCompat`):
   - `IsLoaded` = `Chainloader.PluginInfos.ContainsKey("Azumatt.AzuCraftyBoxes")`.
   - When loaded: find the type `AzuCraftyBoxes.API` in its assembly. Add a postfix on `API.GetNearbyContainers(...)` that returns a **new** filtered list without containers whose `GetPrefabName()` is in `ExcludedContainers` (never mutating CraftyBoxes' cached list, which PullMats notes is reused). Add a prefix on `API.CanItemBePulled(containerPrefab, itemPrefab)` that returns false for excluded prefabs. Together these stop vanilla crafting/building through CraftyBoxes and PullMats' pulls from taking board food or coins.
   - Missing methods → `cat=Compat` warning telling the user to add each excluded prefab with `exclude: [All]` to `Azumatt.AzuCraftyBoxes.yml`. All reflection is in try/catch.
   - The session header (phase 02) logs each compat module's state: `evt=compat mod=AzuCraftyBoxes loaded=true patched=2/2`.
8. **Deconstruction:** for now, a board with no roster deconstructs normally and drops its storage (vanilla `Container` drop on destroy). The hireling-aware confirmation and contract voiding come in phase 06.

## Build gate
- `dotnet build -c Release` and `dotnet test` pass.

## Test plan
- **Test macros** (phase 02 harness): `FixturesBoard.cs`. Fixtures: `base <radius> <pieces>` (spawns a workbench, a bed and N wood floors around the player as player-built pieces, `Piece.SetCreator(localPlayerId)`, so they count for `BaseRequirement`), `base_partial <missing: workbench|bed|pieces>`, `board_here` (places a board at the crosshair through the real placement check), `crafty_probe`, `board_force_add <item> <n>`, `clear_area <radius>` (removes VFH test objects and pieces created by fixtures, tracked by a `vfh_fixture` ZDO flag). Checks: `placement_ok` (runs the placement rule at the crosshair, returns true/false plus the missing token), `board_count`, `board_storage food|coins`, `board_level`. Aliases added to `test/alias_vfh.yaml`: `vfh_t_board1` (ward access, split a/b), `vfh_t_azu1` (Azu exclusion: `board_storage food == 0` after Azu store), `vfh_t_crafty1` and `vfh_t_pull1` (split a/b around the craft attempt and the N press), plus single-player `vfh_t_place1`…`vfh_t_place4` for the placement cases in this test plan. Each row in `docs/test-checklist.md` names its alias, and a row passes when its `evt=test.result` line says `pass=true`.
- Single-player, new world, `devcommands`:
  - Try placing in an empty field: blocked, message lists the workbench.
  - Add a workbench, a bed, and fewer than 40 pieces: blocked with "(n/40)". At 40 it can be placed.
  - Place a second board 50 m away: blocked with the distance message. At 110 m (with its own base pieces) it's allowed.
  - `MaxBoardsPerWorld = 1` in the cfg: the second board is blocked even 500 m away while the first board's zone is unloaded (this checks the server registry).
  - Storage accepts CookedMeat and Coins, and rejects RawMeat, Wood and a sword. With `AllowRawFood = true`, RawMeat is accepted.
  - Hover shows correct food points (CookedMeat ×3 = 3 × its health+stamina).
  - Relog: board id and level persist (`vfh_board_info` prints the same GUID).
- Ward: a friend's ward with you not permitted means you can't open the storage. (Dedicated server row **VFH-BOARD-1** in the checklist.)
- AzuAutoStore enabled in Gale: stand next to the board with Wood and CookedMeat in your inventory, then trigger Azu's store hotkey and its ground auto-pickup with cooked meat dropped near the board. Nothing ends up in the board. Row **VFH-AZU-1**.
- AzuCraftyBoxes: put Coins and CookedMeat **only** in the board, with an empty chest nearby. Run `vfh_fixture crafty_probe`, which registers a temporary workbench recipe costing 1 Coins + 1 CookedMeat for the session. It shows as not craftable, and with the same items in a normal chest it crafts. Row **VFH-CRAFTY-1**.
- PullMats: select a piece whose materials are only in the board (`vfh_fixture board_force_add Wood 20` puts Wood in the board, bypassing its storage filter), then press N: PullMats reports them missing. With Wood in a normal chest, the pull works. Row **VFH-PULL-1**.
- **Logs:** every scenario above is run with `vfh_debug All on`. Afterwards `VikingsForHire.log` must show the expected events with their ids (Info for state changes, Debug for AI switches, plans and ops), and no `lvl=E` lines. Any new code path in this phase logs through `VfhLog` per the phase 02 conventions, and guarded ticks and patches use `VfhLog.Guard`.

## Commit
`feat(board): hiring board piece with base placement rules, storage and AzuAutoStore exclusion`

## Rollback
Revert the commit. Boards already placed in test worlds become unknown prefabs, which Valheim removes on load with a warning. Use a throwaway test world for this phase.

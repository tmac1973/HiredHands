# Phase 05 — Orders on the board

**Depends on:** 02 (`OrderList`), 04 (catalogs and `StockCounter` for the picker and the "have" counts) · **Enables:** 07–09 (workers read the board's orders)

## Goal
A new **Orders** tab on the Hiring Board where anyone with ward access adds, edits, reorders, pauses and removes the board's production orders. Orders are saved on the board's ZDO and changed through the server like contract ops, so every player sees the same list. Workers aren't reading them yet; the tab shows have/target counts so the list is useful to check by hand.

## Files touched
- `src/VikingsForHire/Board/BoardZdo.cs`: key `vfh_orders` (string, `OrderList.Serialize()`), `GetOrders`/`SetOrders`.
- `src/VikingsForHire/Core/RosterOps.cs`: `RosterOpType.Orders` with fields `OrderEdit` (`Add`, `Remove`, `Up`, `Down`, `Target`, `Pause`), `OrderItem`, `OrderKind`, `OrderTarget`, `OrderPaused`; written/read in `Write`/`Read`.
- `src/VikingsForHire/Board/BoardRosterOps.cs` (where `RosterOpType` ops are applied): applies the edit to the board's `OrderList` and saves it.
- `src/VikingsForHire/UI/OrdersTab.cs` (new, `IBoardTab`), `UI/BoardPanel.cs` (adds the tab after Roster).
- `src/VikingsForHire/Board/BoardOrders.cs` (new): `For(HiringBoard)` → `OrderList` (parsed, cached by ZDO data revision); `Stock(board)` via `StockCounter` over the board's maximum work radius.
- `src/VikingsForHire/Localization/English.json`: tab and row strings.
- `src/VikingsForHire/Testing/FixturesRoster.cs`: fixture `order <add|remove|clear> [item] [target] [seed|crop|kitchen]`, check `order <item> <target|paused|position>`.
- `test/alias_vfh.yaml`: `vfh_t_orders` (VFH-ORDER-1).
- `docs/test-checklist.md`: row VFH-ORDER-1.

## Steps
1. **Storage:** the board's ZDO owner applies edits (same path as other board ops through `MutationService.SubmitBoard`, with the ward check the board's other ops use). `OrderList` caps at 64 orders.
2. **Tab layout** (built with `PanelUi` like the other tabs):
   - Two groups, **Farm** (Seed orders first, then Crop) and **Kitchen**, each a list of rows: item icon, name, "have / target" (green when met), `-`/`+` (steps of 5; Shift for 1), up/down arrows (within its group's priority), pause toggle, remove (×).
   - "Add order" button per group opens a scrollable picker: Farm lists catalog crop items (seeds marked "seed") and regrowing items; Kitchen lists catalog kitchen outputs. Each entry shows its level; entries above the board's hired Farmer/Cook level, or with no Farmer/Cook hired, show greyed "needs a level N Farmer/Cook" but can still be added. Items `CropInfo.IsSeedItem` calls seeds (phase 02: CarrotSeeds, TurnipSeeds…) are added as `Seed` orders; other farm items (Carrot, Barley, Raspberry…) as `Crop`; kitchen outputs as `Kitchen`.
   - New orders start at target 20 (seed orders 10).
   - `Signature` includes the serialized orders, the counted stock of listed items (rounded) and the hired worker levels, so the tab refreshes when anything changes.
3. **Have counts** use `StockCounter` (`Chests + Growing`) (chests above reserves plus growing) over the board's max radius, refreshed at most once a second while the tab is open.
4. **Ward:** players without access see the list read-only (buttons disabled), as for contracts.

## Build gate
- `dotnet build -c Release` with 0 warnings; `dotnet test` passes (RosterOp round-trip test for the new fields; an order edit applied twice is idempotent for Add).

## Test plan
- `vfh_t_orders` (VFH-ORDER-1): `order clear`; `order add CarrotSeeds 10 seed`; `order add Carrot 20 crop`; `order add CookedMeat 40 kitchen`; checks positions (seed first), targets; `order remove Carrot`; check gone. Run in single player and from a client of the local dedicated server (the edit goes through the server).
- By hand: open the tab, add/reorder/pause/remove; a second player sees the same list; a player without ward access can't edit.

## Commit
`feat(board): Orders tab with production orders saved on the board`

## Rollback
Revert the commit; the `vfh_orders` ZDO key is simply ignored by older builds.

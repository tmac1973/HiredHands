# Phase 06 — Contracts, roster, payments & hireling lifecycle

**Depends on:** 02, 03, 04, 05 · **Enables:** 07–10 (hired workers to run jobs), 11 (v1 release), 12–15 (followers come from the roster)

## Goal
Make hiring real. In the Contracts tab you post a contract (job, level, work radius, stance). The hire fee in food points (plus coins at L2+) is taken from the board's storage, and a generated viking arrives after a delay, walks in from the edge of the base and reports to the board. The Roster tab manages hirelings: edit radius/stance, promote, dismiss, cancel pending. Daily upkeep is charged while the base is loaded, and unpaid hirelings leave. Death follows the permadeath/respawn config. Destroying a board voids its contracts. Every roster change goes through one server-routed mutation service, so changes stay correct even when the board or hireling is in an unloaded zone.

## Files touched
- `src/VikingsForHire/Core/Roster.cs`: Unity-free roster model: `ContractEntry { ContractId, Hid, Job, Level, Radius, Stance, State (Pending|Active|Leaving), UnpaidDays, ArriveAt (double), Snapshot (byte[]?), RespawnPending (bool) }`. Has `Roster.Serialize/Deserialize` (versioned) and the pure operations `Post`, `Activate`, `Edit`, `Promote`, `Dismiss`, `CancelPending`, `MarkDied(permadeath)`, `RemoveEntry(hid)`, `VoidBoard()` (clears every entry), `ApplyUpkeepDay(storageFunds)` → `UpkeepResult`, and `Counts()`. The roster serializer writes format version 1.
- `src/VikingsForHire/Core/RosterOps.cs`: serializable operation records (`RosterOp` with a type tag + fields, one per `Roster` operation above) so the same op can be applied locally or sent over the network. Also `HirelingOp` records for hireling ZDO changes: `SetFields(map of key → value)`, used for mode, stance, radius, level, owner, follow mode and the leaving timestamp.
- `src/VikingsForHire/Net/MutationService.cs`: server-side router. `Submit(targetZdoId or boardId, op)`: if the target ZDO's owner is a connected peer other than the server, forward with `ZRoutedRpc.InvokeRoutedRPC(ownerPeer, zdoId, "VFH_ApplyOp", bytes)`. Otherwise the server takes ownership (`zdo.SetOwner(ZDOMan.GetSessionID())`) and applies the op straight to the ZDO data. Clients call `Submit` through CustomRPC `VFH_SubmitOp`. Signature: `Submit(target, op, allowLocal = true)`. Fast path (only when `allowLocal`): if the calling client already owns the target ZDO (for example a player's own followers), it applies the op locally and skips the server round trip. In single-player and on a listen host, the server is local.
- `src/VikingsForHire/Board/BoardStorageLedger.cs`: owner-side helpers to count and take food points (via `FoodPoints.PlanPayment`) and coins from the board `Container` inventory and to refund into it (overflow drops at the board).
- `src/VikingsForHire/Board/HiringBoard.cs`: an owner tick loop every 5 s for arrivals, upkeep and respawns. Applies ops to its own ZDO. Spawns arrivals.
- `src/VikingsForHire/Board/ArrivalSpawner.cs`: finds the spawn point and spawns from the snapshot.
- `src/VikingsForHire/Board/BoardDestroyPatches.cs`: deconstruct confirmation and contract voiding.
- `src/VikingsForHire/Hirelings/Hireling.cs`: uses `vfh_radius` and `vfh_leaving_since` (keys reserved in phase 05). Reacts to `vfh_level`/`vfh_stance`/`vfh_radius` changes (re-applies gear/stats). `Leaving` mode handling. Death hook. Orphaned-board check.
- `src/VikingsForHire/Hirelings/Work/DropPile.cs`: `DropPile.Drop(board, items)` drops items at `board position + board forward × DropPileOffset`, stacked via vanilla `ItemDrop.DropItem` with a small random spread (0.5 m), and tags each `ItemDrop` with `m_customData["vfh_pile"]=1`. Phase 08 reuses it for delivery overflow.
- `src/VikingsForHire/Hirelings/LeaveBehaviour.cs`: a priority-1000 behaviour that drops cargo with `DropPile` when within the work radius of its board (or where it stands when farther away or the board is gone), walks 30 m away from home, then destroys itself (owner `ZNetScene.Destroy`). A 60 s timeout forces the destroy.
- `src/VikingsForHire/UI/ContractsTab.cs`: the real contract form.
- `src/VikingsForHire/UI/RosterTab.cs`: the real roster list.
- `src/VikingsForHire/Localization/English.json`: strings.
- `tests/VikingsForHire.Tests/RosterTests.cs`: all pure roster operations.
- `src/VikingsForHire/Commands/DebugCommands.cs`: adds the cheats `vfh_spawn_contract <job> <level>` (nearest board: creates a real roster entry and spawns at once, skipping payment and delay) and `vfh_dismiss_contract <short-hid>` (submits `Dismiss` through `MutationService` from wherever you are). `vfh_dump_state` prints each board's decoded roster (every entry and field). Adds `vfh_dump_index` (server/SP only: the hireling and board indexes).

- `src/VikingsForHire/Testing/FixturesRoster.cs`: this phase's fixtures and checks (see the test plan).
- `test/alias_vfh.yaml`: adds this phase's row macros.

## Steps
1. **Contracts tab:** job dropdown (5 jobs, localized), level slider 1..`MaxHirelingLevel(boardLevel)`, radius slider 10..`MaxWorkRadius(boardLevel)` in 5 m steps (default 20 or the max, whichever is smaller), stance dropdown limited to `StanceRules.Allowed(job)`, and a live cost preview: hire fee and daily upkeep, food points + coins, red when the board storage can't cover the hire fee. The **Post contract** button is disabled when the cap (`Active + Pending + Leaving` count ≥ `HirelingCap`) is reached or funds are short.
2. **Posting** (op `Post`, applied by the board owner):
   - Re-check the cap and funds against the current ZDO. Take the payment with `BoardStorageLedger`. Generate the appearance and name (`Appearance.Generate`) and build a snapshot with `HirelingSnapshot.Create` (every phase 05 key set: job, level, stance, radius, board id, home, appearance, name, mode `Working`, full health, empty cargo). Add a `Pending` entry with `ArriveAt = ZNet.GetTimeSeconds() + rand(ArrivalDelayMin, ArrivalDelayMax)`.
   - The reply to the poster is `VFH_OpResult(ok, reasonToken)`, which shows a centre message ("Contract posted — Sigrun the Miner arrives soon").
3. **Arrival** (board owner tick): for each `Pending` entry whose `ArriveAt` has passed, `ArrivalSpawner` tries 12 evenly spaced directions at `ArrivalSpawnDistance` from the board. A point is valid when `ZoneSystem.GetSolidHeight` is above water level, there's no `Piece` within 2 m, and `Pathfinding.instance.GetPath` from that point to the board succeeds. It uses the first valid one, or the board's front + 2 m as a fallback. It spawns from the snapshot, then applies op `Activate(contractId, hid)`, sets `vfh_mode = Working`, and the hireling walks to the board (IdleBehaviour home = board). If nobody's near, arrival waits until the board's zone is loaded again: delayed arrival never happens while unloaded.
4. **Roster tab:** one row per entry: name, job, level, state/status token, health %, unpaid days. Buttons:
   - **Edit**: radius/stance (op `Edit` on the roster, plus a hireling ZDO update through `MutationService` targeting the hireling's ZDOID, looked up by `vfh_hid` in the server's hireling index, see step 9).
   - **Promote**: a level picker from current+1 up to the board max, paying `PromotionCost(job, current, chosen)`.
   - **Dismiss**: confirmation popup (`UnifiedPopup`), then the hireling enters `Leaving`. No refund.
   - **Cancel**: for `Pending` entries only, a full refund into board storage.
5. **Upkeep** (board owner tick): `day = EnvMan.instance.GetDay()`. If `day > vfh_last_upkeep_day`: call `Roster.ApplyUpkeepDay` once (only one day is charged, however many passed, so unloaded days are free) for every `Active` entry, in posting order, all-or-nothing per entry. Entries it can't pay get `UnpaidDays++` and the hireling status becomes `$vfh_status_unpaid`. When `UnpaidDays > UnpaidDaysBeforeLeaving`, set `Leaving`. A paid day resets `UnpaidDays` to 0. Save `vfh_last_upkeep_day = day`. On a brand-new board, `vfh_last_upkeep_day` is set to the current day so the first charge is the next morning.
6. **Death:**
   - Hireling `OnDeath` (owner): drop cargo where it died (vanilla `Container.DropAllItems`; gear never drops), then `MutationService.Submit(boardId, MarkDied(hid, PermadeathEnabled))`. `MarkDied` carries no snapshot: every `ContractEntry` keeps the snapshot created at Post time, and `Edit`/`Promote` update the entry's job/level/stance/radius fields. When the entry respawns, `ArrivalSpawner` applies those fields over the stored snapshot.
   - Permadeath on: the entry is removed. A message goes to all players within 50 m of the board when it's loaded ("Sigrun has died"), and a vanilla `Chat` message goes to the players the server knows are near the board.
   - Permadeath off: the entry goes to `Pending` with `RespawnPending = true`, `ArriveAt = now + RespawnCooldownSeconds`, reusing the entry's stored snapshot (cargo empty, health full). When it arrives, the board owner charges `RespawnCost` first. If funds are short, the entry stays pending with status `$vfh_status_awaiting_payment` and retries every tick.
7. **Leaving:** when a hireling ZDO's `vfh_mode` becomes `Leaving` (via the mutation service), `LeaveBehaviour` takes over. When the hireling destroys itself it submits `RemoveEntry(hid)`. If it's unloaded when it's set to leave, it carries out the leave as soon as it loads. Hireling ZDOs left in `Leaving` for more than 3 in-game days (5400 s of `ZNet.GetTimeSeconds()`, since one Valheim day is 1800 s) are destroyed by the server sweep (step 9).
8. **Board destruction:**
   - Prefix on `Player.RemovePiece` for `VFH_HiringBoard` with any roster entries: show a `UnifiedPopup` confirm ("N hirelings will leave and their contracts will be void"), and remove only on confirm.
   - Prefix on `WearNTear.Destroy` for `VFH_HiringBoard` (it runs on the owner before the ZDO is destroyed; deconstruction goes through it too): submit `VoidBoard(boardId)` to the server. The server sets `vfh_mode = Leaving` on every hireling ZDO with that board id (via the hireling index) and discards pending entries **without** refund (the container contents already drop as in vanilla).
   - Safety net: every hireling owner checks once a minute with CustomRPC `VFH_BoardExists(boardId)`, answered from `BoardRegistryServer`. If the board doesn't exist, it leaves.
9. **Server hireling index:** like `BoardRegistryServer`, keep `hid → ZDOID` and `boardId → set<hid>` by scanning `VFH_Hireling` ZDOs on start and updating them with the same create/destroy postfixes. Every 10 minutes a sweep destroys hireling ZDOs in `Leaving` older than 5400 s (`now − vfh_leaving_since`, both in whole seconds of ZNet time) and ZDOs whose board id is unknown.
10. **Hover/status:** the hireling hover shows status: Arriving, Working, Unpaid (n/2 days), Leaving.

## Build gate
- `dotnet build -c Release` and `dotnet test` pass (`RosterTests` cover cap, payment, upkeep with partial funds, promotion, cancel refund, death in both modes, and serialization round-trip and versioning).

## Test plan
- **Test macros** (phase 02 harness): `FixturesRoster.cs`. Fixtures: `stock_board <foodPoints> <coins>` (fills board storage with cooked meat and coins), `post <job> <level> <radius>` (posts through the real UI path), `skip_days <n>` (wraps vanilla `skiptime` in 1800 s steps with a `wait` between). Checks: `roster <Pending|Active|Leaving|all>`, `roster_entry <short-hid> <field>`, `board_storage food|coins`, `last_upkeep_day`, `index <hid> exists` (server-forwarded: true when the server index maps the hid to a live ZDO). Aliases added to `test/alias_vfh.yaml`: `vfh_t_hire3`…`vfh_t_hire6`, plus single-player `vfh_t_contract1` (post→arrive), `vfh_t_upkeep1` (pay, unpaid ×3 → leaves) and `vfh_t_death1`/`vfh_t_death2` (permadeath on/off). All use `fast_timers on`. Each row in `docs/test-checklist.md` names its alias, and a row passes when its `evt=test.result` line says `pass=true`.
- Single-player:
  - Post an L1 Woodcutter with only cooked meat in storage: food points are taken (cheapest first), no coins. After 90–240 s the viking appears about 35 m out and walks to the board.
  - Post an L2 contract without coins: blocked, preview in red.
  - Reach the cap: Post is disabled. Cancel a pending one and the refund lands in storage.
  - Upkeep: `devcommands` + `skiptime 1800` with the base loaded charges one day. Empty the storage and skip 3 days: the status goes to Unpaid 1, then 2, then the hireling leaves, dropping its cargo at the board.
  - Log out for 2 real hours of server time (dedicated) and return: at most one day is charged.
  - Promote L1→L2: the difference is charged, and the gear and health update.
  - Death with permadeath: the entry is removed and the cargo drops. With permadeath off: they're back after 600 s, charged 50%. With no funds, they wait as "awaiting payment".
  - Deconstruct a board with hirelings: the confirm popup appears. On confirm, the hirelings drop their cargo and walk off, and storage drops.
- Dedicated server, two clients (rows **VFH-HIRE-3..6**): client A posts while client B owns the board ZDO (B stood there first). It's applied once. B sees it in the Roster live. Client A kills a contracted hireling (`vfh_kill_hirelings`) while client B owns the board ZDO: the `MarkDied` op is forwarded to B (the log shows `evt=op.forward` on the server and `evt=op.apply` on B) and the entry is removed once. Then both clients leave the base area and the server applies a `Dismiss` submitted from far away (`vfh_dismiss_contract <short-hid>`, a cheat command added in this phase) directly to the unloaded board ZDO (`evt=op.apply peer=server`). The roster shows it gone when you come back. Restart the server mid-pending: the arrival still happens.
- **Logs:** every scenario above is run with `vfh_debug All on`. Afterwards `VikingsForHire.log` must show the expected events with their ids (Info for state changes, Debug for AI switches, plans and ops), and no `lvl=E` lines. Any new code path in this phase logs through `VfhLog` per the phase 02 conventions, and guarded ticks and patches use `VfhLog.Guard`.

## Commit
`feat(contracts): contracts, roster, food/coin payments, upkeep, death and board voiding`

## Rollback
Revert the commit. Board `vfh_roster` keys and hireling ZDOs from test worlds remain. Phase 05 code ignores the roster keys, and stray hirelings can be removed with `vfh_kill_hirelings`. Use a test world.

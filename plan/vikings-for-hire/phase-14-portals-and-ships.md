# Phase 14 — Portal & teleport travel and ship stowing

**Depends on:** 02, 05, 06, 12, 13 · **Enables:** 15 (orphan rules account for transit and stowed states), 16

## Goal
Let followers go wherever the player goes. When the owner teleports (portals, dungeon entrances and exits, any `Player.TeleportTo`), followers in Follow or Gather Nearby within `PortalFollowRadius` go along, unless their cargo holds non-teleportable items, in which case they stay behind (follow mode Stay) with a message (configurable). When the owner takes a ship's helm, nearby followers are **stowed** aboard as passengers and reappear next to the owner on disembarking. Everything is built on phase 05 snapshots, so no follower is ever lost or duplicated.

## Files touched
- `src/VikingsForHire/Followers/TeleportTravel.cs`: patches and the transit state machine.
- `src/VikingsForHire/Followers/TransitStore.cs`: the local player's in-transit follower snapshots, mirrored to `Player.m_customData["vfh_transit"]` (base64 of a ZPackage list) so they survive a crash or quit.
- `src/VikingsForHire/Followers/ShipStowage.cs`: stow and unstow logic plus the ship ZDO key `vfh_stowed` (byte[] list of snapshots + owner ids).
- `src/VikingsForHire/Followers/FollowerOps.cs`: adds the server notifications `ReportTransitStart(hid, snapshot)`, `ReportTransitEnd(hid, zdoId)`, `ReportStowed(hid, shipZdoId, snapshot)` and `ReportUnstowed(hid, zdoId)`, plus the server op `ReturnHome(snapshot, fromPosition)`.
- `src/VikingsForHire/Core/Roster.cs`: adds `QueueReturn(hid, snapshot, arriveAt)`. It converts the hireling's **existing** entry (found by hid) to `Pending` with `ReturnPending = true`, the new snapshot, and `ArriveAt`. It never adds a second entry, so the cap count is unchanged and arrival `Activate`s the same contract id. If no entry exists (the contract was voided or dismissed meanwhile), it returns `NotFound`, and `ReturnHome` then leaves the cargo in a `CargoCrate` as for a missing board. It's idempotent per hid (a second call for a hid that's already queued is ignored), and the `ReturnPending` flag on `ContractEntry`. The roster serializer goes to format version 2, and it still reads version 1, which defaults `ReturnPending = false`.
- `src/VikingsForHire/Board/ArrivalSpawner.cs`: for entries with `ReturnPending`, spawns without charging, keeps the cargo, and sets `vfh_mode = Working`, `vfh_owner = 0`, `vfh_deliver_pending = true`.
- `src/VikingsForHire/Core/TransitEncoding.cs`: pure encode/decode of a list of snapshot byte arrays to and from a base64 string (used by `TransitStore` for `Player.m_customData`).
- `tests/VikingsForHire.Tests/TransitEncodingTests.cs`: round trip, empty list, and corrupt input (returns an empty list and logs).
- `src/VikingsForHire/Core/RosterOps.cs`: adds the `QueueReturn` op. Also a generic `ZdoListOp.RemoveSnapshot(key, hid)` that `MutationService` applies to any ZDO's snapshot-list key. It's used for the ship's `vfh_stowed`.
- `tests/VikingsForHire.Tests/RosterTests.cs`: `QueueReturn` idempotency and v1 → v2 reading.
- `src/VikingsForHire/Net/HirelingIndexServer.cs` (the phase 06 index, extracted to its own file here): tracks a per-hid state `Present | InTransit | Stowed | Returning` with a timestamp, the carrier (player id or ship ZDOID) and the held snapshot. `ReturnHome` sets `Returning` and does nothing if the hid is already `Returning` or `Present` under another ZDO. The arrival spawn sets it back to `Present`.
- `src/VikingsForHire/Localization/English.json`: strings.

- `src/VikingsForHire/Testing/FixturesTravel.cs`: this phase's fixtures and checks (see the test plan).
- `test/alias_vfh.yaml`: adds this phase's row macros.

## Steps
1. **Teleport capture:** prefix on `Player.TeleportTo(pos, rot, distantTeleport)` for the local player (covers `TeleportWorld` portals, `Teleport` dungeon doors, and the vanilla console `goto`):
   - Pick followers owned by this player with mode Following (follow modes Follow or GatherNearby; followers in follow mode `Stay` don't come along) within `PortalFollowRadius`.
   - For each one: if `!AllowNonTeleportableThroughPortals` and its cargo `Inventory.IsTeleportable()` is false, it stays (follow mode `Stay`, `vfh_stay_pos` = its position) and shows the message "Ragnar can't take ore through the portal". Only a real portal (`TeleportWorld`) checks teleportability. Dungeon doors (`Teleport`) always allow.
   - Otherwise capture a snapshot (`HirelingSnapshot.Capture`, including cargo), add it to `TransitStore` (and save to custom data), send `ReportTransitStart(hid, snapshot)` to the server, which holds it in memory until `ReportTransitEnd`, then `ZNetScene.Destroy` the follower. The destroy is local because the owner owns it (phase 12).
2. **Teleport release:** postfix polling in `Player.Update` while `TransitStore` isn't empty. When `!IsTeleporting()` and `ZNetScene.IsAreaReady(player.position)`, spawn each snapshot at a free spot in a 2–4 m ring behind the player (ground-snapped). Send `ReportTransitEnd(hid, newZdoId)` (the server updates its `hid → ZDOID` index. The roster only stores hids, so the board needs no change), then clear the store and the custom data.
3. **ReturnHome server op (introduced here, reused by phase 15):** `ReturnHome(snapshot, fromPosition)` makes the server look up the board by the snapshot's board id (if it's missing, the hireling is discarded and its cargo is left in a lootable vanilla `CargoCrate` at `fromPosition`. The server creates the crate's ZDO with `ZDOMan.CreateNewZDO(position, prefabHash)` and writes the cargo into its `items` key, so this works even in unloaded zones), compute `OrphanRules.ReturnSeconds(distance(fromPosition, board), ReturnSecondsPer100m, ReturnMinSeconds, ReturnMaxSeconds)`, and submit the roster op `QueueReturn(hid, snapshot, ArriveAt = now + seconds)` to the board. That reuses the phase 06 pending-arrival path (`Pending` entry with snapshot, no payment on arrival, `ReturnPending = true`, spawned by the phase 06 `ArrivalSpawner` with mode `Working` and owner 0, keeping cargo). Add `QueueReturn` and the `ReturnPending` flag to `Core/Roster.cs` with unit tests.
   **Crash or quit during transit:** on `Player.OnSpawned` (login), if `vfh_transit` custom data isn't empty, send each snapshot as `ReturnHome(snapshot, fromPosition = player position)`, then clear it. `HirelingIndexServer` (this phase owns this check; phase 15 doesn't duplicate it) applies the same rule every 5 s to any `InTransit` hid whose carrier has been offline for over `OrphanStaySeconds`, using the snapshot the client sent with `ReportTransitStart`. That message includes the snapshot bytes so the server can recover it alone.
4. **Ship stowing:** postfix on `Player.StartShipControl(ShipControlls)` when the local player takes the helm:
   - Pick your followers within `ShipStowRadius` in follow modes Follow/GatherNearby. Snapshot each, then `ZNetScene.Destroy` it. Send the snapshots to the ship's ZNetView owner via the RPC `VFH_Stow(bytes)`, which appends them to the ship's `vfh_stowed` ZDO key, and tell the server `ReportStowed(hid, shipZdoId, snapshot)`.
   - Hover on the ship shows "Passengers: 2". Stowed followers don't fight.
5. **Unstowing:** when the owner stops controlling the ship (`Player.StopShipControl` postfix) **and** steps onto ground (`IsOnGround()` with the ground not being the ship, checked every 0.5 s for 30 s after leaving the helm, while within 15 m of the ship) → request `VFH_Unstow(ownerId)`. The ship owner removes that owner's snapshots from `vfh_stowed` and returns them. The owner client spawns them around itself (as in step 2) and reports `ReportUnstowed`. If another player takes the helm first, the original owner's followers stay stowed until the original owner disembarks from that ship.
6. **Ship destroyed:** prefix on `WearNTear.Destroy` for objects with a `Ship` component and a non-empty `vfh_stowed` (runs on the ship's owner): send each snapshot to the server as `ReturnHome(snapshot, fromPosition = ship position)` (step 3).
7. **Owner logout while stowed:** stowed snapshots stay on the ship. When the owner logs back in and boards and leaves that ship, they unstow. The phase 15 orphan rules treat a stowed hid as orphaned if its owner is offline for longer than `OrphanStaySeconds`.

## Build gate
- `dotnet build -c Release` and `dotnet test` pass (including `TransitEncodingTests` and the roster additions).

## Test plan
- **Test macros** (phase 02 harness): `FixturesTravel.cs`. Fixtures: `portal_pair <tag> <distance>` (two connected wood portals, one at the player and one `distance` m away, with the tag set through the ZDO), `ship <prefab>` (a Karve or Longship next to the player on water), `give_follower <item> <n>`. Checks: `followers_near <radius>`, `transit_count`, `stowed <ship-tag>`, and extends the phase 06 `index` check with `index <hid> state`. Aliases added to `test/alias_vfh.yaml`: `vfh_t_travel1` (crash recovery, split a/b around a forced quit), `vfh_t_travel2` (ship, split a/b/c around taking the helm and disembarking), `vfh_t_travel3` (dedicated), plus single-player `vfh_t_portal1` (wood goes through, ore stays). Each row in `docs/test-checklist.md` names its alias, and a row passes when its `evt=test.result` line says `pass=true`.
- Portal with 2 followers carrying Wood: both arrive next to you on the other side, with cargo intact and health preserved.
- A follower carrying CopperOre: stays behind with the message. Its partner without ore goes through. With `AllowNonTeleportableThroughPortals=true` both go.
- Enter and exit a Burial Chamber with followers: they come along both ways, even with ore.
- Kill the game process (`pkill valheim`) mid-portal-loading, then relog: the followers are queued home and reappear at the board after the distance-based timer, still holding their cargo (row **VFH-TRAVEL-1**).
- Karve: take the helm with 2 followers nearby, and they vanish with "Passengers: 2". Sail, beach, step off: they reappear beside you. Destroy the ship with followers stowed: they come back at the board (row **VFH-TRAVEL-2**).
- Dedicated server: a second player sees followers vanish and reappear correctly through portals, and the board's roster shows the right state throughout (row **VFH-TRAVEL-3**).
- **Logs:** every scenario above is run with `vfh_debug All on`. Afterwards `VikingsForHire.log` must show the expected events with their ids (Info for state changes, Debug for AI switches, plans and ops), and no `lvl=E` lines. Any new code path in this phase logs through `VfhLog` per the phase 02 conventions, and guarded ticks and patches use `VfhLog.Guard`.

## Commit
`feat(followers): follow through portals and teleports with vanilla teleport rules, and ship passenger stowing`

## Rollback
Revert the commit. Before reverting, make sure no snapshots are in transit or stowed (dock and unstow, finish portal trips). Leftover `vfh_stowed` ship keys and `vfh_transit` custom data are ignored by earlier code, which strands those followers. Use `vfh_kill_hirelings` plus board roster cleanup (`Cancel`/`Dismiss`) in test worlds.

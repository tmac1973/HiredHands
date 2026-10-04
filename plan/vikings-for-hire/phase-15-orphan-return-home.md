# Phase 15 — Orphan detection & "Return Home"

**Depends on:** 02, 05, 06, 08, 12, 13, 14 · **Enables:** 16

## Goal
Followers who get separated from their owner never stay lost. The followers that are stuck, too far away for too long, left in Stay while the owner wanders far off, or whose owner died, logged out or teleported without them, become **orphaned**. They despawn into a "Returning Home" state and reappear at their home board after a distance-based timer (about walking pace, with a minimum and a maximum), still carrying their cargo. Ore that returns this way takes at least as long as walking it home. The board's Roster shows the countdown.

## Files touched
- `src/VikingsForHire/Followers/OrphanMonitor.cs`: owner-client-side checks for loaded followers (stuck, distance, stay distance).
- `src/VikingsForHire/Net/OrphanServiceServer.cs`: server-side checks for followers whose owner is offline or dead, and for stowed followers. Long `InTransit` is already handled by phase 14's `HirelingIndexServer`.
- `src/VikingsForHire/Core/OrphanRules.cs`: adds `ShouldOrphan` (`ReturnSeconds` already exists from phase 02), with all thresholds from config.
- `src/VikingsForHire/Followers/FollowerOps.cs`: `Orphan(hid)` → capture the snapshot, destroy, and send `ReturnHome` (phase 14 op).
- `src/VikingsForHire/Net/FollowerSessionServer.cs`: replaces phase 12's logout rule (step 4).
- `src/VikingsForHire/UI/RosterTab.cs`: "Returning home — 3:40" rows (from `Pending` entries with `ReturnPending`).
- `src/VikingsForHire/Localization/English.json`: strings.
- `tests/VikingsForHire.Tests/OrphanRulesTests.cs`: every trigger, the thresholds, and the timer clamping.

- `src/VikingsForHire/Testing/FixturesOrphan.cs`: this phase's fixtures and checks (see the test plan).
- `test/alias_vfh.yaml`: adds this phase's row macros.

## Steps
1. **Triggers** (`OrphanRules.ShouldOrphan(state, cfg)`; state = mode, follow mode, distance to owner, seconds over distance, seconds stuck, owner online/dead, inside home radius. A follower left behind by its owner's teleport is covered by the distance and stay-distance rules):
   - Following (Follow/GatherNearby) and more than `OrphanDistance` (60 m) from the owner for `OrphanDistanceSeconds` (30 s).
   - Following, and pathing has made no progress (moved < 1 m while having a move target) for `OrphanStuckSeconds` (20 s). Stuck detection samples the position every 2 s.
   - Follow mode `Stay` (including followers left behind at a portal because of ore) and more than `OrphanStayDistance` (150 m) from the owner for `OrphanStaySeconds` (120 s).
   - Owner dead: orphan 30 s after the owner's death, **unless** the follower is within 60 m of the owner's tombstone and in Stay. (Only Stay followers wait. Moving ones go home so you aren't stuck guarding a corpse run.)
   - Owner offline (logged out, disconnected) outside the home radius: orphan after 30 s (server-side).
   - Stowed or in transit with the owner offline for more than `OrphanStaySeconds` (server-side, using the snapshot held from phase 14).
   - Inside the home board's work radius, followers never orphan. If the owner logs out, they're released to work with `ReleaseToWork` instead (phase 12 rule kept for that case).
2. **Owner-side monitor** (`OrphanMonitor`, every 2 s on the owner client, which owns its followers): evaluate the loaded followers. On a trigger, `Orphan(hid)`: snapshot (with cargo and current health), `ZNetScene.Destroy`, then `ReturnHome(snapshot, fromPosition = follower position)`. Show the owner a message ("Ragnar lost track of you and is heading home (≈4 min)").
3. **Server-side service** (`OrphanServiceServer`, every 5 s):
   - For hireling ZDOs with `vfh_mode == Following` (any follow mode) and an `vfh_owner` that isn't connected: start a 30 s grace timer (kept in memory), then orphan them straight from the ZDO data. Build the snapshot with `HirelingSnapshot.FromZdo(zdo)` (phase 05), `ZDOMan.DestroyZDO`, then `ReturnHome`. This works even when the zone is unloaded, because the server holds all ZDOs.
   - For `Stowed` index entries meeting the step 1 rules: `ReturnHome` from the stored snapshot, and also apply `ZdoListOp.RemoveSnapshot("vfh_stowed", hid)` (phase 14) to the ship through `MutationService`.
   - Owner died: the server learns about it from a client message `ReportOwnerDied(playerId, tombstonePos)` sent from the `Player.OnDeath` postfix. The owner client also runs its own monitor, and whichever acts first wins: the second sees the ZDO gone and does nothing.
4. **Logout replacement:** phase 12's "elsewhere → Stay" rule is replaced. Followers outside the home radius at logout are orphaned by the server after the grace period. Inside the home radius they're released to work.
5. **Return timer:** `OrphanRules.ReturnSeconds(straight-line distance from the orphan point to the board, ReturnSecondsPer100m = 25, min 60, max 1200)`. Arrival goes through `ArrivalSpawner`'s `ReturnPending` path (phase 14): it spawns at the base edge and walks in, with mode `Working`, owner 0, cargo intact, and `vfh_deliver_pending = true`. Once there, normal job logic delivers the cargo per item type to chests (phase 08), so a miner returning with copper drops it into the copper chest on its own.
6. **Duplication guards:** every orphan path goes through `ReturnHome`, which (phase 14) skips hids already `Returning` or `Present` under another ZDO, and `QueueReturn` is idempotent per hid. This phase adds no new guard, only the VFH-ORPHAN-4 stress test.
7. **Board gone:** if the home board was destroyed while a follower was away, phase 14's `ReturnHome` already discards the hireling and leaves its cargo in a `CargoCrate` at the orphan point. This phase only adds the owner message "Ragnar's hiring board is gone — he left your service" and the test below.

## Build gate
- `dotnet build -c Release` and `dotnet test` pass (`OrphanRulesTests` cover every trigger and threshold).

## Test plan
- **Test macros** (phase 02 harness): `FixturesOrphan.cs`. Fixtures: `strand_follower <distance>` (moves the nearest follower `distance` m away through a snapshot respawn, so the distance rule fires), `trap_follower` (encloses it in fixture walls so the stuck rule fires). Checks: `roster_entry <h> ReturnPending`, `roster_entry <h> ArriveAt_in` (seconds remaining), `hireling_count`, and `index <hid> state` (phase 14). Aliases added to `test/alias_vfh.yaml`: `vfh_t_orphan1`…`vfh_t_orphan4` (with `fast_timers on`, asserting the return timer within ±2 s of `ReturnSeconds(distance, 2.5, 6, 120)`, which is the `fast_timers` values), plus single-player `vfh_t_board_gone1` (cargo crate appears). Each row in `docs/test-checklist.md` names its alias, and a row passes when its `evt=test.result` line says `pass=true`.
- Run 70 m away from a following woodcutter on a cliff it can't climb: after about 30 s, "lost track" message. It reappears at the board after `max(60, distance/100 × 25)` s with its Wood, and delivers it to the wood chest.
- Trap a follower in a pit (dig with a pickaxe): it orphans after 20 s stuck.
- Leave a Stay follower and walk 160 m away for 2 minutes: it orphans.
- Die with Stay followers near your body: they wait. Moving ones go home after 30 s.
- Log out mid-field (dedicated): followers return home after the grace + timer, even with nobody near them (rows **VFH-ORPHAN-1..3**).
- Ore loophole check: an orphaned miner carrying copper 800 m from base takes 200 s (≥ the minimum), and the copper arrives with it.
- Destroy the home board while a follower is out: it's discarded and a cargo crate holding its items appears at its last position.
- Duplication stress: alt-F4 at the same moment a follower orphans. Exactly one copy returns (row **VFH-ORPHAN-4**).
- **Logs:** every scenario above is run with `vfh_debug All on`. Afterwards `VikingsForHire.log` must show the expected events with their ids (Info for state changes, Debug for AI switches, plans and ops), and no `lvl=E` lines. Any new code path in this phase logs through `VfhLog` per the phase 02 conventions, and guarded ticks and patches use `VfhLog.Guard`.

## Commit
`feat(followers): orphan detection and distance-timed return home with cargo`

## Rollback
Revert the commit. Pending `ReturnPending` entries already queued still arrive (phase 14 code handles them). Followers fall back to phase 14 behaviour, so stranded ones need dismissing or `vfh_kill_hirelings`.

## Manual return home (agreed 2026-10-04)
Besides the automatic cases above, the owner can send a follower home on purpose: **right click with the Command Stone on your follower in the field** (phase 13's release input) starts the same return trip as an orphan: it despawns, and after the distance-based timer respawns at its board with its cargo, delivers it and resumes work. Logged as `follow.sent_home`.

## As built
Like phase 14, a follower keeps its own world object: no snapshots, no `ReturnHome`/`QueueReturn` roster ops, no cargo crates, no duplication guards (there's nothing to duplicate).
- **Heading home** (`Followers/HomeReturn.cs`, server side): the ZDO turns `Returning` (new `HirelingMode` 5), stops being a follower (owner 0, its stone slot frees), gets `vfh_return_at` = now + `ReturnSeconds(distance to the board, ReturnSecondsPer100m, ReturnMinSeconds, ReturnMaxSeconds)`, `vfh_deliver_pending`, and is moved a few metres from its board with its ZDO owner cleared, so a client near the board takes it over. Every client hides it (as for ship passengers) and it can't be hurt. When its time comes, whoever simulates it sets it on the ground there as a worker; it delivers its cargo and goes back to work. With nobody near the board it appears as soon as someone is. The contract stays Active throughout (it still counts and is paid for); the Roster tab shows "Returning home (m:ss)".
- **Lost followers** (`Net/OrphanMonitor.cs`, server, every 5 s, `OrphanRules.Check`): following and more than `OrphanDistance` (60 m) from its owner for `OrphanDistanceSeconds` (30 s); in Stay or Gather Here with its owner more than `OrphanStayDistance` (150 m) away for `OrphanStaySeconds` (120 s); owner offline for 30 s. Distances are along the ground, so a dungeon doesn't count as far. Inside its board's area a follower never heads home this way, and passengers stay aboard while their owner is online. The owner gets "… lost track of you and is heading home (about n min)". The phase 12 logout rule stays as the first step (home: back to work; elsewhere: Stay), and the offline rule sends the rest home 30 s later.
- **Send home by hand:** right click your follower with the stone. At home it goes back to work (as before); in the field it heads home ("… is heading home (about n min)"), via the server (`FollowerServer.Kind.SendHome`).
- Not built: the stuck rule (the phase 13 catch-up teleport unsticks followers), the owner-death rule (respawning far away trips the distance rules), the board-gone cargo crate (a returning hireling whose board is gone leaves through the existing board-gone path when it's next loaded), and the logout grace kept in memory only.
- Tests: `vfh_t_home1` (send home, comes back working); orphan rows by hand (VFH-HOME-2, VFH-ORPHAN-1..3).

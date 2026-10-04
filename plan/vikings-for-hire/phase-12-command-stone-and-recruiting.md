# Phase 12 — Command Stone, recruiting followers & basic follow

**Depends on:** 02, 03, 04, 05, 06, 07, 08, 11 · **Enables:** 13 (field orders), 14 (portals & ships), 15 (orphan return)

## Goal
Start milestone 2. Add the **Command Stone**, a 4-quality item crafted and upgraded at the Workbench. Each quality needs a hiring board of a high enough level within range of that workbench, plus its own biome mats. With the stone in hand, you aim at one of your board's hirelings and press the primary attack to **recruit** it as a follower, up to the stone's cap. Its base contract pauses and it follows you, fighting by its stance. Aiming at your follower inside its home board's work radius **releases** it back to work: it deposits its cargo per item type, then resumes its job.

## Files touched
- `src/VikingsForHire/Followers/CommandStoneItem.cs`: registers `VFH_CommandStone` with Jotunn `ItemManager` (cloned from `Crystal`), plus the recipe and the per-quality requirement patch.
- `src/VikingsForHire/Followers/StoneCraftingGate.cs`: patches enforcing the board-level requirement for each quality.
- `src/VikingsForHire/Followers/StoneInput.cs`: intercepts the primary attack while the stone is equipped and resolves the look target.
- `src/VikingsForHire/Followers/FollowerOps.cs`: the `Recruit`, `ReleaseToWork` and `ReleaseAllToWork` ops (named differently from the roster's `Dismiss`, which ends a contract) (on top of phase 06 `MutationService`), plus the server-side cap check.
- `src/VikingsForHire/Followers/FollowBehaviour.cs`: priority 400 when `vfh_mode == Following`. Follow mode `Follow`: wraps vanilla `MonsterAI.SetFollowTarget`/`Follow` (stop at 3 m, run when more than 10 m away). Follow mode `Stay`: hold the position stored in the new ZDO key `vfh_stay_pos` (Vector3), walking back if pushed more than 3 m away. Phase 13 adds `GatherNearby`.
- `src/VikingsForHire/Followers/OwnerSession.cs`: client-side list of the local player's followers, used by the HUD and the cap display.
- `src/VikingsForHire/Net/FollowerSessionServer.cs`: server-side owner-logout handling (step 8).
- `src/VikingsForHire/Hirelings/HirelingAI.cs`: job/gather behaviours only run in `Working` mode. `FollowBehaviour` runs in `Following`. Sets `Context.LeashCenter` (phase 07) to the owner while following with follow mode `Follow`, or to `vfh_stay_pos` while in `Stay`. The leash distance is 30 m.
- `src/VikingsForHire/UI/RosterTab.cs`: shows "Following <player name>" for followers.
- `src/VikingsForHire/Localization/English.json`: strings.

- `src/VikingsForHire/Testing/FixturesFollow.cs`: this phase's fixtures and checks (see the test plan).
- `test/alias_vfh.yaml`: adds this phase's row macros.

## Steps
1. **Item:** clone `Crystal` as `VFH_CommandStone`: `m_itemType = OneHandedWeapon`, every damage field 0, `m_maxQuality = 4`, `m_weight = 1`, `m_teleportable = true`, `m_maxDurability` with `m_useDurability = false`, `m_attachOverride = ItemType.Hands` so it's held. Name `$vfh_command_stone`, with a description listing the follower cap per quality. Icon: the Crystal icon tinted with Jotunn `RenderManager` gold.
2. **Recipe:** Jotunn `CustomRecipe` at `piece_workbench`, `m_minStationLevel = 1`. The requirement list is the union of every quality's mats from YAML. A prefix on `Piece.Requirement.GetAmount(int qualityLevel)` (only for requirements belonging to this recipe, matched by reference held in `CommandStoneItem`) returns the YAML amount for that item at that quality, with 0 when unused. Vanilla UI hides 0-amount rows. Data reloads rebuild the requirement list.
3. **Board-level gate** (`StoneCraftingGate`): postfix on `Player.HaveRequirements(Recipe, bool discover, int qualityLevel, int amount)` and on `InventoryGui.SetupRequirementList` for the stone recipe. Find the current crafting station (`Player.GetCurrentCraftingStation()`). Require a loaded `HiringBoard` within `StoneBoardSearchRadius` (30 m) of it with level ≥ `RequiredBoardLevelForStone(quality)`. If that fails, crafting is disabled and the description area shows `$vfh_stone_needs_board` ("Needs a Level 4 Hiring Board within 30m of this workbench").
4. **Primary-attack interception** (`StoneInput`): prefix on `Humanoid.StartAttack` for the local player when the right-hand item is `VFH_CommandStone`. Return false (no swing). Raycast from the camera (`GameCamera` forward, 50 m, layer mask `Character | piece | terrain | static_solid | Default`). For this phase:
   - Hit a hireling of a board the player has ward access to, with mode `Working`/`Idle` → `Recruit`.
   - Hit your own follower while it's inside its home board's work radius → `ReleaseToWork`.
   - **Shift +** primary attack anywhere → `ReleaseAllToWork` for your followers inside their home radius.
   - Anything else → no-op here (phase 13 adds context orders).
5. **Recruit op** (always sent to the server: `Recruit`, `ReleaseToWork` and `ReleaseAllToWork` are submitted with `MutationService.Submit(..., allowLocal: false)`, which skips the phase 06 local-owner fast path so the server's cap check can't be bypassed): the server counts hirelings in its index with `vfh_owner == playerId` and compares that to `StoneFollowerCap(quality)`. The client sends the equipped stone's quality, and the server re-checks it against the YAML. If it's below the cap, the server mutates the hireling ZDO: `vfh_mode = Following`, `vfh_owner = playerId`, `vfh_owner_name = name`, `vfh_follow_mode = FollowMode.Follow` (keys reserved in phase 05, enum from phase 02). The roster entry state stays `Active` (followers still count against the board cap and still pay upkeep through the board). Result message: "Ragnar is following you (1/2)" or "Follower limit reached (2/2)".
6. **FollowBehaviour:** `SetFollowTarget(owner's Player GameObject)` found via `Player.GetPlayer(ownerId)`. If the owner isn't loaded on this client, the hireling's ZDO ownership is moved to the owner's peer (`zdo.SetOwner(ownerPeerUid)`, done by the server when the follower is recruited and every 10 s by the server for followers whose owner is online). That way followers always simulate on the owner's machine, which keeps following responsive and portal handling local.
7. **ReleaseToWork:** the server sets `vfh_mode = Working`, `vfh_owner = 0`, `vfh_deliver_pending = true`. Phase 08's `DeliverBehaviour` (registered for every job) empties the cargo first because of that flag, and then the job resumes. Miner/woodcutter cargo from the field goes into matching chests per item type, or the drop pile.
8. **Owner logout** (until phase 15 replaces this for the non-base case): a server-side prefix on `ZNet.Disconnect(ZNetPeer)` (the peer's player id is still readable there) does this for each follower of that player: inside its home radius → `ReleaseToWork`; elsewhere → follow mode `Stay` with `vfh_stay_pos` = its current ZDO position.
9. **Board ops interplay:** ending (roster `Dismiss`) or voiding a contract of a follower makes it leave from wherever it is (phase 06 LeaveBehaviour, which drops cargo where it stands when it's away from home).

## Build gate
- `dotnet build -c Release` and `dotnet test` pass.

## Test plan
- **Test macros** (phase 02 harness): `FixturesFollow.cs`. Fixtures: `stone <quality>` (adds a Command Stone of that quality to the inventory), `recruit_nearest` (issues the real `Recruit` op on the nearest hireling), `release_nearest`. Checks: `followers` (count for the local player, server-forwarded), `hireling <h> vfh_mode|vfh_follow_mode|vfh_owner`, `craftable <recipe> <quality>`. Aliases added to `test/alias_vfh.yaml`: `vfh_t_follow1` (logout rule, split a/b around the relog), `vfh_t_follow2` (two players' caps), plus single-player `vfh_t_stone1` (quality gating vs board level) and `vfh_t_recruit1` (cap, release, cargo delivered). Each row in `docs/test-checklist.md` names its alias, and a row passes when its `evt=test.result` line says `pass=true`.
- With an L1 board, the stone recipe shows as unavailable with the board message. Upgrade to L2: Q1 is craftable with Q1 mats only. Upgrading to Q2 needs L4 and the Q2 mats only (verify the shown amounts match the YAML for each quality).
- Recruit with a Q1 stone: one follower follows. A second recruit is refused (1/1).
- The follower fights by its stance while following (Defensive guard defends you within 30 m).
- Walk back into the home radius and release it to work: it deposits field cargo per type and resumes woodcutting.
- Owner logout far from base (dedicated, row **VFH-FOLLOW-1**): the follower holds its position in follow mode `Stay`. Inside base it returns to work.
- Two players with stones: each one's cap is separate (row **VFH-FOLLOW-2**).
- **Logs:** every scenario above is run with `vfh_debug All on`. Afterwards `VikingsForHire.log` must show the expected events with their ids (Info for state changes, Debug for AI switches, plans and ops), and no `lvl=E` lines. Any new code path in this phase logs through `VfhLog` per the phase 02 conventions, and guarded ticks and patches use `VfhLog.Guard`.

## Commit
`feat(followers): command stone with board-gated qualities, recruiting, releasing to work and following`

## Rollback
Revert the commit. Hireling ZDOs left in `Following` behave like idle in phase 11 code, because unknown modes fall back to idle. Release followers to work before reverting in worlds you keep. Crafted stones become unknown items, which vanilla removes from inventories with a log warning.

## As built
- The stone is a clone of the Club (a one-handed item with a hand attachment), wearing the Crystal's mesh and icon. Its recipe lists every material any quality uses; `Piece.Requirement.GetAmount` returns the data file's amount for the quality being made (0 hides the row) and the list is rebuilt when the data changes.
- Recruit / release go through a dedicated server RPC (`VFH_FollowerOp`, `Net/FollowerServer.cs`) rather than `MutationService` with `allowLocal: false`: the server checks the cap, then writes the hireling through `MutationService` and replies with the message. `OwnerSession` wasn't needed: counts come from the server.
- Aiming at your own follower **away** from home toggles it between Follow and Stay (a small addition so a follower left in Stay, e.g. after a logout, can be picked up again before phase 13's hotkeys).
- Every spawn (arrival or respawn) clears the follower fields, so nobody comes back as someone's follower.
- The crafting gate looks for the board near the station you're using (the game only tracks that while its crafting menu is open); the `craftable` test check uses the nearest workbench.
- Keeping up (`FollowCatchUp`): a follower sprints whenever its owner sprints or it's more than 5 m behind, gets a sprint bonus (`FollowerCatchUpSpeedBonus`, +25%) beyond 15 m, jumps or detours sideways when it hasn't moved 1 m in 2 s, and as the safety net teleports to solid ground just behind the owner when it's more than `FollowerCatchUpTeleportDistance` (40 m) behind or stuck for `FollowerStuckTeleportSeconds` (5 s), only while the owner can't see it and is on foot on land. Followers have no stamina: their sprint is unlimited.

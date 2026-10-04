# Phase 13 — Field orders: context commands, follow modes & stances

**Depends on:** 02, 05, 06, 07, 08, 09, 12 · **Enables:** 14, 15, 16

## Goal
Make followers useful in the field without micromanagement. With the Command Stone in hand, the primary attack on a target gives a **context order**: rock/ore → miners mine it, tree/log → woodcutters chop it, enemy → guards attack it, ground → followers move there and hold. A hotkey cycles the **follow mode** (Follow / Stay / Gather Nearby) and another cycles the **stance** of all your followers within 30 m. In **Gather Nearby**, followers harvest on their own by job around you and fill their cargo. Each follower's cargo can be opened (E) to load your own loot onto them.

## Files touched
- `src/VikingsForHire/Followers/StoneInput.cs`: adds context-order resolution and the two hotkeys (Jotunn `ButtonConfig`s `CycleFollowModeKey`, default G, and `CycleStanceKey`, default H, plus gamepad alternatives `JoystickButton` DPad-Left/DPad-Right only while the stone is equipped).
- `src/VikingsForHire/Followers/FieldOrders.cs`: order model and dispatch. Orders are sent as hireling ZDO mutations through phase 06 `MutationService`. They're cheap because followers are owned by the owner's client (phase 12), so `MutationService`'s local-owner fast path (phase 06) applies them with no network round trip.
- `src/VikingsForHire/Followers/FollowModes.cs`: helpers for cycling `vfh_follow_mode` (`Core.FollowMode`, phase 02) and encoding `vfh_order` (byte[]: order type + target ZDOID + position + expiry; key reserved in phase 05).
- `src/VikingsForHire/Followers/OrderBehaviour.cs`: priority 500, executes the current explicit order: `Harvest(target)` reusing the phase 08/09 profiles for one target, `Attack(target)` reusing the phase 07 Engage logic, `MoveHold(position)`.
- `src/VikingsForHire/Followers/FieldGatherBehaviour.cs`: priority 350 in GatherNearby mode. Runs `GatherBehaviour` with center = owner position and radius = `GatherNearbyRadius` (15 m), and no delivery (cargo just fills). When cargo is full it switches to Follow and shows "Cargo full" over the hireling and in the owner's top-left message. In GatherNearby, `FollowBehaviour` (priority 400) only `Wants()` control when the follower is more than 15 m from the owner or `FieldGatherBehaviour` has no target. Otherwise it yields so the gathering runs.
- `src/VikingsForHire/Followers/FollowerHud.cs`: small top-left list of the local player's followers when the stone is equipped: name, job, mode, stance, cargo `used/slots`, health bar. Built with Jotunn `GUIManager`.
- `src/VikingsForHire/Hirelings/Hireling.cs`: cargo interaction allowed for the follower's owner. Hover adds the mode/stance.
- `src/VikingsForHire/Localization/English.json`: strings.

- `src/VikingsForHire/Testing/FixturesOrders.cs`: this phase's fixtures and checks (see the test plan).
- `test/alias_vfh.yaml`: adds this phase's row macros.

## Steps
1. **Context resolution** (primary attack with the stone, after phase 12's recruit/release checks fail):
   - Hit collider has `MineRock`/`MineRock5`/a pickaxe-type `Destructible` → order `Harvest` to every follower within 30 m whose job is Miner and whose tool tier is high enough. Others ignore it. If none qualify, show "No miner can mine that".
   - `TreeBase`/`TreeLog`/a tree `Destructible` → order `Harvest` to woodcutters (same rules).
   - `Character` where `BaseAI.IsEnemy(player, target)` → order `Attack` to every guard follower within 30 m (workers ignore it and keep their stance behaviour).
   - Terrain/static ground → `MoveHold(point)` to all followers within 30 m. They spread out in a 2 m ring around the point, then follow mode becomes `Stay` with `vfh_stay_pos` set to the follower's ring slot.
   - Feedback: a local-only marker at the target (the vanilla map-ping VFX prefab instantiated on this client for 2 s, never networked) and a centre message "2 followers: Mine".
2. **Order lifecycle:** an order lasts until done (target destroyed or dead, position reached) or 120 s. Harvest orders also collect that target's drops into cargo. After an order the follower goes back to its follow mode (`MoveHold` leaves it in `Stay`).
3. **Follow mode hotkey (G):** cycles Follow → Stay → Gather Nearby for all your followers within 30 m. Centre message shows the new mode. `Stay` sets `vfh_stay_pos` to the current position and holds there (phase 12), defending by stance.
4. **Stance hotkey (H):** cycles stances for all followers within 30 m. Guards cycle Passive → Defensive → Aggressive, and workers cycle Flee ↔ Defend, each within its own allowed set. This updates the hireling ZDO's `vfh_stance` and its roster entry (via `MutationService` to the board), so the base contract keeps the new stance.
5. **Cargo access:** E on your own follower opens its cargo (`Container.Interact`). Other players can't open it (`$vfh_not_your_follower`). Non-teleportable items are allowed in cargo (portal rules apply in phase 14).
6. **No micromanagement in fights:** combat (priority 900/950) still preempts orders and modes. An explicit `Attack` order only sets the guard's preferred target. Stances continue to decide when to engage anything else.
7. **Gamepad:** with the stone equipped, DPad-Left = follow mode, DPad-Right = stance, and primary attack works as usual for context orders.

## Build gate
- `dotnet build -c Release` and `dotnet test` pass.

## Test plan
- **Test macros** (phase 02 harness): `FixturesOrders.cs`. Fixtures: `order <harvest|attack|move> <target-tag|x,z>` (issues the real order code path without the raycast). Checks: `hireling <h> order`, `hireling <h> cargo.<item>`, `hireling <h> stance`. Aliases added to `test/alias_vfh.yaml`: `vfh_t_follow3` (cargo access), `vfh_t_follow4` (raid while following), plus single-player `vfh_t_orders1` (mine-order fills cargo, guard ignores it) and `vfh_t_modes1` (mode and stance cycling). Each row in `docs/test-checklist.md` names its alias, and a row passes when its `evt=test.result` line says `pass=true`.
- Bring a miner (L3) and a melee guard (L3) to a Black Forest copper deposit with a Q2 stone:
  - Aim at the deposit and attack: the miner mines it to completion and the cargo fills with CopperOre/Stone. The guard ignores the order.
  - Aim at a Greydwarf: only the guard attacks it.
  - Aim at the ground 15 m away: both move there and Stay.
  - G → Gather Nearby: the miner mines deposits within 15 m of you on its own. When cargo is full it reverts to Follow with a message.
  - H: the guard cycles through three stances and the miner between two. Back at base, the roster shows the updated stance.
- Load 10 Wood into the follower's cargo with E. A second player can't open it (row **VFH-FOLLOW-3**).
- Raid while following: everyone acts by stance and nothing needs pressing (row **VFH-FOLLOW-4**).
- **Logs:** every scenario above is run with `vfh_debug All on`. Afterwards `VikingsForHire.log` must show the expected events with their ids (Info for state changes, Debug for AI switches, plans and ops), and no `lvl=E` lines. Any new code path in this phase logs through `VfhLog` per the phase 02 conventions, and guarded ticks and patches use `VfhLog.Guard`.

## Commit
`feat(followers): context orders, follow modes, stance cycling, field gathering and follower HUD`

## Rollback
Revert the commit. Followers fall back to phase 12 behaviour (basic follow only). The extra ZDO keys are ignored.

## Changes agreed 2026-10-04 (after playing with phase 12)
- **Recall without aiming:** with the stone in hand, the **secondary attack (right click)** sets every one of your followers within 50 m back to `Follow`, wherever you're looking. Mid-fight you can't aim at a follower, so this is the panic button. The stone has no block, so the button is free.
- **Gather Here replaces Gather Nearby.** The third follow mode makes gatherers (woodcutters, miners) work within `GatherNearbyRadius` (15 m) of the spot where you set the mode (stored as `vfh_stay_pos`), not around you. Non-gatherers in this mode just hold the spot like `Stay`. When cargo is full they stop and wait at the spot ("Cargo full" over the hireling and a message to the owner) until recalled or sent home. `FieldGatherBehaviour` uses the stay position as its centre.
- **G** cycles Follow → Stay → Gather Here for followers within 30 m; aiming the stone at your follower away from home keeps toggling Follow/Stay (phase 12).

## Stone controls and guard posts (agreed 2026-10-04, replaces the recall/Shift notes above where they differ)

| Input | Aiming at | Result |
|---|---|---|
| Left click | a board hireling | Recruit: it follows you (uses a stone slot) |
| Left click | your follower inside its board's work radius | Guards: **post it here** (below). Workers: back to work |
| Left click | your follower in the field | Toggle Follow / Stay |
| Right click | your follower or a posted guard | **Release**: at home back to patrol/job (a posted guard loses its post); in the field the return-home trip (phase 15) |
| Right click | nothing (no hireling under the crosshair) | **Recall**: every follower of yours within 50 m switches to Follow |
| G / H | — | Cycle follow mode (Follow / Stay / Gather Here) / stance of followers within 30 m |

Shift + attack (phase 12's release-all) is retired in favour of right click.

**Guard posts.** Posting a guard sets the contract's post (position + facing = the player's facing at the time): stored on the roster entry (`ContractEntry.Post`, so a respawned guard returns to it) and on the hireling ZDO (`vfh_post`, `vfh_post_dir`). The hireling leaves follower mode (mode `Working`, owner 0), so it no longer counts against the stone's cap; it still counts against the board's cap and pays upkeep. A posted guard runs `PostBehaviour` (priority 100, replacing `GuardPatrolBehaviour` for that hireling): walk to the post, stand facing the post direction, engage by stance with `LeashCenter` = post and radius = `PostLeashRadius` (config, default 20 m), and walk back after the fight. Archers shoot from the post and only leave it for the sidearm when something is within 6 m. A post the pathfinder can't reach is still stored; the guard stands as close as it can get and logs `post.unreachable`. Only guards can be posted; left-clicking a worker follower at home sends it back to work. The Roster tab shows "Posted" for posted guards, with a button to clear the post.

## No hotkeys: Shift+E command panel (agreed 2026-10-04, replaces G/H everywhere above)
Other mods often use G and H, so there are no new hotkeys (`CycleFollowModeKey` / `CycleStanceKey` and the gamepad D-pad bindings are dropped).
- **E** on your own follower opens its cargo (step 5).
- **Shift+E** (Valheim's alternate interact) on one of your hirelings (a follower, a posted guard, or a hireling of a board you have ward access to) opens a small command panel (Jotunn `GUIManager`, built like the board panel): follow mode buttons Follow / Stay / Gather Here (followers only), stance buttons for the hireling's allowed stances (saved to the roster entry too), **Release** (back to work, or the return trip in the field) and **Clear post** (posted guards), plus an "Apply to all my followers nearby" toggle (followers within 30 m). Hover text shows "[E] Cargo  [Shift+E] Orders".
- Left/right click with the stone stay as in the control table above; right click on nothing is still the recall.

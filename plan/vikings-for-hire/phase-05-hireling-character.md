# Phase 05 — Hireling character: prefab, appearance, gear, stats & persistence

**Depends on:** 02, 03 · **Enables:** 06 (contracts spawn hirelings), 07–10 (AI behaviours), 12–15 (followers, snapshots)

## Goal
Create the `VFH_Hireling` networked character. It looks like a randomly generated player viking, wears cosmetic gear for its level and job, has stats from the level tables, carries a cargo inventory, persists through zone unload and restart, and can be snapshotted to bytes and rebuilt exactly. That last part is what delayed arrival, death-respawn, portals, ships and return-home all rely on. Its AI in this phase is a minimal "idle near home point" loop. Jobs and combat come later. Hirelings are spawned with a debug command.

## Files touched
- `src/VikingsForHire/Hirelings/HirelingPrefab.cs`: builds `VFH_Hireling` from a clone of `Player`.
- `src/VikingsForHire/Hirelings/Hireling.cs`: main component. Holds identity, typed ZDO accessors, hover/interact, stats application and events.
- `src/VikingsForHire/Hirelings/HirelingZdo.cs`: ZDO key constants.
- `src/VikingsForHire/Hirelings/HirelingAI.cs`: `class HirelingAI : MonsterAI`. Overrides `UpdateAI` with a pluggable behaviour stack (`IHirelingBehaviour`, sorted by priority). Phase 05 ships `IdleBehaviour` only.
- `src/VikingsForHire/Hirelings/IHirelingBehaviour.cs`: `int Priority; bool Wants(HirelingAI ai); void Tick(HirelingAI ai, float dt);`.
- `src/VikingsForHire/Hirelings/Appearance.cs`: random appearance generation and application.
- `src/VikingsForHire/Hirelings/GearApplier.cs`: equips the level/job gear as non-droppable `ItemData`.
- `src/VikingsForHire/Hirelings/HirelingSnapshot.cs`: serializes and rebuilds (ZPackage) **every** custom ZDO data entry of the hireling **generically**. It copies all float, int, long, string, Vector3, Quaternion and byte[] key-hash/value pairs from `ZDOExtraData` (which includes the cargo `items` key and current health), so keys added by later phases are carried through transit, stowing and return-home without changing the snapshot code.
- `src/VikingsForHire/Core/HirelingRecord.cs`: Unity-free typed view of the logical fields that server-side code reads from a snapshot (hid, board id, job, level, stance, radius, home, mode, owner). It has a format-version byte (1) and a tested `Write(IPackageWriter)`/`Read(IPackageReader)` over a thin interface. `HirelingSnapshot` stores the record header followed by the generic key/value block.
- `src/VikingsForHire/Hirelings/DamagePatches.cs`: damage rules (below).
- `src/VikingsForHire/Commands/DebugCommands.cs`: extends `vfh_dump_state` with every loaded hireling (all `vfh_*` ZDO fields, owner peer, current behaviour, target, cargo summary) and adds `vfh_spawn <job> <level>`, `vfh_snapshot_test` (snapshots the nearest hireling, destroys it, rebuilds it 3 m away and logs a field-by-field comparison) and `vfh_kill_hirelings`.
- `tests/VikingsForHire.Tests/HirelingRecordTests.cs`: round-trip and version tests.

- `src/VikingsForHire/Testing/FixturesHireling.cs`: this phase's fixtures and checks (see the test plan).
- `test/alias_vfh.yaml`: adds this phase's row macros.

## Steps
1. **Prefab** (`PrefabManager.OnVanillaPrefabsAvailable`): `PrefabManager.Instance.CreateClonedPrefab("VFH_Hireling", "Player")`, then:
   - Remove `Player`, `PlayerController`, `Talker`, `Skills`, `PlayerCustomizaton`-related components, the audio listener and any camera child. Keep the `VisEquipment`, `Animator`, `ZSyncAnimation`, `ZSyncTransform`, `Rigidbody`, `CapsuleCollider`, `FootStep` and `CharacterAnimEvent`.
   - Add `Humanoid` with the values copied from the removed `Player`'s `Character` fields (`m_walkSpeed`, `m_runSpeed`, `m_jumpForce`, `m_hitEffects`, `m_critHitEffects`, `m_deathEffects`, `m_eye`), `m_faction = Character.Faction.Players`, `m_name = "$vfh_hireling"`, `m_group = "VFH_Hireling"`, `m_boss = false`, `m_tolerateWater = true`. Set `m_defaultItems` to empty.
   - Add `HirelingAI` (`m_viewRange 30`, `m_viewAngle 90`, `m_hearRange 20`, `m_pathAgentType = Pathfinding.AgentType.Humanoid`, `m_randomMoveRange 5`, `m_moveMinAngle 10`, `m_avoidFire true`, `m_afraidOfFire false`, `m_avoidWater true`, `m_jumpInterval 0`, `m_circulateWhileCharging false`).
   - Add a `Container` for cargo (`m_name = "$vfh_hireling_cargo"`, width 8, height 4, so 32 slots, the max level's slots). Usable slots are limited per level by the `CargoGate` described below. `m_privacy = Public`, `m_checkGuardStone = false`. Add `Hireling` and set `ZNetView.m_persistent = true`, `m_type = ZDO.ObjectType.Default`.
   - Register the prefab with Jotunn so it's in `ZNetScene`.
2. **ZDO keys** (`HirelingZdo`): `vfh_hid` (string GUID), `vfh_board_id`, `vfh_job` (int), `vfh_level` (int), `vfh_stance` (int), `vfh_mode` (int, `Core.HirelingMode`; any mode with no registered behaviour in the current build falls back to `IdleBehaviour`), `vfh_follow_mode` (int, `Core.FollowMode`, used from phase 12), `vfh_owner_name` (string, phase 12), `vfh_radius` (float, phase 06), `vfh_leaving_since` (long: whole seconds of `ZNet.GetTimeSeconds()`, phase 06), `vfh_order` (byte[], phase 13), `vfh_deliver_pending` (bool, phase 08), `vfh_owner` (long player id, 0 for base workers), `vfh_name` (string), `vfh_model` (int 0/1), `vfh_hair`, `vfh_beard` (strings), `vfh_skin` (Vector3), `vfh_hair_color` (Vector3), `vfh_home` (Vector3: the board position, cached for unloaded boards), `vfh_status` (string token for the hover status, e.g. `$vfh_status_storage_full`). Cargo uses vanilla `Container` ZDO storage (`items`).
3. **Appearance** (`Appearance.Generate(System.Random)`): model 0/1 at 50/50. Hair and beard are chosen from `ObjectDB.m_items` where `ItemType == Customization`, names `Hair*` and `Beard*` respectively, with beards only for model 0 plus `BeardNone`. Skin colour is a lerp between the vanilla `PlayerCustomizaton` skin min and max. Hair colour is a random HSV in vanilla's hair range. Name is picked from the gender-matched list. `Apply()` calls `VisEquipment.SetModel`, `SetHairItem`, `SetBeardItem`, `SetSkinColor`, `SetHairColor` on every client from ZDO values in `Awake`, and again when the ZDO revision changes.
4. **Gear** (`GearApplier`): build `ItemDrop.ItemData` instances from the level/job gear prefabs (`ObjectDB.GetItemPrefab(name).GetComponent<ItemDrop>().m_itemData.Clone()`), add them to the Humanoid's **own** inventory (separate from the cargo `Container`) and equip them (`EquipItem`). Rules:
   - Gear items get `m_crafterName = "$vfh_hireling"` and a custom data flag `vfh_gear=1` (`ItemData.m_customData`).
   - The prefab has no `CharacterDrop` component, and vanilla only drops a non-player `Humanoid`'s inventory through `CharacterDrop`, so gear is never dropped or lootable. Cargo lives in the separate `Container` and is dropped explicitly by the death handler (phase 06).
   - Durability is never consumed (prefix on `Humanoid.DrainEquipedItemDurability` returns early for hirelings).
   - Ranged guards get infinite ammo: the configured arrow is equipped as ammo with stack 100, refilled to 100 whenever it drops below 20.
5. **Stats** (`Hireling.ApplyLevelStats()`): `SetMaxHealth(levelTable.health)`. On first spawn health starts full, and afterwards it's kept from the ZDO. Armor is applied in `DamagePatches`.
5b. **Compat exclusion:** add `VFH_Hireling` to `ExcludedContainers` (phase 03), so AzuAutoStore never stores into hireling cargo, and AzuCraftyBoxes and PullMats never pull from it. Tests: rows **VFH-AZU-4** (drop Wood next to a hireling with Azu's ground pickup on: it never lands in the cargo) and **VFH-CRAFTY-2** (Wood only in a hireling's cargo: neither crafting nor a PullMats pull uses it).
6. **Cargo gate:** a prefix on `Inventory.CanAddItem`, and on `InventoryGui` moves into the container, limits the cargo inventory to the first `levelTable.cargoSlots` slots (in grid order). Slots beyond that are visually greyed with an overlay image added by a postfix on `InventoryGrid.UpdateGui` when the open container belongs to a hireling.
7. **Damage rules** (`DamagePatches`, a prefix on `Character.RPC_Damage`, which runs on the target's owner):
   - Target is a hireling, attacker is a player, and `FriendlyFireOnHirelings == false` → damage set to zero.
   - Target is a hireling → multiply incoming damage with vanilla `HitData.ApplyArmor(levelTable.armor)`.
   - Attacker is a hireling and the target is a `Character` → scale damage by `guardDamageMult` (guards) or `guardDamageMult × workerCombatFactor` (workers).
   - Attacker is a hireling and the target is a player `Piece` (`WearNTear.Damage` prefix) → damage set to zero. Hirelings never damage buildings.
   - Hireling vs hireling and hireling vs tamed creatures → damage set to zero (same faction, but this guards against AoE).
8. **Interaction:** hover text shows `Name — Job (Lv N)`, status token, health, and "[E] Cargo". E opens the cargo container if the player passes the ward check at the hireling's position (base workers) or is the follower's owner (followers, wired up in phase 13).
9. **IdleBehaviour:** stays within 6 m of `vfh_home` with random moves (`MonsterAI.IdleMovement`). When it's more than 10 m away, it walks back with `MoveTo`.
10. **Snapshot** (`HirelingSnapshot.Capture(Hireling)`, `HirelingSnapshot.FromZdo(ZDO)` and `HirelingSnapshot.Create(HirelingRecord record, IDictionary<string, object> extraFields)` → `byte[]`. `Create` builds a snapshot from field values for a hireling that doesn't exist yet, as a new contract does in phase 06. `HirelingSnapshot.Spawn(byte[], Vector3, Quaternion)` sets `ZNetView.m_initZDO` to a new ZDO pre-filled with every stored key/value, then instantiates `VFH_Hireling`, the vanilla pattern used by `ZNetScene.CreateObject`). `FromZdo` works on a raw ZDO without an instance, so the server can snapshot unloaded hirelings (phase 15).
11. **Debug spawn:** `vfh_spawn Miner 4` spawns at the crosshair, linked to the nearest board (`vfh_board_id`, `vfh_home`) with mode `Idle` and default stance.

## Build gate
- `dotnet build -c Release` and `dotnet test` pass (including `HirelingRecordTests`).

## Test plan
- **Test macros** (phase 02 harness): `FixturesHireling.cs`. Fixtures: `hirelings <job> <level> <n>` (wraps `vfh_spawn`). Checks: `hireling_count [job]`, `hireling <nearest|short-hid> <field>` (any `vfh_*` key, or `health`, `gear.<slot>`, `cargo.<item>`, `cargo_slots`), `snapshot_roundtrip` (true when every field matches after rebuild). Aliases added to `test/alias_vfh.yaml`: `vfh_t_hire1` (persistence, split a before relog / b after), `vfh_t_hire2` (run on the second client: appearance fields equal the first client's logged values). Each row in `docs/test-checklist.md` names its alias, and a row passes when its `evt=test.result` line says `pass=true`.
- Spawn 10 hirelings of mixed jobs and levels: varied genders, hair, beards and colours. Gear matches the tables (e.g. Miner L4 = iron pickaxe and iron armour). Names are gender-matched.
- Hit a hireling with a sword: no damage with friendly fire off, damage with it on.
- Spawn a Greyling next to an L1 hireling: the Greyling attacks it and it takes damage reduced by armor. (It doesn't fight back yet; combat is phase 07.)
- Kill a hireling (`vfh_kill_hirelings`): nothing drops. Gear is never droppable, and cargo dropping on death arrives in phase 06.
- Put 20 stacks into an L1 hireling's cargo: only 8 slots are usable.
- `vfh_snapshot_test`: every field matches after rebuild, including the cargo contents and health.
- Persistence: spawn 3, log out, log in, then restart the dedicated server. Same names, looks, gear and cargo (row **VFH-HIRE-1**).
- Multiplayer: a second client sees identical appearance and gear (row **VFH-HIRE-2**).
- **Logs:** every scenario above is run with `vfh_debug All on`. Afterwards `VikingsForHire.log` must show the expected events with their ids (Info for state changes, Debug for AI switches, plans and ops), and no `lvl=E` lines. Any new code path in this phase logs through `VfhLog` per the phase 02 conventions, and guarded ticks and patches use `VfhLog.Guard`.

## Commit
`feat(hireling): networked player-look hireling with random appearance, level gear, stats and snapshots`

## Rollback
Revert the commit, then run `vfh_kill_hirelings` before reverting in any world you want to keep. Otherwise leftover `VFH_Hireling` ZDOs stay as unknown prefabs, which are harmless and not instantiated.

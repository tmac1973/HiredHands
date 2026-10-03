# In-game test macros

Tests are ServerDevcommands aliases in `test/alias_vfh.yaml` (deployed to `BepInEx/config/alias_vfh.yaml` by every
build and by `scripts/run-dedicated-server.sh`). Run `devcommands` first: the `vfh_test_*`, `vfh_assert*` and
`vfh_fixture` commands are cheats.

## Running a row

Type the row's alias (e.g. `vfh_t_harness1`). Each assert shows ✓ or ✗ mid-screen and the run ends with PASS or FAIL.
The log gets one line per assert and one result line:

```
[VFH] ... cat=Test evt=test.assert row=VFH-HARNESS-1 check="cfg ArrivalDelayMinSeconds == 5" expected=5 actual=5 pass=true waited=0
[VFH] ... cat=Test evt=test.result row=VFH-HARNESS-1 pass=true checks=6 failed=0
```

Rows that need you to do something midway come in `_a` / `_b` parts; `docs/test-checklist.md` says what to do between them.

## Commands

| Command | |
|---|---|
| `vfh_test_begin <ROW>` / `vfh_test_end` | Start / finish a run; the end logs `evt=test.result` |
| `vfh_assert <check> [args] <op> <value>` | Check once. Ops: `== != >= <= > <` (numbers numerically, true/false, text ignoring case) |
| `vfh_assert_eventually <sec> <check> [args] <op> <value>` | Re-check every second until it passes or times out |
| `vfh_fixture <name> [args]` | A setup step; `vfh_fixture list` shows them |
| `vfh_checks` | Lists the checks |
| `vfh_test_summary` / `vfh_test_reset` | Results since login / clear them |

All of these run strictly in order, even though ServerDevcommands fires a chained line at once: an
`assert_eventually` holds back everything after it.

## Checks (phase 02)

| Check | Value |
|---|---|
| `cfg <key>` | Effective config value, test overrides included |
| `data <path>` | Data table value. Names and dictionary keys any case, lists by 1-based position: `data BoardLevels.1.Cost.Wood`, `data Jobs.Miner.CostMult` |
| `data_source` | `local`, `server` or `defaults` |
| `data_reloads` | Times the tables were (re)loaded this session |
| `log_errors` | Error lines logged this session (rows assert `== 0`) |
| `fast_timers` | `true` while test timers are on |

## Fixtures (phase 02)

| Fixture | |
|---|---|
| `fast_timers on\|off` | Arrival 5–10 s, respawn 10 s, orphan and return timers ÷10. On a server it's applied on the server and every client (you must be in `adminlist.txt`). Memory only; cleared on leaving the world |

## Checks (phase 03)

| Check | Value |
|---|---|
| `placement_ok [m=5]` | Can a hiring board go `m` metres ahead: `true`, `false` or `pending` (client waiting for the server) |
| `placement_missing [m=5]` | Unmet requirements, comma separated: `Workbench`, `Bed`, `Pieces`, `BoardTooClose`, `WorldBoardLimit` |
| `board_count` | Hiring boards in the whole world (asked of the server) |
| `boards_near [r=50]` | Loaded hiring boards within `r` metres |
| `board_level` | Level of the nearest board |
| `board_storage food\|coins` | Food points or coins in the nearest board |
| `board_items <item>` | Count of an item in the nearest board |
| `food_points <item>` | Food points one item is worth |
| `board_access` | Whether you pass the ward check at the nearest board |
| `azu_sees_board` / `crafty_sees_board` | Whether any hiring board is in AzuAutoStore's / AzuCraftyBoxes' container list (`absent` when the mod isn't installed) |

## Fixtures (phase 03)

Fixtures build 5–6m straight ahead of you: stand on open ground facing open ground. Everything they spawn is
tagged, and `clear_area` removes it.

| Fixture | |
|---|---|
| `base [radius=12] [floors=40]` | A workbench, a bed and wood floors, all counted as built by you |
| `base_partial workbench\|bed\|pieces` | The same base missing one requirement (`pieces` = only 10 floors) |
| `board_here` | Places a hiring board 5m ahead through the real base check |
| `board_put <item> <n>` | Gives you the items and moves them into the nearest board the way the UI does (the filter applies) |
| `board_force_add <item> <n>` | Puts items in the nearest board, skipping the filter |
| `crafty_probe` | Adds a session-only workbench recipe: Wood from 1 Coins + 1 CookedMeat |
| `clear_area [radius=40]` | Removes everything the fixtures spawned |

## Phase 04

| Check / fixture | |
|---|---|
| check `inv <item>` | How many of an item you carry |
| check `upgrade_last` | Last upgrade outcome: `ok`, `missing`, `max`, `busy`, `conflict`, `timeout`, `no_access` |
| fixture `upgrade_mats <toLevel>` | Adds exactly that level's upgrade cost to your inventory |
| fixture `upgrade` | Upgrades the nearest board through the real request (pays from your inventory) and waits for the answer |
| command `vfh_board_setlevel <1-8>` | Cheat: sets the nearest board's level for free |

## Phase 05

| Check / fixture / command | |
|---|---|
| check `hireling <nearest\|id-prefix> <field>` | `name job level mode stance health maxhealth charlevel tamed faction armor behaviour`, `gear.right\|left\|helmet\|chest\|legs\|ammo`, `cargo.<item>`, `cargo_used`, `cargo_slots`, or any `vfh_` key (with or without the prefix) |
| check `hireling_count [job]` | Loaded hirelings within 50m |
| check `snapshot_roundtrip` | Result of the last snapshot test: `true`, or `false: …` listing differing values |
| fixture `hirelings <job> <level> [n]` | Spawns hirelings 4m ahead, linked to the nearest board |
| fixture `cargo_put <item> <stacks>` | Moves full stacks from you into the nearest hireling's cargo the way the UI does |
| fixture `snapshot_test` / `kill_hirelings [r]` | As the commands below |
| command `vfh_spawn <job> <level> [count]` | Spawns where you look (jobs: Woodcutter, Miner, Smelter, GuardMelee, GuardRanged) |
| command `vfh_snapshot_test` | Snapshot the nearest hireling, destroy it, rebuild it 3m away, compare every value |
| command `vfh_kill_hirelings [r]` | Kills hirelings within `r` (50) metres |

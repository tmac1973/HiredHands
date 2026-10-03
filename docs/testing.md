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

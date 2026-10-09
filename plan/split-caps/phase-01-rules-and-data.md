# Phase 01 — Rules and data

**Depends on:** nothing · **Enables:** 02 (enforcement uses the rules), 03 (the tabs show the counts), 04 (docs)

## Goal
The pure rules and the data behind the split caps, unit tested, with no change yet to what the game does. This phase
adds:
- each job's kind (combat or worker);
- `combatCap` and `workerCap` per board level, and `maxPerBoard` per job, in the data file with the agreed defaults;
- older data files filled in automatically;
- one roster check that answers "may this board post this job?" and says which limit is full.

## Files touched
- `src/VikingsForHire/Core/JobType.cs`:
  - add `IsCombat()`: `IsGuard()` today; the wizard and healer join when they exist;
  - remove `OnePerBoard()` (its only caller is `Roster.JobTaken`, also removed here). Farmer and Cook become
    `maxPerBoard: 1` data.
- `src/VikingsForHire/Core/Data/VfhData.cs`:
  - `BoardLevelData`: new `CombatCap` and `WorkerCap`, each with a `YamlMember` description;
  - `HirelingCap` stays, so older files still load, with the description "Not used since 0.7.0 (combatCap and
    workerCap replace it)";
  - `JobData`: new `MaxPerBoard` (int, 0 = no limit beyond the cap), with a description.
- `src/VikingsForHire/Core/Data/DefaultData.cs`:
  - `Board(...)` takes the combat and worker caps (table below);
  - each job's `MaxPerBoard`: Woodcutter 2, Miner 2, Smelter (Steward) 1, Farmer 1, Cook 1, GuardMelee 0,
    GuardRanged 0.
- `src/VikingsForHire/Core/Data/DataValidator.cs`: `combatCap` and `workerCap` must be 0 or more, and `maxPerBoard`
  0 or more.
- `src/VikingsForHire/Core/LevelRules.cs`:
  - `CombatCap(boardLevel)`, `WorkerCap(boardLevel)`, `Cap(boardLevel, job)` (the cap for that job's kind) and
    `MaxPerBoard(job)`;
  - `HirelingCap(boardLevel)` is removed, and its callers move in phases 02–03.
- `src/VikingsForHire/Core/Roster.cs`:
  - `OpOutcome` gains `CombatCapReached`, `WorkerCapReached` and `JobLimitReached`, appended at the end. `CapReached`
    and `JobTaken` stay in the enum but are unused, with a comment saying since when;
  - new `int KindCount(bool combat)` (every entry of that kind, leaving ones included, as `Count` does today);
  - new `int JobCount(JobType job)` (entries of that job that aren't `Leaving`, as `JobTaken` does today);
  - new `OpOutcome CanPost(JobType job, LevelRules rules, int boardLevel)`;
  - `Post(entry, cap)` becomes `Post(entry, LevelRules rules, int boardLevel)`, using `CanPost`;
  - `JobTaken` and `HasRoom` are removed.
- `tests/VikingsForHire.Tests/LevelRulesTests.cs`, `RosterTests.cs` and `DataDefaultsTests.cs`: updated and extended
  (Test plan).

## Steps
1. **Defaults** in `DefaultData.Board(level, combatCap, workerCap, radius, cost…)`:

   | Level | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
   |---|---|---|---|---|---|---|---|---|
   | combatCap | 1 | 1 | 2 | 2 | 3 | 3 | 4 | 4 |
   | workerCap | 2 | 4 | 6 | 8 | 8 | 8 | 8 | 8 |

   `HirelingCap` keeps its old value (2…10) in the defaults, so the file still shows it, marked unused.
2. **Kinds.** `IsCombat(job) => job.IsGuard()`. Every other job is a worker: `!IsCombat`.
3. **`Roster.CanPost(job, rules, boardLevel)`.** It checks in this order and returns the first that fails:
   1. the kind's cap: `KindCount(job.IsCombat()) >= rules.Cap(boardLevel, job)` gives `CombatCapReached` or
      `WorkerCapReached`;
   2. the job's limit: `rules.MaxPerBoard(job) > 0 && JobCount(job) >= rules.MaxPerBoard(job)` gives
      `JobLimitReached`;
   3. otherwise `Ok`.

   Because the check only ever counts, a roster already over a cap stays as it is and simply refuses new contracts of
   that kind, which is what the overview asks. Nothing else reads the caps.
4. **Older data files.** `DataDefaults.FillMissing` already fills keys missing from list entries (board levels, by
   position) and dictionary records (jobs) from the shipped defaults. A 0.6 file therefore gets `combatCap`,
   `workerCap` and `maxPerBoard` with no new code. A test (below) proves it.
5. **Logging.** None in this phase: these are pure rules. Phase 02 logs the refusals.

## Build gate
- `dotnet build -c Release` fails to compile at the callers of the removed `HirelingCap`, `HasRoom` and `JobTaken`
  (`BoardRosterOps`, `ContractsTab`, `UpgradeTab`, the debug command). So this phase also makes the smallest compile
  fixes there:
  - `BoardRosterOps` calls `roster.CanPost` and maps every refusal to the existing `$vfh_op_cap` message ("This board
    can't take more hirelings: upgrade it for more", no arguments). The outcome is the new `OpOutcome` value. Phase 02
    replaces the message with one for each outcome;
  - `ContractsTab` and `UpgradeTab` show `rules.WorkerCap(level) + rules.CombatCap(level)` as the single number for
    now, and `post.interactable` uses `roster.CanPost(...) == OpOutcome.Ok`; phase 03 replaces both;
  - `DebugCommands` line 146 reads `b.HirelingCap` from the data, which still exists, so it compiles unchanged until
    phase 03.
- `dotnet build -c Release` then passes, with no new warnings, and `dotnet test` passes.

## Test plan
Unit tests:
- **`JobType`** (in `LevelRulesTests`): `IsCombat` is true for GuardMelee and GuardRanged, and false for Woodcutter,
  Miner, Smelter, Farmer and Cook.
- **`LevelRulesTests`:**
  - `CombatCap` and `WorkerCap` for levels 1–8 match the table;
  - `Cap(level, GuardRanged)` is the combat cap and `Cap(level, Cook)` the worker cap;
  - `MaxPerBoard` is as listed for every job.
- **`RosterTests`:**
  - a level 1 board posts a guard, then refuses a second (`CombatCapReached`);
  - it posts 2 workers, then refuses a third (`WorkerCapReached`);
  - a level 3 board with 2 woodcutters refuses a third with worker room left (`JobLimitReached`);
  - a second Steward is refused;
  - a Farmer's `Leaving` contract doesn't block a new Farmer, but does count towards the worker cap;
  - a roster built over the cap (entries added directly) refuses that kind and still accepts the other.
- **`DataDefaultsTests`:** a 0.6-style YAML without `combatCap`, `workerCap` or `maxPerBoard` loads with the shipped
  values, and `filled` lists them.

## Commit
`feat(caps): combat and worker caps per board level and a per-job limit (rules, data, tests)`

## Rollback
Revert the commit, and the shared cap comes back. Data files saved meanwhile have the new keys, which 0.6 rejects. They
only exist on test machines until release, and deleting the data file restores the defaults.

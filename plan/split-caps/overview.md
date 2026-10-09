# Split hireling caps — Project Overview

## Problem
A hiring board has one cap shared by every job: 2, 3, 4, 5, 6, 7, 8 and 10 hirelings at board levels 1–8
(`boardLevels.hirelingCap`, checked by `Roster.HasRoom` when a contract is posted).
- **Automation comes too late.** A modest automated base needs a level 3 or 4 board: woodcutter, miner, Steward,
  farmer, cook, plus a guard. That's the Iron Age.
- **Armies come too easily.** The same cap lets a level 8 board field ten guards.

Neither is the intended curve. The mod should let players automate the base early, because waiting until the end game
isn't fun, while keeping the fighting force small all game.

## Goals
- **Two caps per board level,** one for **combat** hirelings and one for **workers**, each with its own count:

  | Board level | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 |
  |---|---|---|---|---|---|---|---|---|
  | Combat | 1 | 1 | 2 | 2 | 3 | 3 | 4 | 4 |
  | Workers | 2 | 4 | 6 | 8 | 8 | 8 | 8 | 8 |

- **Combat** means the melee guard and the ranged guard today, and later the wizard and the healer. Any mix is allowed
  within the cap, for example 4 melee guards at level 8.
- **Workers** are everyone else, with a limit per job inside the worker cap: Woodcutter 2, Miner 2, Steward 1,
  Farmer 1, Cook 1, and the Rancher 1 once it exists. Eight workers is exactly one full base, reached at board level 4.
  - Farmer and Cook were already one per board.
  - The Steward, unlimited until now, becomes one per board.
- **The job gates stay.** Miner, Farmer and Cook still need a level 2 board, so a level 1 board means woodcutters, a
  Steward and one guard.
- **Server owners can change all of it in the data file:**
  - `combatCap` and `workerCap` per board level;
  - `maxPerBoard` per job, where 0 means no limit beyond the cap.

  Older data files get these filled in from the defaults automatically. The old `hirelingCap` is no longer used.
- **Existing worlds keep what they have.** A board already over a cap or a job limit keeps those hirelings: they
  respawn and get promoted as usual. Only new contracts of that kind are refused until the count drops below the limit.
- **Players can see where they stand.**
  - The Contracts tab shows both counts and the chosen job's count on one line, for example "Combat 1/2 · Workers 3/6
    · Woodcutter 1/2", in red when the chosen job's kind or the job is full or over.
  - The Upgrade tab shows what the next level adds to each cap.
  - Refusing a contract says which limit is full.
- **New jobs slot in.** Each future job declares its kind (combat or worker) and its `maxPerBoard`, and needs no new
  cap code. The Rancher and the casters will be the first.
- **It ships as 0.7.0**, on its own.

## Non-goals
- **The Rancher, wizard and healer themselves.** They're separate projects; this one only makes room for them.
- **Changes to board upgrade costs, work radius, hire and upkeep prices,** or the job gates.
- **Ending contracts** that are over a new cap or limit.
- **A per-hireling or per-player cap.** The caps belong to the board, as today.
- **Separate caps for followers.** A recruited follower counts against its job's kind, as it does today against the
  shared cap.
- **New in-game settings** in the config file. The numbers live in the data file, like the other level tables.

## Users & primary flow
**Players:**
1. They build a level 1 board and post a woodcutter and a Steward (Workers 2/2) and a guard (Combat 1/1).
2. Posting a second guard is refused: "Combat hirelings: 1/1 at this board level. Upgrade the board for more."
3. They upgrade to level 2. The Upgrade tab showed "Workers 2 → 4" and "Combat hirelings 1" (unchanged) before they
   paid, and now a miner and a farmer can be hired.
4. By level 4 the base is fully worked (Workers 8/8: 2 woodcutters, 2 miners, Steward, Farmer, Cook, and one slot
   waiting for the Rancher). The fighting force grows to 4 only by level 7.
5. A third woodcutter is refused ("Woodcutter: 2/2 per board") even with room in the worker cap.

**Server owners:**
- edit `combatCap`, `workerCap` and `maxPerBoard` in the data file to change the curve;
- update an existing world, where nobody already hired is sent away.

## Constraints
- **Where the code is:**
  - The cap is enforced in `BoardRosterOps` (the Post op, on the board owner's machine through `MutationService`).
  - It's read through `LevelRules.HirelingCap` from `DataStore.Current.BoardLevels`.
  - It's shown in `UI/ContractsTab.cs` and `UI/UpgradeTab.cs`.
  - Job rules live in `Core/JobType.cs` (`IsGuard`, `OnePerBoard`) and `Roster.JobTaken`.
  - The data file is loaded by `DataYaml`/`DataDefaults.FillMissing`, which fills missing keys from the defaults,
    including in list entries, and rejects unknown keys.
- **Multiplayer:**
  - The cap check runs where the roster is changed (the board's owner), as today.
  - Clients read the same synced data file, so the UI and the check agree.
- **Data file compatibility:** 0.6 builds reject a file with the new keys. Everyone updates together, which the
  release notes say, as for 0.5 and 0.6.
- **What counts:**
  - Against a kind's cap, every contract counts, pending, active and leaving, as today ("everyone counts until
    they've actually gone").
  - Against a job's limit, a leaving contract doesn't count, as the Farmer and Cook rule works today. A replacement
    can be hired while the old one walks off, but it still has to fit the kind's cap.
- **The roster's save format is unchanged.** Caps are worked out from the jobs in the roster; nothing new is stored
  per contract.

## Success criteria
- **Unit tests** cover:
  - the caps for every board level;
  - each job's kind and limit;
  - refusals (combat full, workers full, job limit full);
  - over-cap rosters, which are kept and block only new contracts;
  - data files without the new keys, which get the defaults.
- **In-game test rows,** run through ModTestBridge:
  - a level 1 board takes 1 guard and refuses a 2nd;
  - it takes 2 workers and refuses a 3rd;
  - a 3rd woodcutter is refused at level 3 with worker room left;
  - a 2nd Steward is refused;
  - after an upgrade to level 3, a 2nd guard is accepted;
  - a roster made over the cap (with the test fixture) keeps its hirelings and refuses a new contract of that kind.
- **The tabs show the counts correctly:** the Contracts tab shows both caps and the job's count, and the Upgrade tab
  the next level's caps (checked by Tim).
- **Existing test rows still pass,** including those that hire several hirelings at one board. Rows that assumed the
  old shared cap are updated.
- **The README's board-levels table** and the CHANGELOG describe the new caps.

## Decisions
- **Combat ramp** → +1 at levels 1, 3, 5 and 7: 1, 1, 2, 2, 3, 3, 4, 4.
- **Combat mix** → Any mix within the combat cap; no per-type limit for combat jobs.
- **Worker per-job limits** → Woodcutter 2, Miner 2, Steward 1, Farmer 1, Cook 1, later Rancher 1, inside the worker
  cap of 2, 4, 6, 8 (8 from level 4 up).
- **Existing worlds over a cap** → Keep their hirelings and block new contracts of that kind until the count drops.
- **Configurable** → Yes, in the data file: `combatCap`/`workerCap` per board level and `maxPerBoard` per job.
- **Job gates** → Kept: Miner, Farmer and Cook still need a level 2 board.
- **Release** → 0.7.0 on its own.

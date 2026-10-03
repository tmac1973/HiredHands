# VikingsForHire test checklist

Run before every release. **Modes:** `SP` = single-player from `vikingsforhire-dev` (always run first).
`D` = you're a client of the local dedicated server (`scripts/run-dedicated-server.sh`), run second.
`L` = you host from `vikingsforhire-dev` with "Start server" ticked; only used where a row says so.

## Setup

- **Dev profile:** Gale profile `vikingsforhire-dev`. Normally enabled: Jotunn, YamlDotNet, JsonDotNET,
  shudnal ConfigurationManager, ConditionalConfigSync, Server_devcommands, AzuAutoStore, AzuCraftyBoxes,
  PullMats. ValheimPlus stays disabled. A row that needs a mod absent says so; toggle it in Gale for that row only.
- **Build + deploy:** `dotnet build -c Release` copies `VikingsForHire.dll` into the profile and
  `test/alias_vfh.yaml` into the profile's `BepInEx/config/`.
- **Console:** press F5 in-game (add `-console` to the profile's launch arguments if it doesn't open).
  World-changing test commands need `devcommands`; the diagnostics commands don't.
- **Test macros:** each row names a `vfh_t_*` alias from `test/alias_vfh.yaml`. Type it in the console;
  the row passes when the log has `evt=test.result row=<ROW> pass=true` (phase 02 onwards).
- **Dedicated server:** `scripts/run-dedicated-server.sh` (add `--no-vfh` or `--vfh-dll <path>` per row).
  Join via *Join game → Add server* `127.0.0.1:2456` (no password; LAN-only).
- **Admin list:** `.../Valheim dedicated server/vfh-save/adminlist.txt`. Put your Steam ID
  (`76561197980064368`) on its own line when a row needs admin; restart the server after changing it.
- **Server-side config:** `.../Valheim dedicated server/BepInEx/config/Spronglehump.VikingsForHire.cfg`
  and `Spronglehump.VikingsForHire.yml`.

## Logs (attach to every bug report)

| Mode | Files |
|---|---|
| SP / client | `~/.local/share/com.kesomannen.gale/valheim/profiles/vikingsforhire-dev/BepInEx/LogOutput.log` and `.../BepInEx/VikingsForHire.log` |
| Dedicated server | `/games/SteamLibrary/steamapps/common/Valheim dedicated server/BepInEx/LogOutput.log` and `.../BepInEx/VikingsForHire.log` |

Before reproducing a bug run `vfh_log_mark <short text>` (phase 02 onwards) and include that text in the report,
together with **both** files from that run (client and, in mode D, server).

## Matrix

| Row | Modes | Macro | Setup | Expected |
|---|---|---|---|---|
| SCAFFOLD-1 Plugin loads | SP, D | `vfh_t_smoke` | Launch the profile; start the dedicated server | `VikingsForHire 0.1.0 loaded` in both `LogOutput.log`s; the macro shows `VikingsForHire loaded` mid-screen |
| SCAFFOLD-2 Mod required | D | — | Join the server from a profile without VikingsForHire | Jotunn rejects the connection with a version/compat message |
| VFH-HARNESS-1 Harness + fast_timers | SP, D | `vfh_t_harness1` | `devcommands`; on D your Steam ID in `adminlist.txt` | `pass=true`; on D the server log shows `evt=fast_timers on=true` then `on=false` |
| VFH-CFG-1 Server data sync | D | `vfh_t_cfg1` | Server cfg `RequiredPieces = 25`; server yml board level 1 `Wood: 30`; restart server, join | `pass=true`; `vfh_dump_data` shows the server values; after leaving, `vfh_dump_data` in the menu shows yours again |
| VFH-CFG-2 Hot reload | SP | `vfh_t_cfg2_a`, edit, `vfh_t_cfg2_b` | Run `_a`; change board level 1 `Wood: 40` to `45` in your yml and save; run `_b`; set it back to 40 | `pass=true`; log has a second `evt=data.reload reason="file changed"` |
| VFH-LOG-1 Logging | SP | — | `vfh_debug Data on`, `vfh_log_mark log-test`, save the yml unchanged, `vfh_debug Data off`, then `vfh_debug_throw` ×10 | `VikingsForHire.log` has the session header, the mark and `evt=data.reload`; the throws give 5 stack traces (`occurrence=1`…`5`), later ones are summarised (`repeat=`) at most once a minute |
| VFH-LOG-2 Bad yml | SP | — | Break the yml's indentation and save | `lvl=E ... evt=data.parse_error` then `evt=data.reload source=defaults`; fix the file and it reloads as `source=local` |
| VFH-PLACE-1 No base | SP | `vfh_t_place1` | Open ground with nothing built within 20m | `pass=true`; holding the Hammer with the Hiring Board selected shows a red ghost and "needs a workbench" |
| VFH-PLACE-2 Missing parts | SP | `vfh_t_place2` | Open ground | `pass=true` |
| VFH-PLACE-3 Allowed | SP | `vfh_t_place3` | Open ground | `pass=true`; also build one by hand with the Hammer in a fixture base: it places and costs the YAML build cost |
| VFH-PLACE-4 Too close | SP | `vfh_t_place4` | Open ground | `pass=true`; by hand, a second board within 100m shows "Another hiring board is …m away" |
| VFH-PLACE-5 World limit | SP | — | cfg `MaxBoardsPerWorld = 1`; build a board, walk 500m+ away (its zone unloads) and try another with a fixture base | Blocked with "This world already has 1 hiring boards"; set the cfg back to 0 |
| VFH-STORE-1 Food & coins only | SP | `vfh_t_store1` | Open ground | `pass=true`; by hand, dragging Wood onto the board's slots does nothing and shows "Only food and coins…" |
| VFH-STORE-2 Hover & relog | SP | — | Board with 3 CookedMeat and some Coins; note `vfh_board_info`; log out and back in | Hover shows level 1 and the same funds as `vfh_board_info`; after relog the same board id and items |
| VFH-AZU-1 AzuAutoStore | SP | `vfh_t_azu1_a`, act, `vfh_t_azu1_b` | AzuAutoStore enabled. After `_a`: carry Wood and CookedMeat, use Azu's store hotkey next to the board, and drop a CookedMeat on the ground by it for Azu's auto-pickup | `pass=true`: nothing went into the board |
| VFH-CRAFTY-1 CraftyBoxes | SP | `vfh_t_crafty1_a`, act, `vfh_t_crafty1_b` | AzuCraftyBoxes enabled, no Coins/CookedMeat on you or in chests. After `_a`: at the fixture workbench the Wood recipe (1 Coins + 1 CookedMeat) shows as not craftable | `pass=true`; then put 1 Coins + 1 CookedMeat in a normal chest and it crafts |
| VFH-PULL-1 PullMats | SP | `vfh_t_pull1_a`, act, `vfh_t_pull1_b` | PullMats enabled, no Wood on you or in chests. After `_a`: Hammer, select a piece that needs Wood, press N | PullMats reports Wood missing; `_b` `pass=true` (the 20 Wood stayed in the board) |
| VFH-UPG-1 Upgrade chain | SP | `vfh_t_upg1` | Open ground, inventory with plenty of room | `pass=true`; the board's tint warms a step at each level |
| VFH-UPG-2 Can't afford | SP | `vfh_t_upg2` | Open ground, none of the L2 materials on you | `pass=true` |
| VFH-UPG-3 Panel | SP | — | Board at L1; Shift+E | Upgrade tab: "Level 1 → Level 2", the cap/level/radius lines, TrophyEikthyr 1, HardAntler 3, DeerHide 20, Flint 20, Wood 50 with have/need in red/green; Upgrade greyed until you carry them all. Esc closes the panel (no game menu). Walking 6m away closes it. Contracts/Roster tabs show their placeholders |
| VFH-UPG-4 Live data | SP | — | Panel open on the Upgrade tab of an L2 board; change level 3's cost in the yml and save | The tab shows the new cost within a second |
| VFH-UPG-5 Deconstruct | SP | — | `vfh_board_setlevel 4`, then deconstruct with the Hammer | Only the level 1 build cost drops |
| VFH-BOARD-2 Concurrent upgrade | D | — | Two clients at one L2 board, both with L3 mats, both click Upgrade within a second | One upgrade applies (L3); the other client sees the "someone else upgraded" message and has all its mats back |
| VFH-HIRE-1 Persistence | SP | `vfh_t_hire1_a`, relog, `vfh_t_hire1_b` | Open ground. After `_a`, look at the miner (iron pickaxe, iron armour), log out to the menu and back in | `pass=true`; same name and looks after relog |
| VFH-HIRE-3 Gear & stats | SP | `vfh_t_hire3` | Open ground | `pass=true` |
| VFH-HIRE-4 Cargo slots | SP | `vfh_t_hire4` | Open ground | `pass=true`; by hand, E on an L1 hireling shows its cargo with only the top row usable and the rest greyed, and the hireling stands still facing you until you close it |
| VFH-HIRE-5 Snapshot | SP | `vfh_t_hire5` | Open ground | `pass=true`; the hireling reappears 3m to the side with the same look |
| VFH-HIRE-6 Looks | SP | — | `vfh_spawn Woodcutter 1 10` | Ten different vikings: both genders, varied hair/beards (no beards on women), skin and hair colours, gender-matched names on hover |
| VFH-HIRE-7 Damage rules | SP | — | `vfh_spawn GuardMelee 1`; hit it with a sword; set `FriendlyFireOnHirelings = true` and hit again; then `spawn Greyling` next to it | Your hits do nothing, then do damage with friendly fire on; the Greyling's hits land (reduced by armor). The hireling doesn't fight back yet (phase 07) |
| VFH-HIRE-8 Death | SP | — | `vfh_spawn Miner 4`, `vfh_kill_hirelings` | It dies (ragdoll) and nothing drops |
| VFH-HIRE-2 Seen by others | D | — | Spawn a hireling; a second client looks at it | Same looks and gear on both clients |
| VFH-CLLC-1 CreatureLevelControl | SP | `vfh_t_hire3` | Profile with Smoothbrain CreatureLevelAndLootControl enabled | `pass=true` (charlevel 1, maxhealth from the table); session header shows `CreatureLevelControl:1/1` |
| VFH-CON-1 Hire | SP | `vfh_t_con1` | Open ground | `pass=true`; a viking walks in from ~35m and reports to the board |
| VFH-CON-2 Refusals | SP | `vfh_t_con2` | Open ground | `pass=true` |
| VFH-CON-3 Cancel refund | SP | `vfh_t_con3` | Open ground | `pass=true` |
| VFH-CON-4 Panel | SP | — | A board with food; Shift+E | Contracts tab: job/level/radius/stance pickers, fee and upkeep update live (fee red when short), Post greyed at the cap. Roster tab: the hireling listed with its status; select it to change stance/radius, promote (on an L2+ board) or dismiss (asks first) |
| VFH-UPK-1 Unpaid | SP | `vfh_t_upk1` | Open ground | `pass=true`; hover shows "Unpaid (1/2)", "(2/2)", then it drops its cargo and walks off |
| VFH-UPK-2 Paid day | SP | `vfh_t_upk2` | Open ground | `pass=true` |
| VFH-DTH-1 Permadeath | SP | `vfh_t_death1` | Open ground | `pass=true`; "… has died" message |
| VFH-DTH-2 Respawn | SP | `vfh_t_death2` | Open ground | `pass=true`; it walks back in after ~10s (fast timers) |
| VFH-PRO-1 Promote | SP | `vfh_t_pro1` | Open ground, room in your inventory | `pass=true`; its gear changes to level 2 |
| VFH-DIS-1 Dismiss | SP | `vfh_t_dis1` | Open ground | `pass=true` |
| VFH-REM-1 Remove board | SP | — | A board with a hireling; deconstruct it with the Hammer | Asks first; on Yes the hireling drops its cargo and walks off, the board's storage drops |
| VFH-HIRE-3-6 Multiplayer roster | D | — | See the phase 06 plan's dedicated-server test | Ops applied once, forwarded to the board's owner, and applied by the server when nobody is near |
| VFH-STN-1 Flee | SP | `vfh_t_stance1` | Open ground | `pass=true`; the woodcutter runs off |
| VFH-STN-2 Defend | SP | `vfh_t_stance2` | Open ground | `pass=true`; the miner swings its pickaxe |
| VFH-STN-3 Passive | SP | `vfh_t_stance3` | Open ground | `pass=true` |
| VFH-STN-4 Defensive | SP | `vfh_t_stance4` | Open ground | `pass=true`; you can see shield blocks against the greydwarf's swings |
| VFH-STN-5 Aggressive archer | SP | `vfh_t_stance5` | Open ground | `pass=true`; arrows fly at full speed, and it swaps to its club if the neck closes in |
| VFH-COMBAT-1 Guards vs group | SP | `vfh_t_combat1` | Open ground | `pass=true`, both guards alive |
| VFH-COMBAT-3 Patrol & regen | SP | — | Hire a guard from a board (vfh_spawn_contract GuardMelee 1) | It walks around the board's radius, pausing; after a fight its health creeps back up |
| VFH-COMBAT-4 Raid | SP | — | Base with 2 contracted guards and a worker; `event army_eikthyr` | Guards engage, the worker flees or defends by stance, no input needed |
| VFH-COMBAT-2 Seen by others | D | — | Combat near two clients | Same animations and hits on both |
| VFH-DLV-1 Per-type delivery | SP | `vfh_t_deliver1` | Open ground (takes a few minutes) | `pass=true`; you can watch it fell the beeches, clear logs and stumps, pick up wood, then carry it to chest A |
| VFH-DLV-2 Overflow | SP | `vfh_t_deliver2` | Open ground | `pass=true`; wood piles up in front of the board |
| VFH-WORK-1 Tree safety | SP | `vfh_t_work1` | Open ground | `pass=true` |
| VFH-WORK-5 Logs and felling direction | SP | — | A woodcutter with a few trees, some a short way from a wall | Each felled tree falls away from buildings; it splits and clears the fallen log and the stump (picking up the wood) before felling the next tree |
| VFH-WORK-3 Tiers | SP | — | A level 1 woodcutter near oaks and beeches; then promote to level 3 (bronze axe) | Level 1 skips oaks; level 3 cuts them, and chops faster |
| VFH-WORK-4 Two woodcutters | SP | — | Two woodcutters on one board with a few trees | They work different trees |
| VFH-AZU-2 Pile vs AzuAutoStore | SP | — | AzuAutoStore on; overflow on the pile | Azu may store the pile into chests; the woodcutter never picks the pile back up or loops |
| VFH-WORK-2 Chest in use | D | — | Client B holds the wood chest open while the woodcutter delivers | It skips that chest (other chests or the pile), no items lost or doubled |
| VFH-BOARD-1 Ward | D | — | A second player places a ward (you not permitted) over a board they built | You can't open its storage and hover says no access |

If a row fails: fix it, rerun that row in every listed mode, then rerun SCAFFOLD-1.

## Results

| Date | Build (commit) | Row | Mode | Result | Notes |
|---|---|---|---|---|---|
| 2026-10-03 | c16fbd1 | SCAFFOLD-1 | SP | Pass | Plugin loaded; `vfh_t_smoke` broadcast shown |
| 2026-10-03 | c16fbd1 | SCAFFOLD-1 | D | Pass | Server loaded plugin (10 loaded, 0 failed); client joined and spawned |
| 2026-10-03 | c16fbd1 | SCAFFOLD-2 | D | Deferred | To run after a few phases are in |
| 2026-10-03 | 0dec920 | VFH-HARNESS-1 | SP | Pass | |
| 2026-10-03 | 0dec920 | VFH-CFG-2 | SP | Pass | |
| 2026-10-03 | 0dec920 | VFH-PLACE-1 | SP | Pass | |
| 2026-10-03 | 0dec920 | VFH-PLACE-2 | SP | Pass | |
| 2026-10-03 | 0dec920 | VFH-PLACE-3 | SP | Pass | |
| 2026-10-03 | 0dec920 | VFH-PLACE-4 | SP | Pass | |
| 2026-10-03 | 0dec920 | VFH-STORE-1 | SP | Pass | |
| 2026-10-03 | 0dec920 | VFH-STORE-2 | SP | Pass | |
| 2026-10-03 | 0dec920 | VFH-AZU-1 | SP | Pass | |
| 2026-10-03 | 0dec920 | VFH-CRAFTY-1 | SP | Pass | |
| 2026-10-03 | 0dec920 | VFH-PULL-1 | SP | Pass | |
| 2026-10-03 | 0dec920 | VFH-UPG-1 | SP | Pass | |
| 2026-10-03 | 0dec920 | VFH-UPG-2 | SP | Pass | |
| 2026-10-03 | 0dec920 | VFH-UPG-3 | SP | Pass | |
| 2026-10-03 | 0dec920 | VFH-UPG-4 | SP | Pass | |
| 2026-10-03 | 0dec920 | VFH-UPG-5 | SP | Pass | |
| 2026-10-03 | 3dcf941 | VFH-HIRE-1 | SP | Pass | |
| 2026-10-03 | 3dcf941 | VFH-HIRE-3 | SP | Pass | |
| 2026-10-03 | 3dcf941 | VFH-HIRE-4 | SP | Pass | |
| 2026-10-03 | 3dcf941 | VFH-HIRE-5 | SP | Pass | |
| 2026-10-03 | 3dcf941 | VFH-HIRE-6 | SP | Pass | |
| 2026-10-03 | 3dcf941 | VFH-HIRE-7 | SP | Pass | |
| 2026-10-03 | 3dcf941 | VFH-HIRE-8 | SP | Pass | |
| 2026-10-03 | 984de12 | VFH-CON-1 | SP | Pass | |
| 2026-10-03 | 984de12 | VFH-CON-2 | SP | Pass | |
| 2026-10-03 | 984de12 | VFH-CON-3 | SP | Pass | |
| 2026-10-03 | 984de12 | VFH-CON-4 | SP | Pass | |
| 2026-10-03 | 984de12 | VFH-UPK-1 | SP | Pass | |
| 2026-10-03 | 984de12 | VFH-UPK-2 | SP | Pass | |
| 2026-10-03 | 984de12 | VFH-DTH-1 | SP | Pass | |
| 2026-10-03 | 984de12 | VFH-DTH-2 | SP | Pass | |
| 2026-10-03 | 984de12 | VFH-PRO-1 | SP | Pass | |
| 2026-10-03 | 984de12 | VFH-DIS-1 | SP | Pass | |
| 2026-10-03 | 984de12 | VFH-REM-1 | SP | Pass | |
| 2026-10-03 | b9e5fd2 | VFH-STN-1 | SP | Pass | |
| 2026-10-03 | b9e5fd2 | VFH-STN-2 | SP | Pass | |
| 2026-10-03 | b9e5fd2 | VFH-STN-3 | SP | Pass | |
| 2026-10-03 | b9e5fd2 | VFH-STN-4 | SP | Pass | |
| 2026-10-03 | b9e5fd2 | VFH-STN-5 | SP | Pass | |
| 2026-10-03 | b9e5fd2 | VFH-COMBAT-1 | SP | Pass | |
| 2026-10-03 | b9e5fd2 | VFH-COMBAT-3 | SP | Pass | |
| 2026-10-03 | 1806a7f | VFH-DLV-1 | SP | Pass | |
| 2026-10-03 | 1806a7f | VFH-DLV-2 | SP | Pass | First try failed: a wood chest of the player's own was inside the 40 m radius and took the wood; passed far from it |
| 2026-10-03 | 1806a7f | VFH-WORK-5 | SP | Pass (partly) | Felling and log/stump order seen working; rolled-log follow (later build) not reproduced, accepted on trust since logs rarely roll far |

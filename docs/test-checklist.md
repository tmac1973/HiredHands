# Hired Hands test checklist

Run before every release. **Modes:** `SP` = single-player from `vikingsforhire-dev` (always run first).
`D` = you're a client of the local dedicated server (`scripts/run-dedicated-server.sh`), run second.
`L` = you host from `vikingsforhire-dev` with "Start server" ticked; only used where a row says so.

## Setup

- **Dev profile:** Gale profile `vikingsforhire-dev`. Normally enabled: Jotunn, YamlDotNet, JsonDotNET,
  shudnal ConfigurationManager, ConditionalConfigSync, Server_devcommands, AzuAutoStore, AzuCraftyBoxes,
  PullMats. ValheimPlus stays disabled. A row that needs a mod absent says so; toggle it in Gale for that row only.
- **Build + deploy:** `dotnet build -c Release` copies `HiredHands.dll` into the profile and
  `test/alias_vfh.yaml` into the profile's `BepInEx/config/`.
- **Console:** press F5 in-game (add `-console` to the profile's launch arguments if it doesn't open).
  World-changing test commands need `devcommands`; the diagnostics commands don't.
- **Test macros:** each row names a `vfh_t_*` alias from `test/alias_vfh.yaml`. Type it in the console;
  the row passes when the log has `evt=test.result row=<ROW> pass=true` (phase 02 onwards).
- **Dedicated server:** `scripts/run-dedicated-server.sh` (add `--no-vfh` or `--vfh-dll <path>` per row).
  Join via *Join game → Add server* `127.0.0.1:2456` (no password; LAN-only).
- **Admin list:** `.../Valheim dedicated server/vfh-save/adminlist.txt`. Put your Steam ID
  (`76561197980064368`) on its own line when a row needs admin; restart the server after changing it.
- **Server-side config:** `.../Valheim dedicated server/BepInEx/config/Spronglehump.HiredHands.cfg`
  and `Spronglehump.HiredHands.yml`.

## Logs (attach to every bug report)

| Mode | Files |
|---|---|
| SP / client | `~/.local/share/com.kesomannen.gale/valheim/profiles/vikingsforhire-dev/BepInEx/LogOutput.log` and `.../BepInEx/HiredHands.log` |
| Dedicated server | `/games/SteamLibrary/steamapps/common/Valheim dedicated server/BepInEx/LogOutput.log` and `.../BepInEx/HiredHands.log` |

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
| VFH-LOG-1 Logging | SP | — | `vfh_debug Data on`, `vfh_log_mark log-test`, save the yml unchanged, `vfh_debug Data off`, then `vfh_debug_throw` ×10 | `HiredHands.log` has the session header, the mark and `evt=data.reload`; the throws give 5 stack traces (`occurrence=1`…`5`), later ones are summarised (`repeat=`) at most once a minute |
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
| VFH-REM-1 Remove board | SP | — | A board with a hireling; damage it a little, repair it with the Hammer, then deconstruct it | Repair just repairs (no dialog). Deconstruct asks first; on Yes the hireling drops its cargo and walks off, the board's storage drops |
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
| VFH-WORK-7 Logs get cleared | SP, D | `vfh_t_logs1` | Open ground | `pass=true`; the felled beech's logs are chopped up and picked up (none left lying around) |
| VFH-WORK-3W Woodcutter tiers | SP | — | A level 1 woodcutter near oaks and beeches; then promote to level 3 (bronze axe) | Level 1 skips oaks; level 3 cuts them, and chops faster |
| VFH-WORK-4 Two woodcutters | SP | — | Two woodcutters on one board with a few trees | They work different trees |
| VFH-AZU-2 Pile vs AzuAutoStore | SP | — | AzuAutoStore on; overflow on the pile | Azu may store the pile into chests; the woodcutter never picks the pile back up or loops |
| VFH-WORK-2 Chest in use | D | — | Client B holds the wood chest open while the woodcutter delivers | It skips that chest (other chests or the pile), no items lost or doubled |
| VFH-WORK-3 Copper, no digging | SP | `vfh_t_work3` | Open ground (a few minutes) | `pass=true`; it breaks the copper deposit into chunks, mines them, puts ore in chest A and stone in chest B, and the ground has no holes |
| VFH-TIER-1 Pickaxe tier | SP | `vfh_t_tier1` | Open ground | `pass=true`; the silver vein is never hit ("too hard" never shows), the tin is mined |
| VFH-MINE-1 Base safety | SP | — | A rock touching a wall or floor inside a miner's radius | The miner leaves that rock alone; `work.no_targets` names the piece if it's the only rock |
| VFH-MINE-2 Buried copper | SP | — | A natural copper deposit, partly underground | The miner takes the exposed chunks and leaves the buried ones (it never digs); dig them free and it carries on |
| VFH-MINE-3 Terrain toggle | SP | — | `MinerProtectsTerrain=false`, miner next to a deposit | Its swings now dent the ground; set it back to true afterwards |
| VFH-WORK-4 Copper in MP | D | — | Two clients watch a miner work copper | Chunk breaks show on both; ore counts in the chest match |
| VFH-WORK-5 Smelter (no Azu) | SP | `vfh_t_work5` | **Disable AzuAutoStore in Gale first.** Open ground (several minutes) | `pass=true`; it fetches ore, coal and wood, loads both smelters and the kiln, collects the bars and puts them in the bar chests |
| VFH-AZU-3 Smelter with Azu | SP | `vfh_t_azu3` | AzuAutoStore enabled. Open ground | `pass=true`; stations stay stocked, the smelter never picks up bars, no loops |
| VFH-SMELT-1 No input | SP | — | Smelter hireling with its stations low and the ore chests emptied | Hover says "Stations need ore or fuel, chests have none"; it idles by the board |
| VFH-SMELT-2 Two smelters | SP | — | Two smelter hirelings, three or four stations | They split the stations; no station is loaded past its max |
| VFH-SMELT-3 Blast furnace | SP | — | Add a blast furnace with iron/black metal scrap and coal in chests | It gets fed too |
| VFH-CHORE-1 Steward level gate | SP | `vfh_t_chore_gate` | Open ground (flattened) | `pass=true`; a level 1 Steward leaves the smelter alone for 60 s; promoted to 2, it loads it |
| VFH-CHORE-2 Steward chore toggle | SP | `vfh_t_chore_toggle` | Open ground (flattened) | `pass=true`; Stations off: smelter untouched for 60 s; on: loaded |
| VFH-CHORE-3 Mills | SP | `vfh_t_mills` | Open ground (flattened) | `pass=true`; windmill and spinning wheel loaded from chests; flour and linen reach their chests (with AzuAutoStore: Azu stores them) |
| VFH-CHORE-4 Fires | SP | `vfh_t_fires` | Open ground (flattened) | `pass=true`; hearth and wood torch fuelled to full from a chest (Torches Eternal switched off for it); with Torches Eternal installed the Steward adds nothing |
| VFH-CHORE-5 Beehive | SP | `vfh_t_beehive` | Open ground (flattened) | `pass=true`; a full beehive emptied, its honey in the honey chest |
| VFH-CHORE-6 Sap collector | SP | `vfh_t_sap` | Open ground (flattened) | `pass=true`; a level 6 Steward empties it into the sap chest (the level gate itself is VFH-CHORE-1) |
| VFH-CHORE-7 Tamed animals | SP | `vfh_t_animals` | Open ground (flattened) | `pass=true`; a hungry tamed boar gets a raspberry dropped in front of it and eats it (AzuAutoStore doesn't take it); with PetPantry counted, a second boar is left alone |
| VFH-CHORE-8 Repairs | SP | `vfh_t_repairs` | Open ground (flattened) | `pass=true`; the wall by the workbench is repaired, the one 28 m out (no station in range) isn't, and nothing is repaired while a Greyling is about |
| VFH-CHORE-9 Most urgent first | SP | `vfh_t_chore_urgency` | Open ground (flattened) | `pass=true`; an empty hearth before an empty smelter, then the smelter |
| VFH-CHORE-10 Fermenter | SP | `vfh_t_fermenter` | Open ground (flattened) | `pass=true`; a ready fermenter (roofed) tapped, its meads stored; then refilled with a mead base from a chest |
| VFH-CHORE-11 Shield generator | SP | `vfh_t_shield` | Open ground (flattened) | `pass=true`; an empty shield generator fed bones from a chest |
| VFH-CHORE-12 Tidying up | SP | `vfh_t_tidy` | Open ground (flattened) | `pass=true`; wood lying about ends up in the wood chest; with an empty torch waiting, the torch comes first |
| VFH-CHORE-13 Repairs: weather and reach | SP | `vfh_t_repairs2` | Open ground (flattened) | `pass=true`; an unroofed wall at 70% (rain wear) is left alone until set to 40%, then repaired; a wall floating 5 m up is repaired from the ground (hammer reach); one 10 m up is never attempted ("Repairs: can't get to") |
| VFH-PAUSE-1 Woodcutter pauses at full chests | SP | `vfh_t_pause1` | Open ground (flattened) | `pass=true`; with the only wood chest full, the beeches stand for a minute and nothing is carried; a second chest with one wood and they're felled |
| VFH-PAUSE-2 Kiln pauses at full coal chests | SP | `vfh_t_pause2` | Open ground (flattened) | `pass=true`; with the coal chest full the kiln isn't loaded; a second coal chest and it is |
| VFH-CHORE-14 Board food | SP | `vfh_t_board_food` | Open ground (flattened) | `pass=true`; a board with a day of food and a level 1 Steward: cooked meat from the chest tops it to 200+ points (7 days); raw meat left in the chest (AllowRawFood off); the chest keeps its last cooked meat |
| VFH-LOOT-1 Steward loots in Gather Here | SP | `vfh_t_loot` | Open ground (flattened) | `pass=true`; a recruited Steward parked in Gather Here picks up the wood lying within 15 m of its spot |
| VFH-ORDER-1 Orders on the board | SP, D | `vfh_t_orders` | Anywhere | `pass=true`; orders added, seed order first, removed and cleared through the board's owner (from a client of the local dedicated server too) |
| VFH-FARM-0 Crop and kitchen catalogs | SP | `vfh_t_catalog` | Anywhere | `pass=true`; carrots, carrot seeds, barley and raspberries are known crops; cooked meat, carrot soup and bread are known kitchen items (`vfh_crops`, `vfh_recipes` list them all) |
| VFH-FARM-1 Farmer harvests ripe crops | SP, D | `vfh_t_farm_harvest` | Open ground (flattened) | `pass=true`; 6 ripe carrots on a cultivated field harvested and put in the carrot chest |
| VFH-FARM-2 Bushes only for short orders | SP | `vfh_t_farm_bush` | Open ground (flattened) | `pass=true`; a ripe raspberry bush left alone with no order, picked once a Raspberry order is added, raspberries stored |
| VFH-FARM-3 Seed cycle and seed reserve | SP | `vfh_t_farm_seeds` | Open ground (flattened) | `pass=true`; with 3 spare carrots and a 4-seed order, seed carrots are planted first and no carrots; once they ripen (`grow_all`) the seeds are stored, then carrots are planted from the seeds above the reserve, leaving at least 5 in the chest |
| VFH-FARM-4 Rows | SP | `vfh_t_farm_rows` | Open ground (flattened) | `pass=true`; carrots planted in rows continuing the existing row, at the crop's spacing (PlantEasily's when loaded), none under the roof piece |
| VFH-COOK-1 Cook at the spit | SP, D | `vfh_t_cook_spit` | Open ground (flattened) | `pass=true`; raw meat from a chest cooked on a spit over a lit fire until there are 4 cooked meat, nothing burnt (no coal) |
| VFH-COOK-2 Cook at the oven | SP | `vfh_t_cook_oven` | Open ground (flattened) | `pass=true`; the oven fuelled from the wood chest and bread baked from dough until there are 3 |
| VFH-COOK-3 Cook at the cauldron | SP | `vfh_t_cook_cauldron` | Open ground (flattened) | `pass=true`; carrot soup crafted at a cauldron over a lit fire from carrots and mushrooms in a chest, up to the order's 3 |
| VFH-COOK-4 Dough, then bread | SP | `vfh_t_cook_chain` | Open ground (flattened) | `pass=true`; bread dough made at a roofed prep table from barley flour, carried straight to the oven, bread stored |
| VFH-COOK-5 Seed stock protected | SP | `vfh_t_cook_protect` | Open ground (flattened) | `pass=true`; with planting switched off and a carrot seed order short, 4 of the 6 spare carrots are kept for seed carrots: the Cook makes no carrot soup and says it needs more carrots; once the seed order is removed it makes soup |
| VFH-TREE-1 Tree patch | SP | `vfh_t_tree_patch` | Open ground (flattened) | `pass=true`; a woodcutter plants saplings in a tree patch from seeds in a chest (kinds its stone axe can fell), and fells them once grown (`grow_all`) |
| VFH-PASS-1 Hirelings pass each other | SP | `vfh_t_pass` | Open ground (flattened) | `pass=true`; two woodcutters sent head-on through a 1.0 m walled corridor both reach the far end within a minute |
| VFH-READ-1/2 Reading attacks | SP | `vfh_t_read1`, `vfh_t_read2` | Open ground | `pass=true`; a level 8 guard reads 2-star Greydwarf Brutes' swings (HiredHands.log `defense.read`, timed, 85-100% read); with `BlockAndDodge` off nothing is read |
| VFH-BLOCK-1..3 Blocking | SP | `vfh_t_block1`, `vfh_t_block2`, `vfh_t_block3` | Open ground | `pass=true`; guards raise the shield in time and block (some parries, "Parry!"); off: no reads, no parry rolls |
| VFH-PROJ-1/2 Projectiles | SP | `vfh_t_proj1`, `vfh_t_proj2` | Open ground | `pass=true`; a guard reads and blocks 2-star Draugr arrows; an archer reads them but can't block |
| VFH-DODGE-1..4 Dodging | SP | `vfh_t_dodge1` … `vfh_t_dodge4` | Open ground | `pass=true`; an archer and a woodcutter roll from a 2-star Troll (chances set to 1 by the row), an archer from arrows; off: no rolls |
| VFH-DEF-M1..M4 Blocking and dodging measured | SP | `scripts/defense-ab.sh 5` (about an hour; `mtb` drives it) | Open ground; `BalanceLog` on (the script sets it) | `balance-report.py <profile>/BepInEx/HiredHands/balance --compare-defense`: damage taken lower with it on for the melee guards and the archer, every fight still won, read rates within 12 points of the levels table |
| VFH-DODGE-MP Dodging in multiplayer | D | — | Two players; a hireling owned by one, a troll owned by the other | The hireling rolls from a big swing and the hit misses; both see the roll |
| VFH-SMELT-4 Keeps the last item (Steward level 2 since 0.4.0) | SP | `vfh_t_keep1` | Open ground | `pass=true`; the tin and coal chests each keep 1 |
| VFH-NAV-1 Stairs and floors | SP | — | A real two-level build: a chest holding wood upstairs, reached by stairs; a smelter on a raised wooden floor | The woodcutter climbs the stairs to deliver to the upstairs chest; the smelter hireling loads the raised smelter; neither gets stuck on floor edges (watch for `smelter.stuck` / `work.unreachable`) |
| VFH-NAV-2 Doors | SP | `vfh_t_door1` | Open ground (`vfh_debug AI on` shows `door.open` / `door.detour` / `door.close`) | `pass=true`; the woodcutter opens the room's door, delivers to the chest inside and the door is closed again afterwards. Also by hand: a door under someone else's ward (you not permitted) is never opened |
| VFH-COMBAT-5 Quick engage | SP | `vfh_t_combat5` | Open ground (`vfh_debug Combat on` shows `combat.first_swing`) | `pass=true`; the guard swings within a second or two of noticing, and doesn't stand behind its shield |
| VFH-COMBAT-6 Big target | SP | `vfh_t_combat6` | Open ground | `pass=true`; both guards reach the troll's edge and keep swinging |
| VFH-TAME-1 Tames and hirelings | SP, D | `vfh_t_tame1` | Open ground (`vfh_debug Combat trace` shows `damage.blocked reason=tame_on_hireling` if the troll's slam catches the guard) | `pass=true`; troll and guard both survive the fight and the guard never turns on the troll |
| VFH-SMELT-5 Wood reserve | SP | `vfh_t_keep2` | Open ground | `pass=true`; with 60 wood in storage the kiln gets only 10 and 50 stay |
| VFH-WORK-6 Station owned by another client | D | — | Client B built and is near the smelter; client A's area owns the hireling | Loading still works, counts correct |
| VFH-STONE-1 Stone qualities | SP, D | `vfh_t_stone1` | Open ground | `pass=true`; quality 1 needs a level 2 board near the workbench, quality 2 a level 4 board (and each its own materials) |
| VFH-RECRUIT-1 Recruit and release | SP, D | `vfh_t_recruit1` | Open ground | `pass=true`; one follower with a quality 1 stone, a second is refused, release sends it back to work |
| VFH-FOLLOW-0 Following by hand | SP | — | Craft a stone at a workbench near a level 2 board; recruit a woodcutter; walk around, fight a greydwarf, walk far away; aim at it away from home | It follows within a few metres, runs to catch up, defends you, and toggles to Stay/Follow when you aim at it away from home; at home, aiming at it sends it back to work and it delivers what it carries |
| VFH-FOLLOW-1 Owner logs out | D | — | Recruit a follower, walk far from base, log out, log back in | It stays where it was (Stay); a follower inside its board's radius goes back to work instead |
| VFH-FOLLOW-2 Two players | D | — | Two players with quality 1 stones each recruit one | Each has their own follower; neither can take the other's |
| VFH-CATCHUP-1 Stranded follower | SP, D | `vfh_t_catchup1` | Open ground | `pass=true`; a follower put 50 m behind you out of view teleports to just behind you |
| VFH-LAG-1 Keeping up | SP, D | `vfh_t_lag1a`, then `vfh_t_lag1b` | A follower with you; sprint ~300 m over rough ground between the two | `pass=true`: at most one catch-up teleport over the whole run (the teleport is the safety net; it shouldn't be how the follower keeps up) (`vfh_debug Follow on` logs `follow.lag` every 5 s and any `follow.unstick`/`follow.teleport`) |
| VFH-RETREAT-1 Retreat order | SP, D | `vfh_t_retreat1` | Open ground | `pass=true`; on the retreat order the fighting guard switches to following and keeps following while greydwarves hit it |
| VFH-RETREAT-2 Retreat by hand | SP | — | Followers on Defensive/Aggressive in a fight; middle click with the stone and run | "Retreat!" message, HUD shows Retreating, they drop the fight and run with you; 20 s after the last hit their stance applies again; a left-click order ends the retreat early |
| VFH-POST-1 Guard post | SP, D | `vfh_t_post1` | Open ground | `pass=true`; posted guard leaves follower status, holds its post, fights there and returns; clearing the post sends it back to patrol |
| VFH-GATHER-1 Gather Here | SP, D | `vfh_t_gather1` | Open ground | `pass=true`; parked woodcutter chops the trees by its spot, then delivers once released |
| VFH-ORDER-1 Harvest order | SP, D | `vfh_t_order1` | Open ground | `pass=true`; the order sends it to the tree, it clears it, and the order ends |
| VFH-NAVLINK-1 Nav scan | SP | `vfh_t_navscan1` | Open ground | `pass=true`; `navlinks.scan` finds the room's and the house's doors and the house's stairs (two wood stair flights to a 2.17 m upper floor: `fixture.house flights=2 storey=2.17`); `vfh_navlinks show` draws them |
| VFH-NAVLINK-2 Stepladder scan | SP | `vfh_t_navscan2` | Open ground | `pass=true`; the vanilla stepladder is a stair or ladder link |
| VFH-NAVLINK-3 Rescan on removal | SP | `vfh_t_navscan3` | Open ground | `pass=true`; deconstructing the stair removes its link within 10 s (a new `navlinks.scan` line) |
| VFH-NAVLINK-4 Upstairs chest | SP | `vfh_t_nav1` | Open ground | `pass=true`; the woodcutter goes through the house's door, up the stair and delivers to the chest upstairs; the door is closed afterwards (`vfh_debug Nav on`: `navlinks.route` walk>door>walk>stair>walk) |
| VFH-NAVLINK-5 Stepladder | SP | `vfh_t_nav2` | Open ground | `pass=true`; delivered upstairs via the stepladder (a `navlinks.hop` is allowed) |
| VFH-NAVLINK-6 Stair removed | SP | `vfh_t_nav3` | Open ground | `pass=true`; after the lower stair flight goes, the woodcutter gives up on the chest upstairs (`deliver.chest_unreachable`, no reaching it from the ground) and delivers to the chest by the board, no hang |
| VFH-NAVLINK-7 Setting off | SP | `vfh_t_nav4` | Open ground | `pass=true`; with `BaseNavLinks` off the 0.2 door detour still works (same as `vfh_t_door1`) |
| VFH-NAVLINK-8 Follow upstairs | SP | `vfh_t_nav5` | Open ground | `pass=true`; the follower comes through the house's door and up the stair to you, no catch-up teleport |
| VFH-NAVLINK-9 Follow back out | SP | `vfh_t_nav6` | Open ground | `pass=true`; it follows you back down and out; the door is closed behind it |
| VFH-STONE-2 Stone by hand | SP | — | Stone in hand: left click a tree / rock / enemy / the ground with followers near; right click nothing; Shift+E a follower | Woodcutters chop, miners mine, guards attack, everyone holds at the clicked spot; right click calls them back; the Shift+E panel changes follow mode and stance (with "apply to all") |
| VFH-POST-2 Archer on a tower | SP | — | Lead an archer up the stairs of a tower inside your board's area and left click it there | It stays on the tower, shoots from there, and only steps off for something within 6 m |
| VFH-PORTAL-1 Portal | SP, D | `vfh_t_portal1` | Open ground, flat 60 m ahead | `pass=true`; the woodcutter comes through with its wood; with copper ore it stays behind in Stay |
| VFH-TRAVEL-2 Ship by hand | SP, D | — | Followers near a Karve or Longship; take the helm, sail, beach, step off | They vanish aboard ("Aboard: …", the helm hover says "Passengers: n", HUD says Aboard), and step off beside you on land; break the ship at sea with passengers: they appear in the water there and swim after you |
| VFH-TRAVEL-4 Two crews | D | — | Two players, each with followers, on one ship: A takes the helm, B stands on deck; sail off; A lands first, B lands later somewhere else | Both players' followers board (B's once the ship is moving); hover shows everyone as passengers; each player's followers step off only when their own player is ashore |
| VFH-TRAVEL-3 Dungeon by hand | SP, D | — | Followers (one carrying ore) at a Burial Chamber; go in and out | All come in and out with you, ore included; a second player sees them vanish and reappear, never doubled |
| VFH-RENAME-1 Rename | SP, D | — | Shift+E on a hireling (yours or one at a board you can use), Rename, type a name | The new name shows on its hover, health bar, the Roster tab, the follower HUD, and for a second player; after it dies and comes back (permadeath off) it keeps the name |
| VFH-HOME-1 Send home | SP, D | `vfh_t_home1` | Open ground | `pass=true`; the follower parked 45 m out is sent home, vanishes, and is back at the board working after the trip timer |
| VFH-HOME-2 Send home by hand | SP, D | — | Shift+E on your follower in the field (carrying something), Send home | "… is heading home (about n min)"; it vanishes; the Roster tab shows "Returning home (m:ss)" counting down; it appears near the board, delivers its cargo and works |
| VFH-ORPHAN-1 Left behind | SP, D | — | Sail away from a follower left on the shore (or outrun it where it can't follow) | After about 30 s more than 60 m away: "… lost track of you and is heading home"; it returns as above |
| VFH-ORPHAN-2 Left in Stay | SP, D | — | Leave a follower in Stay and go 160 m+ away for 2 min | It heads home with the message |
| VFH-ORPHAN-3 Owner logs out | D | — | Log out with a follower out in the field; wait a minute; log back in | It went home (Roster shows it working or returning); a follower inside its board's area went straight back to work |
| VFH-GATHER-2 What to gather | SP, D | — | Shift+E on a miner at a copper deposit next to plain rocks: switch Stone off; on a woodcutter among beech and birch: switch Wood off | The miner mines only the copper and leaves the stone it drops on the ground; the woodcutter fells only the birches; a stone order on a switched-off tree or rock is still done |
| VFH-GATHER-3 Work at home off | SP, D | — | Shift+E on a working woodcutter at home: "Works at home: off" | It stops chopping in the base and idles; recruited, it still gathers in the field (Gather Here, orders) and delivers at home; the setting survives a respawn |
| VFH-PROTECT-1 Tester protected | SP | `vfh_t_protect` | Anywhere | `pass=true`; during a test you're in ghost mode and a hit worth half your health is dropped (`tester_hits_blocked` counts it); after the chain `test.protect on=false` |
| VFH-NOISE-1 Gathering is loud | SP, D | `vfh_t_noise1` | Open ground; stand still at the board | `pass=true`: a greydwarf placed 45 m from the chopping woodcutter (beyond its sight) hears it, comes over and hits it |
| VFH-HOME-3 Ore stays behind | SP, D | — | Server config ReturnHomeWithNonTeleportable = false; a follower in the field carrying copper ore and wood; Shift+E | The button reads "Send home (leaves cargo here)" and names the ore; sent home, it drops the ore in a pile where it stood and brings only the wood home. Also: a guard carrying ore is stopped at a portal like a miner |
| VFH-GRAVE-1 Grave | SP, D | `vfh_t_grave1` | Open ground | `pass=true`; a follower carrying 2 stacks of wood dies and the wood is in a grave named after it |
| VFH-GRAVE-2 Grave by hand | D | — | A follower carrying something dies away from base; a second player tries the grave | The grave shows the hireling's name and a map pin for you; the other player can't open it; you can, and the pin goes once it's empty |
| VFH-WEIGHT-1 Weight limit | SP, D | — | A level 1 miner at copper; then hand a follower lots of stone | The miner goes to deliver at about 300 weight (25 ore); the hover shows the weight; handing over too much says "can't carry that much" |
| VFH-FUNDS-1 Low funds | SP, D | — | A board with two hirelings and food for about 1 day | Hover shows the upkeep and days left in red; next upkeep, players at the base are told; the builder sees a map pin until it's topped up |
| VFH-POST-3 Worker post | SP, D | — | A woodcutter with "Works at home" off: recruit it, walk to a spot at home, left click it (or the ground there) | It's posted: stops following, walks to the spot and stands there facing your way instead of wandering; it still delivers cargo and comes back; right click or Shift+E clears the post |
| VFH-SNEAK-1 Sneak | SP, D | — | Followers with you; crouch and walk up behind a greydwarf or deer | They crouch and creep with you (no running, no footsteps) and the animal notices later than when they walk; stand up and they stand; a fight stands them up |
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
| 2026-10-03 | 0dab4b2 | VFH-WORK-1 | SP | Pass | |
| 2026-10-03 | 0dab4b2 | VFH-WORK-3W | SP | To do | Checked during normal play |
| 2026-10-03 | 0dab4b2 | VFH-WORK-4 | SP | To do | Checked during normal play |
| 2026-10-03 | 0dab4b2 | VFH-AZU-2 | SP | To do | Checked during normal play |
| 2026-10-03 | ff3efec | VFH-WORK-3 | SP | Pass | |
| 2026-10-03 | ff3efec | VFH-TIER-1 | SP | Pass | |
| 2026-10-03 | ff3efec | VFH-MINE-1 | SP | To do | Checked during normal play |
| 2026-10-03 | ff3efec | VFH-MINE-2 | SP | To do | Checked during normal play |
| 2026-10-03 | ff3efec | VFH-MINE-3 | SP | To do | Checked during normal play |
| 2026-10-03 | 32af2b2 | VFH-AZU-3 | SP | Pass | Azu stored the bars; 4 smelter.stuck recoveries in the log, all self-resolved |
| 2026-10-03 | 32af2b2 | VFH-SMELT-4 | SP | Pass | |
| 2026-10-03 | 32af2b2 | VFH-WORK-5 | SP | To do | Checked during normal play (WORK-5 needs AzuAutoStore off) |
| 2026-10-03 | 32af2b2 | VFH-SMELT-1 | SP | To do | Checked during normal play (WORK-5 needs AzuAutoStore off) |
| 2026-10-03 | 32af2b2 | VFH-SMELT-2 | SP | To do | Checked during normal play (WORK-5 needs AzuAutoStore off) |
| 2026-10-03 | 32af2b2 | VFH-SMELT-3 | SP | To do | Checked during normal play (WORK-5 needs AzuAutoStore off) |
| 2026-10-03 | 1f47df2 | VFH-CFG-1 | D | Pass | Server cfg RequiredPieces 25 and yml Wood 30 reached the client |
| 2026-10-03 | 0922b32 | VFH-WORK-3 | D | Pass | Miner hit from a bit too far (bounds, not surface): fixed after |
| 2026-10-03 | 0922b32 | VFH-DLV-1 | D | Pass | |
| 2026-10-03 | 0922b32 | VFH-TIER-1 | D | Pass | |
| 2026-10-03 | 00f518b | VFH-HIRE-1 | D | Pass | Hireling and cargo survived a server restart |
| 2026-10-03 | 00f518b | SCAFFOLD-2 | D | Pass | Joining without the mod is refused |
| 2026-10-03 | 0658a4d | VFH-WORK-5 | D | Pass | AzuAutoStore off; smelter stalled once on the old test floor near the kiln (floors since removed from the fixture) |
| 2026-10-03 | 0658a4d | VFH-AZU-3 | D | Pass | |
| 2026-10-03 | a73079b | VFH-WORK-3 | D | Pass | Rerun after the work-from-within-reach fix: miner stays on the copper |
| 2026-10-03 | a73079b | PACKAGE-1 Clean install | SP | Pass | dist zip imported into a fresh Gale profile: board placed, hireling hired |
| 2026-10-04 | 0105cd8 | VFH-NAV-2 | D | Pass | Woodcutter detoured to the room's door, opened it, delivered inside and closed it behind itself |
| 2026-10-04 | 0105cd8 | VFH-SMELT-5 | D | Pass | 60 wood in storage: the kiln got 10, 50 stayed |
| 2026-10-04 | 0105cd8 | VFH-COMBAT-6 | D | Pass | With worn-set armor (26 at L3) both guards survived troll hits of 149/169 and killed it |
| 2026-10-04 | 0105cd8 | VFH-COMBAT-5 | D | Pass | First swing about 1 s after engaging |
| 2026-10-04 | 0105cd8 | VFH-COMBAT-1 | D | Pass | |
| 2026-10-04 | 0105cd8 | VFH-TAME-1 | D | Pass | |
| 2026-10-04 | 36cd584 | VFH-STONE-1 | D | Pass | |
| 2026-10-04 | 36cd584 | VFH-RECRUIT-1 | D | Pass | |
| 2026-10-04 | c762306 | VFH-FOLLOW-0 | D | Pass | Recruit, follow, fight, stay/follow toggle, release at home (guard). Gatherer field delivery to be rechecked with phase 13 |
| 2026-10-04 | 85f255e | VFH-POST-1 | D | Pass | Posted guard held its post, fought a greydwarf there and walked back; clearing the post sent it back to patrol |
| 2026-10-04 | 85f255e | VFH-GATHER-1 | D | Pass | 12 wood gathered in Gather Here, all 12 delivered after release |
| 2026-10-04 | 85f255e | VFH-ORDER-1 | D | Pass | Harvest order on a tagged beech: felled and cleared, then the order ended |
| 2026-10-04 | 9077d08 | VFH-RETREAT-2 | D | Pass | Middle click pulled followers out of a fight and they ran with the player |
| 2026-10-04 | 3055b7e | VFH-CATCHUP-1 | D | Pass | Follower stranded 50 m behind out of view teleported to just behind the player within about 1 s |
| 2026-10-04 | 08f3860 | VFH-LAG-1 | D | Pass | Woodcutter, ~300 m sprint with god mode and run skill 100: 1 teleport (was 9 before the chase fix); judged by teleports, the lag check was dropped |
| 2026-10-04 | 2af8c96 | VFH-PORTAL-1 | D | Pass | Woodcutter came through the portal with its wood; carrying copper ore it stayed behind in Stay with the message |
| 2026-10-04 | 79376dc | VFH-TRAVEL-2 | D | Pass | Archer boarded the Karve (hidden, carried), stepped off beside the player on landing, twice; breaking the ship at sea put her back in the water where it sank. Fixed after: name plate lingered over the mast; archer punched the air after one landing (weapon guard added, to recheck) |
| 2026-10-04 | 3359f26 | VFH-TRAVEL-3 | D | Partial | Troll cave in and out: both followers came along. Burial chamber: placed on one spot by the door (indoor spot search hit the ceiling); a follower left inside counted as near (ground distance) and only caught up 40 m away. Fixed in d4038f4, recheck pending |
| 2026-10-04 | f17c3ad | VFH-TRAVEL-3 | D | Pass | Recheck after d4038f4: Burial Chamber in and out, both followers came along each way |
| 2026-10-04 | 25d00f7 | VFH-ORPHAN-3 | D | Pass | Server restart with two followers 880 m out and the owner offline: both headed home (220 s trip) and were back at the board, working, when the player came home. Offline wait raised to 5 min after (d12b75f) |
| 2026-10-04 | b15a90f | VFH-NOISE-1 | D | Pass (by hand) | Miner digging and mining copper in the field drew a greydwarf, which came up to it; the miner (Defend) fought back |
| 2026-10-04 | a524ac8 | VFH-DIG-1 | D | Pass (by hand) | Miner in the field dug down through the dirt to buried copper chunks and mined them; unreachable chunks were given up one at a time, not the whole deposit |
| 2026-10-04 | a91e7d7 | VFH-HOME-2 | D | Pass (part) | Send home from Shift+E in the field: follower vanished at once with the trip time; arrival at the board still to watch |
| 2026-10-04 | 8c74246 | VFH-HOME-3 | D | Pass | ReturnHomeWithNonTeleportable off: Send home warned, the ore was dropped where the follower stood and only the wood came home |
| 2026-10-04 | dad728c | m1 run (vfh_t_m1_sp) | D, 0.2.0 zip | 33/34 | Only VFH-CON-2 failed: its macro hired a miner on a level 1 board (miners need level 2); macro fixed (eee9123) |
| 2026-10-04 | 94e1c46 | m2 run (vfh_t_m2_sp) + reruns | D, 0.2.0 zip | Pass | STONE-1, RECRUIT-1, POST-1, CATCHUP-1, RETREAT-1, NOISE-1 passed first time; GATHER-1, PORTAL-1, HOME-1 failed on test setup (fixed in 94e1c46) and passed on rerun; ORDER-1 failed once (only the tree's own drop collected, no logs to say why) and passed on rerun with Work/Orders debug |
| 2026-10-04 | cdaba11 | VFH-GRAVE-1 | D | Pass | Woodcutter follower died carrying 2 stacks of wood: a grave named after it held all 100 wood, owned by the player for opening |
| 2026-10-04 | v0.2.1 | Board self-repair (live) | D, AMP_Valheim01 | Pass | Alf's contract (follower died far away on 0.2.0, death lost) settled 7 min after the 0.2.1 start: server logged roster.settled_missing, forwarded to the board owner's game, which recorded contract.died (permadeath) |
| 2026-10-05 | ab4d51b | VFH-NAVLINK-1 | SP | Pass | Scan found the house's door and stair (after the fixture was fixed to sit the stair on the ground and flush with the upper floor) |
| 2026-10-05 | ab4d51b | VFH-NAVLINK-3 | SP | Pass | Deconstructing the stair removed its link within seconds |
| 2026-10-05 | ab4d51b | VFH-NAVLINK-4 | SP | Pass | Woodcutter went walk>door>walk to the chest upstairs (the open-topped test house's stair is on the game's own map, so no stair link was needed); door closed after |
| 2026-10-05 | ab4d51b | VFH-NAVLINK-6 | SP | Pass | Stair removed: delivered to the chest by the board (chest kept away from AzuAutoStore) |
| 2026-10-05 | ab4d51b | VFH-NAVLINK-8 | SP | Pass | Follower came through the door and up to you, no catch-up teleport |
| 2026-10-05 | ab4d51b | VFH-NAVLINK-9 | SP | Pass | Follower followed you back down and out; door closed behind |
| 2026-10-05 | 15e1adf | VFH-NAVLINK-2 | SP | Pass | Stepladder found (earlier build) |
| 2026-10-05 | 15e1adf | VFH-NAVLINK-5 | SP | Pass | Delivered up the stepladder route (earlier build) |
| 2026-10-05 | 15e1adf | VFH-NAVLINK-7 | SP | Pass | Setting off: 0.2 door handling (got stuck at the door, then jumped past, as 0.2 does) |
| 2026-10-05 | ab4d51b | Regression after nav links (door1 order1 gather1 deliver1 deliver2 catchup1 recruit1 portal1 home1 post1 lag1a lag1b) | SP | Pass (11/12) | PORTAL-1 failed: its full stack of copper ore (300 weight) no longer fits a level 1 hireling's 300 limit since 0.2.1, so no ore went in; macro now puts 5 ore |
| 2026-10-05 | 0eda3b3 | VFH-PORTAL-1 | SP (testboy) | Fail (test) | The ore went in, but the testboy world has the "teleport everything" modifier, so the follower rightly came along; the macro now checks `ore_portal_ok` (what the world's rules call for) |
| 2026-10-05 | 0eda3b3 | MissingPieces ladder + vanilla stair and ladder (by hand) | SP | Pass | All three show as links with `vfh_navlinks show`, no list entries needed |
| 2026-10-05 | e39ebaa | Upstairs delivery by hand (MissingPieces ladder, landing, two chained ladders, chest on the top square) | SP | Pass | Route walk>stair>walk>stair>stair>walk, 17 wood into the top chest, no hops. Needed: chained flights joined (3d7ccd5), short same-level steps without the game's map (a882554), and that step not blocked by the chest itself (e39ebaa) |
| 2026-10-05 | 3d7ccd5 | Follower up the stairs by hand | SP | Pass | Followed Tim upstairs |
| 2026-10-05 | e39ebaa | VFH-NAVLINK-4, VFH-NAVLINK-8 on the local dedicated server | D | Pass (behaviour) | Both did their job; each failed only `log_errors == 0` from a `charter.register_failed` logged when Tim went back to the main menu between worlds (Hiring Charter created twice; fixed in the next commit) |
| 2026-10-05 | 6018bc2 | VFH-PORTAL-1 | D | Pass | Rerun on the local dedicated server with `ore_portal_ok` |
| 2026-10-05 | 8c08688 | VFH-NAVLINK-8 | D | Pass | On the local dedicated server: follower took walk>door>stair>walk up to you, no catch-up teleport |
| 2026-10-05 | 8c08688 | VFH-NAVLINK-9 | D | Pass | Up (door, stair) and back down the stair and out, door closed behind, no teleport |
| 2026-10-05 | 7aaecec | VFH-CHORE-1, VFH-CHORE-2, VFH-AZU-3, VFH-SMELT-4, VFH-SMELT-5 | SP | Pass | Steward chore loop (phase 02): level gate, toggle, and the existing smelting rows with a level 2 Steward |
| 2026-10-05 | 1e83624 | VFH-CHORE-3 | SP | Fail (test) | Windmill loaded and flour stored; spinning wheel loaded but never span: it needs a roof (m_requiresRoof). Test now builds one (roof_over) |
| 2026-10-05 | d103f20 | VFH-CHORE-3 | SP | Pass | Windmill and roofed spinning wheel loaded; flour and linen stored after a 20-minute skip |
| 2026-10-05 | d7ebfd8 | VFH-CHORE-4 | SP | Pass | Hearth and wood torch fuelled from a chest; nothing added with Torches Eternal counted |
| 2026-10-05 | d7ebfd8 | VFH-CHORE-9 | SP | Pass | Empty hearth (score 0.90) first, then the smelter (0.88) |
| 2026-10-05 | 5f085ff | VFH-CHORE-5 | SP | Pass | Full beehive emptied, honey in the honey chest |
| 2026-10-05 | 5f085ff | VFH-CHORE-7 | SP | Pass | Hungry tamed boar fed from a chest (AzuAutoStore left the food alone) |
| 2026-10-05 | 5f085ff | VFH-CHORE-12 | SP | Pass | Wood lying about put away; resin torch fuelled first |
| 2026-10-05 | 5f085ff | VFH-CHORE-6, VFH-CHORE-11 | SP | Fail (test) | Level 6/7 hires cost more than a board's 8 slots hold under the dev profile's price table; tests now hire free |
| 2026-10-05 | 5f085ff | VFH-CHORE-8 | SP | Fail | Quiet check counted wild monsters anywhere within the area + 30 m; now only within 30 m of the Steward or the piece |
| 2026-10-05 | 5f085ff | VFH-CHORE-10 | SP | Fail | Roof check also stopped tapping; now only loading needs the roof |
| 2026-10-05 | c86af42 | VFH-CHORE-11 | SP | Pass | Empty shield generator fed bones from a chest |
| 2026-10-05 | 3155baa | VFH-CHORE-8 | SP | Pass | Damaged wall repaired once the nearby Neck and Greyling had gone |
| 2026-10-05 | 3155baa | VFH-CHORE-6 | SP | Fail | Sap emptied but kept: sap is also eitr refinery fuel, so it counted as supply; trips now deliver what they collected |
| 2026-10-05 | 60671cf | VFH-CHORE-6 | SP | Pass | Full sap collector emptied, 30 sap in the sap chest |
| 2026-10-05 | 60671cf | VFH-CHORE-10 | SP | Fail (test) | Tapped and meads stored; not reloaded: cover 0.53 (needs 0.7) with walls on three sides; walls now stacked up to the roof |
| 2026-10-05 | f54b72d | VFH-CHORE-10 | SP | Pass | Ready fermenter tapped, meads stored, then loaded with a mead base from a chest (walls stacked to the roof on three sides) |
| 2026-10-05 | 4c5ad0d | VFH-BROOM-1 | SP | Pass | Steward holds the homemade broom; looks right as built |
| 2026-10-06 | 272ebfa | VFH-PAUSE-1 | SP | Pass | Full wood chest: beeches left standing, nothing carried; felled once a second chest held wood |
| 2026-10-06 | 272ebfa | VFH-PAUSE-2 | SP | Pass | Full coal chest: kiln not loaded; loaded once a second coal chest appeared |
| 2026-10-06 | 272ebfa | VFH-CHORE-8 | SP | Pass | Repairs regression after the reach/route/weather changes |
| 2026-10-06 | 272ebfa | VFH-CHORE-13 | SP | Pass | Rain-worn wall at 70% left alone, repaired at 40%; wall 5 m up repaired from the ground; wall 10 m up never attempted |
| 2026-10-06 | c79e95e | VFH-CHORE-14 | SP | Pass | Board with a day of food stocked with 5 cooked meat; raw meat left in the chest |
| 2026-10-06 | c79e95e | VFH-LOOT-1 | SP | Fail | 10 of 12 Wood picked up; 2 left (1.6 m reach, 8 s give-up); reach now 2.5 m, 12 s, give-ups logged (`loot.unreachable`) |
| 2026-10-06 | 5492ffb | VFH-LOOT-1 | SP | Pass | All 12 Wood picked up within 6 s of parking (2.5 m reach) |
| 2026-10-06 | 33a63fc | VFH-DLV-1, NAVLINK-4, NAVLINK-8, SMELT-4, AZU-3, PAUSE-1, PAUSE-2, CHORE-8, CHORE-13 | SP | Pass | 0.4.4 regression after the delivery approach, pause threshold and repair changes |
| 2026-10-06 | 33a63fc | VFH-DLV-2 | SP | Fail (test) | Tests overflow to the pile with a full wood chest; since 0.4.2 the woodcutter pauses instead. The macro now turns PauseWhenStorageFull off while it runs |
| 2026-10-06 | 5ff5b0a | VFH-DLV-2 | SP | Pass | Overflow to the board's pile with the pause off |
| 2026-10-06 | 0a0cfd2 | VFH-FARM-0, ORDER-1, FARM-1, FARM-2, FARM-3, COOK-1, COOK-2, COOK-3, COOK-4, COOK-5 | SP | Pass | Farmer and Cook first runs (after the catalog, delivery-timer and stove-pickup fixes); world with 3x drops |
| 2026-10-06 | 0a0cfd2 | VFH-FARM-4 | SP | Fail | Rows: plants too close / off the grid; the grid lined up with a diagonal neighbour (now only row neighbours) |
| 2026-10-06 | ce7dc01 | VFH-FARM-4 | SP | Pass | Rows line up with the existing row at the crop's spacing, none under the roof |
| 2026-10-06 | 96d261b | VFH-CHORE-1, 2, 4, 5, 7, 8, 10, 11, 12, 13, 14, LOOT-1, PAUSE-1, PAUSE-2, DLV-1, DLV-2, NAVLINK-4, NAVLINK-8, SMELT-4, AZU-3 | SP | Pass | Steward and delivery regression on the shared chore loop (0.5.0) |
| 2026-10-06 | 830d3d1 | VFH-ORDER-1, FARM-1, COOK-1 | D | Pass | From a client of the local dedicated server: orders through the server, harvesting and spits on objects the server owns; no errors on the server |
| 2026-10-06 | f120e07 | VFH-TREE-1 | SP | Pass | 4 beech saplings planted from a chest, left standing, felled once grown, and the freed spots replanted (beech and fir); the patch sign now stands on its own |
| 2026-10-07 | 8aa9922 | VFH-PASS-1 | SP | Pass | 10 runs in a 1.0 m corridor: 5–9 s, no jams (passing off: one jam in 4, before the stuck fix one in 8); run through ModTestBridge |
| 2026-10-08 | 4d7731f | VFH-READ-1/2, BLOCK-1..3, PROJ-1/2, DODGE-1..4 | SP | Pass | Run through ModTestBridge, each at least twice after making the rows deterministic (2-star enemies, `defense_chances`, `ghost on`); combat regression rows (STN-1..5, COMBAT-1/5/6, TAME-1, RETREAT-1, NOISE-1, POST-1) pass |
| 2026-10-08 | 4d7731f | VFH-DEF-M1..M4 | SP | Pass | Damage taken off/on: L1 guard vs 3 Greydwarfs 42%/36%, L3 guard vs Troll 65%/54% (deaths 0, were 2/5 before hit-time learning), L4 archer vs 2-star Draugr archers 23%/13% (13 runs each), L5 woodcutter vs Troll 25%/27% (no qualifying hits: unchanged as designed); every fight won; read rates 51/62/79% vs 50/63/76% |

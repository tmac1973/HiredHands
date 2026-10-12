# Bug reports and commands

## Why isn't it working?

- **Shift+E on a hireling** opens its panel; the **Problems** list there says why its orders or chores aren't being met
  (no seeds, a missing ingredient, a pen too crowded to breed, a torch out of reach, not enough food for the board).
- **Its hover** and the board's Roster tab say what it's doing, or what it's waiting for ("Paused: no room left for Wood").
- **Can't find it?** *Call to board* on the board's Roster tab brings it back.

## Logs

Logs go to `BepInEx/LogOutput.log` and `BepInEx/HiredHands.log`, one line per event (`[VFH] … evt=…`). On a server,
`ClientLogsToServer` collects every player's Hired Hands log on the server too, so one place has the whole story.

These need no cheats:

- `vfh_debug All on` turns on detailed logging;
- `vfh_log_mark <text>` adds a marker, to find a moment in the log;
- `vfh_dump_state` logs every board and hireling;
- `vfh_crops`, `vfh_recipes` list what the Farmer and Cook know;
- `vfh_navlinks show` draws the doors, stairs and ladders the board found.

Report bugs on [GitHub](https://github.com/tmac1973/HiredHands/issues) with what happened, roughly when, and the logs.

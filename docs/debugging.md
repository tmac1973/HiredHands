# Debugging VikingsForHire

## Where the logs are

| Mode | Files |
|---|---|
| Single-player / client | `~/.local/share/com.kesomannen.gale/valheim/profiles/vikingsforhire-dev/BepInEx/VikingsForHire.log` and `.../BepInEx/LogOutput.log` |
| Dedicated server | `/games/SteamLibrary/steamapps/common/Valheim dedicated server/BepInEx/VikingsForHire.log` and `.../BepInEx/LogOutput.log` |

`VikingsForHire.log` holds only this mod's lines and is appended to across sessions. It rotates at
`LogFileMaxMB` (default 10 MB) to `VikingsForHire.1.log` … `.3.log`. The same lines also appear in `LogOutput.log`.

## Reporting a bug

1. Open the console (F5) and run `vfh_log_mark before <short description>`.
2. Reproduce the bug.
3. Run `vfh_log_mark after <short description>` and `vfh_dump_state`.
4. Send both log files from that run (client, plus server when on the dedicated server) and the mark text.

These diagnostics commands don't need `devcommands`.

## Line format

```
[VFH] t=1834.2 f=99120 role=SP lvl=D cat=Work evt=deliver.plan hid=3f2a item=Wood n=40
```

- `t` game time (s), `f` frame, `role` = `SP` (single-player), `HOST`, `CLIENT`, `SERVER` (dedicated) or `MENU`.
- `lvl` = `E`rror, `W`arning, `I`nfo, `D`ebug, `T`race. `cat` is the area, `evt` the event.
- Long ids (`hid`, `board`, `contract`) are shortened to 4 characters. The first time an id appears, an
  `evt=id.map` line gives the full id.
- Every session starts with `evt=session.header` (mod and game version, role, world), `evt=session.mods`
  (every loaded plugin) and a `config:` line with every effective setting.

## Verbosity

Error, Warning and Info always log. Debug and Trace are per category:

- `vfh_debug All on` turns Debug on everywhere; `vfh_debug Work trace` adds Trace for one category;
  `vfh_debug All off` goes back to Info only. The choice is saved to `[7 - Debug]` in the cfg.
- Categories: Core, Data, Board, Placement, Roster, Payment, Hireling, AI, Combat, Work, Deliver, Smelter, Follow,
  Orders, Travel, Orphan, Net, Compat, UI, Perf, Test.

## Useful commands

| Command | What it does |
|---|---|
| `vfh_log_mark <text>` | Writes `evt=mark text=…` so a repro is easy to find |
| `vfh_debug <cat\|All> <on\|off\|trace>` | Changes verbosity |
| `vfh_dump_data` | Prints the data tables and settings in effect (the server's when connected) |
| `vfh_dump_state` | Logs the session header plus every loaded board and hireling (later phases) |

## Grepping

```bash
grep 'lvl=E' VikingsForHire.log
grep 'evt=test.result' VikingsForHire.log
grep -A50 'text="before chest bug"' VikingsForHire.log
```

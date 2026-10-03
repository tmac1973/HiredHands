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

If a row fails: fix it, rerun that row in every listed mode, then rerun SCAFFOLD-1.

## Results

| Date | Build (commit) | Row | Mode | Result | Notes |
|---|---|---|---|---|---|
| 2026-10-03 | c16fbd1 | SCAFFOLD-1 | SP | Pass | Plugin loaded; `vfh_t_smoke` broadcast shown |
| 2026-10-03 | c16fbd1 | SCAFFOLD-1 | D | Pass | Server loaded plugin (10 loaded, 0 failed); client joined and spawned |
| 2026-10-03 | c16fbd1 | SCAFFOLD-2 | D | Deferred | To run after a few phases are in |

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
  Join via *Join game → Add server* `127.0.0.1:2456`, password `vikings`.
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

If a row fails: fix it, rerun that row in every listed mode, then rerun SCAFFOLD-1.

## Results

| Date | Build (commit) | Row | Mode | Result | Notes |
|---|---|---|---|---|---|

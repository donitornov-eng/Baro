---
name: Barotrauma Mod Testing
description: Deploy, launch and verify the Global Overhaul content package - LocalMods junction, where client/server logs are, console commands for spawning and reloading, and the pass/fail checklist for "the mod loaded without errors". Use after changing C#, XML or config, or when the mod does not appear in game.
---

# Testing the mod

## 1. Deploy

The repository is already junctioned into the game:

```
D:\Steam\steamapps\common\Barotrauma\LocalMods\GlobalOverhaul -> G:\OpenCode\BOGM
```

Re-create or remove it with:

```
pwsh tools/deploy.ps1
pwsh tools/deploy.ps1 -Remove
```

No copying needed - the game reads the live files. LuaCs must be enabled in the
mod list as well (workshop id `2559634234`), otherwise `CSharp/Shared` is never
compiled.

## 2. Launch

- Client: `D:\Steam\steamapps\common\Barotrauma\Barotrauma.exe`.
  LuaCs ships a patched client at
  `...\workshop\content\602960\2559634234\Binary\Barotrauma.exe`; if the LuaCs
  settings menu/icon does not appear in game, launch that binary instead.
- Dedicated server: `D:\Steam\steamapps\common\Barotrauma\LuaCsDedicatedServer.bat`
  (it points at the LuaCs binary in `WorkshopMods\Installed\2559634234`).

## 3. Where the logs are

| log | path |
|---|---|
| dedicated server runs | `D:\Steam\steamapps\common\Barotrauma\ServerLogs\*.txt` |
| mod/plugin logs | `%LOCALAPPDATA%\Daedalic Entertainment GmbH\Barotrauma\ModConfigs\*.log` |
| crash | `D:\Steam\steamapps\common\Barotrauma\crashreport.log`, `servercrashreport.log` |
| client debug console | in game, `F3` |

The plugin writes everything with the `[GlobalOverhaul]` prefix:

```
Select-String -Path "D:\Steam\steamapps\common\Barotrauma\ServerLogs\*.txt" -Pattern 'GlobalOverhaul|Global Overhaul'
```

## 4. In-game verification commands

| command | what it proves |
|---|---|
| `go_status` | rules loaded, toggles and effective multipliers |
| `go_reload` | `Config/*.xml` re-read without restart |
| `go_apply` | balance rules re-applied to existing items |
| `spawn go_crawlerelite` | the character variant exists and spawns |
| `spawn <item identifier>` | a new item prefab is registered |

Also useful vanilla commands: `itemlist`, `characterlist`, `teleport`, `money`.

## 5. Pass/fail checklist

- [ ] Package appears in the mod list as "Global Overhaul" and can be enabled
- [ ] No XML error for any `filelist.xml` entry (missing file / wrong root element)
- [ ] Log contains `[GlobalOverhaul] Installed and verified N patch target(s)` with
      the expected N, and `Ready. Rules: ...`
- [ ] No `MissingMethodException`, no Roslyn compile diagnostics, no Harmony
      "patch target not found" warnings
- [ ] `go_status` reports the toggles from `Config/Settings.xml`
- [ ] Reload cycle: disable/enable the package in the mod list, confirm the log
      shows patches installed again and no duplicate-patch warnings
- [ ] Restart the game and repeat - a clean second load proves idempotency

## 6. Common failures

| symptom | cause |
|---|---|
| mod listed but nothing changes | Roslyn compile error in `CSharp/Shared` - search the log for `error CS` |
| `Could not resolve patch target` | game version drift - check `_ref/` is current (`pwsh tools/refresh-ref.ps1 -Force`) |
| settings always default | `Config/Settings.xml` missing or a `Name` mismatch with `Bind("...")` |
| duplicate effects after reload | state mutated twice, or two Harmony instances |
| item spawns but is invisible | `Sprite`/`sourcerect` wrong or texture path not relative to `Content/` |
| entity shows a raw key as its name | missing `entityname.<identifier>` in `Text/*.xml` |

---
name: Global Overhaul Project
description: Conventions and layout of this specific mod repository - where code and config live, which Harmony patches already exist, how rules and settings are loaded, and the non-negotiable rule that all balance values come from config. Load before changing anything in G:\OpenCode\BOGM.
---

# Global Overhaul - repository conventions

Content package `Global Overhaul` (gameversion 1.13.5.0), hybrid XML + in-memory C#
via LuaCs. Working directory: `G:\OpenCode\BOGM`, junctioned to
`<game>\LocalMods\GlobalOverhaul`.

## Layout

```
filelist.xml                     content package manifest
ModConfig.xml                    LuaCs wiring: CSharp/Shared + Config/Settings.xml
CSharp/Shared/
  GlobalOverhaulPlugin.cs        IAssemblyPlugin entry point, patch plan table, logging
  LiveSettings.cs                ISettingBase<T> fields bound to Config/Settings.xml
  Rules.cs                       XML rule tables (stock/prices/enemies/balance) + reload
  CompilePls.cs                  IgnoresAccessChecksTo attributes
  Patches/                       (expected) one class per patched vanilla area
Config/
  Settings.xml                   toggles + global multipliers (LuaCs settings menu)
  TraderStock.xml                per-item stock rules          (go_reload)
  Prices.xml                     per-item price rules          (go_reload)
  EnemySpawns.xml                spawn rules                   (go_reload)
  BalanceRules.xml               damage/condition rules        (go_reload)
Content/
  Items/GlobalOverhaulWeapons.xml
  Characters/GlobalOverhaulCrawlerElite.xml
Text/English.xml, Text/Russian.xml
_ref/                            decompiled game + LuaCs sources (generated)
tools/                           refresh-ref.ps1, deploy.ps1
```

`CSharp/Client` and `CSharp/Server` exist in the plan but are currently unused -
`ModConfig.xml` compiles only `CSharp/Shared` for both sides. If you add
side-specific code, add the corresponding `<Assembly Folder=... Target="Client|Server">`
entry, because the legacy auto-discovery paths are not active while
`ModConfig.xml` exists.

## Current patch targets (GlobalOverhaulPlugin.PreInitPatching)

| Target | Patch class | Purpose |
|---|---|---|
| `Item..ctor(Rectangle, ItemPrefab, Submarine, bool, ushort)` | `BalancePatches` (postfix) | apply item balance on creation |
| `Location.StoreInfo.CreateStock()` | `TraderPatches` (postfix) | add/remove stock |
| `Location.LoadStores(XElement)` | `TraderPatches` (postfix) | per-location store tweaks |
| `Location.StoreInfo.GetAdjustedItemBuyPrice(ItemPrefab, PriceInfo, bool)` | `TraderPatches` (postfix) | buy price multiplier |
| `Location.StoreInfo.GetAdjustedItemSellPrice(ItemPrefab, PriceInfo, bool)` | `TraderPatches` (postfix) | sell price multiplier |
| `MonsterEvent.InitEventSpecific(EventSet)` | `EnemyPatches` (postfix) | monster substitution |
| `Projectile.Shoot(...)` | `BalancePatches` (prefix) | projectile damage scaling |

The plugin resolves every target up front, verifies registration via
`Harmony.GetPatchInfo`, and unpatches everything on failure. Keep that property:
a new patch goes into the `plans` list, not into an ad-hoc `PatchAll()`.

## Non-negotiable rules

1. **No hard-coded balance numbers.** Damage, prices, HP, spawn counts and stock
   come from `Config/*.xml` or `Config/Settings.xml`. If a value cannot be
   configured, add a setting first.
2. **Read settings live** (`LiveSettings` properties read `.Value` on each call) so
   the LuaCs settings menu takes effect without a reload.
3. **Rules reload must be atomic**: `Rules.LoadAll` builds new lists and swaps them
   under a lock; keep that pattern.
4. **Every patch is idempotent** - reloading the package must not double-apply.
5. **Log with `[GlobalOverhaul]`** via `GlobalOverhaulPlugin.Log/LogWarning/LogError`.
6. **Do not patch what XML can do.** Store availability and prices are expressible
   with `<Price storeidentifier=... />` (see the `barotrauma-xml` skill). Reserve
   C# for logic XML cannot express.
7. Before writing a signature, check `_ref/` (see `barotrauma-ref`). After changing
   content, verify with the `barotrauma-test` checklist.

## Build / verify loop

There is no build step. Edit sources -> restart the game (or reload the package) ->
read the log. Use `pwsh tools/refresh-ref.ps1 -Force` only when the game or LuaCs
was updated.

## Open items (foundation stage)

- `CSharp/Shared/Patches/` classes referenced by the plugin (`TraderPatches`,
  `EnemyPatches`, `BalancePatches`) are not in the repo yet - the plugin will not
  compile until they exist.
- `Config/TraderStock.xml`, `Prices.xml`, `EnemySpawns.xml`, `BalanceRules.xml`
  are optional (missing files log a warning) but are the intended tuning surface.
- `Content/Items/GlobalOverhaulWeapons.xml` is an empty container, waiting for
  stage 4 content.

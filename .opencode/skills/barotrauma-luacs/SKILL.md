---
name: Barotrauma LuaCs C# Modding
description: Write or fix in-memory C# for Barotrauma via LuaCsForBarotrauma - ModConfig.xml assembly wiring, IAssemblyPlugin lifecycle, injected services, settings files, console commands, Harmony patch patterns and reload safety. Use for anything in CSharp/Shared, CSharp/Client, CSharp/Server or ModConfig.xml.
---

# LuaCs C# modding (Barotrauma 1.13.5, LuaCs 1.0.117)

LuaCs compiles `.cs` files **in memory at load time** with Roslyn. There is no
DLL to build: edit the sources, restart (or reload the package), read the log.
API reference: `_ref/luacs/Barotrauma.LuaCs/`.

## ModConfig.xml

```xml
<ModConfig>
  <Assembly Folder="%ModDir%/CSharp/Shared" Target="Any" IsScript="true"
            FriendlyName="GlobalOverhaulAssembly" />
  <Config File="%ModDir%/Config/Settings.xml" />
</ModConfig>
```

Verified attributes (`ModConfigFileParserService.cs`):

| attribute | values | meaning |
|---|---|---|
| `File` / `Folder` | path | one file, or every `*.cs`/`*.dll` recursively |
| `IsScript` | `true`/`false` | compile `.cs` sources vs load `.dll` |
| `Target` | `Any`, `Client`, `Server` | where it runs |
| `Platform` | `Any`, `Windows`, `Linux`, `Mac` | |
| `FriendlyName` | string | assembly name; **all sources with the same name compile into one assembly** |
| `UseInternalAccessName` | bool | compile with internal access |
| `IsReferenceModeOnly` | bool | only add as a compile reference |
| `LoadPriority` | int | ordering |
| `IsFileRequired`, `Optional`, `Name` | | file/dependency handling |

`<Config File=...>` files are parsed as settings and must exist (default
`IsFileRequired="true"`), otherwise the package fails to load.

**If `ModConfig.xml` exists, LuaCs uses it exclusively** and skips legacy
auto-discovery (`CSharp/Shared` + `CSharp/Client` + `CSharp/Server` + `Binary/*.dll`).
Never mix both conventions - you would get two compilations and duplicate patches.

## IAssemblyPlugin

```csharp
public sealed class MyPlugin : IAssemblyPlugin
{
    public IConfigService ConfigService { get; set; }
    public IPluginManagementService PluginService { get; set; }
    public ILoggerService LoggerService { get; set; }

    public void PreInitPatching() { }      // install Harmony patches here
    public void Initialize() { }           // read config, build rule tables
    public void OnLoadCompleted() { }      // content is loaded, safe to touch prefabs
    public void Dispose() { }              // unpatch, unregister commands
}
```

- LuaCs instantiates the class and calls `InjectProperties(plugin)`, so public
  settable properties of service interfaces are filled automatically.
- Lifecycle order: `PreInitPatching` -> `Initialize` -> `OnLoadCompleted` -> ... -> `Dispose`.
- Content prefabs are **not** guaranteed to exist in `PreInitPatching`; resolve
  prefabs in `Initialize`/`OnLoadCompleted` or lazily.
- `Dispose` must be idempotent - it runs on package reload.

## Settings file format

```xml
<Configuration>
  <Settings>
    <Setting Name="GlobalBuyPriceMultiplier" Type="float" Value="1.0" ShowInMenus="true" />
    <Setting Name="EnemyMaxExtraPerEvent" Type="int" Value="4" />
  </Settings>
</Configuration>
```

Types registered by `SettingsEntryRegistrar`: `bool, byte, sbyte, short, ushort,
int, uint, long, ulong, string, float, single, double, rangeInt, rangeFloat,
control, listBool, listInt, listFloat, listString, ...`.

Read them with `ConfigService.TryGetConfig(Package, "Name", out ISettingBase<T> setting)`;
`setting.Value` is live, so patches can read it on every call and settings-menu
changes apply immediately. Missing settings must degrade to a built-in default.

## Console commands

```csharp
var commands = new ConsoleCommandsService();       // internal type: needs internal access
commands.RegisterCommand("go_reload", "help text", args => Reload(), null, isCheat: false);
commands.Dispose();                                // unregisters everything
```

Signature: `RegisterCommand(string name, string help, Action<string[]> onExecute,
Func<string[][]> getValidArgs = null, bool isCheat = false)`.

## Harmony patterns

```csharp
private Harmony _harmony;

public void PreInitPatching()
{
    _harmony = new Harmony("global.overhaul");
    var target = AccessTools.Method(typeof(Location.StoreInfo), nameof(Location.StoreInfo.CreateStock));
    if (target == null) { /* log + abort, do not half-patch */ }
    _harmony.Patch(target, postfix: new HarmonyMethod(AccessTools.Method(typeof(MyPatches), nameof(MyPatches.Postfix))));
}

public void Dispose() { _harmony?.UnpatchSelf(); _harmony = null; }
```

Rules that keep reloads safe:

1. **One** `Harmony` instance with a unique id, stored in a field, `UnpatchSelf()` in `Dispose`.
2. **Idempotency**: prefix/postfix must not mutate persistent state twice. Store
   original values once (or compute from the prefab's base value), never
   multiply in place on every load.
3. Resolve **all** targets before patching; on any failure unpatch everything and
   log `MissingMethodException` with the target description. A silent half-patched
   mod is worse than a mod that refuses to load.
4. Prefer `Postfix` + modify the result. Use `Prefix` with `__result`/`return false`
   only when the whole method must be replaced.
5. `internal` members: direct calls usually compile (LuaCs injects
   `IgnoresAccessChecksTo("Barotrauma")`); otherwise `AccessTools`.
6. Do not patch per-frame hot paths (`Update`, `Draw`) without caching.
7. Static state survives reload - clear it in `Initialize`/`Dispose`.

## Compile-time quirks

- All `.cs` files in the folder compile into one assembly; `using` directives must
  be per file, no implicit usings.
- LuaCs rewrites source text before compiling: `ItemPrefab.GetItemPrefab` ->
  `ModUtils.ItemPrefab.GetItemPrefab`, `GameMain.LuaCs` -> `LuaCsSetup.Instance`,
  `Client.ClientList` -> `ModUtils.Client.ClientList`. Prefer the rewritten forms
  directly.
- LuaCs falls back to compiling with internal access if the first attempt fails,
  and logs a warning - treat that warning as a smell, not a success.
- Compilation errors appear in the log as Roslyn diagnostics; the package loads
  without the plugin, so "mod loaded but nothing works" usually means compile errors.

## Logging

`LuaCsLogger.LogMessage(text, color * 0.8f, color)` writes to the debug console and
the log. Always wrap in try/catch with a `DebugConsole.NewMessage` fallback, because
logging is the first thing that breaks during early loading.

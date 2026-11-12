---
name: Barotrauma Reference Sources
description: Find the real signature of any Barotrauma / LuaCs class or member in the decompiled reference tree at _ref/ before writing C# or Harmony patches. Use when you need to know a method's exact name, parameters, visibility, or behaviour in the installed game build.
---

# Barotrauma reference sources

The game is installed at `D:\Steam\steamapps\common\Barotrauma` (version **1.13.5.0**).
`_ref/` holds decompiled sources generated from the exact installed binaries.

## What is where

| Path | Content | Use for |
|---|---|---|
| `_ref/project/` | `Barotrauma.dll` (1.13.5.0) decompiled to one `.cs` per type | every gameplay class, method body, default value |
| `_ref/project-core/` | `BarotraumaCore.dll` | `Identifier` and small shared helpers |
| `_ref/luacs/` | `Binary/Barotrauma.dll` shipped by LuaCsForBarotrauma 1.0.117 | **only** the `Barotrauma.LuaCs.*` API |
| `_ref/index.txt` | `TypeName<TAB>namespace\File.cs` | locating a type fast |
| `_ref/README.md` | version table and caveats | |

Vanilla content XML is *not* decompiled - read it directly from
`D:\Steam\steamapps\common\Barotrauma\Content\`.

## Lookup recipes

```
# type -> file
Select-String -Path _ref/index.txt -Pattern '^StoreInfo'

# declaration + all usages
grep -rn "CreateStock" _ref/project --include=*.cs

# a single member with surrounding context
grep -n -A 30 "public List<PurchasedItem> CreateStock" _ref/project/Barotrauma/Location.cs

# LuaCs API only
grep -rn "IAssemblyPlugin" _ref/luacs --include=*.cs
```

The `grep` tool is usually better than shell `Select-String` because it returns
file + line previews.

## Hard rules

1. **Never guess a signature.** If a name is not in `_ref/project/`, it does not
   exist in this build. Barotrauma renames things between minor versions.
2. **Nested types** are declared inside their parent file. `Barotrauma.Location+StoreInfo`
   lives in `_ref/project/Barotrauma/Location.cs` as `public class StoreInfo` inside
   `class Location`. In C# source you reference it as `Location.StoreInfo`.
3. **Compile-time vs runtime.** LuaCs compiles against *publicized* reference
   assemblies that are still 1.13.4. A 1.13.5 member can decompile fine but fail
   to compile. If that happens, use reflection:
   `AccessTools.Method(typeof(X), "Name", new[]{...})` or `AccessTools.TypeByName("Ns.Type")`.
4. **`internal`/`private` members** are visible in the decompiled output but not
   normally callable. LuaCs injects `IgnoresAccessChecksTo("Barotrauma")`, and the
   project also declares it in `CSharp/Shared/CompilePls.cs`, so direct calls
   usually compile. When in doubt, reflect.
5. Do not copy decompiled code into the mod - it is for reading only.

## After a game or LuaCs update

```
pwsh tools/refresh-ref.ps1 -Force
```

Then update the version table in `_ref/README.md`. Verify the mod still loads
(see the `barotrauma-test` skill): every `AccessTools.Method(...)` target is
resolved at startup and a missing one throws `MissingMethodException`.

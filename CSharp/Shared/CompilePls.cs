using System.Runtime.CompilerServices;

// LuaCs already injects IgnoresAccessChecksTo("Barotrauma"). Keeping the extra
// attributes here is harmless and makes the intent explicit for the in-memory
// compiler when the shared folder is compiled for the dedicated server too.
[assembly: IgnoresAccessChecksTo("Barotrauma")]
[assembly: IgnoresAccessChecksTo("BarotraumaCore")]
[assembly: IgnoresAccessChecksTo("DedicatedServer")]

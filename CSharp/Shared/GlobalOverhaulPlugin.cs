using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Barotrauma;
using Barotrauma.Items.Components;
using Barotrauma.LuaCs;
using Barotrauma.LuaCs.Data;
using HarmonyLib;
using Microsoft.Xna.Framework;

namespace GlobalOverhaul
{
    /// <summary>
    /// Entry point for the in-memory C# part of the mod.
    ///
    /// All gameplay changes are installed as Harmony patches and are driven by
    /// Config/Settings.xml plus the rule files in Config/. Nothing in the patch
    /// bodies hard-codes balance values, so the same build can be retuned without
    /// recompiling.
    /// </summary>
    public sealed partial class GlobalOverhaulPlugin : IAssemblyPlugin
    {
        public const string HarmonyId = "global.overhaul";

        private sealed class PatchPlan
        {
            public PatchPlan(MethodBase target, Type patchType, string prefix, string postfix, string description)
            {
                Target = target;
                PatchType = patchType;
                Prefix = prefix;
                Postfix = postfix;
                Description = description;
            }

            public MethodBase Target { get; }
            public Type PatchType { get; }
            public string Prefix { get; }
            public string Postfix { get; }
            public string Description { get; }
        }

        private Harmony _harmony;
        private ConsoleCommandsService _commands;
        private bool _faulted;
        private bool _disposed;

        // Injected by the LuaCs plugin service before PreInitPatching/Initialize.
        public IConfigService ConfigService { get; set; }
        public IPluginManagementService PluginService { get; set; }
        public ILoggerService LoggerService { get; set; }

        public static ContentPackage Package { get; private set; }
        public static string ModDirectory { get; private set; } = string.Empty;

        public void PreInitPatching()
        {
            if (_disposed || _faulted || _harmony != null) { return; }

            try
            {
                _harmony = new Harmony(HarmonyId);

                var plans = new List<PatchPlan>
                {
                    new PatchPlan(
                        AccessTools.Constructor(typeof(Item), new[]
                        {
                            typeof(Rectangle), typeof(ItemPrefab), typeof(Submarine), typeof(bool), typeof(ushort)
                        }),
                        typeof(BalancePatches), null, nameof(BalancePatches.ItemConstructorPostfix),
                        "Item..ctor(Rectangle, ItemPrefab, Submarine, bool, ushort)"),

                    new PatchPlan(
                        AccessTools.Method(typeof(Location.StoreInfo), nameof(Location.StoreInfo.CreateStock), Type.EmptyTypes),
                        typeof(TraderPatches), null, nameof(TraderPatches.CreateStockPostfix),
                        "Location.StoreInfo.CreateStock()"),

                    new PatchPlan(
                        AccessTools.Method(typeof(Location), nameof(Location.LoadStores), new[] { typeof(XElement) }),
                        typeof(TraderPatches), null, nameof(TraderPatches.LoadStoresPostfix),
                        "Location.LoadStores(XElement)"),

                    new PatchPlan(
                        AccessTools.Method(typeof(Location.StoreInfo), nameof(Location.StoreInfo.GetAdjustedItemBuyPrice),
                            new[] { typeof(ItemPrefab), typeof(PriceInfo), typeof(bool) }),
                        typeof(TraderPatches), null, nameof(TraderPatches.BuyPricePostfix),
                        "Location.StoreInfo.GetAdjustedItemBuyPrice(ItemPrefab, PriceInfo, bool)"),

                    new PatchPlan(
                        AccessTools.Method(typeof(Location.StoreInfo), nameof(Location.StoreInfo.GetAdjustedItemSellPrice),
                            new[] { typeof(ItemPrefab), typeof(PriceInfo), typeof(bool) }),
                        typeof(TraderPatches), null, nameof(TraderPatches.SellPricePostfix),
                        "Location.StoreInfo.GetAdjustedItemSellPrice(ItemPrefab, PriceInfo, bool)"),

                    new PatchPlan(
                        AccessTools.Method(typeof(MonsterEvent), nameof(MonsterEvent.InitEventSpecific), new[] { typeof(EventSet) }),
                        typeof(EnemyPatches), null, nameof(EnemyPatches.InitEventSpecificPostfix),
                        "MonsterEvent.InitEventSpecific(EventSet)"),

                    new PatchPlan(
                        AccessTools.Method(typeof(Projectile), nameof(Projectile.Shoot)),
                        typeof(BalancePatches), nameof(BalancePatches.ProjectileShootPrefix), null,
                        "Projectile.Shoot(...)"),
                };

                foreach (var plan in plans)
                {
                    if (plan.Target == null)
                    {
                        throw new MissingMethodException($"Could not resolve patch target: {plan.Description}");
                    }

                    _harmony.Patch(
                        plan.Target,
                        prefix: string.IsNullOrEmpty(plan.Prefix)
                            ? null
                            : new HarmonyMethod(RequirePatchMethod(plan.PatchType, plan.Prefix)),
                        postfix: string.IsNullOrEmpty(plan.Postfix)
                            ? null
                            : new HarmonyMethod(RequirePatchMethod(plan.PatchType, plan.Postfix)));
                }

                VerifyPatches(plans);
                Log($"Installed and verified {plans.Count} patch target(s).");
            }
            catch (Exception ex)
            {
                _faulted = true;
                LogError($"Initialization failed; all patches were removed. {ex}");
                TryUnpatch();
            }
        }

        public void Initialize()
        {
            if (_disposed || _faulted) { return; }

            try
            {
                if (!TryResolvePackage()) { return; }

                BindSettings();
                Rules.LoadAll(ModDirectory);
                RegisterConsoleCommands();
                Log($"Ready. Rules: {Rules.Summary()}");
            }
            catch (Exception ex)
            {
                _faulted = true;
                LogError($"Initialize failed. {ex}");
                TryUnpatch();
            }
        }

        public void OnLoadCompleted()
        {
            if (_disposed || _faulted) { return; }
            if (DebugLoggingEnabled)
            {
                Log("Load completed. Use 'go_status' to inspect rules and 'go_reload' after editing Config/*.xml.");
            }
        }

        public void Dispose()
        {
            if (_disposed) { return; }
            _disposed = true;
            try { _commands?.Dispose(); } catch { }
            _commands = null;
            TryUnpatch();
        }

        private void TryUnpatch()
        {
            if (_harmony == null) { return; }
            try
            {
                _harmony.UnpatchSelf();
                _harmony = null;
            }
            catch (Exception ex)
            {
                LogError($"Unpatch failed; a process restart may be required. {ex}");
            }
        }

        private static HarmonyMethod RequirePatchMethod(Type patchType, string methodName)
        {
            var method = AccessTools.Method(patchType, methodName);
            if (method == null)
            {
                throw new MissingMethodException(patchType.FullName, methodName);
            }
            return new HarmonyMethod(method);
        }

        private static void VerifyPatches(List<PatchPlan> plans)
        {
            foreach (var plan in plans)
            {
                var info = Harmony.GetPatchInfo(plan.Target);
                int owned = 0;
                if (info != null)
                {
                    owned += info.Prefixes.Count(p => p.owner == HarmonyId);
                    owned += info.Postfixes.Count(p => p.owner == HarmonyId);
                }

                int expected = (string.IsNullOrEmpty(plan.Prefix) ? 0 : 1) +
                               (string.IsNullOrEmpty(plan.Postfix) ? 0 : 1);
                if (owned < expected)
                {
                    throw new InvalidOperationException(
                        $"Patch registration for {plan.Description} is incomplete (owned={owned}, expected={expected}).");
                }
            }
        }

        private bool TryResolvePackage()
        {
            if (PluginService == null)
            {
                LogError("IPluginManagementService was not injected.");
                return false;
            }

            if (!PluginService.TryGetPackageForPlugin<GlobalOverhaulPlugin>(out var package) || package == null)
            {
                LogError("Could not find the ContentPackage that owns this plugin.");
                return false;
            }

            Package = package;
            ModDirectory = package.Dir;
            if (string.IsNullOrEmpty(ModDirectory))
            {
                ModDirectory = System.IO.Path.GetDirectoryName(package.Path) ?? string.Empty;
            }

            if (string.IsNullOrEmpty(ModDirectory))
            {
                LogError("Could not determine the mod directory; XML rules cannot be loaded.");
                return false;
            }

            return true;
        }

        private void BindSettings()
        {
            Bind("EnableTraderStock", out EnableTraderStock);
            Bind("EnableTraderPrices", out EnableTraderPrices);
            Bind("EnableEnemySpawns", out EnableEnemySpawns);
            Bind("EnableItemBalance", out EnableItemBalance);
            Bind("DebugLogging", out DebugLogging);

            Bind("GlobalBuyPriceMultiplier", out GlobalBuyPriceMultiplier);
            Bind("GlobalSellPriceMultiplier", out GlobalSellPriceMultiplier);
            Bind("GlobalStockMultiplier", out GlobalStockMultiplier);

            Bind("ItemDamageMultiplier", out ItemDamageMultiplier);
            Bind("ItemReloadMultiplier", out ItemReloadMultiplier);
            Bind("ItemMaxConditionMultiplier", out ItemMaxConditionMultiplier);

            Bind("EnemyAmountMultiplier", out EnemyAmountMultiplier);
            Bind("EnemyMaxExtraPerEvent", out EnemyMaxExtraPerEvent);
        }

        private void Bind<T>(string name, out ISettingBase<T> setting)
            where T : IEquatable<T>, IConvertible
        {
            setting = null;
            if (ConfigService == null || Package == null) { return; }

            if (!ConfigService.TryGetConfig(Package, name, out setting) || setting == null)
            {
                LogWarning($"Setting '{name}' was not found; the built-in default will be used.");
                return;
            }

            setting.OnValueChanged += _ =>
            {
                if (DebugLoggingEnabled)
                {
                    Log($"Setting '{name}' changed to '{setting.GetStringValue()}'.");
                }
            };
        }

        private void RegisterConsoleCommands()
        {
            _commands?.Dispose();
            _commands = new ConsoleCommandsService();

            _commands.RegisterCommand(
                "go_reload",
                "Global Overhaul: reload Config/TraderStock.xml, Prices.xml, EnemySpawns.xml and BalanceRules.xml.",
                _ => Rules.LoadAll(ModDirectory));

            _commands.RegisterCommand(
                "go_status",
                "Global Overhaul: print the number of loaded rules and the effective global settings.",
                _ => PrintStatus());

            _commands.RegisterCommand(
                "go_apply",
                "Global Overhaul: restore item base values and reapply the current balance rules to every existing item.",
                _ => BalancePatches.ReapplyAll());
        }

        private static void PrintStatus()
        {
            Log($"Status: {Rules.Summary()}");
            Log(
                $"Toggles: stock={TraderStockEnabled}, prices={TraderPricesEnabled}, enemies={EnemySpawnsEnabled}, items={ItemBalanceEnabled}");
            Log(
                $"Global multipliers: buy={GlobalBuyPriceMultiplier:0.###}, sell={GlobalSellPriceMultiplier:0.###}, stock={GlobalStockMultiplier:0.###}, " +
                $"damage={DamageMultiplier:0.###}, reload={ReloadMultiplier:0.###}, condition={MaxConditionMultiplier:0.###}, " +
                $"enemyAmount={EnemyAmountMultiplierValue:0.###}, enemyMaxExtra={EnemyMaxExtra}");
            Log("Commands: go_reload, go_status, go_apply.");
        }

        // ---------------------------------------------------------------------
        // Logging
        // ---------------------------------------------------------------------

        public static void Log(string message)
        {
            WriteLog(message, Color.Cyan);
        }

        public static void LogWarning(string message)
        {
            WriteLog(message, Color.Yellow);
        }

        public static void LogError(string message)
        {
            WriteLog(message, Color.Red);
        }

        private static void WriteLog(string message, Color color)
        {
            string text = "[GlobalOverhaul] " + message;
            try
            {
                LuaCsLogger.LogMessage(text, color * 0.8f, color);
            }
            catch
            {
                try { DebugConsole.NewMessage(text, color); } catch { }
            }
        }
    }
}

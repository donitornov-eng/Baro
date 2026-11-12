using Barotrauma.LuaCs.Data;

namespace GlobalOverhaul
{
    public sealed partial class GlobalOverhaulPlugin
    {
        // Bound to Config/Settings.xml. The properties below are read by the
        // patches on every call, so edits made in the LuaCs settings menu take
        // effect immediately (after go_apply for already-created items).
        public static ISettingBase<bool> EnableTraderStock;
        public static ISettingBase<bool> EnableTraderPrices;
        public static ISettingBase<bool> EnableEnemySpawns;
        public static ISettingBase<bool> EnableItemBalance;
        public static ISettingBase<bool> DebugLogging;

        public static ISettingBase<float> GlobalBuyPriceMultiplier;
        public static ISettingBase<float> GlobalSellPriceMultiplier;
        public static ISettingBase<float> GlobalStockMultiplier;

        public static ISettingBase<float> ItemDamageMultiplier;
        public static ISettingBase<float> ItemReloadMultiplier;
        public static ISettingBase<float> ItemMaxConditionMultiplier;

        public static ISettingBase<float> EnemyAmountMultiplier;
        public static ISettingBase<int> EnemyMaxExtraPerEvent;

        public static bool TraderStockEnabled => EnableTraderStock == null || EnableTraderStock.Value;
        public static bool TraderPricesEnabled => EnableTraderPrices == null || EnableTraderPrices.Value;
        public static bool EnemySpawnsEnabled => EnableEnemySpawns == null || EnableEnemySpawns.Value;
        public static bool ItemBalanceEnabled => EnableItemBalance == null || EnableItemBalance.Value;
        public static bool DebugLoggingEnabled => DebugLogging != null && DebugLogging.Value;

        public static float BuyPriceMultiplier => GlobalBuyPriceMultiplier?.Value ?? 1f;
        public static float SellPriceMultiplier => GlobalSellPriceMultiplier?.Value ?? 1f;
        public static float StockMultiplier => GlobalStockMultiplier?.Value ?? 1f;

        public static float DamageMultiplier => ItemDamageMultiplier?.Value ?? 1f;
        public static float ReloadMultiplier => ItemReloadMultiplier?.Value ?? 1f;
        public static float MaxConditionMultiplier => ItemMaxConditionMultiplier?.Value ?? 1f;

        public static float EnemyAmountMultiplierValue => EnemyAmountMultiplier?.Value ?? 1f;
        public static int EnemyMaxExtra => EnemyMaxExtraPerEvent?.Value ?? 4;
    }
}

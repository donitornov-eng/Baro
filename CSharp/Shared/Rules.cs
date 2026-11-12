using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Barotrauma;

namespace GlobalOverhaul
{
    internal sealed class StockRuleConfig
    {
        public HashSet<string> Items { get; } = NewSet();
        public HashSet<string> Tags { get; } = NewSet();
        public HashSet<string> Stores { get; } = NewSet();
        public double QuantityMultiplier { get; set; } = 1.0;
        public int Add { get; set; }
        public int? Min { get; set; }
        public int? Max { get; set; }

        public bool MatchesStore(string storeId)
        {
            return Stores.Count == 0 || Stores.Contains("*") || Stores.Contains(storeId);
        }

        public bool Matches(ItemPrefab prefab, string storeId)
        {
            if (!MatchesStore(storeId)) { return false; }

            bool hasSelector = Items.Count > 0 || Tags.Count > 0;
            if (!hasSelector) { return true; }

            if (Items.Count > 0 && prefab != null && Items.Contains(prefab.Identifier.Value))
            {
                return true;
            }

            if (Tags.Count > 0 && prefab != null)
            {
                foreach (var tag in prefab.Tags)
                {
                    if (Tags.Contains(tag.Value)) { return true; }
                }
            }

            return false;
        }

        internal static HashSet<string> NewSet()
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    internal sealed class PriceRuleConfig
    {
        public HashSet<string> Items { get; } = StockRuleConfig.NewSet();
        public HashSet<string> Tags { get; } = StockRuleConfig.NewSet();
        public HashSet<string> Stores { get; } = StockRuleConfig.NewSet();
        public double BuyMultiplier { get; set; } = 1.0;
        public double SellMultiplier { get; set; } = 1.0;

        public bool Matches(ItemPrefab prefab, string storeId)
        {
            if (Stores.Count > 0 && !Stores.Contains("*") && !Stores.Contains(storeId)) { return false; }

            bool hasSelector = Items.Count > 0 || Tags.Count > 0;
            if (!hasSelector) { return true; }

            if (Items.Count > 0 && prefab != null && Items.Contains(prefab.Identifier.Value))
            {
                return true;
            }

            if (Tags.Count > 0 && prefab != null)
            {
                foreach (var tag in prefab.Tags)
                {
                    if (Tags.Contains(tag.Value)) { return true; }
                }
            }

            return false;
        }
    }

    internal sealed class EnemyRuleConfig
    {
        public HashSet<string> Species { get; } = StockRuleConfig.NewSet();
        public HashSet<string> AddSpecies { get; } = StockRuleConfig.NewSet();
        public HashSet<string> ParentSets { get; } = StockRuleConfig.NewSet();
        public HashSet<string> Biomes { get; } = StockRuleConfig.NewSet();
        public double Chance { get; set; } = 1.0;
        public int CountMin { get; set; } = 1;
        public int CountMax { get; set; } = 1;
        public float MinDifficulty { get; set; }
        public float MaxDifficulty { get; set; } = 1.01f;
        public double AmountMultiplier { get; set; } = 1.0;

        public bool IsGlobal => Species.Count == 0 && ParentSets.Count == 0 && Biomes.Count == 0;

        public bool Matches(MonsterEvent ev, string parentSet, string biome, float difficulty)
        {
            if (difficulty < MinDifficulty || difficulty > MaxDifficulty) { return false; }

            if (Species.Count > 0 && !Species.Contains("*") &&
                (ev == null || !Species.Contains(ev.SpeciesName.Value)))
            {
                return false;
            }

            if (ParentSets.Count > 0 && !ParentSets.Contains("*") && !ParentSets.Contains(parentSet))
            {
                return false;
            }

            if (Biomes.Count > 0 && !Biomes.Contains("*") && !Biomes.Contains(biome))
            {
                return false;
            }

            return true;
        }

        public string PickAddSpecies(MonsterEvent ev)
        {
            if (AddSpecies.Count == 0) { return ev?.SpeciesName.Value ?? string.Empty; }
            // First entry wins; list syntax is accepted so the same rule can be
            // reused for several variants without duplicating the rule.
            return AddSpecies.First();
        }
    }

    internal sealed class BalanceStatConfig
    {
        public string Key { get; set; }
        public string Component { get; set; }
        public string Member { get; set; }
        public double Multiplier { get; set; } = 1.0;
        public double Add { get; set; }
        public double? Min { get; set; }
        public double? Max { get; set; }
    }

    internal sealed class BalanceRuleConfig
    {
        public HashSet<string> Items { get; } = StockRuleConfig.NewSet();
        public HashSet<string> Tags { get; } = StockRuleConfig.NewSet();
        public List<BalanceStatConfig> Stats { get; } = new List<BalanceStatConfig>();

        public bool IsGlobal => Items.Count == 0 && Tags.Count == 0;

        public bool Matches(Item item)
        {
            if (item?.Prefab == null) { return false; }
            if (IsGlobal) { return true; }

            if (Items.Count > 0 && Items.Contains(item.Prefab.Identifier.Value))
            {
                return true;
            }

            if (Tags.Count > 0)
            {
                foreach (var tag in item.Prefab.Tags)
                {
                    if (Tags.Contains(tag.Value)) { return true; }
                }
            }

            return false;
        }
    }

    internal static class Rules
    {
        private static readonly object Sync = new object();

        public static List<StockRuleConfig> StockRules { get; private set; } = new List<StockRuleConfig>();
        public static List<PriceRuleConfig> PriceRules { get; private set; } = new List<PriceRuleConfig>();
        public static List<EnemyRuleConfig> EnemyRules { get; private set; } = new List<EnemyRuleConfig>();
        public static List<BalanceRuleConfig> BalanceRules { get; private set; } = new List<BalanceRuleConfig>();

        public static string Summary()
        {
            return $"stock={StockRules.Count}, prices={PriceRules.Count}, enemies={EnemyRules.Count}, balance={BalanceRules.Count}";
        }

        public static void LoadAll(string modDirectory)
        {
            if (string.IsNullOrEmpty(modDirectory))
            {
                GlobalOverhaulPlugin.LogWarning("Rules not loaded: mod directory is unknown.");
                return;
            }

            string configDirectory = System.IO.Path.Combine(modDirectory, "Config");

            var stock = LoadStock(System.IO.Path.Combine(configDirectory, "TraderStock.xml"));
            var prices = LoadPrices(System.IO.Path.Combine(configDirectory, "Prices.xml"));
            var enemies = LoadEnemies(System.IO.Path.Combine(configDirectory, "EnemySpawns.xml"));
            var balance = LoadBalance(System.IO.Path.Combine(configDirectory, "BalanceRules.xml"));

            lock (Sync)
            {
                if (stock != null) { StockRules = stock; }
                if (prices != null) { PriceRules = prices; }
                if (enemies != null) { EnemyRules = enemies; }
                if (balance != null) { BalanceRules = balance; }
            }

            GlobalOverhaulPlugin.Log($"Rules reloaded: {Summary()}.");
        }

        private static XElement LoadRoot(string path, string expectedRoot)
        {
            if (!System.IO.File.Exists(path))
            {
                GlobalOverhaulPlugin.LogWarning($"Rule file not found: {path}");
                return null;
            }

            try
            {
                var document = XDocument.Load(path);
                if (document.Root == null)
                {
                    GlobalOverhaulPlugin.LogError($"Rule file has no root element: {path}");
                    return null;
                }

                if (!string.Equals(document.Root.Name.LocalName, expectedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    GlobalOverhaulPlugin.LogError(
                        $"Rule file '{path}' has root <{document.Root.Name.LocalName}>; expected <{expectedRoot}>.");
                    return null;
                }

                return document.Root;
            }
            catch (Exception ex)
            {
                GlobalOverhaulPlugin.LogError($"Could not parse rule file '{path}': {ex.Message}");
                return null;
            }
        }

        private static List<StockRuleConfig> LoadStock(string path)
        {
            var root = LoadRoot(path, "TraderStock");
            if (root == null) { return null; }

            var rules = new List<StockRuleConfig>();
            foreach (var element in root.Elements())
            {
                string name = element.Name.LocalName;
                if (!name.Equals("Global", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("Rule", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var rule = new StockRuleConfig
                {
                    QuantityMultiplier = AttrDouble(element, "quantityMultiplier", AttrDouble(element, "multiplier", 1.0)),
                    Add = AttrInt(element, "add", 0),
                    Min = AttrNullableInt(element, "min", "minamount", "minavailable"),
                    Max = AttrNullableInt(element, "max", "maxamount", "maxavailable")
                };

                if (!name.Equals("Global", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var value in AttrSet(element, "item", "items", "identifier")) { rule.Items.Add(value); }
                    foreach (var value in AttrSet(element, "tag", "tags")) { rule.Tags.Add(value); }
                    foreach (var value in AttrSet(element, "store", "stores", "storeidentifier")) { rule.Stores.Add(value); }
                }

                rules.Add(rule);
            }

            return rules;
        }

        private static List<PriceRuleConfig> LoadPrices(string path)
        {
            var root = LoadRoot(path, "Prices");
            if (root == null) { return null; }

            var rules = new List<PriceRuleConfig>();
            foreach (var element in root.Elements())
            {
                string name = element.Name.LocalName;
                if (!name.Equals("Global", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("Rule", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var rule = new PriceRuleConfig
                {
                    BuyMultiplier = AttrDouble(element, "buyMultiplier", AttrDouble(element, "buy", 1.0)),
                    SellMultiplier = AttrDouble(element, "sellMultiplier", AttrDouble(element, "sell", 1.0))
                };

                if (!name.Equals("Global", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var value in AttrSet(element, "item", "items", "identifier")) { rule.Items.Add(value); }
                    foreach (var value in AttrSet(element, "tag", "tags")) { rule.Tags.Add(value); }
                    foreach (var value in AttrSet(element, "store", "stores", "storeidentifier")) { rule.Stores.Add(value); }
                }

                rules.Add(rule);
            }

            return rules;
        }

        private static List<EnemyRuleConfig> LoadEnemies(string path)
        {
            var root = LoadRoot(path, "EnemySpawns");
            if (root == null) { return null; }

            var rules = new List<EnemyRuleConfig>();
            foreach (var element in root.Elements())
            {
                string name = element.Name.LocalName;
                if (!name.Equals("Global", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("Rule", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var rule = new EnemyRuleConfig
                {
                    Chance = AttrDouble(element, "chance", 1.0),
                    CountMin = Math.Max(0, AttrInt(element, "countMin", AttrInt(element, "mincount", 1))),
                    CountMax = Math.Max(0, AttrInt(element, "countMax", AttrInt(element, "maxcount", 1))),
                    MinDifficulty = (float)AttrDouble(element, "minDifficulty", 0.0),
                    MaxDifficulty = (float)AttrDouble(element, "maxDifficulty", 1.01),
                    AmountMultiplier = AttrDouble(element, "amountMultiplier", 1.0)
                };

                if (rule.CountMax < rule.CountMin) { rule.CountMax = rule.CountMin; }

                if (!name.Equals("Global", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var value in AttrSet(element, "species", "monster")) { rule.Species.Add(value); }
                    foreach (var value in AttrSet(element, "addSpecies", "add", "spawn")) { rule.AddSpecies.Add(value); }
                    foreach (var value in AttrSet(element, "parent", "parentSet", "eventset")) { rule.ParentSets.Add(value); }
                    foreach (var value in AttrSet(element, "biome", "biomes")) { rule.Biomes.Add(value); }
                }

                rules.Add(rule);
            }

            return rules;
        }

        private static List<BalanceRuleConfig> LoadBalance(string path)
        {
            var root = LoadRoot(path, "BalanceRules");
            if (root == null) { return null; }

            var rules = new List<BalanceRuleConfig>();
            foreach (var element in root.Elements())
            {
                string name = element.Name.LocalName;
                if (!name.Equals("Global", StringComparison.OrdinalIgnoreCase) &&
                    !name.Equals("Rule", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var rule = new BalanceRuleConfig();
                if (!name.Equals("Global", StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var value in AttrSet(element, "item", "items", "identifier")) { rule.Items.Add(value); }
                    foreach (var value in AttrSet(element, "tag", "tags")) { rule.Tags.Add(value); }
                }

                foreach (var statElement in element.Elements())
                {
                    if (!statElement.Name.LocalName.Equals("Stat", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var stat = new BalanceStatConfig
                    {
                        Key = Attr(statElement, "key", "stat"),
                        Component = Attr(statElement, "component", "type"),
                        Member = Attr(statElement, "member", "field"),
                        Multiplier = AttrDouble(statElement, "multiplier", AttrDouble(statElement, "value", 1.0)),
                        Add = AttrDouble(statElement, "add", 0.0),
                        Min = AttrNullableDouble(statElement, "min"),
                        Max = AttrNullableDouble(statElement, "max")
                    };

                    if (string.IsNullOrEmpty(stat.Key) && string.IsNullOrEmpty(stat.Member))
                    {
                        GlobalOverhaulPlugin.LogWarning(
                            $"Ignoring balance stat with neither key nor component/member in {path}.");
                        continue;
                    }

                    rule.Stats.Add(stat);
                }

                rules.Add(rule);
            }

            return rules;
        }

        // ---------------------------------------------------------------------
        // XML helpers
        // ---------------------------------------------------------------------

        private static string Attr(XElement element, params string[] names)
        {
            foreach (var name in names)
            {
                var attribute = element.Attribute(name);
                if (attribute != null && !string.IsNullOrWhiteSpace(attribute.Value))
                {
                    return attribute.Value.Trim();
                }
            }
            return null;
        }

        private static double AttrDouble(XElement element, string name, double fallback)
        {
            string value = Attr(element, name);
            return value != null &&
                   double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                ? parsed
                : fallback;
        }

        private static double? AttrNullableDouble(XElement element, string name)
        {
            string value = Attr(element, name);
            if (value == null) { return null; }
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
                ? parsed
                : (double?)null;
        }

        private static int AttrInt(XElement element, string name, int fallback)
        {
            string value = Attr(element, name);
            return value != null &&
                   int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : fallback;
        }

        private static int? AttrNullableInt(XElement element, params string[] names)
        {
            string value = Attr(element, names);
            if (value == null) { return null; }
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : (int?)null;
        }

        private static IEnumerable<string> AttrSet(XElement element, params string[] names)
        {
            string value = Attr(element, names);
            if (string.IsNullOrEmpty(value)) { yield break; }

            foreach (var part in value.Split(new[] { ',', ';', ' ', '\t', '\r', '\n' },
                         StringSplitOptions.RemoveEmptyEntries))
            {
                yield return part.Trim();
            }
        }
    }
}

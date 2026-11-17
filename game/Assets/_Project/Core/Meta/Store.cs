using System.Collections.Generic;

namespace TiltDrop.Core
{
    public enum ProductKind { NonConsumable, Consumable }

    public sealed class ProductDef
    {
        public string Id;
        public ProductKind Kind;
        public string FallbackPriceEur; // shown only until the store returns the localized price
        public int Coins, Undo, Hammer;
        public bool RemovesAds;
        public bool OncePerUser;
        public string Title;
    }

    /// <summary>Store catalog (GDD §5.4). Prices are configured in € on the stores; the localized
    /// store price string is always what the UI shows.</summary>
    public static class StoreCatalog
    {
        public const string RemoveAds = "com.laszlo.tiltdrop.removeads";
        public const string Starter = "com.laszlo.tiltdrop.starter";
        public const string NoAdsBundle = "com.laszlo.tiltdrop.noadsbundle";
        public const string Piggy = "com.laszlo.tiltdrop.piggy";

        public static readonly List<ProductDef> Products = new List<ProductDef>
        {
            new ProductDef { Id = RemoveAds, Kind = ProductKind.NonConsumable, FallbackPriceEur = "€4.99", RemovesAds = true, Title = "Remove Ads" },
            new ProductDef { Id = Starter, Kind = ProductKind.Consumable, FallbackPriceEur = "€1.99", Coins = 1000, Undo = 2, Hammer = 2, OncePerUser = true, Title = "Starter Pack" },
            new ProductDef { Id = NoAdsBundle, Kind = ProductKind.NonConsumable, FallbackPriceEur = "€7.99", RemovesAds = true, Coins = 1500, Undo = 3, Hammer = 3, Title = "No Ads + Bundle" },
            new ProductDef { Id = "com.laszlo.tiltdrop.coins1", Kind = ProductKind.Consumable, FallbackPriceEur = "€1.99", Coins = 1000, Title = "1,000 Coins" },
            new ProductDef { Id = "com.laszlo.tiltdrop.coins2", Kind = ProductKind.Consumable, FallbackPriceEur = "€4.99", Coins = 2800, Title = "2,800 Coins" },
            new ProductDef { Id = "com.laszlo.tiltdrop.coins3", Kind = ProductKind.Consumable, FallbackPriceEur = "€9.99", Coins = 6000, Title = "6,000 Coins" },
            new ProductDef { Id = "com.laszlo.tiltdrop.coins4", Kind = ProductKind.Consumable, FallbackPriceEur = "€19.99", Coins = 13000, Title = "13,000 Coins" },
            new ProductDef { Id = "com.laszlo.tiltdrop.coins5", Kind = ProductKind.Consumable, FallbackPriceEur = "€49.99", Coins = 35000, Title = "35,000 Coins" },
            new ProductDef { Id = Piggy, Kind = ProductKind.Consumable, FallbackPriceEur = "€3.99", Title = "Piggy Bank" },
        };

        public static ProductDef Find(string id) => Products.Find(p => p.Id == id);

        /// <summary>CPC principle: show the approximate € value of a coin price (at the smallest pack's rate).</summary>
        public static string ApproxEur(int coins)
        {
            const double eurPerCoin = 1.99 / 1000.0;
            return "≈ €" + (coins * eurPerCoin).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        }
    }
}

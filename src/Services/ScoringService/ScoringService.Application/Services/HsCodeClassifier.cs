namespace ScoringService.Application.Services;

/// <summary>
/// HS Code auto-classifier that maps product names/brands to harmonized tariff codes.
/// Uses keyword matching against a curated mapping table.
/// </summary>
public class HsCodeClassifier : IHsCodeClassifier
{
    private static readonly Dictionary<string, string> HsCodeMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        // Electronics & IT
        { "laptop", "8471.30" },
        { "notebook", "8471.30" },
        { "computer", "8471.30" },
        { "tablet", "8471.30" },
        { "chromebook", "8471.30" },
        { "phone", "8517.12" },
        { "smartphone", "8517.12" },
        { "mobile phone", "8517.12" },
        { "cellular", "8517.12" },
        { "headphone", "8518.30" },
        { "earphone", "8518.30" },
        { "earbuds", "8518.30" },
        { "headset", "8518.30" },
        { "charger", "8504.40" },
        { "laptop charger", "8504.40" },
        { "charging", "8504.40" },
        { "battery", "8507.60" },
        { "power bank", "8507.60" },
        { "camera", "8525.80" },
        { "digital camera", "8525.80" },
        { "television", "8528.72" },
        { "tv", "8528.72" },
        { "smart tv", "8528.72" },
        { "watch", "9102.11" },
        { "wristwatch", "9102.11" },
        { "smartwatch", "9102.11" },

        // Food & Beverage
        { "coffee", "0901.11" },
        { "espresso", "0901.11" },
        { "instant coffee", "0901.12" },
        { "protein", "2106.10" },
        { "whey", "2106.10" },
        { "supplement", "2106.90" },
        { "vitamin", "2106.90" },
        { "nutraceutical", "2106.90" },
        { "energy drink", "2202.99" },

        // Personal Care & Beauty
        { "skincare", "3304.99" },
        { "moisturizer", "3304.99" },
        { "cream", "3304.99" },
        { "serum", "3304.99" },
        { "lotion", "3304.99" },
        { "shampoo", "3401.30" },
        { "soap", "3401.30" },
        { "perfume", "3303.00" },
        { "fragrance", "3303.00" },
        { "cologne", "3303.00" },

        // Tobacco
        { "tobacco", "2403.10" },
        { "cigar", "2402.10" },
        { "cigars", "2402.10" },
        { "cigarette", "2403.10" },

        // Apparel & Accessories
        { "shoes", "6403.99" },
        { "footwear", "6403.99" },
        { "sneaker", "6403.99" },
        { "sandal", "6402.20" },
        { "handbag", "4202.99" },
        { "bag", "4202.99" },
        { "backpack", "4202.99" },
        { "purse", "4202.32" },
        { "wallet", "4202.32" },
        { "sunglasses", "9004.10" },

        // Kitchen & Home
        { "blender", "8509.40" },
        { "mixer", "8509.40" },
        { "vacuum", "8508.11" },
        { "air purifier", "8421.39" },
    };

    private static readonly HashSet<string> BrandHints = new(StringComparer.OrdinalIgnoreCase)
    {
        "Apple", "Samsung", "Sony", "LG", "Dell", "HP", "Lenovo", "Asus",
        "Bose", "JBL", "Jabra", "Logitech", "Corsair", "Razer",
        "Nestle", "Herbalife", "GNC", "Nature's Bounty",
        "Maybelline", "MAC", "Estee Lauder", "SK-II",
        "Nike", "Adidas", "Puma", "New Balance", "Under Armour",
    };

    public string? Classify(string productName, string? category = null, string? brand = null)
    {
        var text = $"{productName} {category} {brand}".ToLowerInvariant();
        var bestKey = "";
        var bestLen = 0;

        foreach (var (key, _) in HsCodeMappings)
        {
            if (text.Contains(key, StringComparison.OrdinalIgnoreCase) && key.Length > bestLen)
            {
                bestLen = key.Length;
                bestKey = key;
            }
        }

        return bestLen > 0 && HsCodeMappings.TryGetValue(bestKey, out var hsCode)
            ? hsCode
            : null;
    }

    public IReadOnlyDictionary<string, string> GetAllMappings() => HsCodeMappings;
}

public interface IHsCodeClassifier
{
    string? Classify(string productName, string? category = null, string? brand = null);
    IReadOnlyDictionary<string, string> GetAllMappings();
}

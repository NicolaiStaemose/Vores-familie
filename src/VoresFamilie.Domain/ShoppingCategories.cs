namespace VoresFamilie.Domain;

/// <summary>Departments in the shop. The order of the values is not the order in the shop (the web page decides that).</summary>
public enum ShoppingCategory
{
    Other = 0,
    FruitVeg,
    Dairy,
    Meat,
    Bread,
    Pantry,
    Frozen,
    Drinks,
    Household,
}

/// <summary>Guesses the department of an item from its name (Danish words).</summary>
public static class ShoppingCategories
{
    // The first match wins, so specific groups come first (frozen pizza before "pizza", tinned tomatoes before "tomat").
    // A keyword ending in a space must be a whole word ("is ", "vand "), other short words must start a word,
    // and long words (5+ letters) may sit inside a compound ("rugbrød", "hvidvin").
    private static readonly (ShoppingCategory Category, string[] Words)[] Rules =
    [
        (ShoppingCategory.Frozen, ["frost", "frossen", "frosne", "pizza", "ispinde", "pommes", "is "]),
        (ShoppingCategory.Household, ["toiletpapir", "køkkenrulle", "opvaske", "vaskemiddel", "skyllemiddel", "shampoo", "tandpasta",
            "sæbe", "affaldsposer", "sulfo", "batteri", "vatpinde", "tandbørste", "deodorant", "bleer", "hundefoder", "kattemad",
            "opvasketabs", "rengøring", "lyspære", "fryseposer", "bagepapir", "alufolie"]),
        (ShoppingCategory.Drinks, ["vand ", "juice", "sodavand", "cola", "saft", "øl ", "vin ", "danskvand", "læsk", "cider", "energidrik",
            "mineralvand", "rødvin", "hvidvin", "rosévin"]),
        (ShoppingCategory.Pantry, ["dåse", "flåede", "hakkede tomater", "tomatpuré", "puré", "ketchup", "remoulade", "sennep", "mayonnaise",
            "pålægschokolade"]),
        (ShoppingCategory.Bread, ["brød", "toast", "bolle", "tortilla", "wrap", "fladbrød", "baguette", "rundstykke", "croissant", "pita", "knækbrød"]),
        (ShoppingCategory.Meat, ["kylling", "kød", "hakket", "bøf", "flæsk", "bacon", "pølse", "skinke", "laks", "fisk", "torsk", "rejer",
            "tun ", "medister", "frikadelle", "svin", "okse", "lam ", "kalkun", "leverpostej", "pålæg", "rullepølse", "hotdog", "nuggets"]),
        (ShoppingCategory.Dairy, ["mælk", "ost", "smør", "yoghurt", "fløde", "æg", "skyr", "creme fraiche", "kærnemælk", "mozzarella",
            "parmesan", "cheddar", "kvark", "ymer", "hytteost", "feta", "margarine"]),
        (ShoppingCategory.FruitVeg, ["æble", "banan", "tomat", "agurk", "salat", "gulerod", "kartof", "løg", "hvidløg", "peberfrugt",
            "citron", "appelsin", "avocado", "broccoli", "spinat", "svamp", "frugt", "grønt", "kål", "porre", "squash", "bær", "jordbær",
            "drue", "druer", "pære", "kiwi", "mango", "ingefær", "persille", "basilikum", "dild", "purløg", "majs", "radise", "selleri", "blomkål"]),
        (ShoppingCategory.Pantry, ["mel ", "sukker", "ris ", "pasta", "spaghetti", "nudler", "havregryn", "müsli", "cornflakes", "olie",
            "olivenolie", "rapsolie", "eddike", "krydderi", "salt", "peber", "bønner", "linser", "bouillon", "sovs", "kaffe", "te ", "honning",
            "marmelade", "nutella", "chokolade", "kiks", "chips", "nødder", "rosiner", "bagepulver", "gær", "kakao", "makroner", "couscous",
            "bulgur", "hvedemel"]),
    ];
    public static ShoppingCategory Suggest(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return ShoppingCategory.Other;
        var text = name.Trim().ToLowerInvariant();
        var words = text.Split([' ', ',', '-', '/', '(', ')'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var (category, keywords) in Rules)
            foreach (var keyword in keywords)
                if (Matches(text, words, keyword)) return category;
        return ShoppingCategory.Other;
    }

    // Short words must start a word ("ost" in "ostehaps" but not in "frost"); longer words may also end a compound ("rugbrød"),
    private static bool Matches(string text, string[] words, string keyword)
    {
        var trimmed = keyword.Trim();
        if (trimmed.Contains(' ')) return text.Contains(trimmed, StringComparison.Ordinal);
        if (keyword.EndsWith(' ')) return words.Any(w => w == trimmed); // an exact word, e.g. "is"
        if (trimmed.Length >= 5 && text.Contains(trimmed, StringComparison.Ordinal)) return true;
        // Danish compounds end in their main word ("rugbrød", "letmælk"), so 4+ letter words may also end a word.
        return words.Any(w => w.StartsWith(trimmed, StringComparison.Ordinal)
                              || (trimmed.Length >= 4 && w.EndsWith(trimmed, StringComparison.Ordinal)));
    }
}



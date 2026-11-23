using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace StreamlinedLogistics;

/// <summary>
/// The item categories of chestsort_categories.json in the game folder, the list Quick Deposit and shroudlink's Assistants use:
/// {"categories": {"Ores": {"words": ["metals", "materials"], "items": ["Material_T2_Copper_Bar", "Material_*_Ore", "-*_Seedling", "722268629"], "rest": false}}}.
/// A category's items are debug names without case where * stands for any text, or plain item ids, applied in order; a leading - removes
/// what an entry matches. A category marked "rest" also takes every item that no category lists. Matching follows Quick Deposit's
/// categories.cpp, which follows shroudlink's mod/categories crate.
/// </summary>
public sealed class CategoryTable
{
    /// <summary>One bit per category.</summary>
    public const int MaxCategories = 64;

    private readonly record struct Entry(bool Removes, bool IsId, uint Id, string Pattern);

    private readonly List<string> _names = new();
    private readonly List<List<string>> _words = new();
    private readonly List<List<Entry>> _entries = new();
    private readonly Dictionary<string, ulong> _chestWords = new(StringComparer.Ordinal);
    private ulong _restBits;

    /// <summary>The categories' names, in the file's order. A category's bit is 1 shifted by its place here.</summary>
    public IReadOnlyList<string> Names => _names;

    /// <summary>Reads the file's text. False, with the reason, when it is not a categories file.</summary>
    public static bool TryParse(string? text, out CategoryTable table, out string error)
    {
        table = new CategoryTable();
        error = "";
        if (string.IsNullOrWhiteSpace(text))
        {
            error = "the file is empty";
            return false;
        }
        try
        {
            using JsonDocument document = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("categories", out JsonElement categories) || categories.ValueKind != JsonValueKind.Object)
            {
                error = "the file has no \"categories\" object";
                return false;
            }
            foreach (JsonProperty category in categories.EnumerateObject())
            {
                if (table._names.Count >= MaxCategories || category.Value.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }
                var words = new List<string>();
                if (category.Value.TryGetProperty("words", out JsonElement wordList) && wordList.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement word in wordList.EnumerateArray())
                    {
                        if (word.ValueKind == JsonValueKind.String)
                        {
                            words.Add(word.GetString()!.Trim().ToLowerInvariant());
                        }
                    }
                }
                var entries = new List<Entry>();
                if (category.Value.TryGetProperty("items", out JsonElement itemList) && itemList.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in itemList.EnumerateArray())
                    {
                        if (item.ValueKind != JsonValueKind.String)
                        {
                            continue;
                        }
                        string entryText = item.GetString()!.Trim();
                        bool removes = entryText.StartsWith('-');
                        if (removes)
                        {
                            entryText = entryText[1..].Trim();
                        }
                        if (entryText.Length == 0)
                        {
                            continue;
                        }
                        bool isId = TryParseId(entryText, out uint id);
                        entries.Add(new Entry(removes, isId, id, entryText.ToLowerInvariant()));
                    }
                }
                if (category.Value.TryGetProperty("rest", out JsonElement rest) && rest.ValueKind == JsonValueKind.True)
                {
                    table._restBits |= 1ul << table._names.Count;
                }
                // A chest's name answers to the category's own name and to its words.
                ulong bit = 1ul << table._names.Count;
                foreach (string word in new List<string>(words) { category.Name.Trim().ToLowerInvariant() })
                {
                    if (word.Length > 0)
                    {
                        table._chestWords[word] = table._chestWords.GetValueOrDefault(word) | bit;
                    }
                }
                table._names.Add(category.Name);
                table._words.Add(words);
                table._entries.Add(entries);
            }
            // "Ore" works like "Ores", unless "ore" means something else on its own.
            foreach (KeyValuePair<string, ulong> word in new List<KeyValuePair<string, ulong>>(table._chestWords))
            {
                if (word.Key.Length > 3 && word.Key.EndsWith('s'))
                {
                    table._chestWords.TryAdd(word.Key[..^1], word.Value);
                }
            }
            return true;
        }
        catch (JsonException problem)
        {
            error = problem.Message;
            table = new CategoryTable();
            return false;
        }
    }

    /// <summary>The categories an item is in, by its id and debug name, as bits; the "rest" categories when no category lists it.</summary>
    public ulong BitsOf(uint itemId, string? debugName)
    {
        string name = debugName?.ToLowerInvariant() ?? "";
        ulong bits = 0;
        for (int category = 0; category < _entries.Count; category++)
        {
            bool isIn = false;
            foreach (Entry entry in _entries[category])
            {
                bool matches = entry.IsId ? entry.Id == itemId : name.Length > 0 && WildcardMatch(entry.Pattern, name);
                if (matches)
                {
                    isIn = !entry.Removes;
                }
            }
            if (isIn)
            {
                bits |= 1ul << category;
            }
        }
        return bits != 0 ? bits : _restBits;
    }

    /// <summary>
    /// The categories a chest's name mentions, as bits (0: none). The name's words are its runs of letters and digits, without case:
    /// "Ores &amp; Gems" is Ores and Gems, "Ammo 2" is Ammo, "Ore" is Ores.
    /// </summary>
    public ulong BitsOfChestName(string? chestName)
    {
        if (string.IsNullOrEmpty(chestName))
        {
            return 0;
        }
        ulong bits = 0;
        int wordStart = -1;
        for (int position = 0; position <= chestName.Length; position++)
        {
            bool inWord = position < chestName.Length && char.IsLetterOrDigit(chestName[position]);
            if (inWord)
            {
                if (wordStart < 0)
                {
                    wordStart = position;
                }
                continue;
            }
            if (wordStart >= 0)
            {
                if (_chestWords.TryGetValue(chestName[wordStart..position].ToLowerInvariant(), out ulong wordBits))
                {
                    bits |= wordBits;
                }
                wordStart = -1;
            }
        }
        return bits;
    }

    /// <summary>The bits of the categories with these names (case ignored); names the file does not have are left out.</summary>
    public ulong BitsOfNames(IEnumerable<string> names)
    {
        ulong bits = 0;
        foreach (string name in names)
        {
            for (int category = 0; category < _names.Count; category++)
            {
                if (string.Equals(_names[category], name, StringComparison.OrdinalIgnoreCase))
                {
                    bits |= 1ul << category;
                }
            }
        }
        return bits;
    }

    /// <summary>
    /// The categories hidden until the player says otherwise: what is only ever used through the craft menu or the Construction Hammer.
    /// Those are the categories that answer to the word "materials", plus Blocks and Terrain.
    /// </summary>
    public IReadOnlyList<string> DefaultHidden()
    {
        var hidden = new List<string>();
        for (int category = 0; category < _names.Count; category++)
        {
            string name = _names[category];
            if (_words[category].Contains("materials") || name.Equals("Blocks", StringComparison.OrdinalIgnoreCase) || name.Equals("Terrain", StringComparison.OrdinalIgnoreCase))
            {
                hidden.Add(name);
            }
        }
        return hidden;
    }

    /// <summary>For each category in the file's order: true when it answers to the word "materials" (the category tree files them under Materials).</summary>
    public IReadOnlyList<bool> MaterialFlags()
    {
        var flags = new List<bool>(_names.Count);
        for (int category = 0; category < _names.Count; category++)
        {
            flags.Add(_words[category].Contains("materials"));
        }
        return flags;
    }

    /// <summary>True when the text matches the pattern, where * stands for any text. Both in lower case.</summary>
    public static bool WildcardMatch(string pattern, string text)
    {
        string[] parts = pattern.Split('*');
        if (parts.Length == 1)
        {
            return pattern == text;
        }
        string first = parts[0];
        string last = parts[^1];
        if (text.Length < first.Length + last.Length || !text.StartsWith(first, StringComparison.Ordinal) || !text.EndsWith(last, StringComparison.Ordinal))
        {
            return false;
        }
        int position = first.Length;
        int end = text.Length - last.Length;
        for (int part = 1; part + 1 < parts.Length; part++)
        {
            int found = text.IndexOf(parts[part], position, StringComparison.Ordinal);
            if (found < 0 || found + parts[part].Length > end)
            {
                return false;
            }
            position = found + parts[part].Length;
        }
        return true;
    }

    // A plain item id: digits only, as shroudlink reads one.
    private static bool TryParseId(string text, out uint id)
    {
        id = 0;
        if (text.Length == 0 || text.Length > 10)
        {
            return false;
        }
        foreach (char digit in text)
        {
            if (digit < '0' || digit > '9')
            {
                return false;
            }
        }
        return uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out id);
    }
}

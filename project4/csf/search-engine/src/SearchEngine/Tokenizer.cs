using System.Text.RegularExpressions;

namespace SearchEngine;

public static partial class Tokenizer
{
    private static readonly HashSet<string> Stop = ["a", "an", "and", "are", "as", "at", "be", "by", "for", "from", "in", "is", "it", "of", "on", "or", "that", "the", "to", "was", "with"];

    [GeneratedRegex("[a-z0-9]+")]
    private static partial Regex Word();

    public static IEnumerable<string> Tokenize(string text) =>
        Word().Matches(text.ToLowerInvariant()).Select(m => m.Value).Where(w => !Stop.Contains(w));
}

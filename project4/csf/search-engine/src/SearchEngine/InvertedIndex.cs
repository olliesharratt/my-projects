namespace SearchEngine;

public record SearchHit(string Document, double Score);

/// <summary>Inverted index (term -> {doc -> term frequency}) ranked with Okapi BM25.</summary>
public class InvertedIndex
{
    private const double K1 = 1.2, B = 0.75;
    private readonly Dictionary<string, Dictionary<int, int>> _postings = new();
    private readonly List<string> _docs = [];
    private readonly List<int> _lengths = [];

    public int DocCount => _docs.Count;

    public void Add(string name, string text)
    {
        var id = _docs.Count;
        var tokens = Tokenizer.Tokenize(text).ToList();
        _docs.Add(name);
        _lengths.Add(tokens.Count);
        foreach (var g in tokens.GroupBy(t => t))
        {
            if (!_postings.TryGetValue(g.Key, out var p)) _postings[g.Key] = p = new();
            p[id] = g.Count();
        }
    }

    /// <summary>Exact term if indexed, else the closest indexed term within a small edit distance, else null.</summary>
    public string? Correct(string term)
    {
        if (_postings.ContainsKey(term)) return term;
        var max = term.Length > 5 ? 2 : 1;
        string? best = null;
        var bestDist = int.MaxValue;
        foreach (var v in _postings.Keys)
        {
            if (Math.Abs(v.Length - term.Length) > max) continue; // cheap pre-filter
            var d = EditDistance.Levenshtein(term, v);
            if (d <= max && d < bestDist) { bestDist = d; best = v; }
        }
        return best;
    }

    public List<SearchHit> Search(string query, int top = 5, bool fuzzy = true)
    {
        var avgLen = _lengths.Count == 0 ? 0 : _lengths.Average();
        var scores = new Dictionary<int, double>();
        foreach (var raw in Tokenizer.Tokenize(query).Distinct())
        {
            var term = fuzzy ? Correct(raw) : (_postings.ContainsKey(raw) ? raw : null);
            if (term is null) continue;
            var postings = _postings[term];
            var idf = Math.Log(1 + (DocCount - postings.Count + 0.5) / (postings.Count + 0.5));
            foreach (var (doc, tf) in postings)
            {
                var norm = tf + K1 * (1 - B + B * _lengths[doc] / avgLen);
                scores[doc] = scores.GetValueOrDefault(doc) + idf * tf * (K1 + 1) / norm;
            }
        }
        return scores.OrderByDescending(kv => kv.Value).Take(top).Select(kv => new SearchHit(_docs[kv.Key], kv.Value)).ToList();
    }
}

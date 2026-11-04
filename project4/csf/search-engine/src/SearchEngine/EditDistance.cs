namespace SearchEngine;

public static class EditDistance
{
    /// <summary>Levenshtein distance with two rolling rows: O(n*m) time, O(m) space.</summary>
    public static int Levenshtein(string a, string b)
    {
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var sub = prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                cur[j] = Math.Min(sub, Math.Min(prev[j] + 1, cur[j - 1] + 1));
            }
            (prev, cur) = (cur, prev);
        }
        return prev[b.Length];
    }
}

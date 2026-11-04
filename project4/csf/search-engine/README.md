# search-engine: BM25 from scratch

Indexes a folder of `.txt` files with an inverted index, ranks results with Okapi BM25 and corrects typos using edit distance. C# / .NET 8, no dependencies.

```bash
dotnet run --project src/SearchEngine -- sample-docs
```

Example typo correction:
```text
> turbyne
	1.509  turbines.txt
```

## How it works
- **Tokeniser:** lowercase, split on non-alphanumerics, drop stop words.
- **Inverted index:** `term -> {docId -> term frequency}` plus document lengths. Query time depends on the posting lists of the query terms, not the corpus size.
- **BM25:** `score = sum over terms of IDF * tf*(k1+1) / (tf + k1*(1 - b + b*len/avgLen))` with k1 = 1.2 and b = 0.75. IDF rewards rare terms; the length normalisation stops long documents winning just by being long. Improvement over raw TF-IDF: term frequency saturates.
- **Fuzzy matching:** a query term not in the vocabulary is replaced by the closest indexed term within edit distance 1 (2 for words over 5 letters). Levenshtein is computed with two rolling rows, O(n*m) time and O(m) space, with a length pre-filter to skip hopeless candidates.

Next steps: phrase queries, a trie or BK-tree to speed up fuzzy lookup, persisting the index to disk, snippet highlighting, evaluation with precision@k on a labelled set.

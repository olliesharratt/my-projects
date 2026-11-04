using SearchEngine;

if (args.Length < 1) { Console.Error.WriteLine("Usage: SearchEngine <folder-of-.txt-files>"); return 64; }

var index = new InvertedIndex();
foreach (var file in Directory.EnumerateFiles(args[0], "*.txt", SearchOption.AllDirectories))
    index.Add(Path.GetRelativePath(args[0], file), File.ReadAllText(file));
Console.WriteLine($"Indexed {index.DocCount} documents. Type a query (empty line to quit).");

while (true)
{
    Console.Write("> ");
    var q = Console.ReadLine();
    if (string.IsNullOrWhiteSpace(q)) break;
    var hits = index.Search(q);
    if (hits.Count == 0) Console.WriteLine("  no results");
    foreach (var h in hits) Console.WriteLine($"  {h.Score:F3}  {h.Document}");
}
return 0;

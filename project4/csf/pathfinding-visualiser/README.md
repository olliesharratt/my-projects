# Pathfinding Visualiser

An interactive grid for comparing BFS, Dijkstra, and A*. Open [index.html](index.html) directly in a browser; no build step or dependencies are required.

## Demo

Select **Wall**, drag a vertical barrier across the grid, and leave a gap. Run BFS, then Dijkstra or A*. BFS minimizes steps and ignores weights; Dijkstra and A* account for weighted cells. The status line reports explored cells, path length, and path cost.

| Algorithm | Finds shortest path with weights? | Time | Notes |
|---|---|---|---|
| BFS | No (shortest by steps only) | O(V+E) | FIFO queue |
| Dijkstra | Yes | O((V+E) log V) | Binary min-heap, lazy deletion |
| A* | Yes (admissible heuristic) | O((V+E) log V), usually explores far less | Manhattan distance, never overestimates because the minimum cell cost is 1 |

Try painting a weighted strip across the middle with a gap. Compare the route and explored-cell count between Dijkstra and A*.

## Limitations

The grid uses four-direction movement and fixed costs; there is no automated browser test suite yet.

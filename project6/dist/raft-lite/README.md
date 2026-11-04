# raft-lite: leader election from the Raft paper

The leader-election half of Raft (terms, randomized election timeouts, vote granting, heartbeats, step-down), in C#.
**Not included:** log replication, persistence and membership changes, which is why it's "lite".

```bash
dotnet test tests/RaftLite.Tests        # simulated clusters: crashes, partitions, 50 random seeds
./scripts/cluster.sh                    # 3 real processes talking UDP on localhost
kill <pid of the leader>                # watch the others elect a new one
```

## Design
- **Pure state machine.** `RaftNode` has no sockets, threads or wall clock. A host feeds it `Tick(now)` and `Receive(from, msg, now)`; it sends via `IRaftTransport`. This is the key decision: the same code runs in a deterministic simulator and over UDP.
- **Rules implemented:** a node that hears nothing for a random 150-300 ms becomes a candidate, increments its term, votes for itself and requests votes; a strict majority wins; one vote per term; any message with a higher term makes a node adopt it and step down; leaders send heartbeats every 50 ms.
- **Why randomized timeouts:** if two candidates split the vote, they retry at different random times, so one wins the next round. The 50-seed test checks convergence.
- **Why majorities:** two majorities of the same cluster always overlap, so two leaders can never be elected in the same term. A test asserts "at most one leader per term" across every scenario.

## What the simulator proves (`tests/`)
| Scenario | Checks |
|---|---|
| Fresh cluster | Exactly one leader, everyone on the same term |
| 50 random seeds | Always converges, including split votes |
| Leader crashes, then returns | New leader has a higher term; the old one steps down |
| Minority partition, then heal | Isolated old leader can't be replaced from inside its partition; majority elects a successor; after healing there is one leader |
| Only 2 of 5 nodes alive | No leader (no quorum), so no unsafe progress |
| Duplicate vote requests, stale terms | One vote per term; stale requests rejected |

## Honest limitations
- **Not durable:** real Raft persists `currentTerm` and `votedFor` before replying. Without that, a restarted node could vote twice in one term and break safety.
- **No log**, so the "candidate's log is at least as up to date" vote rule is skipped.
- UDP is lossy, which Raft tolerates (timeouts and retries), but production systems use reliable RPC.

## Next steps
Add a log with `AppendEntries` consistency checks and commit index (the other half of Raft), persist term and vote to disk, then build a replicated key-value store on top. That would pair nicely with the kv-store project.

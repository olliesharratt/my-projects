# thread-pool: a bounded worker pool in C

```bash
make test         # unit tests
make tsan         # same tests under ThreadSanitizer (race detection)
make run-bench    # benchmarks
```

## Design
- **Fixed workers, bounded ring-buffer queue.** One mutex guards the queue; three condition variables separate the waiters: `not_empty` (idle workers), `not_full` (blocked producers), `idle` (`tp_wait`). Separate conditions mean a signal never wakes the wrong kind of thread.
- **Back-pressure:** `tp_submit` blocks when the queue is full instead of growing without bound, so a fast producer can't exhaust memory.
- **Tasks run outside the lock**, so workers execute in parallel and only contend while dequeuing.
- **Graceful shutdown:** `tp_destroy` sets `stop`, wakes everyone, and workers exit only once the queue is drained, so no accepted task is lost.
- Every `pthread_cond_wait` sits in a `while` loop to guard against spurious wakeups.

## Benchmarks
`make run-bench` compares the pool to creating a thread per task, and shows scaling on CPU-bound work. Results depend on your machine; paste your own numbers below and explain them.

| Benchmark | Result |
|---|---|
| 50,000 tiny tasks, pool vs thread-per-task | _paste output_ |
| 64 CPU-bound tasks, 1/2/4/8 threads | _paste output; speedup should flatten at your core count_ |

Why the pool wins on tiny tasks: thread creation costs far more than the task itself. Why speedup flattens: you run out of cores, and extra threads only add context switching.

## Next steps
Per-worker queues with work stealing (less lock contention), futures/results, a lock-free MPMC queue, task priorities.

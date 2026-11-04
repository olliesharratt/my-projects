#include "threadpool.h"
#include <pthread.h>
#include <stdatomic.h>
#include <stdio.h>
#include <stdlib.h>
#include <time.h>
#include <unistd.h>

static atomic_long done;
typedef struct { task_fn fn; void *arg; } job_t;

static double now(void) { struct timespec ts; clock_gettime(CLOCK_MONOTONIC, &ts); return ts.tv_sec + ts.tv_nsec / 1e9; }
static void tiny(void *a) { (void)a; atomic_fetch_add(&done, 1); }
static void burn(void *a) { volatile double x = 0; for (long i = 0; i < *(long *)a; i++) x += i * 0.5; atomic_fetch_add(&done, 1); }
static void *thread_main(void *p) { job_t *j = p; j->fn(j->arg); return NULL; }

/* Baseline: create and join a thread for every task, `width` at a time. */
static double run_spawn(int tasks, int width, task_fn fn, void *arg) {
    atomic_store(&done, 0);
    job_t job = {fn, arg};
    pthread_t *ids = malloc((size_t)width * sizeof *ids);
    double t = now();
    for (int i = 0; i < tasks; i += width) {
        int k = tasks - i < width ? tasks - i : width;
        for (int j = 0; j < k; j++) pthread_create(&ids[j], NULL, thread_main, &job);
        for (int j = 0; j < k; j++) pthread_join(ids[j], NULL);
    }
    double e = now() - t;
    free(ids);
    if (atomic_load(&done) != tasks) printf("ERROR: lost tasks\n");
    return e;
}

static double run_pool(int tasks, int width, task_fn fn, void *arg) {
    atomic_store(&done, 0);
    double t = now();
    threadpool_t *tp = tp_create(width, 1024);
    for (int i = 0; i < tasks; i++) tp_submit(tp, fn, arg);
    tp_wait(tp);
    tp_destroy(tp);
    double e = now() - t;
    if (atomic_load(&done) != tasks) printf("ERROR: lost tasks\n");
    return e;
}

int main(void) {
    printf("CPUs online: %ld\n\n", sysconf(_SC_NPROCESSORS_ONLN));

    int n = 50000, width = 4;
    double s = run_spawn(n, width, tiny, NULL), p = run_pool(n, width, tiny, NULL);
    printf("Benchmark 1: %d tiny tasks, %d threads\n", n, width);
    printf("  thread per task: %7.1f ms\n  thread pool:     %7.1f ms   (%.1fx faster)\n\n", s * 1e3, p * 1e3, s / p);

    long iters = 2000000;
    int tasks = 64;
    printf("Benchmark 2: %d CPU-bound tasks (%ld iterations each), pool scaling\n", tasks, iters);
    double base = 0;
    for (int w = 1; w <= 8; w *= 2) {
        double t = run_pool(tasks, w, burn, &iters);
        if (w == 1) base = t;
        printf("  %d thread%s: %7.1f ms   speedup %.2fx\n", w, w == 1 ? " " : "s", t * 1e3, base / t);
    }
    return 0;
}

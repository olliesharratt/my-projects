#include "threadpool.h"
#include <assert.h>
#include <pthread.h>
#include <stdatomic.h>
#include <stdio.h>
#include <time.h>

static atomic_int counter;
static void inc(void *a) { (void)a; atomic_fetch_add(&counter, 1); }
static void slow_inc(void *a) { (void)a; nanosleep(&(struct timespec){0, 200000}, NULL); atomic_fetch_add(&counter, 1); }

static void test_all_tasks_run_with_tiny_queue(void) {
    atomic_store(&counter, 0);
    threadpool_t *tp = tp_create(4, 8);                 /* queue much smaller than workload: exercises back-pressure */
    for (int i = 0; i < 10000; i++) assert(tp_submit(tp, inc, NULL) == 0);
    tp_wait(tp);
    assert(atomic_load(&counter) == 10000);
    tp_destroy(tp);
}

static threadpool_t *shared;
static void *producer(void *a) { (void)a; for (int i = 0; i < 1000; i++) tp_submit(shared, inc, NULL); return NULL; }

static void test_concurrent_producers(void) {
    atomic_store(&counter, 0);
    shared = tp_create(4, 16);
    pthread_t p[4];
    for (int i = 0; i < 4; i++) pthread_create(&p[i], NULL, producer, NULL);
    for (int i = 0; i < 4; i++) pthread_join(p[i], NULL);
    tp_wait(shared);
    assert(atomic_load(&counter) == 4000);
    tp_destroy(shared);
}

static void test_destroy_drains_queue(void) {
    atomic_store(&counter, 0);
    threadpool_t *tp = tp_create(2, 128);
    for (int i = 0; i < 100; i++) tp_submit(tp, slow_inc, NULL);
    tp_destroy(tp);                                     /* no tp_wait: destroy itself must finish queued work */
    assert(atomic_load(&counter) == 100);
}

static void test_pool_reusable_after_wait(void) {
    atomic_store(&counter, 0);
    threadpool_t *tp = tp_create(2, 4);
    for (int i = 0; i < 50; i++) tp_submit(tp, inc, NULL);
    tp_wait(tp);
    for (int i = 0; i < 50; i++) tp_submit(tp, inc, NULL);
    tp_wait(tp);
    assert(atomic_load(&counter) == 100);
    tp_destroy(tp);
}

static void test_invalid_arguments(void) {
    assert(tp_create(0, 8) == NULL);
    assert(tp_create(2, 0) == NULL);
}

int main(void) {
    test_all_tasks_run_with_tiny_queue();   puts("ok   all tasks run with tiny queue");
    test_concurrent_producers();            puts("ok   concurrent producers");
    test_destroy_drains_queue();            puts("ok   destroy drains queue");
    test_pool_reusable_after_wait();        puts("ok   reusable after wait");
    test_invalid_arguments();               puts("ok   invalid arguments");
    return 0;
}

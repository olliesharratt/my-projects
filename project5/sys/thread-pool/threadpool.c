#include "threadpool.h"
#include <pthread.h>
#include <stdlib.h>

typedef struct { task_fn fn; void *arg; } task_t;

struct threadpool {
    pthread_t *threads;
    int nthreads;
    task_t *queue;                 /* ring buffer */
    int cap, head, tail, count;
    int active;                    /* tasks currently executing */
    int stop;
    pthread_mutex_t lock;
    pthread_cond_t not_empty;      /* workers wait here */
    pthread_cond_t not_full;       /* producers wait here */
    pthread_cond_t idle;           /* tp_wait waits here */
};

static void *worker(void *p) {
    threadpool_t *tp = p;
    for (;;) {
        pthread_mutex_lock(&tp->lock);
        while (tp->count == 0 && !tp->stop) pthread_cond_wait(&tp->not_empty, &tp->lock);
        if (tp->count == 0) { pthread_mutex_unlock(&tp->lock); return NULL; }   /* stop && drained */
        task_t t = tp->queue[tp->head];
        tp->head = (tp->head + 1) % tp->cap;
        tp->count--; tp->active++;
        pthread_cond_signal(&tp->not_full);
        pthread_mutex_unlock(&tp->lock);

        t.fn(t.arg);                                                            /* run outside the lock */

        pthread_mutex_lock(&tp->lock);
        tp->active--;
        if (tp->count == 0 && tp->active == 0) pthread_cond_broadcast(&tp->idle);
        pthread_mutex_unlock(&tp->lock);
    }
}

threadpool_t *tp_create(int nthreads, int queue_capacity) {
    if (nthreads < 1 || queue_capacity < 1) return NULL;
    threadpool_t *tp = calloc(1, sizeof *tp);
    if (!tp) return NULL;
    tp->threads = calloc((size_t)nthreads, sizeof *tp->threads);
    tp->queue = calloc((size_t)queue_capacity, sizeof *tp->queue);
    tp->cap = queue_capacity;
    pthread_mutex_init(&tp->lock, NULL);
    pthread_cond_init(&tp->not_empty, NULL);
    pthread_cond_init(&tp->not_full, NULL);
    pthread_cond_init(&tp->idle, NULL);
    if (!tp->threads || !tp->queue) { free(tp->threads); free(tp->queue); free(tp); return NULL; }
    for (int i = 0; i < nthreads; i++) {
        if (pthread_create(&tp->threads[i], NULL, worker, tp) != 0) { tp->nthreads = i; tp_destroy(tp); return NULL; }
    }
    tp->nthreads = nthreads;
    return tp;
}

int tp_submit(threadpool_t *tp, task_fn fn, void *arg) {
    pthread_mutex_lock(&tp->lock);
    while (tp->count == tp->cap && !tp->stop) pthread_cond_wait(&tp->not_full, &tp->lock);
    if (tp->stop) { pthread_mutex_unlock(&tp->lock); return -1; }
    tp->queue[tp->tail] = (task_t){fn, arg};
    tp->tail = (tp->tail + 1) % tp->cap;
    tp->count++;
    pthread_cond_signal(&tp->not_empty);
    pthread_mutex_unlock(&tp->lock);
    return 0;
}

void tp_wait(threadpool_t *tp) {
    pthread_mutex_lock(&tp->lock);
    while (tp->count > 0 || tp->active > 0) pthread_cond_wait(&tp->idle, &tp->lock);
    pthread_mutex_unlock(&tp->lock);
}

void tp_destroy(threadpool_t *tp) {
    if (!tp) return;
    pthread_mutex_lock(&tp->lock);
    tp->stop = 1;
    pthread_cond_broadcast(&tp->not_empty);
    pthread_cond_broadcast(&tp->not_full);
    pthread_mutex_unlock(&tp->lock);
    for (int i = 0; i < tp->nthreads; i++) pthread_join(tp->threads[i], NULL);
    pthread_mutex_destroy(&tp->lock);
    pthread_cond_destroy(&tp->not_empty);
    pthread_cond_destroy(&tp->not_full);
    pthread_cond_destroy(&tp->idle);
    free(tp->threads); free(tp->queue); free(tp);
}

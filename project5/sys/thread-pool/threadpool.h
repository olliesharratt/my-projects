#ifndef THREADPOOL_H
#define THREADPOOL_H

typedef struct threadpool threadpool_t;
typedef void (*task_fn)(void *arg);

/* Fixed number of workers and a bounded FIFO queue. Returns NULL on failure. */
threadpool_t *tp_create(int nthreads, int queue_capacity);
/* Blocks while the queue is full (back-pressure). Returns 0 on success, -1 if shutting down. */
int tp_submit(threadpool_t *tp, task_fn fn, void *arg);
/* Blocks until every submitted task has finished. The pool stays usable afterwards. */
void tp_wait(threadpool_t *tp);
/* Graceful shutdown: runs every queued task, then joins the workers and frees the pool. */
void tp_destroy(threadpool_t *tp);

#endif

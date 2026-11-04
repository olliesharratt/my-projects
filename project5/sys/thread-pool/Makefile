CC ?= cc
CFLAGS = -O2 -Wall -Wextra -pthread -std=c11 -D_POSIX_C_SOURCE=200809L

all: test_pool bench

test_pool: test_pool.c threadpool.c threadpool.h
	$(CC) $(CFLAGS) -o $@ test_pool.c threadpool.c

bench: bench.c threadpool.c threadpool.h
	$(CC) $(CFLAGS) -o $@ bench.c threadpool.c

test: test_pool
	./test_pool

run-bench: bench
	./bench

# Data-race detection: the test suite under ThreadSanitizer.
tsan:
	$(CC) -g -O1 -Wall -pthread -std=c11 -D_POSIX_C_SOURCE=200809L -fsanitize=thread -o test_pool_tsan test_pool.c threadpool.c && ./test_pool_tsan

clean:
	rm -f test_pool bench test_pool_tsan

.PHONY: all test run-bench tsan clean

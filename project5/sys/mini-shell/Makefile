CC ?= cc
CFLAGS = -O2 -Wall -Wextra -std=c11

msh: msh.c
	$(CC) $(CFLAGS) -o $@ msh.c

test: msh
	bash ./tests/test.sh

clean:
	rm -f msh

.PHONY: test clean

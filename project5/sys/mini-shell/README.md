# msh: a Unix shell in C

```bash
make && ./msh
msh$ ls -l | grep '\.c' | wc -l
msh$ echo hello > out.txt && cat < out.txt     # (no &&: see limitations)
msh$ sleep 5 &
[1] 4242
msh$ jobs
[1] Running  sleep 5
make test
```

## What it supports
Pipelines of up to 16 stages, `<` `>` `>>`, `&` background jobs with `jobs`, single and double quotes, exit status (`127` for unknown commands), builtins `cd` `pwd` `jobs` `exit`.

## How it works
1. **Lexer** splits a line into words and operators, tracking quote state.
2. **Parser** builds a `Pipeline` of `Cmd`s, each with `argv`, optional `in`/`out` files.
3. **Executor** forks one child per stage and connects stage *i* to *i+1* with `pipe()` and `dup2()`. The parent closes its copies of every pipe end; if it didn't, the reader would never see EOF and the pipeline would hang.
4. **Builtins** like `cd` run in the shell process, because a child's `chdir` would be lost.
5. **Signals:** the shell ignores `SIGINT`; children reset it to default before `exec` (an ignored signal would otherwise be inherited). Background jobs get their own process group so `^C` at the prompt doesn't kill them.
6. **Jobs:** finished children are reaped with `waitpid(-1, WNOHANG)` before each prompt, so no zombies pile up.

## Limitations (good "what I'd do next" material)
No `&&`, `;`, variables, globbing or `$?`; no job control (`fg`/`bg`/`^Z`); background completion is announced at the next prompt rather than instantly (a `SIGCHLD` handler would fix that, taking care with async-signal-safety); builtins ignore redirection.

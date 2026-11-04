/* msh: a small Unix shell. Pipes, redirection (< > >>), background jobs (&),
 * quoting, and the builtins cd, pwd, jobs, exit. POSIX only, no dependencies. */
#define _POSIX_C_SOURCE 200809L
#include <errno.h>
#include <fcntl.h>
#include <limits.h>
#include <signal.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <sys/wait.h>
#include <unistd.h>

#define MAX_TOKENS 256
#define MAX_ARGS 64
#define MAX_CMDS 16
#define MAX_JOBS 32

typedef struct { char *s; int op; } Tok;                     /* op=1 for unquoted | < > >> & */
typedef struct { char *argv[MAX_ARGS]; char *in, *out; int append; } Cmd;
typedef struct { Cmd cmds[MAX_CMDS]; int n; int bg; } Pipeline;
typedef struct { pid_t pid; int id; int active; char text[128]; } Job;

static Job jobs[MAX_JOBS];
static int next_job_id = 1;
static int last_status = 0;

/* ---- lexer: splits a line into words and operators, honouring '...' and "..." ---- */
static int tokenize(const char *p, Tok *t, int max, int *pn) {
    *pn = 0;
    for (;;) {
        while (*p == ' ' || *p == '\t' || *p == '\n') p++;
        if (!*p) return 0;
        if (*pn >= max) { fprintf(stderr, "msh: too many tokens\n"); return -1; }
        if (strchr("|<>&", *p)) {
            size_t len = (p[0] == '>' && p[1] == '>') ? 2 : 1;
            t[*pn].s = strndup(p, len); t[*pn].op = 1; (*pn)++; p += len;
            continue;
        }
        char buf[1024]; size_t b = 0; char quote = 0;
        while (*p && (quote || !strchr(" \t\n|<>&", *p))) {
            if (quote) { if (*p == quote) quote = 0; else if (b < sizeof buf - 1) buf[b++] = *p; }
            else if (*p == '\'' || *p == '"') quote = *p;
            else if (b < sizeof buf - 1) buf[b++] = *p;
            p++;
        }
        if (quote) { fprintf(stderr, "msh: unterminated quote\n"); return -1; }
        buf[b] = 0;
        t[*pn].s = strdup(buf); t[*pn].op = 0; (*pn)++;
    }
}

/* ---- parser: tokens -> pipeline of commands ---- */
static int syntax_error(void) { fprintf(stderr, "msh: syntax error\n"); last_status = 2; return -1; }

static int parse(Tok *t, int n, Pipeline *pl) {
    memset(pl, 0, sizeof *pl);
    pl->n = 1;
    int argc = 0;
    for (int i = 0; i < n; i++) {
        Cmd *c = &pl->cmds[pl->n - 1];
        if (!t[i].op) {
            if (argc >= MAX_ARGS - 1) { fprintf(stderr, "msh: too many arguments\n"); last_status = 2; return -1; }
            c->argv[argc++] = t[i].s;
            continue;
        }
        const char *o = t[i].s;
        if (!strcmp(o, "|")) {
            if (argc == 0 || pl->n >= MAX_CMDS) return syntax_error();
            c->argv[argc] = NULL; argc = 0; pl->n++;
        } else if (!strcmp(o, "&")) {
            if (i != n - 1) return syntax_error();
            pl->bg = 1;
        } else {
            if (i + 1 >= n || t[i + 1].op) return syntax_error();
            char *file = t[++i].s;
            if (!strcmp(o, "<")) c->in = file; else { c->out = file; c->append = !strcmp(o, ">>"); }
        }
    }
    pl->cmds[pl->n - 1].argv[argc] = NULL;
    for (int i = 0; i < pl->n; i++) if (!pl->cmds[i].argv[0]) return syntax_error();
    return 0;
}

/* ---- jobs ---- */
static void add_job(pid_t pid, const char *line) {
    for (int i = 0; i < MAX_JOBS; i++) {
        if (jobs[i].active) continue;
        jobs[i].pid = pid; jobs[i].id = next_job_id++; jobs[i].active = 1;
        snprintf(jobs[i].text, sizeof jobs[i].text, "%s", line);
        size_t l = strlen(jobs[i].text);
        while (l > 0 && strchr(" \t\n&", jobs[i].text[l - 1])) jobs[i].text[--l] = 0;
        printf("[%d] %d\n", jobs[i].id, (int)pid);
        return;
    }
    fprintf(stderr, "msh: job table full\n");
}

/* Reap any finished children (no zombies) and announce finished background jobs. */
static void reap(void) {
    int st; pid_t p;
    while ((p = waitpid(-1, &st, WNOHANG)) > 0)
        for (int i = 0; i < MAX_JOBS; i++)
            if (jobs[i].active && jobs[i].pid == p) {
                printf("[%d] Done  %s\n", jobs[i].id, jobs[i].text);
                jobs[i].active = 0;
            }
}

/* ---- builtins run inside the shell process (cd must, or it would only change a child's directory) ---- */
static int builtin(char **argv) {
    if (!strcmp(argv[0], "exit")) exit(argv[1] ? atoi(argv[1]) : last_status);
    if (!strcmp(argv[0], "cd")) {
        const char *d = argv[1] ? argv[1] : getenv("HOME");
        if (!d || chdir(d) < 0) { perror("cd"); last_status = 1; } else last_status = 0;
        return 1;
    }
    if (!strcmp(argv[0], "pwd")) {
        char b[PATH_MAX];
        if (getcwd(b, sizeof b)) puts(b);
        last_status = 0;
        return 1;
    }
    if (!strcmp(argv[0], "jobs")) {
        for (int i = 0; i < MAX_JOBS; i++) if (jobs[i].active) printf("[%d] Running  %s\n", jobs[i].id, jobs[i].text);
        last_status = 0;
        return 1;
    }
    return 0;
}

/* ---- executor: fork one child per stage, wire stages together with pipe()/dup2() ---- */
static void run(Pipeline *pl, const char *line) {
    if (pl->n == 1 && !pl->bg && builtin(pl->cmds[0].argv)) return;

    pid_t pids[MAX_CMDS];
    int started = 0, prev = -1;
    for (int i = 0; i < pl->n; i++) {
        Cmd *c = &pl->cmds[i];
        int fd[2] = {-1, -1};
        if (i < pl->n - 1 && pipe(fd) < 0) { perror("pipe"); break; }
        pid_t pid = fork();
        if (pid < 0) { perror("fork"); if (fd[0] >= 0) { close(fd[0]); close(fd[1]); } break; }
        if (pid == 0) {
            signal(SIGINT, SIG_DFL);                  /* the shell ignores ^C; children must not */
            if (pl->bg) setpgid(0, 0);                /* own process group: ^C at the prompt won't kill it */
            if (prev != -1) { dup2(prev, STDIN_FILENO); close(prev); }
            if (fd[1] != -1) { dup2(fd[1], STDOUT_FILENO); close(fd[0]); close(fd[1]); }
            if (c->in) {
                int f = open(c->in, O_RDONLY);
                if (f < 0) { perror(c->in); _exit(1); }
                dup2(f, STDIN_FILENO); close(f);
            }
            if (c->out) {
                int f = open(c->out, O_WRONLY | O_CREAT | (c->append ? O_APPEND : O_TRUNC), 0644);
                if (f < 0) { perror(c->out); _exit(1); }
                dup2(f, STDOUT_FILENO); close(f);
            }
            execvp(c->argv[0], c->argv);
            fprintf(stderr, "msh: %s: %s\n", c->argv[0], strerror(errno));
            _exit(127);
        }
        pids[started++] = pid;
        if (prev != -1) close(prev);                  /* parent must close its copies or readers never see EOF */
        if (fd[1] != -1) close(fd[1]);
        prev = fd[0];
    }
    if (prev != -1) close(prev);

    if (pl->bg) { if (started > 0) add_job(pids[started - 1], line); return; }
    for (int i = 0; i < started; i++) {
        int st = 0;
        while (waitpid(pids[i], &st, 0) < 0 && errno == EINTR) {}
        if (i == started - 1) last_status = WIFEXITED(st) ? WEXITSTATUS(st) : 128 + WTERMSIG(st);
    }
}

int main(void) {
    signal(SIGINT, SIG_IGN);
    char *line = NULL; size_t cap = 0;
    int tty = isatty(STDIN_FILENO);
    for (;;) {
        reap();
        if (tty) { printf("msh$ "); fflush(stdout); }
        if (getline(&line, &cap, stdin) < 0) break;   /* ^D */
        Tok toks[MAX_TOKENS]; int nt = 0;
        if (tokenize(line, toks, MAX_TOKENS, &nt) == 0 && nt > 0) {
            Pipeline pl;
            if (parse(toks, nt, &pl) == 0) { fflush(stdout); run(&pl, line); }
        }
        for (int i = 0; i < nt; i++) free(toks[i].s);
    }
    if (tty) putchar('\n');
    free(line);
    return last_status;
}

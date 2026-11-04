#!/bin/bash
# Black-box tests: feed commands to msh on stdin and compare stdout.
cd "$(dirname "$0")/.." || exit 1
MSH=./msh; fail=0; tmp=$(mktemp -d)
check() { if [ "$2" == "$3" ]; then echo "ok   $1"; else echo "FAIL $1"; echo "  expected: [$2]"; echo "  actual:   [$3]"; fail=1; fi; }

check "simple command"  "hi"    "$(printf 'echo hi\n' | $MSH)"
check "pipe"            "HELLO" "$(printf 'echo hello | tr a-z A-Z\n' | $MSH)"
check "three-stage pipe" "2"    "$(printf 'printf "a\\nb\\nc\\n" | grep -v b | wc -l\n' | $MSH | tr -d ' ')"
check "redirect out and in" "data" "$(printf 'echo data > %s/o.txt\ncat < %s/o.txt\n' "$tmp" "$tmp" | $MSH)"
check "append" $'a\nb' "$(printf 'echo a > %s/f\necho b >> %s/f\ncat %s/f\n' "$tmp" "$tmp" "$tmp" | $MSH)"
check "quotes keep spaces" "a   b" "$(printf 'echo "a   b"\n' | $MSH)"
check "cd and pwd"      "$(cd "$tmp" && pwd -P)" "$(printf 'cd %s\npwd\n' "$tmp" | $MSH)"
check "pipe plus redirect" "3" "$(printf 'printf "x\\ny\\nz\\n" | wc -l > %s/n\ncat %s/n\n' "$tmp" "$tmp" | $MSH | tr -d ' ')"
check "background job lifecycle" $'[1] PID\n[1] Running  sleep 0.3\n[1] Done  sleep 0.3' \
  "$(printf 'sleep 0.3 &\njobs\nsleep 0.8\n' | $MSH | sed 's/^\[1\] [0-9][0-9]*$/[1] PID/')"
printf 'nosuchcmd\nexit\n' | $MSH 2>/dev/null; check "unknown command exits 127" "127" "$?"
printf 'false\nexit\n' | $MSH; check "exit status propagates" "1" "$?"

rm -rf "$tmp"; exit $fail

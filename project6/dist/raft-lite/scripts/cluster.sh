#!/bin/bash
# Starts a 3-node cluster on localhost. Ctrl+C stops everything.
# Watch the logs, then `kill <pid of the leader>` in another terminal to see a new leader elected.
cd "$(dirname "$0")/.." || exit 1
dotnet build src/RaftLite -c Release -v q >/dev/null || exit 1
trap 'kill $(jobs -p) 2>/dev/null' EXIT
for i in 1 2 3; do
  peers=()
  for j in 1 2 3; do [ "$j" != "$i" ] && peers+=("n$j=127.0.0.1:700$j"); done
  dotnet src/RaftLite/bin/Release/net8.0/RaftLite.dll "n$i" "700$i" "${peers[@]}" &
  echo "n$i pid $!"
done
wait

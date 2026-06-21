#!/bin/bash
# Hält den Mac beim Start wach (aktiv solange der Server läuft)
caffeinate -i &
CAFE=$!

cd /Users/revia-dev/Documents/Revia/Revia
/usr/local/share/dotnet/dotnet run --launch-profile lan

kill $CAFE 2>/dev/null

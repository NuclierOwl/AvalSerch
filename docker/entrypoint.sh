#!/bin/bash
set -e

# Виртуальный дисплей для Avalonia (GUI без монитора)
export DISPLAY="${DISPLAY:-:99}"

Xvfb "${DISPLAY}" -screen 0 1920x1080x24 -ac +extension GLX +render -noreset &
sleep 1

exec "$@"

#!/usr/bin/env bash
set -euo pipefail

APP_ROOT="./publicacion/NegativeLauncher-macos-universal.app"
EXECUTABLE="$APP_ROOT/Contents/MacOS/NegativeLauncher.AvaloniaPrototype"
TEST_HOME="$RUNNER_TEMP/negative-launcher-macos-test-home"
SCREENSHOT_DIR="$RUNNER_TEMP/negative-launcher-ui-screenshots-$NEGATIVE_LAUNCHER_TARGET_RID"
SMOKE_LOG="$RUNNER_TEMP/negative-launcher-macos-ui-smoke-$NEGATIVE_LAUNCHER_TARGET_RID.log"
APP_LOG="$TEST_HOME/Library/Application Support/NegativeClient/logs/negative-launcher.log"
mkdir -p "$TEST_HOME" "$SCREENSHOT_DIR"
test -x "$EXECUTABLE"

HOME="$TEST_HOME" \
  NEGATIVE_LAUNCHER_UI_SMOKE=1 \
  NEGATIVE_LAUNCHER_UI_SCREENSHOT_DIR="$SCREENSHOT_DIR" \
  "$EXECUTABLE" > "$SMOKE_LOG" 2>&1 &
APP_PID=$!
APP_FINISHED_SMOKE=false
for _ in $(seq 1 90); do
  if grep -Fq "NEGATIVE_LAUNCHER_UI_SMOKE_OK" "$SMOKE_LOG"; then
    APP_FINISHED_SMOKE=true
    break
  fi
  if grep -Fq "NEGATIVE_LAUNCHER_UI_SMOKE_FAILED" "$SMOKE_LOG"; then
    break
  fi
  if ! kill -0 "$APP_PID" 2>/dev/null; then
    break
  fi
  sleep 1
done
if [ "$APP_FINISHED_SMOKE" != true ]; then
  cat "$SMOKE_LOG"
  if [ -f "$APP_LOG" ]; then cat "$APP_LOG"; fi
  kill "$APP_PID" 2>/dev/null || true
  wait "$APP_PID" 2>/dev/null || true
  echo "La app macOS no terminó el recorrido automatizado." >&2
  exit 1
fi
kill "$APP_PID" 2>/dev/null || true
wait "$APP_PID" 2>/dev/null || true

test -s "$APP_LOG"
grep -Fq "Inicialización de datos completada; la ventana principal está lista." "$APP_LOG"
python3 - "$SCREENSHOT_DIR" <<'PY'
from pathlib import Path
import struct
import sys

screenshot_dir = Path(sys.argv[1])
expected = (
    "home.png",
    "instances.png",
    "gallery.png",
    "gallery-viewer.png",
    "modpack-dialog.png",
    "developer.png",
    "settings.png",
)
missing = []
invalid = []
for filename in expected:
    path = screenshot_dir / filename
    try:
        header = path.read_bytes()[:24]
    except OSError:
        missing.append(filename)
        continue
    if (
        len(header) < 24
        or header[:8] != b"\x89PNG\r\n\x1a\n"
        or header[12:16] != b"IHDR"
        or min(struct.unpack(">II", header[16:24])) <= 0
    ):
        invalid.append(filename)

if missing or invalid:
    if missing:
        print("Faltan capturas del recorrido macOS: " + ", ".join(missing), file=sys.stderr)
    if invalid:
        print("Las capturas no son PNG válidos: " + ", ".join(invalid), file=sys.stderr)
    sys.exit(1)

print(f"Capturas del recorrido macOS verificadas: {len(expected)}.")
PY
cat "$SMOKE_LOG"

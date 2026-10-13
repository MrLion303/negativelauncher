#!/usr/bin/env bash
set -euo pipefail

PUBLISH_ROOT="$RUNNER_TEMP/negative-launcher-linux-publish"
PACKAGE_DIR="$RUNNER_TEMP/NegativeLauncher-Linux"
mkdir -p "$PUBLISH_ROOT" "$PACKAGE_DIR" "$PACKAGE_DIR/Assets" ./publicacion

for RID in linux-x64 linux-arm64; do
  dotnet publish "Prototipo Avalonia/NegativeLauncher.AvaloniaPrototype.csproj" \
    --configuration Release \
    --runtime "$RID" \
    --self-contained true \
    --output "$PUBLISH_ROOT/$RID" \
    -p:UseAppHost=true \
    -p:PublishTrimmed=false
  test -x "$PUBLISH_ROOT/$RID/NegativeLauncher.AvaloniaPrototype"
  mkdir -p "$PACKAGE_DIR/$RID"
  cp -a "$PUBLISH_ROOT/$RID/." "$PACKAGE_DIR/$RID/"
  chmod +x "$PACKAGE_DIR/$RID/NegativeLauncher.AvaloniaPrototype"
done
cp "$PUBLISH_ROOT/linux-x64/Assets/negativeclient_logo.png" "$PACKAGE_DIR/Assets/"

cat > "$PACKAGE_DIR/iniciar-negative-launcher.sh" <<'LAUNCHER'
#!/usr/bin/env sh
set -eu
SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
HOST_ARCH="$(uname -m)"
case "$HOST_ARCH" in
  x86_64|amd64) RID="linux-x64" ;;
  aarch64|arm64) RID="linux-arm64" ;;
  *)
    echo "Arquitectura Linux no incluida en este paquete: $HOST_ARCH" >&2
    exit 1
    ;;
esac
PAYLOAD="$SCRIPT_DIR/$RID"
EXECUTABLE="$PAYLOAD/NegativeLauncher.AvaloniaPrototype"
if [ ! -x "$EXECUTABLE" ]; then
  echo "No se encontró el launcher para $RID dentro del paquete." >&2
  exit 1
fi
cd "$PAYLOAD"
exec "./NegativeLauncher.AvaloniaPrototype" "$@"
LAUNCHER
chmod +x "$PACKAGE_DIR/iniciar-negative-launcher.sh"

cat > "$PACKAGE_DIR/instalar-acceso-escritorio.sh" <<'INSTALLER'
#!/usr/bin/env sh
set -eu
SCRIPT_DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
LAUNCHER="$SCRIPT_DIR/iniciar-negative-launcher.sh"
if [ ! -x "$LAUNCHER" ]; then
  echo "Ejecuta este instalador desde la carpeta completa de Negative Launcher." >&2
  exit 1
fi
ESCAPED_LAUNCHER="$(printf '%s' "$LAUNCHER" | sed 's/\\/\\\\/g; s/"/\\"/g; s/%/%%/g')"
DATA_HOME="$(printenv XDG_DATA_HOME 2>/dev/null || true)"
case "$DATA_HOME" in
  /*) ;;
  *) DATA_HOME="$HOME/.local/share" ;;
esac
APPLICATIONS_DIR="$DATA_HOME/applications"
mkdir -p "$APPLICATIONS_DIR"
ICON_ENTRY=""
ICON_SOURCE="$SCRIPT_DIR/Assets/negativeclient_logo.png"
if [ -f "$ICON_SOURCE" ]; then
  ICON_DIR="$DATA_HOME/icons/hicolor/256x256/apps"
  mkdir -p "$ICON_DIR"
  cp "$ICON_SOURCE" "$ICON_DIR/negative-launcher.png"
  ICON_ENTRY="Icon=negative-launcher"
fi
DESKTOP_FILE="$APPLICATIONS_DIR/negative-launcher.desktop"
cat > "$DESKTOP_FILE" <<ENTRY
[Desktop Entry]
Version=1.0
Type=Application
Name=Negative Launcher
Comment=Launcher de Minecraft de NegativeStudios
Exec="$ESCAPED_LAUNCHER"
$ICON_ENTRY
Terminal=false
Categories=Game;
ENTRY
chmod 644 "$DESKTOP_FILE"
printf 'Acceso creado: %s\n' "$DESKTOP_FILE"
INSTALLER
chmod +x "$PACKAGE_DIR/instalar-acceso-escritorio.sh"

cat > "$PACKAGE_DIR/LEEME.txt" <<'README'
Negative Launcher 0.1.1 — Linux x64 y ARM64

INICIO
1. Extrae el paquete completo en una carpeta permanente.
2. Ejecuta iniciar-negative-launcher.sh desde el gestor de archivos
   o desde una terminal. El selector elige x64 o ARM64 automáticamente.
3. No muevas las carpetas linux-x64 o linux-arm64 fuera de esta carpeta.
4. Opcional: ejecuta instalar-acceso-escritorio.sh para crear un acceso
   en el menú de aplicaciones de Linux.

Este paquete contiene las publicaciones autocontenidas de ambas
arquitecturas. En distribuciones mínimas puede ser necesario instalar
las bibliotecas gráficas que requiere Avalonia.

El paquete de macOS se publica por separado como una app universal.
README

sh -n "$PACKAGE_DIR/iniciar-negative-launcher.sh"
sh -n "$PACKAGE_DIR/instalar-acceso-escritorio.sh"
test -s "$PACKAGE_DIR/Assets/negativeclient_logo.png"

TEST_ROOT="$RUNNER_TEMP/negative-launcher-dispatch-test"
FAKE_BIN="$TEST_ROOT/fake-bin"
mkdir -p "$FAKE_BIN"
cp "$PACKAGE_DIR/iniciar-negative-launcher.sh" "$TEST_ROOT/"
cat > "$FAKE_BIN/uname" <<'UNAME'
#!/usr/bin/env sh
printf '%s\n' "$NEGATIVE_LAUNCHER_TEST_UNAME"
UNAME
chmod +x "$FAKE_BIN/uname"
for RID in linux-x64 linux-arm64; do
  mkdir -p "$TEST_ROOT/$RID"
  cat > "$TEST_ROOT/$RID/NegativeLauncher.AvaloniaPrototype" <<'STUB'
#!/usr/bin/env sh
printf '%s\n' "$NEGATIVE_LAUNCHER_TEST_RID" "$@" > "$NEGATIVE_LAUNCHER_TEST_ARGS"
STUB
  chmod +x "$TEST_ROOT/$RID/NegativeLauncher.AvaloniaPrototype"
done

NEGATIVE_LAUNCHER_TEST_UNAME=x86_64 \
  NEGATIVE_LAUNCHER_TEST_RID=linux-x64 \
  NEGATIVE_LAUNCHER_TEST_ARGS="$TEST_ROOT/x64-args.txt" \
  PATH="$FAKE_BIN:$PATH" \
  "$TEST_ROOT/iniciar-negative-launcher.sh" "argumento con espacios" segundo
grep -Fx "linux-x64" "$TEST_ROOT/x64-args.txt"
grep -Fx "argumento con espacios" "$TEST_ROOT/x64-args.txt"
grep -Fx "segundo" "$TEST_ROOT/x64-args.txt"

NEGATIVE_LAUNCHER_TEST_UNAME=aarch64 \
  NEGATIVE_LAUNCHER_TEST_RID=linux-arm64 \
  NEGATIVE_LAUNCHER_TEST_ARGS="$TEST_ROOT/arm64-args.txt" \
  PATH="$FAKE_BIN:$PATH" \
  "$TEST_ROOT/iniciar-negative-launcher.sh" "argumento con espacios" segundo
grep -Fx "linux-arm64" "$TEST_ROOT/arm64-args.txt"
grep -Fx "argumento con espacios" "$TEST_ROOT/arm64-args.txt"
grep -Fx "segundo" "$TEST_ROOT/arm64-args.txt"

mkdir -p "$TEST_ROOT/home"
HOME="$TEST_ROOT/home" \
  XDG_DATA_HOME="$TEST_ROOT/home/.local/share" \
  "$PACKAGE_DIR/instalar-acceso-escritorio.sh"
DESKTOP_FILE="$TEST_ROOT/home/.local/share/applications/negative-launcher.desktop"
grep -Fx "Name=Negative Launcher" "$DESKTOP_FILE"
grep -Fx "Terminal=false" "$DESKTOP_FILE"
grep -Fx "Categories=Game;" "$DESKTOP_FILE"
grep -Fx "Icon=negative-launcher" "$DESKTOP_FILE"
test -s "$TEST_ROOT/home/.local/share/icons/hicolor/256x256/apps/negative-launcher.png"
ESCAPED_LAUNCHER="$(printf '%s' "$PACKAGE_DIR/iniciar-negative-launcher.sh" | sed 's/\\/\\\\/g; s/"/\\"/g; s/%/%%/g')"
grep -Fx "Exec=\"$ESCAPED_LAUNCHER\"" "$DESKTOP_FILE"

ARCHIVE="./publicacion/NegativeLauncher-linux-universal.tar.gz"
tar -czf "$ARCHIVE" -C "$PACKAGE_DIR" .
tar -tzf "$ARCHIVE" > "$RUNNER_TEMP/negative-launcher-linux-universal-contents.txt"
grep -Fq "linux-x64/NegativeLauncher.AvaloniaPrototype" "$RUNNER_TEMP/negative-launcher-linux-universal-contents.txt"
grep -Fq "linux-arm64/NegativeLauncher.AvaloniaPrototype" "$RUNNER_TEMP/negative-launcher-linux-universal-contents.txt"
grep -Fq "iniciar-negative-launcher.sh" "$RUNNER_TEMP/negative-launcher-linux-universal-contents.txt"
grep -Fq "instalar-acceso-escritorio.sh" "$RUNNER_TEMP/negative-launcher-linux-universal-contents.txt"
sha256sum "$ARCHIVE"

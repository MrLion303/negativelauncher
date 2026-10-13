#!/usr/bin/env bash
set -euo pipefail

PUBLISH_ROOT="$RUNNER_TEMP/negative-launcher-macos-publish"
mkdir -p "$PUBLISH_ROOT" ./publicacion
for RID in osx-x64 osx-arm64; do
  dotnet publish "Prototipo Avalonia/NegativeLauncher.AvaloniaPrototype.csproj" \
    --configuration Release \
    --runtime "$RID" \
    --self-contained true \
    --output "$PUBLISH_ROOT/$RID" \
    -p:UseAppHost=true \
    -p:PublishTrimmed=false \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true
  test -x "$PUBLISH_ROOT/$RID/NegativeLauncher.AvaloniaPrototype"
done

APP_ROOT="./publicacion/NegativeLauncher-macos-universal.app"
CONTENTS="$APP_ROOT/Contents"
MACOS_DIR="$CONTENTS/MacOS"
RESOURCES_DIR="$CONTENTS/Resources"
mkdir -p "$MACOS_DIR" "$RESOURCES_DIR"
cp -R "$PUBLISH_ROOT/osx-arm64/." "$MACOS_DIR/"
lipo -create \
  "$PUBLISH_ROOT/osx-x64/NegativeLauncher.AvaloniaPrototype" \
  "$PUBLISH_ROOT/osx-arm64/NegativeLauncher.AvaloniaPrototype" \
  -output "$MACOS_DIR/NegativeLauncher.AvaloniaPrototype"
chmod +x "$MACOS_DIR/NegativeLauncher.AvaloniaPrototype"
ARCHS="$(lipo -archs "$MACOS_DIR/NegativeLauncher.AvaloniaPrototype")"
echo "Arquitecturas de Negative Launcher.app: $ARCHS"
echo "$ARCHS" | grep -Fq x86_64
echo "$ARCHS" | grep -Fq arm64

ICON_SOURCE="Negative Client/Negative Client/Assets/negativeclient_logo.png"
ICONSET="$RUNNER_TEMP/NegativeLauncher.iconset"
mkdir -p "$ICONSET"
sips -s format png -z 16 16 "$ICON_SOURCE" --out "$ICONSET/icon_16x16.png" >/dev/null
sips -s format png -z 32 32 "$ICON_SOURCE" --out "$ICONSET/icon_16x16@2x.png" >/dev/null
sips -s format png -z 32 32 "$ICON_SOURCE" --out "$ICONSET/icon_32x32.png" >/dev/null
sips -s format png -z 64 64 "$ICON_SOURCE" --out "$ICONSET/icon_32x32@2x.png" >/dev/null
sips -s format png -z 128 128 "$ICON_SOURCE" --out "$ICONSET/icon_128x128.png" >/dev/null
sips -s format png -z 256 256 "$ICON_SOURCE" --out "$ICONSET/icon_128x128@2x.png" >/dev/null
sips -s format png -z 256 256 "$ICON_SOURCE" --out "$ICONSET/icon_256x256.png" >/dev/null
sips -s format png -z 512 512 "$ICON_SOURCE" --out "$ICONSET/icon_256x256@2x.png" >/dev/null
sips -s format png -z 512 512 "$ICON_SOURCE" --out "$ICONSET/icon_512x512.png" >/dev/null
sips -s format png -z 1024 1024 "$ICON_SOURCE" --out "$ICONSET/icon_512x512@2x.png" >/dev/null
iconutil --convert icns "$ICONSET" --output "$RESOURCES_DIR/NegativeLauncher.icns"

cat > "$CONTENTS/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleExecutable</key>
  <string>NegativeLauncher.AvaloniaPrototype</string>
  <key>CFBundleIconFile</key>
  <string>NegativeLauncher</string>
  <key>CFBundleIdentifier</key>
  <string>com.negativestudios.negativelauncher</string>
  <key>CFBundleName</key>
  <string>Negative Launcher</string>
  <key>CFBundleDisplayName</key>
  <string>Negative Launcher</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>0.1.1</string>
  <key>CFBundleVersion</key>
  <string>0.1.1</string>
  <key>LSMinimumSystemVersion</key>
  <string>12.0</string>
  <key>LSApplicationCategoryType</key>
  <string>public.app-category.games</string>
  <key>NSHighResolutionCapable</key>
  <true/>
</dict>
</plist>
PLIST

plutil -lint "$CONTENTS/Info.plist"
codesign --force --deep --sign - "$APP_ROOT"
codesign --verify --deep --strict "$APP_ROOT"
ditto -c -k --sequesterRsrc --keepParent \
  "$APP_ROOT" "./publicacion/NegativeLauncher-macos-universal.zip"
unzip -t "./publicacion/NegativeLauncher-macos-universal.zip"
shasum -a 256 "./publicacion/NegativeLauncher-macos-universal.zip"

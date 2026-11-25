#!/bin/bash
# Builds Planillium.app (Apple Silicon) into ./dist. Usage: tools/build-app.sh
set -euo pipefail
cd "$(dirname "$0")/.."
export PATH="$HOME/.local/bin:$HOME/.local/share/dotnet:$PATH"
VER=$(sed -n 's|.*<Version>\(.*\)</Version>.*|\1|p' Planillium.Mac.csproj)
APP="dist/Planillium.app"
rm -rf dist/pub "$APP"; mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
dotnet publish Planillium.Mac.csproj -c Release -r osx-arm64 --self-contained true -o dist/pub >/dev/null
cp -R dist/pub/. "$APP/Contents/MacOS/"
# icon
IS=dist/icon.iconset; rm -rf $IS; mkdir -p $IS
for s in 16 32 128 256; do sips -z $s $s Assets/icon.png --out $IS/icon_${s}x${s}.png >/dev/null; done
sips -z 32 32 Assets/icon.png --out $IS/icon_16x16@2x.png >/dev/null
sips -z 64 64 Assets/icon.png --out $IS/icon_32x32@2x.png >/dev/null
sips -z 256 256 Assets/icon.png --out $IS/icon_128x128@2x.png >/dev/null
iconutil -c icns $IS -o "$APP/Contents/Resources/Planillium.icns"; rm -rf $IS
cat > "$APP/Contents/Info.plist" <<PL
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleName</key><string>Planillium</string>
<key>CFBundleDisplayName</key><string>Planillium</string>
<key>CFBundleIdentifier</key><string>com.planillium.app</string>
<key>CFBundleExecutable</key><string>Planillium.Mac</string>
<key>CFBundleIconFile</key><string>Planillium</string>
<key>CFBundleVersion</key><string>$VER</string>
<key>CFBundleShortVersionString</key><string>$VER</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>LSMinimumSystemVersion</key><string>12.0</string>
<key>NSHighResolutionCapable</key><true/>
</dict></plist>
PL
codesign --force --deep --sign - "$APP" >/dev/null 2>&1
echo "Built $APP (v$VER)"

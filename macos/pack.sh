#!/bin/bash
set -euo pipefail

project=$(cd "$(dirname "$0")" && pwd)
destination=${1:-"$project/dist"}
mkdir -p "$destination"
destination=$(cd "$destination" && pwd)
app="$destination/NXNative Easy Builder.app"
if [[ -e "$app" ]]; then
    printf 'Use an empty destination for the .app bundle.\n' >&2
    exit 1
fi

stage=$(mktemp -d "$destination/.nxnative-app.XXXXXXXX")
trap 'rm -rf -- "$stage"' EXIT
bundle="$stage/NXNative Easy Builder.app"
mkdir -p "$bundle/Contents/MacOS" "$bundle/Contents/Resources"
cd "$project"
dotnet publish NXNativeEasyBuilder.csproj -c Release -r osx-arm64 --self-contained true \
    -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true \
    -p:DebugType=None -p:DebugSymbols=false -o "$bundle/Contents/MacOS"
cp Assets/Info.plist "$bundle/Contents/Info.plist"
cp Assets/App.icns "$bundle/Contents/Resources/App.icns"
cp LICENSE "$bundle/Contents/Resources/Builder-License.txt"
cp Assets/ThirdPartyNotices.txt "$bundle/Contents/Resources/ThirdPartyNotices.txt"
chmod +x "$bundle/Contents/MacOS/NXNativeEasyBuilder"

if [[ $(uname -s) == Darwin ]]; then
    for dylib in "$bundle"/Contents/MacOS/*.dylib; do
        codesign --force --sign - "$dylib"
    done
    codesign --force --sign - "$bundle"
    codesign --verify --deep --strict "$bundle"
else
    "${NXMAC_RCODESIGN:-rcodesign}" sign "$bundle"
fi
python3 scripts/verify_bundle.py "$bundle"
mv "$bundle" "$app"
printf '%s\n' "$app"

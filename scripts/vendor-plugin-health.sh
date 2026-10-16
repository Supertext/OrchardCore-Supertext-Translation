#!/bin/sh
# Copies the .NET part of Supertext Plugin Health (version reports to the Supertext Commando
# Center, the API key check result, the ping answer) from the Commando Center repository into
# lib/supertext-plugin-health, with its namespace changed from Supertext.PluginHealth to
# Supertext.OrchardCore.Translation.PluginHealth. The module compiles this copy into its own
# assembly, like the bundled SDK, so sites install nothing besides the module.
#
#   scripts/vendor-plugin-health.sh [commit-or-branch]   (default: main)
#   PLUGIN_HEALTH_DIR=/path/to/clone scripts/vendor-plugin-health.sh   (use a local clone as it is)
set -eu
REF="${1:-main}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

if [ -n "${PLUGIN_HEALTH_DIR:-}" ]; then
    SRC="$PLUGIN_HEALTH_DIR"
else
    git clone --quiet https://github.com/Supertext/Commando-Center-Supertext-Translation "$TMP/center"
    git -C "$TMP/center" checkout --quiet "$REF"
    SRC="$TMP/center"
fi
COMMIT="$(git -C "$SRC" rev-parse HEAD)"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$SRC/plugin-health/dotnet/src/Supertext.PluginHealth/Supertext.PluginHealth.csproj" | head -n 1)"
if [ -z "$VERSION" ]; then
    echo "vendor-plugin-health: no <Version> in plugin-health/dotnet/src/Supertext.PluginHealth/Supertext.PluginHealth.csproj" >&2
    exit 1
fi

DEST="$ROOT/lib/supertext-plugin-health"
rm -rf "$DEST"
mkdir -p "$DEST/src"
(cd "$SRC/plugin-health/dotnet/src/Supertext.PluginHealth" && find . -name '*.cs' -not -path './obj/*' -not -path './bin/*' | while read -r f; do
    mkdir -p "$DEST/src/$(dirname "$f")"
    cp "$f" "$DEST/src/$f"
done)
cp "$SRC/LICENSE" "$DEST/LICENSE"
find "$DEST/src" -name '*.cs' -exec sed -i 's/\bSupertext\.PluginHealth\b/Supertext.OrchardCore.Translation.PluginHealth/g' {} +
printf '%s\n' "$COMMIT" > "$DEST/COMMIT"
printf '%s\n' "$VERSION" > "$DEST/VERSION"
cat > "$DEST/README.md" <<EOT
# Supertext Plugin Health (bundled copy)

.NET part of [Supertext Plugin Health](https://github.com/Supertext/Commando-Center-Supertext-Translation/tree/main/plugin-health),
version $VERSION, commit \`$COMMIT\`, namespace \`Supertext.OrchardCore.Translation.PluginHealth\` instead of \`Supertext.PluginHealth\`.
Compiled into Supertext.OrchardCore.Translation.dll (see the package's \`.csproj\`).

Don't edit these files: fix it in Commando-Center-Supertext-Translation/plugin-health, then run \`scripts/vendor-plugin-health.sh\`.
EOT
if grep -rqE 'Supertext\.PluginHealth([^.A-Za-z]|$)' "$DEST/src"; then
    echo "vendor-plugin-health: unprefixed Supertext.PluginHealth reference left in $DEST/src" >&2
    exit 1
fi
echo "lib/supertext-plugin-health: Supertext Plugin Health (.NET) $VERSION from commit $COMMIT"

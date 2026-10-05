#!/bin/sh
# Links App_Data (SQLite database, media, data protection keys, logs) into /data and starts
# Orchard Core. On first boot, AutoSetup installs the site from the SupertextDemo recipe with
# the DEMO_ADMIN_* account; the demo's DemoSetupEvents then adds languages, the editor
# account and sample content on every start.
set -e

APP=/app
DATA=/data
mkdir -p "$DATA/App_Data"
[ -L "$APP/App_Data" ] || rm -rf "$APP/App_Data"
ln -sfn "$DATA/App_Data" "$APP/App_Data"

# Fallback names (kept for consistency with the other Supertext demos).
DEMO_ADMIN_EMAIL="${DEMO_ADMIN_EMAIL:-$ORCHARD_ADMIN_EMAIL}"
DEMO_ADMIN_PASSWORD="${DEMO_ADMIN_PASSWORD:-$ORCHARD_ADMIN_PASSWORD}"
export DEMO_ADMIN_EMAIL DEMO_ADMIN_PASSWORD

if [ ! -f "$DATA/App_Data/Sites/Default/appsettings.json" ]; then
  if [ -z "$DEMO_ADMIN_EMAIL" ] || [ -z "$DEMO_ADMIN_PASSWORD" ]; then
    echo "DEMO_ADMIN_EMAIL / DEMO_ADMIN_PASSWORD are not set - refusing to install without an admin account." >&2
    exit 1
  fi
  echo "First boot: Orchard Core installs the Supertext demo on the first request..."
fi

# Orchard Core user names can't contain "@": the admin's user name is the e-mail's local
# part (same rule as DemoSetupEvents.UserNameFor). Sign in with the e-mail address.
ADMIN_USERNAME=$(printf '%s' "${DEMO_ADMIN_EMAIL%%@*}" | tr -cd 'A-Za-z0-9._+-')
export OrchardCore__OrchardCore_AutoSetup__Tenants__0__AdminUsername="${ADMIN_USERNAME:-admin}"
export OrchardCore__OrchardCore_AutoSetup__Tenants__0__AdminEmail="$DEMO_ADMIN_EMAIL"
export OrchardCore__OrchardCore_AutoSetup__Tenants__0__AdminPassword="$DEMO_ADMIN_PASSWORD"

[ -n "$PORT" ] && export ASPNETCORE_URLS="http://+:$PORT"

cd "$APP"
exec dotnet SupertextDemo.dll

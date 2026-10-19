#!/bin/bash
# Installs the Impact Kids ProPresenter agent for the current user, and starts it now and at every
# login. Rerunning it is the upgrade. Run it from the unzipped release folder:
#
#     ./install.sh                       # production Impact Kids
#     ./install.sh --url https://...     # somewhere else
#
# No sudo, no .app: a LaunchAgent in ~/Library/LaunchAgents. The first time it starts it opens the
# browser so a leader can approve this Mac. See docs/work/2026-10-propresenter-agent.md.
set -euo pipefail

LABEL="au.com.baptist.impactkids.propresenter"
BINARY="impactkids-propresenter-agent"
HERE="$(cd "$(dirname "$0")" && pwd)"
# Always written into the LaunchAgent: the agent has no default, so it can never quietly connect
# somewhere nobody chose.
URL="https://kids.baptist.com.au"

while [ $# -gt 0 ]; do
    case "$1" in
        --url) URL="${2:?--url needs an address}"; shift 2 ;;
        *) echo "Usage: $0 [--url https://impact-kids-address]" >&2; exit 2 ;;
    esac
done

# NOT uname -m. A Terminal running under Rosetta reports x86_64 on an Apple Silicon Mac, and would
# install the Intel build to run translated forever. This asks the hardware.
if [ "$(sysctl -n hw.optional.arm64 2>/dev/null || echo 0)" = "1" ]; then
    ARCH="osx-arm64"
else
    ARCH="osx-x64"
fi

if [ ! -x "$HERE/$ARCH/$BINARY" ]; then
    echo "Could not find $ARCH/$BINARY next to this script - run it from the unzipped release folder." >&2
    exit 1
fi

INSTALL_DIR="$HOME/Library/Application Support/ImpactKids/ProPresenterAgent"
BIN_DIR="$INSTALL_DIR/bin"
LOG_DIR="$HOME/Library/Logs/ImpactKids"
LOG_FILE="$LOG_DIR/ProPresenterAgent.log"
PLIST="$HOME/Library/LaunchAgents/$LABEL.plist"
DOMAIN="gui/$(id -u)"

echo "Installing the Impact Kids ProPresenter agent ($ARCH)"

# Stop the running copy before replacing its files. Not running is fine.
launchctl bootout "$DOMAIN/$LABEL" 2>/dev/null || true

# Replace the program, never the credential beside it - an upgrade must not need approving again.
rm -rf "$BIN_DIR"
mkdir -p "$BIN_DIR" "$LOG_DIR" "$HOME/Library/LaunchAgents"
cp -R "$HERE/$ARCH/." "$BIN_DIR/"

# A downloaded binary carries com.apple.quarantine and Gatekeeper would refuse it; it is also only
# ad-hoc signed, which Apple Silicon requires of anything it runs. Both are fixed here, on this Mac.
xattr -dr com.apple.quarantine "$BIN_DIR" 2>/dev/null || true
codesign --force --sign - "$BIN_DIR/$BINARY" >/dev/null

ENVIRONMENT_XML="
        <key>DOTNET_ENVIRONMENT</key>
        <string>Production</string>
        <key>Agent__ImpactKidsUrl</key>
        <string>$URL</string>"

# WorkingDirectory is load bearing: launchd starts processes in /, and the agent reads its
# appsettings.json from its working directory.
cat > "$PLIST" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>$LABEL</string>
    <key>ProgramArguments</key>
    <array>
        <string>$BIN_DIR/$BINARY</string>
    </array>
    <key>WorkingDirectory</key>
    <string>$BIN_DIR</string>
    <key>EnvironmentVariables</key>
    <dict>$ENVIRONMENT_XML
    </dict>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>ThrottleInterval</key>
    <integer>10</integer>
    <key>StandardOutPath</key>
    <string>$LOG_FILE</string>
    <key>StandardErrorPath</key>
    <string>$LOG_FILE</string>
</dict>
</plist>
PLIST

launchctl bootstrap "$DOMAIN" "$PLIST"

echo
echo "Installed and running. It starts by itself whenever this user logs in."
echo "If this Mac is not connected yet, a browser opens to approve it."
echo "Log: $LOG_FILE"

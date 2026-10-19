#!/bin/bash
# Removes the Impact Kids ProPresenter agent from this Mac, including its credential. The machine
# still appears on Impact Kids' admin page until it is disconnected there too.
set -euo pipefail

LABEL="au.com.baptist.impactkids.propresenter"

launchctl bootout "gui/$(id -u)/$LABEL" 2>/dev/null || true
rm -f "$HOME/Library/LaunchAgents/$LABEL.plist"
rm -rf "$HOME/Library/Application Support/ImpactKids/ProPresenterAgent"

echo "Removed. Disconnect this Mac on the ProPresenter admin page in Impact Kids as well."
echo "The log is left at $HOME/Library/Logs/ImpactKids/ProPresenterAgent.log"

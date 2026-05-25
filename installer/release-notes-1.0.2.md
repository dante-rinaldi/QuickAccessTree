## What's new in v1.0.2

**Bug fixes**
- Fixed: sidebar became unclickable after closing and reopening Explorer (WPF layered window input fix)
- Fixed: color picker in "Per Folder Only" mode was cascading color to all children
- Fixed: "Reset Color" was wiping children's colors instead of only resetting the target folder
- Fixed: setting a color or resetting collapsed the entire tree
- Fixed: visibility delay setting was ignored until Settings was opened (now applied at startup)
- Fixed: visibility delay had no effect due to sidebar being repositioned before the delay fired
- Fixed: "Restore expanded state" kept saving even when disabled
- Fixed: installer "Already Running" error when upgrading over a running instance
- Removed non-functional "Show Sidebar" tray menu item (sidebar appears automatically with Explorer)

**Improvements**
- First-run defaults: Quick Access pinned at root, My Places group with standard Windows folders
- Show This PC and Show Control Panel now enabled by default
- Added version number display on License page
- Added "Check for Updates" button on License page (checks GitHub releases)
- Startup update check runs in background
- Renamed "Auto-hide with Explorer" to "Auto-hide in background"
- Renamed "Restore expanded state on launch" to "Restore expanded state after close/restart"
- Installer now kills any running instance before upgrading

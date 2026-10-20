# quickUI source snapshot

Unmodified quickUI 0.1.1 library, imported from the local `quickUI` project on 2026-09-06. The source had no Git metadata or published package; SHA256SUMS records the exact imported files. MIT license is included.

Armoryeet builds this snapshot into QuickUI.dll and bundles it with the plugin. No sibling checkout or separately installed plugin is required. Gallery and development artifacts are excluded.

Maintain quickUI in its own project. To update this snapshot, copy its library sources, project, lock file, Directory.Build.props, and LICENSE together; regenerate SHA256SUMS, rebuild Armoryeet, refresh dependency locks if needed, and run the model tests and in-game UI checks. Do not fork styling inside this directory.

Version 0.1.1 brightens preset secondary/status text and distinguishes FFXIV warning and accent colors. Built-in presets pass the existing readability checks; saved custom colors are preserved.

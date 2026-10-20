# Armoryeet

<p align="center">
  <img src="src/Armoryeet/images/icon.png" alt="Armoryeet icon" width="128">
</p>

Armoryeet is a Dalamud plugin for safely cleaning unused gear out of the FFXIV Armoury Chest.

It compares Armoury Chest items against equipped gear and every saved gearset, presents the movable results for review, and moves only the items you select. Capacity checks, movement blockers, retry limits, and a final gearset check protect every reviewed move.

- Localized item names and game icons, grouped in Armoury Chest order.
- Select individual items, complete containers, or the entire scan. Compact category checkboxes show partial selection and include filtered-out items.
- Live inventory-capacity checks with an explicit reviewed confirmation.
- Automatic pauses during combat, duties, crafting, gathering, cutscenes, trades, loading, and other unsafe states.
- Gearset and equipped-item protection immediately before every move.
- Character-bound reviews and queues, cleared on logout or character changes.
- Pauses when required inventory or protection data is unavailable.
- Transfer confirmation checks both the source and the attempted destination.
- Clear progress, completion summaries, and item-specific skip reasons. Empty results offer one Scan again action; Move and selection tools appear only when there are review items. Stop remains available throughout active movement.
- Optional post-move rescan, close-after-move behavior, and advanced confirmation bypass.
- Immediate category commands for experienced users.

Soul crystals are always excluded.

## Installation

Add this custom plugin repository in **Dalamud Settings → Experimental → Custom Plugin Repositories**:

```text
https://raw.githubusercontent.com/Pekururu/Armoryeet/main/repo.json
```

Save the repository list, then install **Armoryeet** from `/xlplugins`.

## Reviewed workflow

1. Open Armoryeet with `/yeet` or `/armoryeet`.
2. Scan the enabled Armoury Chest containers.
3. Review the results and adjust the selection.
4. Move the selected items to inventory.
5. Confirm the count and available capacity.

Successfully moved rows disappear. Items skipped because their slot changed, they became protected, or retries were exhausted remain available for review.

## Commands

| Command | Purpose |
| --- | --- |
| `/yeet` | Open Armoryeet. |
| `/armoryeet` | Alias for opening Armoryeet. |
| `/yeet scan` | Scan enabled containers and open reviewed results. |
| `/yeet status` | Report movement state and counts in chat. |
| `/yeet stop` | Stop the active move queue. |
| `/yeetsettings` | Open settings. |
| `/yeet help` | Show the command summary in chat. |

The following expert commands scan and enqueue immediately without the reviewed confirmation window:

```text
/yeet chest
/yeet weapons
/yeet mainhand
/yeet offhand
/yeet armor
/yeet head
/yeet body
/yeet hands
/yeet waist
/yeet legs
/yeet feet
/yeet accessories
/yeet ears
/yeet neck
/yeet wrist
/yeet rings
```

Run `/yeet help` or hover the `?` button in the main window for aliases. Expert commands still apply gearset protection and movement blockers.

## Settings

- **Behavior:** scanning on open/login, post-move rescanning, and close-after-move behavior.
- **Scan scope:** choose the weapon, armour, and accessory containers included by normal scans.
- **Movement:** configure inter-item delay and the advanced confirmation bypass.

Behavior, scan scope, and movement settings are saved immediately.

Open **Settings → Appearance** to choose Tokyo Night (default), Dalamud Default, FFXIV Menu, or custom colors. Changes preview across the main and settings windows; **Apply** saves them. **Cancel**, leaving the Appearance tab, or closing Settings restores the last applied theme. Appearance retains a fixed readable palette. Theme preferences belong to Armoryeet and do not affect other plugins.

Armoryeet bundles quickUI 0.1.1 and its MIT license. No separate theming plugin is required.

## Development and verification

Use .NET SDK `10.0.301` (pinned in `global.json`) and Dalamud API 15 references from XIVLauncher. The SDK discovers the normal launcher installation; set `DALAMUD_HOME` to use a different reference directory.

```bash
dotnet build src/Armoryeet/Armoryeet.csproj -c Release -p:RestoreLockedMode=true
dotnet run --project tests/Armoryeet.ModelTests/Armoryeet.ModelTests.csproj -c Release -p:RestoreLockedMode=true
```

The build uses the checked-in [quickUI source snapshot](vendor/quickUI/README.md); no sibling checkout is needed. CI also verifies the release ZIP contains QuickUI.dll and its license.

The console harness tests theme compatibility, Apply/Cancel, failed saves, and the production assembly, including movement decisions against a simulated inventory boundary and completion while the window is closed. `dotnet test` does not execute these assertions. CI runs both commands against the stable Dalamud distribution.

Before releasing movement changes, verify in-game: logout and character switching, stale reviews, unavailable/loading inventory, gearset protection, full bags, rejected moves, combat pauses, Stop, and completion with the window closed. Confirm normal transfers and HQ items in `/xllog`. An accepted request is sent once, then checked up to ten times at 250 ms intervals while data is available; unconfirmed transfers stay in the review with a skip reason. Stop cancels future work but cannot undo a request already sent to the game.

Before releasing UI changes, check main and settings windows in-game at 1x, 1.5x, and 2x scale. Verify scrolling keeps Move/Stop visible, filtering preserves hidden selections, confirmation still rejects stale reviews, Apply survives reload, leaving Appearance or closing Settings discards drafts, and other plugins retain their own styling.

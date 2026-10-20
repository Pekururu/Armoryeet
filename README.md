# Armoryeet

<p align="center">
  <img src="src/Armoryeet/images/icon.png" alt="Armoryeet icon" width="128">
</p>

Armoryeet is a Dalamud plugin that clears unused gear out of the FFXIV Armoury Chest. It moves only the items you select, and only after checking them against your equipped gear and every saved gearset.

Soul crystals are always excluded.

## Installation

1. In **Dalamud Settings → Experimental → Custom Plugin Repositories**, add:

   ```text
   https://raw.githubusercontent.com/Pekururu/Armoryeet/main/repo.json
   ```

2. Save the list.
3. Install **Armoryeet** from `/xlplugins`.

## How it works

1. Open Armoryeet with `/yeet` or `/armoryeet`.
2. Scan the enabled Armoury Chest containers.
3. Review the results and adjust the selection.
4. Move the selected items to inventory.
5. Confirm the count and available capacity.

Moved rows disappear. Skipped items stay in the review, with a reason. An item is skipped when its slot changed, it became protected, or retries ran out.

### Features

- **Review:** localized item names and game icons, grouped in Armoury Chest order. Select single items, whole containers, or the entire scan. Category checkboxes show partial selection and include filtered-out items.
- **Protection:** equipped items and gearset items are checked again immediately before every move. Movement pauses if inventory or protection data is unavailable.
- **Capacity:** live inventory-capacity check, with explicit confirmation before moving.
- **Safe states:** movement pauses during combat, duties, crafting, gathering, cutscenes, trades, loading, and other unsafe states.
- **Verification:** each transfer is confirmed at both the source and the attempted destination.
- **Character binding:** reviews and queues belong to one character and are cleared on logout or character change.
- **Progress:** progress display, completion summary, and item-specific skip reasons. Stop stays available during movement. Empty results show only Scan again; Move and selection tools appear when there are items to review.
- **Options:** post-move rescan, close-after-move, and an advanced confirmation bypass.

## Commands

| Command | Purpose |
| --- | --- |
| `/yeet`, `/armoryeet` | Open Armoryeet. |
| `/yeet scan` | Scan enabled containers and open the reviewed results. |
| `/yeet status` | Report movement state and counts in chat. |
| `/yeet stop` | Stop the active move queue. |
| `/yeetsettings` | Open settings. |
| `/yeet help` | Show the command summary in chat. |

### Expert commands

These scan and queue immediately, with no review window. Gearset protection and movement blockers still apply.

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

For aliases, run `/yeet help` or hover the `?` button in the main window.

## Settings

Behavior, scan scope, and movement settings save immediately.

- **Behavior:** scan on open/login, post-move rescan, close-after-move.
- **Scan scope:** which weapon, armour, and accessory containers normal scans include.
- **Movement:** delay between items, advanced confirmation bypass.
- **Appearance:** Tokyo Night (default), Dalamud Default, FFXIV Menu, or custom colors.
  - Changes preview across the main and settings windows. **Apply** saves them.
  - **Cancel**, leaving the Appearance tab, or closing Settings restores the last applied theme.
  - Theme preferences belong to Armoryeet and do not affect other plugins.

Armoryeet bundles Diktat 0.1.1 and its MIT license. No separate theming plugin is needed.

## Development

Requirements:

- .NET SDK `10.0.301` (pinned in `global.json`).
- Dalamud API 15 references from XIVLauncher. The SDK finds the normal launcher install; set `DALAMUD_HOME` to use another directory.

```bash
dotnet build src/Armoryeet/Armoryeet.csproj -c Release -p:RestoreLockedMode=true
dotnet run --project tests/Armoryeet.ModelTests/Armoryeet.ModelTests.csproj -c Release -p:RestoreLockedMode=true
```

- The build uses the checked-in Diktat snapshot in `vendor/Diktat`. No sibling checkout is needed.
- The second command is a console harness. It covers theme compatibility, Apply/Cancel, failed saves, and the production assembly, including movement decisions against a simulated inventory boundary and completion with the window closed. `dotnet test` does not run these assertions.
- CI runs both commands against the stable Dalamud distribution and checks that the release ZIP contains `Diktat.dll` and its license.

### Release checks: movement changes

Verify in-game:

- Logout and character switching.
- Stale reviews.
- Unavailable or loading inventory.
- Gearset protection.
- Full bags and rejected moves.
- Combat pauses.
- Stop, and completion with the window closed.
- Normal and HQ transfers in `/xllog`.

An accepted request is sent once, then checked up to ten times at 250 ms intervals while data is available. Unconfirmed transfers stay in the review with a skip reason. Stop cancels future work but cannot undo a request already sent to the game.

### Release checks: UI changes

Check the main and settings windows in-game at 1x, 1.5x, and 2x scale. Verify:

- Scrolling keeps Move and Stop visible.
- Filtering preserves hidden selections.
- Confirmation rejects stale reviews.
- Apply survives a reload.
- Leaving Appearance or closing Settings discards drafts.
- Other plugins keep their own styling.

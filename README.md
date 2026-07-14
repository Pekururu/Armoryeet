# Armoryeet

<p align="center">
  <img src="src/Armoryeet/images/icon.png" alt="Armoryeet icon" width="128">
</p>

Armoryeet is a Dalamud plugin for safely cleaning unused gear out of the FFXIV Armoury Chest.

It compares Armoury Chest items against equipped gear and every saved gearset, presents the movable results for review, and moves only the items you select. Capacity checks, movement blockers, retry limits, and a final gearset check protect every reviewed move.

- Localized item names and game icons, grouped in Armoury Chest order.
- Select individual items, complete containers, or the entire scan.
- Live inventory-capacity checks with an explicit reviewed confirmation.
- Automatic pauses during combat, duties, crafting, gathering, cutscenes, trades, loading, and other unsafe states.
- Gearset and equipped-item protection immediately before every move.
- Clear progress, completion summaries, and item-specific skip reasons.
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

Every setting is saved immediately.

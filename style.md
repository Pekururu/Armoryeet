# Armoryeet UI style

Armoryeet uses quickUI 0.1.1 for its visual system. This supersedes the earlier rounded orange theme proposal. The MIT-licensed library snapshot lives in `vendor/quickUI`; keep reusable styling changes in the upstream quickUI project and update the snapshot together.

## Theme and lifecycle

- Tokyo Night is the default, with opaque square surfaces, thin borders, and compact layouts.
- `PluginUI` and `ConfigUI` derive from `ThemedWindow`. Implement `DrawContent`; let Dalamud's `WindowSystem` own the complete style scope.
- One plugin-owned `ThemeEditor` previews colors before all windows draw. Preserve each window's layout metrics. Appearance keeps its default readable palette.
- Armoryeet owns persistence. Apply saves a detached configuration before updating live preferences. Cancel and leaving Appearance or closing Settings discard unapplied drafts. Existing behavior settings save immediately.
- Never mutate global ImGui styling or persist preview drafts implicitly.

## Components and invariants

Use quickUI metrics, surfaces, sections, selectable rows, buttons, status labels, progress, and modal helpers. Native inputs inherit the scoped theme. Keep action IDs stable and separate from labels.

The review body scrolls while a divided footer keeps capacity guidance left and actions right, stacking when width is limited. Enabled primary actions use a solid accent fill. Compact category headers combine a tri-state checkbox, collapse control, and selected/total count; category toggles include filtered-out items. Summary and confirmation headlines use slightly enlarged text, with protected counts and helper text kept secondary. Open and draw confirmation outside the child ID scope. Preserve Armoury order, group selection across filtered rows, hover details, and item-specific skip reasons.

Empty results hide filtering, selection tools, and Move; completion shows moved/free-slot totals and one scan action. Skipped rows retain their review controls. Settings uses compact checkboxes and an inline Appearance tab with a scrolling editor and fixed Apply/Cancel footer. Confirmation places Cancel before the right-aligned, count-labeled Move action, with stale-review warnings immediately above.

Colors supplement visible state labels. Disabled actions explain their restrictions. Stop remains enabled during movement. Keep all capacity, identity, snapshot, gearset, and movement checks in Armoryeet's existing owners.

Borrow game icons from Dalamud's shared texture cache; never dispose borrowed wraps. Scale custom geometry once using `ImGuiHelpers.GlobalScale` and measure text with the active font.

## Verification

Run the Release build and model harness, including theme persistence tests. Verify QuickUI.dll and QuickUI.LICENSE.txt in the release ZIP. In-game, check all windows, popup cancellation and stale confirmation, scrolling, keyboard focus, Apply/Cancel/reload, and other plugins' styling at normal and enlarged UI scales.

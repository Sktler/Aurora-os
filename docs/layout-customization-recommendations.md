# Layout customization investigation (#248)

## Existing behavior and constraints

`DashboardLayout`, `MainWindow.LoadSavedLayout`, and `SaveLayout` already support
Default, Compact, Chat focused, and Companion focused presets; independently
hidden navigation/information panels; and draggable splitters. Widths are
clamped to 160–600 device-independent pixels, with defaults of 230 and 350.
Settings persist these choices per active profile. Settings entries can also be
pinned independently to Home. Chat and mini-companion windows provide existing
alternatives to the dashboard.

The current splitters do not enforce a useful minimum for the center column.
A layout saved on a large display can therefore squeeze the composer on a
smaller display. Discoverability is limited to the layout menu, panel toggles,
and splitter affordances. The information panel uses a fixed widget order.
These are concrete code/layout findings, not results of user interviews.

## Recommended implementation order

| Priority | Option | Behavior and acceptance criteria |
| --- | --- | --- |
| 1 | Adaptive panel visibility | Preserve a center width of at least 420 DIPs. Below the sum of this minimum, visible side widths, and splitter widths, temporarily collapse the information panel, then navigation. Restore them when space returns. Keep saved preferences separate from temporary responsive state. |
| 1 | Layout controls in Appearance | Expose existing presets, panel toggles, and numeric/slider widths in Settings; retain the Home shortcuts. Keyboard users can change widths without dragging. Reset restores only layout fields, with no effect on data, providers, or permissions. |
| 2 | Widget visibility/order | Allow hiding and reordering weather, provider status, music, and conversation widgets. Persist stable widget IDs per profile; ignore retired/unknown IDs and append new defaults. Provide a single reset. Retain the composer and Emergency Stop. |
| 2 | Remember chat-window geometry | Restore bounded size/position per profile. Clamp to the current monitor work area after display/DPI changes. Keep Chat content usable at its existing minimum size. |
| 3 | Named layout presets | Add user-named presets after validating demand for the first two priorities. Store layout metadata only; never clone credentials, memory, or capability grants. |

Avoid arbitrary docking or unrestricted placement for the demo. Neither is
needed to address the observed constraints, and both introduce new focus,
window-management, and persistence behavior. Emergency Stop must retain its
reserved window space rather than becoming a movable widget.

## Validation and decisions needed for future work

Validate the adaptive layout at 1024×768, 1280×720, and 1920×1080, at 100%, 150%,
and 200% scale; move a window between monitors; unplug a monitor; switch
profiles; and reopen with saved maximum side widths. Test Tab order, keyboard
width controls, screen-reader names, high-contrast focus, and long German text.
The central composer, Send, and Emergency Stop must remain visible and usable.

Before implementing widget ordering, ask users which widgets they hide and
whether they prefer dragging or Move up/Move down actions. Before named
presets, establish whether presets should include window geometry. These are
product decisions for a future implementation; #248 asks for investigation and
recommendations, so this change introduces no new layout settings or migration.

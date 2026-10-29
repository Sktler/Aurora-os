# Context-menu theme regression checks (issue #5)

Run from the repository root on Windows with the SDK selected by `global.json`
and the .NET 8 Windows Desktop runtime for the separate harness:

```powershell
dotnet run --project tests/MenuThemeChecks -- .
.\scripts\test.ps1 -Sandbox -NoRestore
dotnet build Aurora.App/Aurora.App.csproj --configuration Release --no-restore
```

The check process loads the real application XAML resource dictionary without
running Aurora's `App` class. It loads the composer's menu declarations from
`MainWindow.xaml`, removing only the click-handler attributes. The profile test
creates an unstyled `ContextMenu` with the same structure as `ProfileMenu_Click`,
so it also covers implicit styling of menus created in code.

Restore application and test packages before using `-NoRestore` on a fresh checkout.

Checks cover rendered header contrast in normal, highlighted, and disabled
states (at least 4.5:1), header color matching the owning item, re-enabling,
themed separators, template application, and click-event routing. They also
cover localized access keys, custom and templated headers, explicit header
colors, live theme-brush replacement, icons, input gestures, and submenu and
keyboard-navigation template settings. Highlighting is set through WPF's
protected property setter; this does not send mouse or keyboard input.
PNG renders are saved in `artifacts/menu-theme/after/` (ignored by Git).

No Aurora startup, settings, database, API calls, microphone, camera, or speech
services are used. These are isolated presentation tests, not a complete app
interaction test. Before release, open both the composer `+` menu and profile
dropdown in Aurora, check pointer and keyboard highlighting, Escape dismissal,
and their existing actions. Also check the Settings combo boxes for regressions.

## Reproduce the pre-fix rendering

While HEAD still refers to the pre-fix commit, run:

```powershell
dotnet run --project tests/MenuThemeChecks -- . --baseline
```

This reads `HEAD:Aurora.App/App.xaml` without changing the working tree and saves
renders in `artifacts/menu-theme/before/`. At baseline `b35367c`, disabled
items use `TextMutedBrush` (`#D0D7E2`), but rendered headers stay white
(`#FFFFFF`). Contrast checks pass while disabled header-color checks fail.
The diagnostic output shows that the header's foreground comes from a style,
although the header presenter already has the muted color. Once the fix is
committed, HEAD is no longer the pre-fix baseline.

# Context-menu theme regression checks (issue #5)

Run from the repository root on Windows with the .NET 8 runtime and an SDK:

```powershell
dotnet run --project tests/MenuThemeChecks -- .
dotnet build ZoeyOS.sln --configuration Release
```

The check process loads the real application XAML resource dictionary without
running Aurora's `App` class. It loads the composer's menu declarations from
`MainWindow.xaml`, removing only the click-handler attributes. The profile test
creates an unstyled `ContextMenu` with the same structure as `ProfileMenu_Click`,
so it also covers implicit styling of menus created in code.

Checks cover rendered header contrast in normal, highlighted, and disabled
states (at least 4.5:1), disabled text color, themed separators, template
application, and click-event routing. Highlighting is set through WPF's
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

This reads `HEAD:ZoeyOS.App/App.xaml` without changing the working tree and saves
renders in `artifacts/menu-theme/before/`. At baseline `452b180`, the rendered
headers are white (`#FFFFFF`) over the default light menu (`#F5F5F5`). The
contrast and custom-theme assertions intentionally fail. Once the fix is
committed, HEAD is no longer the pre-fix baseline.

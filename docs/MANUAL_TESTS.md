# Manual test checklist (Windows)

Dashboard
- [ ] App starts; values appear within ~2 s; CPU shows "Measuring…" for the first second only.
- [ ] CPU/RAM/disk roughly match Task Manager; process count is close to Task Manager's.
- [ ] GPU and temperature cards say "Unavailable" (no invented numbers).
- [ ] Pause stops updates; Resume restarts them; minimizing/restoring keeps working.

Resolution Manager
- [ ] Your monitor(s) are listed; primary is marked; current mode is selected.
- [ ] Only supported resolutions/refresh rates appear; ★ marks common presets.
- [ ] Apply a different mode: screen changes, "Keep these display settings?" appears with a countdown.
- [ ] Do nothing: after 15 s the previous mode returns automatically.
- [ ] Apply again and press Revert: previous mode returns immediately.
- [ ] Apply again and press Keep: mode stays after restarting the app.
- [ ] Close the app while the confirmation is showing: previous mode is restored.
- [ ] "Windows display settings" opens Settings > Display.

General
- [ ] Window drag, minimize, maximize (taskbar stays visible), close, resize edges.
- [ ] Log file exists in %LocalAppData%\UNIIZI\TWEAKER01\logs.

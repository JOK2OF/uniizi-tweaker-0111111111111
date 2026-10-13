# UNIIZI TWEAKER 01 - status

Legend: DONE = built green by GitHub Actions. WRITTEN = code exists but has NOT been compiled or run yet.
Nothing marked WRITTEN may be assumed to work until a green Actions run and a manual test confirm it.

## Steps
| # | Module | State |
|---|--------|-------|
| 1 | Base shell (borderless window, 14-page sidebar, theme, transitions) | DONE |
| 2 | Dashboard (CPU, RAM, disk, processes, uptime, Windows version, resolution, power plan, startup count, 2 graphs) | DONE - confirmed on a real PC (Windows 11 25H2) |
| 3 | Resolution Manager (detect, CDS_TEST, temporary apply, 15 s auto-revert, Keep/Revert) | DONE (builds green) - NOT yet manually tested |
| 4 | Cache Cleaner | WRITTEN - Core logic + tests + service + view model + view. Not yet wired into MainViewModel / MainWindow.xaml, not compiled, not tested |
| 5 | Memory and processes | TODO |
| 6 | Gaming Optimizer, profiles, Quick Boost | TODO |
| 7 | Startup Manager | TODO |
| 8 | Network Tools | TODO |
| 9 | Game Profiles | TODO |
| 10 | Restore Center, Settings, About, logs, System Monitor, final tests | TODO |

## Step 4 - Cache Cleaner details
Categories (fixed list, nothing else can be added at runtime): user temp (3 days old), Chrome / Edge / Brave / Firefox caches
(separate), Steam and Epic web caches, GPU shader caches (caution, off by default), crash dumps (7 days), old Windows error
reports (30 days, system part may need admin), Recycle Bin (only after an extra explicit confirmation).

Safety rules implemented in `CleanupPolicy` (Core, unit tested): allowed roots only, no symbolic links / junctions followed,
no drive roots, no Windows / Program Files / Documents / Downloads / Desktop / Saved Games, protected names (cookies, login data,
browser profiles data, password files, saves), system and read-only files skipped, minimum age, locked files skipped and reported.
One scan or clean at a time. All work runs off the UI thread. Report shows real counts: files deleted, bytes (sum of deleted file
sizes), skipped items with the reason, error details, per-category lines. History: `%LocalAppData%\UNIIZI\TWEAKER01\cache-cleaner-history.json`.

Known limits: sizes are logical file sizes (not size on disk); progress is a counter, not a percentage (total unknown);
the scan is an estimate and the real result can differ; the log (AppLog) is not wired in yet.

## Known gaps in earlier steps
- GPU load / VRAM / temperatures and CPU temperature show "Unavailable" (needs a sensor library, e.g. LibreHardwareMonitor; some values will stay unavailable without admin rights).
- Dashboard: no time-range selector, no sampling-interval setting; startup counter only counts registry Run keys.
- Resolution Manager: no DPI scaling; custom resolutions through the GPU driver are not implemented.
- No app icon (.ico), exe not code-signed (SmartScreen warning is expected).
- Separate admin helper for privileged operations does not exist yet (to design before steps 6-8).
- Workflow smoke test is `continue-on-error`; check whether it really passes and then make it blocking.

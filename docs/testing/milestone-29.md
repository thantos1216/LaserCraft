# Milestone 0.29 validation

## Evidence and diagnosis

Supplied logs under `F:/Movies2/Personal Built Applications/Testing_results/`:

- `LaserCraft-connection-20261111-184815.txt`, SHA256 `8DA15CD5728397D9725DA4ABEFAC8FD63511BE46DAC7BAEFE1119981531CE63E` (0.27).
- `LaserCraft-connection-20261111-185540.txt`, SHA256 `991908052C7E0C0B237946BD506638A88766B3507360E75E1EA8753D58A3F230` (0.28).

Both report COM5/115200 and fail during Windows serial open/configuration before any TX. No LaserCraft process was running when inspected. This does not establish a leaked process handle or identify the USB/driver fault. Source review found async-void Closed cleanup, which does not keep the UI lifetime alive until disposal finishes. Closing now cancels and awaits tracked startup, cancels response ownership, disconnects/disposes, then closes. Explicit-Abort reopen startup is tracked too. Serial disconnect allows one second before re-enabling reconnect.

Reference: [Microsoft SerialPort.Close documentation](https://learn.microsoft.com/en-us/dotnet/api/system.io.ports.serialport.close?view=netframework-4.8) advises allowing time between close and reopen; no exact delay or driver repair is guaranteed. REA is not required for this source-level lifecycle/UI issue. No LightBurn implementation or assets copied.

## Implemented behavior

Per-device movement distance/feed, pulse power/duration, position fields, placement mode/anchor and detected COM choice persist; theme, display units, last device and rectangle-before-Run preference persist globally. Legacy theme-only JSON migrates. Default values are clamped on load; machine references and firmware-write permission are not persisted. Profile bed follows selection/new/open/connect without scaling artwork. Simulator is hidden from production UI but remains in backend tests. Help is a top-menu window; G-code export is in File. Run has no review modal; preflight still applies. Circle frame is centered on output bounds with max-dimension diameter, not a circumscribed rectangle; translated frame bounds are checked separately.

Units convert the main object geometry, layer speed/interval, jog distance/feed, exact-position fields, bed status and rulers. Projects and G21 output remain millimetres. Display-to-base normalization uses nine mm decimals, finer than emitted motion precision, to avoid round-trip boundary drift. Advanced dialogs remain explicitly metric; snap remains 1 mm.

## Validation scope

Disconnected Debug UI: observed 0.29, compact accessible command icons, physical Attzr selection, automatic 430 mm bed and no lower Help/Apply bed controls. Switched to imperial and observed 16.929 in bed, inch rulers, 47.24409 in/min layer speed, 0.03937 in jog and 23.62205 in/min feed. Set 1.25 in jog, closed and verified no process remained; reopened and observed selected device, imperial mode and 1.25 in jog restored. Returned to metric (31.75 mm jog, 600 mm/min feed), restored original 1 mm jog and closed. No physical connection/motion/firing invoked.

New automated coverage includes per-device preference round-trip/theme migration, pending TCP query cancellation and observed endpoint EOF on disposal, inch-to-mm program equivalence, finite units, zero-power closed circular frames, and frame-specific travel rejection. Existing abort/reconnect, homing, missing acknowledgements, raster/vector and placement regressions remain.

Executed solution checks: 428 passing tests (407 unit, 21 integration), solution build with zero warnings/errors. Disconnected UI close/reopen verification is separate from the TCP disposal test and does not establish Windows COM-driver release. Legacy line-test UI was removed; backend line-control regression coverage remains. Connection diagnostics is available under Laser Tools, outside the compact main controls.

Hardware limitations: physical Windows COM handle release, USB device/driver recovery, real frame shape/placement and direct Run behavior need the user's next hardware test. No drivers, USB settings, firmware settings or external application state were modified.

Portable: `LaserCraft-0.29.0-win-x64-portable.zip`, 75,633,118 bytes; SHA256 `5B78CB8F2D781130A24EF94DD8CF77D01B90A42E85DC36C10CCA6C00B3049694`, clean source `81b9741fcfdc79508ad9378c027601cb94838d4e`. All 235 manifest entries verified by size/hash, plus bundled Help version/units/direct-run instructions, original icon, sample and self-contained runtime metadata. Copied to `F:/Movies2/Personal Built Applications/LaserCraft-0.29.0-win-x64-portable.zip` with matching hash. Final native UI test was the Debug build, not a physical connection from this ZIP.

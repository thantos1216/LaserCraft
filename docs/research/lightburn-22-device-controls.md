# LightBurn 2.2 device-control audit

Observed 2026-11-08 through the running Hyper-V Windows 11 console. Guest title: LightBurn Pro (Trial) 2.2.01, profile Attzr, untitled empty workspace, Disconnected. This is a bounded inventory of visible controls, not an exhaustive functional comparison. No settings were saved and no controller commands were invoked. Reference code/assets were not copied.

REA Docker provider inventory was refreshed through scripts/rea.ps1 (local output tmp/rea-providers-live-14.json). Native UI capture providers report macOS-only/unavailable on this host; Windows VM observations used computer use separately. Provider availability is not evidence of application behavior.

## Observed controls

| Surface | Visible options |
| --- | --- |
| Workspace | Cuts/Layers, Move, Console, Macros, Cameras/Overlays, Variable Text, Pages |
| Move | Get Position; X/Y/Z/U readouts; Move to Position X/Y + Go; Saved Positions + Manage; directional pad; Distance/Speed; Z Distance/Z Speed; Set Origin; Clear Origin; Set Finish Position |
| Laser | Pause/Stop/Start; two Frame modes; Save/Run G-code; Home; Go to Origin; Start From; nine-point Job Origin; selection origin; last position; cut-path optimization; Devices |
| Dimensions / Units | Working Size; CNC Machine; Machine Coordinates; laser-pointer offset X/Y; origin selector; Z enable/reverse/relative-only/optimize; design/control units and linkage |
| Basic Settings | Tab Pulse Width; job checklist; continuous frame; swap X/Y; project rotary settings; scanning-offset table and import/export; auto-home; fast whitespace scan; $J jogging; DTR; laser Fire button; framing power; bounds warning; G-code clustering; finish position; position queries; M8/M7 air output; S maximum; baud; transfer mode; buffer override |
| G-code | Start/end scripts; G0 overscan; emit S with every G1; fetch settings/check work offsets on connect |
| Additional Settings | Simulation speed/acceleration values, rapid speed, corner accuracy/minimum corner speed, cut/global speed scales; Read from Controller |
| Extensions | Permanent API access port and fixed-port option |
| Laser Tools menu | Print/Cut, cameras, rotary, focus/interval/material tests, center finder, taper warp, devices/device settings/machine settings |
| Edit menu | Close/join/optimize paths, delete duplicates, reverse direction, open/current-layer/contained/smaller/circle selection, image options |

Observed profile values included 430 × 430 mm, S maximum 1000, 115200 baud, M8 and buffered transfer. These are software profile values, not independently verified hardware facts; they were not adopted as a physical LaserCraft profile. Button presence does not establish command semantics. The directional pad's other icon actions were not exercised.

## Implementation consequences and remaining gaps

LaserCraft 0.14 adds M7/M8 selection to offline export and a simulator-only Move panel with X/Y distance/feed, power/duration, timed off and explicit Stop. Serial/TCP remain read-only. Always-live photograph trace preview is a requested LaserCraft behavior; this audit did not inspect the 2.2 trace dialog or establish identical tracing algorithms.

Live command scheduling, acknowledgement/buffer management, state/alarm handling, motion cancellation, validated origins/units, homing, physical pulse shutdown and job streaming remain required before first cuts. Z/U, saved positions, work/finish origins, framing execution, advanced G-code options and the other listed device features remain missing. Full parity and production readiness are not established.

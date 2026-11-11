# Milestone 0.27 validation

2026-11-11. Added Start From placement: Absolute Coordinates, Current Position, session-only User Origin; nine drawing anchors; shared frame/cut placement; exact laser-off bed positioning and frame-before-cut UI.

Executed: `dotnet build LaserCraft.slnx --no-restore --verbosity quiet` (zero warnings/errors) and `dotnet test LaserCraft.slnx --no-restore --verbosity quiet`: 413 passing tests (395 unit, 18 integration).

New tests cover positive/negative absolute placement, all 36 device-origin/anchor combinations, frame equivalence, saved-origin stability/missing-origin rejection, footprint overflow, exact coordinate translation, fresh capture, laser-off absolute jog commands, Home invalidation, no G10/G92, and public TCP fragmented replies in all three placement modes.

No USB machine was operated. Actual positioning accuracy, first document cut and frame-before-cut hardware behavior remain unverified. Prior user line success is separate evidence. Preview/export remain offline workflows. Powered jobs have no automatic finish move. Original implementation uses public behavior documentation; no proprietary source/assets copied.

Disconnected desktop UI check: launched the Debug 0.27 build, opened Start From and observed all three choices, selected Absolute Coordinates and verified Job Origin disabled, then opened Move and observed exact bed X/Y, origin capture and Read position controls disabled while disconnected. No Connect, motion or firing action was invoked.

Portable package: `artifacts/LaserCraft-0.27.0-win-x64-portable.zip`, 75,631,533 bytes; SHA256 `CB7BDB35AAE7725DC9581F99D55BA7B7E2514E771F7ADE7AD200881823AF6661`. Source commit `e4cc8be3d65f32af791c04ab83c0d481a3a68fb1`, clean build recorded. All 235 manifest entries verified for size/hash, plus bundled Help version, original icon, vector/image sample and self-contained runtime metadata. The shared F: destination was unavailable during the copy attempt; this package remains under the local artifacts folder.

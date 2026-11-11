# Milestone 0.30 validation

## User evidence

User reports successful cuts. Supplied `F:/Movies2/Personal Built Applications/Testing_results/LaserCraft-connection-20261111-212708.txt`, SHA256 `78830002C6A397F8BABD5FCA593F4276105B177CC61FC29EC96755E71A0EB3DD`, ends with M5/M9 acknowledgements and Idle at MPos 178,89.5. Prior code intentionally ended at the last output endpoint. The screenshot editor shows 60/80%/5 while the effective layer row is 1200/20; log moves use F1200/S200. Existing planner already loops through stored passes. This supports an unapplied-editor diagnosis rather than a planner silently dropping a stored five-pass setting.

## Changes

Layer fields now commit valid edits immediately with population/reentrancy guards, plus validation at Run/Preview/Frame/Save/export boundaries. Equal values do not add undo entries. Empty/invalid edits block output; no separate Apply button. Pass count is visible in the row. Per-layer WholeLayer (default) schedules A/B/A/B; PerObject schedules all passes on A then B. Fill batches remain combined for WholeLayer and independent for PerObject; compound contours within an object stay together. Raster decoding remains once per image even for repeated passes. Project/preset models store the optional order; older data defaults to WholeLayer.

Every successful connected output appends G53 G0 to the captured start X/Y after M5/M9, with S0. Initial head must be within the resolved travel. Completion requires all acknowledgements plus Idle/MPos within 0.01 mm of the start. Return rejection or mismatch is uncertain/failure, never success. Abort/error never initiates a return. Preview timing and offline export do not include a live captured return. Vector icons use explicit centering and 22 px geometry inside 40 × 36 controls.

## Verification

New tests cover five-pass vector visit order, fill/selection pass counts, raster decode-once repetition, legacy/project/preset persistence and invalid order rejection; positive/negative return commands and repeated wire paths; return rejection, wrong/missing final machine coordinates and unreturnable initial positions. Public TCP tests model head motion and run the same five-pass, two-object F60/S800 job twice under both scheduling modes, confirming identical commands and return to 101,101.

Disconnected native Debug UI: drew a 21 × 21 mm rectangle, changed Passes from 1 to 5 without Apply, observed row Pass=5 and Preview completed with 21 segments / 420 mm cut / 21.6 s ideal. This matches five 84 mm perimeters. Centered enlarged vector controls were visually inspected. Save-dialog automation could not address the owned filename field; no project save was claimed. The temporary drawing/pass changes were undone to the clean document and the test window closed. No hardware connection, motion or laser firing was invoked.

No LightBurn code/assets were used; this source-level settings/placement fix did not require binary REA. New physical repeat accuracy, pass count and heat behavior require the user's Attzr test. Controller MPos confirmation is not a measurement of mechanical accuracy; pass scheduling does not establish safe material settings or cooling.

Executed solution checks: 441 passing tests (418 unit, 23 integration), build with zero warnings/errors. UI verification was the disconnected Debug build; no physical operation was performed. Success and failure return tests are distinct from the user's previously successful 0.29 cut evidence.

Portable: `LaserCraft-0.30.0-win-x64-portable.zip`, 75,636,499 bytes, SHA256 `1CA43D6C4BBD681EA3978BAB7F8B46C06E50FDE1D267FE37D770E3C142AB950A`; clean source `30bb614fd75aeb15bbf15a8ab21117be2d0bbab4`. Verified all 235 manifest entries by size/hash, Help version/pass instructions, original application icon, sample and self-contained runtime metadata. Copied to `F:/Movies2/Personal Built Applications/LaserCraft-0.30.0-win-x64-portable.zip` with matching hash. Physical qualification of this package remains pending.

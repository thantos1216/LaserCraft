# Milestone 0.15 validation

2026-11-08 development session. User supplied Attzr 35 W, USB COM (variable), 430 × 430 mm; baud and $30 unknown. No physical endpoint was opened or probed.

- Release tests: 219 unit + 6 integration pass. Modal-parser/query and comparison coverage includes malformed/empty/duplicate/conflicting words, retained unknown words, missing/non-numeric settings, unequal S range, laser mode, program/reporting units, non-idle state, nonzero spindle, travel and unknown homing. Raw source values are retained.
- Query tests require one valid modal report plus terminal acknowledgement; errors/alarms fail. Cancellation stops the active modal query and queued status, without a queued write. A TCP loopback test fragments replies bytewise, interleaves push/status messages, and checks exact $I/$G/? bytes plus acknowledgement consumption before the next query. TCP monitoring does not expose simulator controls.
- Desktop simulator Connect -> Connection check displayed comparisons, endpoint, UTC interval and source reports. Simulator travel/bed were 430 mm from the existing local profile. Position/homing remained Unknown despite other matches. Closed report and disconnected.
- Devices -> Attzr draft displayed Serial, empty COM, 430 mm dimensions and explicit unverified starting-value explanation. Closed without saving. No saved physical profile was installed. COM enumeration exposed no detected port in this session; real USB selection/re-enumeration/hot unplug remain unverified.
- Profile defaults are not manufacturer claims. Collection is sequential, not atomic/live, and does not enable execution. Physical scheduling, execution acknowledgements, recovery, homing, pulse shutdown and first cuts remain incomplete.

Protocol reference: original implementation from [GRBL 1.1 Commands](https://github.com/gnea/grbl/wiki/Grbl-v1.1-Commands) and [Interface](https://github.com/gnea/grbl/wiki/Grbl-v1.1-Interface). Existing VM/REA observations informed control inventory; this protocol change did not require proprietary binary analysis or another reference-app capture.

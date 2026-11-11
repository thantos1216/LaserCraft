# Milestone 0.28 validation

## Supplied evidence

User log: `F:/Movies2/Personal Built Applications/Testing_results/LaserCraft-connection-20261111-151643.txt`, SHA256 `4414B40CE972B5F1BA4311E53193FD624302DCA57A8E6A3AE526C0BC3220BCE6`. The bounded journal dropped 44 older entries. It records successful Home/Idle, then an AbsoluteCoordinates document request. At 15:12:12 UTC the sender attempts `G53 G1 X202.314696 Y264.444298 F600 S800`; no terminal response follows. At 15:14:12 the read is cancelled and reset write fails with a USB-device IOException on COM5. This is an incomplete/uncertain program, not evidence of a final-Idle completion bug. The supplied screenshot identifies 0.24 and therefore is not assumed to represent the same binary as the later placement log.

## Changes and checks

Added a one-second cancellable post-connection startup pause, capability-checked automatic homing, Connect label and connected Home in both panels. Shared homing-setting interpretation owns automatic/manual checks. Abort formerly invalidated the session and disconnected it, leaving jogging unavailable until a manual reconnect. Explicit Abort now waits for job cleanup then reopens once without homing/resuming, preserving the journal. ALARM:3 at startup is retained while identity/ok are still required; no unlock is sent.

Job responses retain a single reader while requesting real-time status once a second. Ten-second silence or sustained Idle without acknowledgement fails with unknown completion and releases job ownership. Acknowledgements and final Idle remain required for success. Tests cover startup delay/cancellation/disabled homing, delayed acknowledgements with Run status, missing ack with silence and Idle, final Run-to-Idle release, abort cleanup/fresh connection jogging, and ALARM:3 identification followed by explicit Home. Existing alarm/error, pause/resume, cancellation, frame, placement and TCP coverage remain.

Reference: [upstream GRBL commands](https://github.com/gnea/grbl/wiki/Grbl-v1.1-Commands), read 2026-11-11. Reset during motion can invalidate position and enter Alarm; status reports are separate from ordinary command acknowledgements. No LightBurn binary analysis is needed to diagnose this source/transport boundary. No proprietary code or assets used.

No physical laser operation was performed. The USB fault's cause, cold-power-on timing sufficiency and physical Abort recovery remain unverified. Automated tests do not establish a repaired USB link or a completed physical cut.

Executed final checks: `dotnet test LaserCraft.slnx --no-restore --verbosity quiet`: 421 passing tests (402 unit, 19 integration). Unit suite takes about 25 seconds because silence/Idle-without-ack regressions exercise real ten-second deadlines. Public TCP recovery test cancels a pending reader, closes/reopens the endpoint, identifies through ALARM:3, then explicitly homes and jogs; no automatic Home/unlock/resume. Solution build passed with zero warnings/errors. Disconnected Debug UI check observed version 0.28, plain Connect, the startup explanation, and Home in both Laser and Move (disabled while disconnected). No physical connection action was invoked.

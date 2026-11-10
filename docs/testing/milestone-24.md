# Milestone 0.24 validation

2026-11-10. Reviewed physical one-line qualification.

- Release tests: 366 unit + 12 integration pass. Added X/negative-Y exact programs, supplied decimal $30/$31 parsing, missing/incompatible modal rejection, numeric budgets, fire/clearance permissions, non-Idle completion and post-write cancellation. Shared bounded batch ownership also retains existing pulse alarm/error/disposal/serialization regressions.
- Public TCP transport loopback checks exact identity/status/settings/modal/jog/pulse/cancel/off and four-line cut bytes with bytewise fragmented replies, followed by Idle. It is a scripted local peer, not the user's machine. Acknowledgement/Idle does not prove optical output or measured accuracy.
- Desktop development publish: 0.24 starts disconnected, Move physical permissions and Review line cut test button disabled. The physical review/run dialog was not exercised against hardware or a desktop fake controller. Its backend is covered by scripted/TCP tests; further desktop/hardware qualification remains necessary.
- No physical endpoint was opened, moved or fired. No firmware settings changed. No proprietary LightBurn code/assets were used; public GRBL protocol was sufficient, so REA extraction was unnecessary for this stage. See user observations for external query evidence and its limits.
- Line test is bounded one-pass motion, not full job streaming/pause/resume/emergency control. Off/mode restoration can fail on partial writes/controller faults. Defaults are not a material recipe. Package checks recorded below when completed.

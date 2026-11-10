# Milestone 0.19 validation

2026-11-09. Console input/Submit and GRBL setting changes.

- Release suite: 295 unit + 11 integration tests pass. New cases cover query classification/write opt-in, line/control/injection limits, exact frames, terminal/push preservation, serialization with status reads, no-write pre-cancellation, post-write cancellation/timeout blocking later writes, disconnect cancellation, reset/response limits, simulator ranges/readback/report units and profile separation.
- Real TCP loopback sends exact $I, $32=1, $$, G0 X1 and ? bytes against a scripted local peer. Replies are fragmented bytewise, with interleaved feedback/status and terminal acknowledgements/errors. This exercises TCP I/O and command ownership; it does not establish physical laser behavior.
- Desktop simulator: Enter submitted $$ and displayed settings/ok; Submit refused $32=0 in query mode. After enabling manual commands, Submit acknowledged $32=0; a later $$ reported $32=0. Up recalled the previous command. Disconnect reset write permission and disabled input/Submit. One mistyped concatenated simulator command returned error:20; no physical endpoint was involved.
- Full visible log is bounded to roughly 64k characters; history holds 100 entries and resets on new connection. Transport evidence remains separately exportable. The simulator's numeric settings are in-memory and deliberately do not emulate arbitrary motion G-code.
- Portable package verification pending final 0.19 build. No physical USB connection, persistent hardware setting change, move or firing was performed. Raw-console capability is implemented, but physical qualification, guided Move/fire, emergency controls and job streaming remain incomplete. Firmware-image flashing is outside this clarified request.

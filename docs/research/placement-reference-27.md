# Placement reference 0.27

Observed reference: user-supplied LightBurn screenshot exposes Absolute Coords, Current Position, User Origin and framing. Official legacy coordinate documentation, read 2026-11-11: https://docs.lightburnsoftware.com/legacy/CoordinatesOrigin . It describes drawing-to-bed absolute output, head-relative output with selectable anchor, and a programmable user origin. Separate configurable finish-position behavior is not implemented here.

Original implementation: an immutable placement request resolves one machine offset used by both frame and powered geometry. Saved origin is local to the connected monitor, cleared by Home/reconnect; no firmware offset changes. Absolute placement uses the signed machine-range minimum plus device-origin drawing mapping. Exact bed targets are distances along controller axes from that minimum.

GRBL command syntax checked against [upstream GRBL 1.1 Jogging](https://github.com/gnea/grbl/wiki/Grbl-v1.1-Jogging): optional G21/G90/G53 words select millimetres, absolute machine targets; M5/S0 is a separate ordinary command before the jog. Jog acknowledgement is not motion completion.

REA provider inventory from this phase is retained locally at tmp/rea-providers-27.json (ignored). Native UI observation reported unsupported_host on this Windows host; no fresh VM 2.2 runtime behavior was established. Existing read-only 2.2 control inventory supports control presence only. Public documentation is behavioral evidence, not proof of complete version parity or authority to mutate firmware.

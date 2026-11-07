# Milestone 0.3 verification

Build/test commands: `dotnet build LaserCraft.slnx` and `dotnet test --no-build`. Final result: zero build warnings/errors and 47 passing tests.

New tests cover layer reordering with object remapping/settings retention, job-order changes, vector/image separation, one image per image layer, reassignment to empty typed layers, deletion remapping, embedded source persistence, image mirroring, explicit unsupported raster-path rejection, air requests only on cut segments and outline hit testing around an image.

Device tests cover exact simulator identity/status commands, invalid/missing coordinates, malformed endpoints, unsupported adapters, profile persistence, TCP exchange with a loopback listener, cancellation of an outstanding read during disconnect and rejection/disposal of an unidentified peer. No hardware or external device endpoint was used.

Interactive checks: original PNG in samples/layers-and-image.lcraft renders with unchanged pixels and a layer-colored outline; simulated controller connects and reports identity/Idle/MPos; image layer moves below the vector layer without losing settings; New layer creates Layer 3; selecting the vector outline and Assign selected changes its layer and display color. Device profiles dialog exposes controller family, simulator/serial/TCP, serial port/baud, network endpoint, bed size, origin, power range and air/DTR/RTS configuration. Dialog was inspected without changing a physical endpoint or connecting to hardware.

PNG decoder verification is performed on original bytes before rendering. The domain validates metadata/limits; desktop decoder validation rejects incomplete/corrupt sources. Unit tests of metadata serialization are distinct from the interactive decoding check.

Known limits: raster toolpaths, full SVG, machine-coordinate mapping, G-code streaming and physical air operation remain unavailable. Serial transport is implemented but physically unverified. grblHAL/FluidNC compatibility remains experimental; other families are profile-only. Common Windows file-picker automation limitations from milestone 0.2 remain; startup project loading was used for the interactive fixture.

# LaserCraft project v1

Extension .lcraft; UTF-8 JSON serialized from DesignDocument. Version must equal 1. Name, Width/Height in millimetres, Shapes and Layers are required. ShapeKind is a numeric enum: Rectangle=0, Ellipse=1. Each shape stores Id, Kind, X, Y, Width, Height and zero-based Layer. Layer records store Name, Color (#RRGGBB), Speed (mm/min), Power (percent), and Output.

Input limits: 32 MB, 100000 shapes, 256 layers, 10000 mm dimensions and absolute coordinates, speed up to 100000 mm/min and power 0..100. These are parser/editor limits, not validated machine limits. Positions may be negative; off-bed objects remain exportable. Duplicate IDs and invalid references are rejected. Unknown JSON fields are ignored by the serializer; future Version values are rejected.

Undo history and selections are session-local. Raster assets, recovery, embedded previews and migration support are not present in v1. Save flushes a same-directory temporary file and atomically renames/replaces it. Disk errors are reported in the status bar.

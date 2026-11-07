# LaserCraft project v1

Extension .lcraft; UTF-8 JSON serialized from DesignDocument. Version must equal 1. Name, Width/Height in millimetres, Shapes and Layers are required. ShapeKind is a numeric enum: Rectangle=0, Ellipse=1. Each shape stores Id, Kind, X, Y, Width, Height and zero-based Layer. Layer records store Name, Color (#RRGGBB), Speed (mm/min), Power (percent), and Output.

Input limits: 32 MB, 100000 shapes, 256 layers, 10000 mm dimensions and absolute coordinates, speed up to 100000 mm/min and power 0..100. These are parser/editor limits, not validated machine limits. Positions may be negative; off-bed objects remain exportable. Duplicate IDs and invalid references are rejected. Unknown JSON fields are ignored by the serializer; future Version values are rejected.

Undo history and selections are session-local. Raster assets, recovery, embedded previews and migration support are not present in v1. Save flushes a same-directory temporary file and atomically renames/replaces it. Disk errors are reported in the status bar.

As of application 0.2, CutLayer has backward-compatible optional fields: Mode (Line=0, Fill=1; default Line), Passes (1..100; default 1), Visible (default true), AirAssist (default false), and Interval (0.01..100 mm; default 0.1). Old projects deserialize these defaults. New projects start with 30 layers; existing layer lists are retained. AirAssist and Power are stored requests only; the geometric simulator does not model physical laser or air output.

Application 0.3 adds LayerKind (Vector=0 default, Image=1) and CutMode.Image=2. Each image object uses ShapeKind.Image=2 with an embedded RasterAsset (Id, MimeType, DataBase64, PixelWidth, PixelHeight), optional FlipX/FlipY flags, and its own image-only layer. Existing vector documents remain readable. Layers array order is execution order; moves remap zero-based object layer references. Image mode requires Image kind and vice versa. One image per image layer is enforced. New projects begin with one layer.

Application 0.4 adds optional CutLayer.Raster settings: Mode (Threshold=0, FloydSteinberg=1, OrderedBayer=2, Grayscale=3), Brightness/Contrast (-100..100), Gamma (0.1..5), Invert, Threshold (0..255), Bidirectional and Overscan (0..100 mm). Missing settings use a default Floyd-Steinberg pipeline. Raw RasterAsset data is unchanged. The layer Interval controls sampling. Planning limits are runtime checks and do not prevent saving a dense design for later adjustment.

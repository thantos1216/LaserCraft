# Arrangement scope and acceptance

2026-11-09. Original implementation for the requested editing backlog, without copying reference code/assets. These definitions establish LaserCraft behavior; exact LightBurn dialog/ordering equivalence has not been established. Existing manual/VM inventory identifies arrangement functions as required. No binary analysis or controller work is necessary for these geometry operations.

| Family | Inputs and result | Constraints and acceptance |
| --- | --- | --- |
| Rectangular array | Valid document/selection, total rows/columns, nonnegative cell gaps -> copied document, selected IDs, pitch/count/bounds facts | Combined-selection cell; originals retained; row-major object copies; vector layer retention; independent image layers and source pixels; preflight format/resource expansion; atomic reject; history and real raster output |
| Distribution | At least three objects, horizontal/vertical centers or gaps -> new positions | Stable axis ordering, outer anchors fixed, orthogonal coordinate unchanged; nonnegative gaps; images/paths and dimensions/settings retained |
| Vector rotation | Vector selection and signed nonzero angle -> collective-center paths | Clockwise in screen coordinates; rectangle exact corners, ellipse 128 samples; existing path/mirror/hole topology, identity and cut length retained; reject raster selection/coordinate or vertex overflow |
| Path direction | Path selection -> reversed contours | Open endpoints swap; closed first point retained; same object/contour/layer order; double reversal restores geometry; native persistence and line output |

Domain-only logic, existing VectorPaths/native serialization/planning and Avalonia UI are the dependencies. Limits derive from existing project budgets. Summary calculation checks expansion before allocating repeated objects; rotation checks expanded primitive vertices. No runtime performance claim is made. Changes affect geometry and therefore offline cut coordinates/direction; they do not open a transport, grant execution authority or guarantee physical accuracy. Circular arrays, nesting, native curve handles, raster rotation, skew and advanced anchors remain separate work.

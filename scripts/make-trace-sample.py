"""Generate original deterministic tracing fixtures using only the Python standard library."""
import base64
import json
from pathlib import Path
import struct
import zlib

root = Path(__file__).resolve().parents[1]
width, height = 256, 192
pixels = bytearray([255] * (width * height))
for y in range(height):
    for x in range(width):
        ring = 20 <= x < 110 and 20 <= y < 110 and not (40 <= x < 90 and 40 <= y < 90)
        triangle = 150 <= x < 230 and 30 <= y < 100 and (x - 150) * 70 <= (y - 30) * 80
        circle = (x - 180) ** 2 + (y - 150) ** 2 <= 18 ** 2
        speckle = (x, y) in ((10, 170), (30, 150), (240, 170))
        if ring or triangle or circle or speckle:
            pixels[y * width + x] = 0

def chunk(kind, data):
    return struct.pack('>I', len(data)) + kind + data + struct.pack('>I', zlib.crc32(kind + data))

scanlines = b''.join(b'\0' + pixels[y * width:(y + 1) * width] for y in range(height))
png = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', width, height, 8, 0, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(scanlines, 9)) + chunk(b'IEND', b'')
(root / 'samples' / 'trace-fixture.png').write_bytes(png)
document = {
    'Version': 1, 'Name': 'Trace outlines demo', 'Width': 400, 'Height': 400,
    'Shapes': [{
        'Id': '7b961018-f141-4d01-a981-6ec94e0739de', 'Kind': 2, 'X': 100, 'Y': 120,
        'Width': 160, 'Height': 120, 'Layer': 1,
        'Image': {'Id': '59748aca-5eb6-4fae-a980-af194d57efbc', 'MimeType': 'image/png',
                  'DataBase64': base64.b64encode(png).decode('ascii'), 'PixelWidth': width, 'PixelHeight': height}
    }],
    'Layers': [
        {'Name': 'Drawing', 'Color': '#4169E1', 'Output': False},
        {'Name': 'Trace source', 'Color': '#D946EF', 'Mode': 2, 'Kind': 1, 'Interval': .625,
         'Raster': {'Mode': 0, 'Threshold': 128}}
    ]
}
(root / 'samples' / 'trace-outlines.lcraft').write_text(json.dumps(document, indent=2) + '\n', encoding='utf-8')
print('Generated original ring, triangle, circle and speckle tracing sample.')

"""Embed the credited, unmodified NASA photograph in a portable demo project."""
import base64
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
photo = (root / 'samples' / 'nasa-blue-marble.jpg').read_bytes()
assert hashlib.sha256(photo).hexdigest() == '13419b34c17722be52b4a71a7f0beb3ecc8673183c8a2cee8572d5cd9bbe3f93'
project = {
    'Version': 1, 'Name': 'Photographic tracing - NASA Blue Marble', 'Width': 400, 'Height': 400,
    'Shapes': [{
        'Id': 'd6999f48-d7d1-4e70-b43d-5c266dc57870', 'Kind': 2, 'X': 100, 'Y': 100,
        'Width': 200, 'Height': 200, 'Layer': 1,
        'Image': {'Id': '52320748-a2a6-4a3b-b755-5ccf2ef47758', 'MimeType': 'image/jpeg',
                  'DataBase64': base64.b64encode(photo).decode('ascii'), 'PixelWidth': 1024, 'PixelHeight': 1024}
    }],
    'Layers': [
        {'Name': 'Drawing', 'Color': '#4169E1', 'Output': False},
        {'Name': 'NASA photo - see PHOTO-CREDITS.md', 'Color': '#D946EF', 'Mode': 2, 'Kind': 1,
         'Interval': 200 / 512, 'Raster': {'Mode': 3, 'Threshold': 160}}
    ]
}
(root / 'samples' / 'photo-tracing.lcraft').write_text(json.dumps(project, indent=2) + '\n', encoding='utf-8')
print('Generated NASA photographic tracing demo; credits in samples/PHOTO-CREDITS.md.')

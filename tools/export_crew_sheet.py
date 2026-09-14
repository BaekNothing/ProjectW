"""Export the approved single 4x4 sheet without upscaling its cells (Pillow)."""
from pathlib import Path
import hashlib
import json
from PIL import Image, ImageOps

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'ArtSource/Portraits/Crew/magical-girl-sheet.png'
IDS = ['crew-han-tech', 'crew-yoon-analysis', 'crew-mi-management', 'crew-kang-adaptation']
sheet = Image.open(SOURCE).convert('RGB')
author = SOURCE.parent / 'Conditions'
runtime = ROOT / 'Assets/MilestonePrototype/RemoteAssets/Portraits/Crew'
author.mkdir(exist_ok=True)
(runtime / 'Conditions').mkdir(exist_ok=True)
records = []
for row in range(4):
    for col, identity in enumerate(IDS):
        box = (round(col * sheet.width / 4), round(row * sheet.height / 4),
               round((col + 1) * sheet.width / 4), round((row + 1) * sheet.height / 4))
        crop = sheet.crop(box)
        # Equal quarter boundaries; identical scale and margins, no per-character distortion.
        crop = ImageOps.contain(crop, (288, 288), Image.Resampling.LANCZOS)
        result = Image.new('RGB', (320, 320), 'white')
        result.paste(crop, ((320 - crop.width) // 2, 320 - crop.height))
        name = f'{identity}-condition-{row}.png'
        result.save(author / name)
        result.save(runtime / 'Conditions' / name)
        if row == 0:
            result.save(SOURCE.parent / f'{identity}.png')
            result.save(runtime / f'{identity}.png')
        records.append({'id': identity, 'condition': row, 'crop': box,
                        'output': name, 'size': [320, 320],
                        'sha256': hashlib.sha256((author / name).read_bytes()).hexdigest()})
(SOURCE.parent / 'sheet-export.json').write_text(json.dumps({
    'sheet': SOURCE.name, 'sheet_size': sheet.size,
    'sheet_sha256': hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
    'exports': records}, indent=2) + '\n', encoding='utf-8')
print(f'Exported {len(records)} condition portraits and four healthy fallbacks.')

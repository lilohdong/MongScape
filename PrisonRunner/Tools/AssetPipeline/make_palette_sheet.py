"""docs/images/style/palette.png: in-game colours (Unity _BaseColor, sRGB) grouped by use."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont
ROOT = Path(__file__).resolve().parents[2]
GROUPS = [
 ('Monkey', [('Fur', '#57250F'), ('Muzzle/palms', '#E6914A'), ('Prison orange', '#E83806'), ('Orange seam', '#871603'), ('Ivory', '#F5EBC7'), ('Eye white', '#FAF5DE'), ('Pupil', '#080502'), ('Mouth', '#1F0302'), ('Tongue', '#BA1F21')]),
 ('Police', [('Blue grey', '#5E7A9E'), ('Navy', '#0A142B'), ('Boots', '#040508'), ('Brass', '#F0940F'), ('Skin', '#E6914A')]),
 ('Mine', [('Stone 0', '#B8AD9C'), ('Stone 3', '#DACCB6'), ('Stone 5', '#F1E0C8'), ('Timber', '#C7B08F'), ('Cart timber', '#97693D'), ('Cart steel', '#65737C'), ('Ore', '#F9CA45'), ('Lantern', '#FF9C30'), ('Cavern', '#0E171F')]),
 ('Prison', [('Concrete', '#B8D1EB'), ('Forged iron', '#333D45'), ('Painted steel', '#8FA6AB'), ('Security red', '#CC160B'), ('Cold lamp', '#70B8F0'), ('Hazard yellow', '#FFC70F'), ('Charcoal', '#060709')]),
 ('Zoo', [('Grass', '#245226'), ('Foliage', '#2B6629'), ('Earth', '#33542B'), ('Log bark', '#80664A'), ('Caution', '#FFBD14'), ('Giraffe', '#E2B86F'), ('Elephant', '#A0A6B1'), ('Paving', '#FFFFFF'), ('Night stone', '#14334F')]),
 ('Art v2 (new)', [('Lion coat', '#C9822F'), ('Lion mane', '#7A3313'), ('Lion muzzle', '#F2D49E'), ('Lion nose', '#3B1A14'), ('Beam', '#FFE7A3'), ('Sign board', '#2A1D14'), ('Sign ink', '#FFD36B')]),
]
W, SW, PAD = 1500, 140, 20
font = None
for f in ('C:/Windows/Fonts/segoeui.ttf', 'C:/Windows/Fonts/arial.ttf'):
    try: font = ImageFont.truetype(f, 15); big = ImageFont.truetype(f, 20); break
    except OSError: pass
H = PAD + len(GROUPS) * (SW + 60)
im = Image.new('RGB', (W, H), (30, 36, 48)); d = ImageDraw.Draw(im)
y = PAD
for title, cols in GROUPS:
    d.text((PAD, y), title, fill=(235, 230, 220), font=big); y += 30
    for i, (name, hx) in enumerate(cols):
        x = PAD + i * (SW + 18)
        d.rounded_rectangle((x, y, x + SW, y + SW - 40), 10, fill=hx)
        d.text((x, y + SW - 36), name, fill=(220, 220, 220), font=font)
        d.text((x, y + SW - 18), hx, fill=(160, 170, 185), font=font)
    y += SW + 30
out = ROOT.parent / 'docs/images/style/palette.png'; im.save(out); print(out)

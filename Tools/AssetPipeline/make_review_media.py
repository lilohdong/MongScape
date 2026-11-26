"""Build contact strips and GIFs from frames rendered by mong_art_common.render_frames.

python make_review_media.py <review folder>   (system Python with Pillow)
- <prefix>_fNNN.png (key frames) -> <prefix>_strip.png
- _gif/<prefix>/*.png            -> <prefix>.gif, then the temporary frames are removed
"""
import re, shutil, sys
from collections import defaultdict
from pathlib import Path
from PIL import Image

def main(folder):
    folder = Path(folder)
    keys = defaultdict(list)
    for p in sorted(folder.glob('*_f[0-9][0-9][0-9].png')):
        keys[re.sub(r'_f\d{3}\.png$', '', p.name)].append(p)
    for prefix, frames in keys.items():
        ims = [Image.open(p).convert('RGB') for p in frames]
        strip = Image.new('RGB', (sum(i.width for i in ims), max(i.height for i in ims)), (38, 46, 61))
        x = 0
        for im in ims: strip.paste(im, (x, 0)); x += im.width
        strip.save(folder / f'{prefix}_strip.png')
        print('strip', prefix, len(ims))
    gif_root = folder / '_gif'
    if gif_root.exists():
        for d in sorted(gif_root.iterdir()):
            frames = sorted(d.glob('*.png'))
            if not frames: continue
            fps = float((d / 'fps.txt').read_text()) if (d / 'fps.txt').exists() else 15
            seq = [Image.open(p).convert('RGB').quantize(colors=200, method=Image.Quantize.MEDIANCUT) for p in frames]
            seq[0].save(folder / f'{d.name}.gif', save_all=True, append_images=seq[1:], duration=int(1000 / fps), loop=0, disposal=2)
            print('gif', d.name, len(seq))
        shutil.rmtree(gif_root)

def side_by_side(out, paths, labels=None, height=700, bg=(38, 46, 61)):
    ims = []
    for p in paths:
        im = Image.open(p).convert('RGB'); ims.append(im.resize((int(im.width * height / im.height), height)))
    sheet = Image.new('RGB', (sum(i.width for i in ims), height), bg)
    x = 0
    for im in ims: sheet.paste(im, (x, 0)); x += im.width
    sheet.save(out)

if __name__ == '__main__':
    for f in sys.argv[1:]: main(f)

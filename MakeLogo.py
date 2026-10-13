"""Export the selected DeskPet logo to Windows ICO and documentation previews."""
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parent
ASSETS = ROOT / 'assets'

if __name__ == '__main__':
    with Image.open(ASSETS / 'deskpet-logo.png') as original:
        logo = original.convert('RGBA')
    sizes = [16, 20, 24, 32, 40, 48, 64, 96, 128, 256]
    frames = [logo.resize((s, s), Image.Resampling.LANCZOS) for s in sizes]
    frames[-1].save(ASSETS / 'pet.ico', format='ICO', sizes=[(s, s) for s in sizes],
                    append_images=frames[:-1])
    logo.resize((256, 256), Image.Resampling.LANCZOS).save(ROOT / 'previews/logo.png')
    check = Image.new('RGBA', (640, 340), '#F1F5FA')
    check.alpha_composite(frames[-1], (40, 40))
    for size, x in [(16, 350), (20, 389), (24, 434), (32, 484), (48, 543)]:
        check.alpha_composite(frames[sizes.index(size)], (x, 145))
    (ROOT / 'work').mkdir(exist_ok=True)
    check.convert('RGB').save(ROOT / 'work/logo-size-check.png')
    print('Selected DeskPet logo: 10 Windows icon sizes and README preview exported')

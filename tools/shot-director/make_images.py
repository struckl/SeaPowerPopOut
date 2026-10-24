"""Workshop images from the raw grabs (grab.ps1): screenshots 1920 wide (or a portrait one), JPG q90, each under
1 MB, and the 1000 x 1000 preview with a title band. Dev only, never shipped.

    python tools/shot-director/make_images.py RAW_DIR

RAW_DIR holds main_before.png, main_after.png, main_hover.png (the game monitor, 3840 x 2160) and right_arranged.png
(the pop-out monitor without its taskbar). Writes workshop/screenshots/*.jpg and workshop/preview.jpg.
"""
import os
import sys

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OVERLAY = 46            # the Steam performance overlay across the top of the game monitor
BACKGROUND = (11, 13, 16)
FONTS = r'C:\Windows\Fonts'


def load(raw, name):
    return Image.open(os.path.join(raw, name)).convert('RGB')


def game(raw, name):
    im = load(raw, name)
    return im.crop((0, OVERLAY, im.width, im.height))


def save(im, name, quality=90):
    path = os.path.join(ROOT, 'workshop', name)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im.save(path, 'JPEG', quality=quality, optimize=True)
    print(f'{path}: {im.width}x{im.height}, {os.path.getsize(path) // 1024} KB')


def fit(im, width=None, height=None):
    if width:
        return im.resize((width, round(im.height * width / im.width)), Image.LANCZOS)
    return im.resize((round(im.width * height / im.height), height), Image.LANCZOS)


def screen(im, height, bezel=10):
    """A monitor: the picture at this height inside a dark bezel."""
    pic = fit(im, height=height)
    out = Image.new('RGB', (pic.width + 2 * bezel, pic.height + 2 * bezel), (30, 33, 38))
    out.paste(pic, (bezel, bezel))
    return out


def label(draw, xy, text, size=22):
    font = ImageFont.truetype(os.path.join(FONTS, 'segoeui.ttf'), size)
    draw.text(xy, text, font=font, fill=(200, 205, 212))


def two_monitors(main, right):
    """The map on the main monitor, the windows on the second one, side by side on a desk-dark background."""
    h = 1300
    a, b = screen(main, h), screen(right, round(h * 1.16))
    margin, gap, foot = 40, 50, 70
    width = margin + a.width + gap + b.width + margin
    height = margin + max(a.height, b.height) + foot
    out = Image.new('RGB', (width, height), BACKGROUND)
    top = margin + (b.height - a.height)
    out.paste(a, (margin, top))
    out.paste(b, (margin + a.width + gap, margin))
    draw = ImageDraw.Draw(out)
    base = margin + b.height + 16
    label(draw, (margin, base), 'Main screen: the map, free', 34)
    label(draw, (margin + a.width + gap, base), 'Second monitor: the windows', 34)
    return fit(out, width=1920)


def preview(main, right):
    """1000 x 1000, like the desk: the right part of the main screen with the free map, the second monitor with the
    windows beside it, a title band."""
    size = 1000
    out = Image.new('RGB', (size, size), BACKGROUND)
    # A faint light behind the screens.
    glow = Image.new('L', (size, size), 0)
    ImageDraw.Draw(glow).ellipse((-200, -300, size + 200, 760), fill=60)
    out.paste(Image.new('RGB', (size, size), (40, 52, 66)), (0, 0), glow.filter(ImageFilter.GaussianBlur(120)))
    bottom = 776
    second = screen(right.crop((0, 0, right.width, round(right.height * 0.76))), 660, bezel=9)
    main_screen = screen(main, 540, bezel=9)
    gap = 26
    mx = size - 34 - second.width - gap - main_screen.width
    for im, x in ((main_screen, mx), (second, size - 34 - second.width)):
        y = bottom - im.height
        shadow = Image.new('RGBA', (im.width + 80, im.height + 80), (0, 0, 0, 0))
        ImageDraw.Draw(shadow).rectangle((40, 40, 40 + im.width, 40 + im.height), fill=(0, 0, 0, 190))
        shadow = shadow.filter(ImageFilter.GaussianBlur(18))
        out.paste(shadow, (x - 40 + 8, y - 40 + 12), shadow)
        out.paste(im, (x, y))
    band = Image.new('RGBA', (size, 210), (14, 18, 24, 205))
    out.paste(band, (0, size - 210), band)
    draw = ImageDraw.Draw(out)
    draw.text((52, size - 196), 'Pop-Out Windows', font=ImageFont.truetype(os.path.join(FONTS, 'seguisb.ttf'), 92),
              fill=(245, 245, 245))
    draw.text((56, size - 76), "The game's windows on your second monitor",
              font=ImageFont.truetype(os.path.join(FONTS, 'segoeui.ttf'), 40), fill=(205, 210, 216))
    return out


def main():
    raw = sys.argv[1]
    before, after = game(raw, 'main_before.png'), game(raw, 'main_after.png')
    hover = load(raw, 'main_hover.png')
    right = load(raw, 'right_arranged.png')
    save(two_monitors(after, right), 'screenshots/01-two-monitors.jpg')
    save(fit(right, height=1600), 'screenshots/02-floating-windows.jpg')
    save(hover.crop((0, 60, 1500, 500)), 'screenshots/03-pop-out-button.jpg')
    save(fit(before, width=1920), 'screenshots/04-before.jpg')
    save(fit(after, width=1920), 'screenshots/05-after.jpg')
    save(preview(after, right), 'preview.jpg')


if __name__ == '__main__':
    main()

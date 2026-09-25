"""Draw the 13 brand-free placeholder product illustrations (docs/PLAN.md §3).

Uniform by construction: every product is built from the shared shapes below, on one
400x400 transparent canvas, in one height band (bottom y=360, tallest item 320 px), with
one outline weight, one corner radius and light from the top-left. Labels are generic
German words in Space Grotesk (the site's own OFL font), never brand names or lettering.

Run from anywhere:  python frontend/scripts/product-art/make_products.py [--sheet]
"""

from __future__ import annotations

import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFont

HERE = Path(__file__).resolve().parent
FRONTEND = HERE.parent.parent
OUT = FRONTEND / "public" / "products"
FONT = FRONTEND / "public" / "fonts" / "space-grotesk-latin-wght.woff2"

SIZE = 400          # output canvas
SS = 4              # supersampling factor for smooth edges
BOTTOM = 360        # every product stands on this line
LINE = 7            # one outline weight (px at 400)
RADIUS = 16         # one corner radius (px at 400)
OUTLINE = (241, 238, 255, 255)   # light contour: reads on the dark --card background
INK = (15, 15, 35, 255)          # label text on light surfaces (--background)
PAPER = (241, 238, 255, 255)     # label text on dark surfaces


def s(v: float) -> int:
    return round(v * SS)


def mix(c: tuple, other: tuple, t: float) -> tuple:
    return tuple(round(a + (b - a) * t) for a, b in zip(c[:3], other[:3])) + (255,)


def lighter(c: tuple, t: float = 0.35) -> tuple:
    return mix(c, (255, 255, 255), t)


def darker(c: tuple, t: float = 0.3) -> tuple:
    return mix(c, (0, 0, 0), t)


class Art:
    def __init__(self) -> None:
        self.img = Image.new("RGBA", (s(SIZE), s(SIZE)), (0, 0, 0, 0))
        self.draw = ImageDraw.Draw(self.img)

    # -- primitives: fill + the one outline weight, then top-left light --------------------
    def _mask(self, painter) -> Image.Image:
        m = Image.new("L", self.img.size, 0)
        painter(ImageDraw.Draw(m))
        return m

    def _light(self, mask: Image.Image, box: tuple, color: tuple) -> None:
        """Top-left light: a highlight band on the left, a shade band on the right."""
        x0, y0, x1, y1 = box
        w = x1 - x0
        for band, tone in (
            ((x0 + w * 0.12, y0, x0 + w * 0.26, y1), lighter(color, 0.38)),
            ((x1 - w * 0.2, y0, x1, y1), darker(color, 0.22)),
        ):
            bm = self._mask(lambda d: d.rectangle([s(v) for v in band], fill=255))
            clip = ImageChops.multiply(bm, mask)
            layer = Image.new("RGBA", self.img.size, tone)
            self.img.paste(layer, (0, 0), clip)

    def rrect(self, box: tuple, color: tuple, radius: float = RADIUS, light: bool = True) -> None:
        sb = [s(v) for v in box]
        self.draw.rounded_rectangle(sb, s(radius), fill=color)
        if light:
            inset = [s(box[0] + LINE / 2), s(box[1] + LINE / 2), s(box[2] - LINE / 2), s(box[3] - LINE / 2)]
            self._light(self._mask(lambda d: d.rounded_rectangle(inset, s(max(radius - LINE / 2, 0)), fill=255)), box, color)
        self.draw.rounded_rectangle(sb, s(radius), outline=OUTLINE, width=s(LINE))

    def poly(self, pts: list, color: tuple, light: bool = True) -> None:
        sp = [(s(x), s(y)) for x, y in pts]
        self.draw.polygon(sp, fill=color)
        if light:
            xs, ys = [p[0] for p in pts], [p[1] for p in pts]
            self._light(self._mask(lambda d: d.polygon(sp, fill=255)), (min(xs), min(ys), max(xs), max(ys)), color)
        self.draw.line(sp + [sp[0]], fill=OUTLINE, width=s(LINE), joint="curve")
        for x, y in sp:  # round the corners of the contour
            r = s(LINE / 2)
            self.draw.ellipse([x - r, y - r, x + r, y + r], fill=OUTLINE)

    def ellipse(self, box: tuple, color: tuple, outline: bool = True) -> None:
        self.draw.ellipse([s(v) for v in box], fill=color, outline=OUTLINE if outline else None, width=s(LINE) if outline else 0)

    def line(self, pts: list, color: tuple = OUTLINE, width: float = LINE) -> None:
        self.draw.line([(s(x), s(y)) for x, y in pts], fill=color, width=s(width), joint="curve")

    def text(self, center: tuple, label: str, size: float, color: tuple, weight: int = 700, angle: float = 0, max_w: float | None = None) -> None:
        def load(sz: float) -> ImageFont.FreeTypeFont:
            f = ImageFont.truetype(str(FONT), s(sz))
            f.set_variation_by_axes([weight])
            return f

        font = load(size)
        while max_w and font.getlength(label) > s(max_w) and size > 8:  # shrink to fit its panel
            size -= 1
            font = load(size)
        if angle:
            tmp = Image.new("RGBA", self.img.size, (0, 0, 0, 0))
            ImageDraw.Draw(tmp).text((s(center[0]), s(center[1])), label, font=font, fill=color, anchor="mm")
            tmp = tmp.rotate(angle, center=(s(center[0]), s(center[1])), resample=Image.BICUBIC)
            self.img.alpha_composite(tmp)
        else:
            self.draw.text((s(center[0]), s(center[1])), label, font=font, fill=color, anchor="mm")

    def save(self, name: str) -> Image.Image:
        out = self.img.resize((SIZE, SIZE), Image.LANCZOS)
        out.save(OUT / name, "WEBP", lossless=False, quality=90, alpha_quality=100, method=6)
        return out


# -- shared shapes ---------------------------------------------------------------------------
def vape(a: Art, body: tuple, accent: tuple, flavour: str) -> None:
    w, h, cx = 112, 292, 200
    x0, x1, top = cx - w / 2, cx + w / 2, BOTTOM - h
    a.rrect((cx - 26, top, cx + 26, top + 56), darker(accent, 0.1), radius=RADIUS)   # mouthpiece
    a.rrect((x0, top + 40, x1, BOTTOM), body, radius=RADIUS * 2)                     # body
    a.rrect((x0 + 18, top + 110, x1 - 18, top + 210), accent, radius=RADIUS / 2, light=False)
    a.text((cx, top + 145), flavour, 20, INK, max_w=w - 44)
    a.text((cx, top + 178), "600 ZÜGE", 15, INK, 600, max_w=w - 44)
    a.ellipse((cx - 9, BOTTOM - 40, cx + 9, BOTTOM - 22), accent, outline=False)     # LED


def can(a: Art, w: float, h: float, body: tuple, band: tuple, label: str, sub: str, band_ink: tuple) -> None:
    cx = 200
    x0, x1, top = cx - w / 2, cx + w / 2, BOTTOM - h
    a.rrect((x0 + 10, top, x1 - 10, top + 26), (196, 200, 214, 255), radius=RADIUS / 2)     # lid rim
    a.rrect((x0, top + 16, x1, BOTTOM), body, radius=RADIUS)
    a.rrect((x0 + LINE / 2, top + h * 0.38, x1 - LINE / 2, top + h * 0.66), band, radius=2, light=False)
    a.text((cx, top + h * 0.52), label, 30, band_ink, max_w=w - 22)
    a.text((cx, BOTTOM - 30), sub, 16, PAPER, 600)


def bottle(a: Art, body: tuple, label_bg: tuple, label: str, sub: str) -> None:
    cx, top = 200, BOTTOM - 320
    a.rrect((cx - 24, top, cx + 24, top + 34), (70, 140, 230, 255), radius=RADIUS / 2)      # cap
    a.poly([(cx - 20, top + 30), (cx + 20, top + 30), (cx + 62, top + 110), (cx - 62, top + 110)], body)  # shoulder
    a.rrect((cx - 64, top + 100, cx + 64, BOTTOM), body, radius=RADIUS * 1.5)
    a.rrect((cx - 64 + LINE / 2, top + 170, cx + 64 - LINE / 2, top + 250), label_bg, radius=2, light=False)
    a.text((cx, top + 200), label, 26, INK, max_w=112)
    a.text((cx, top + 232), sub, 15, INK, 600, max_w=112)


def cigarette_pack(a: Art, band: tuple, label: str) -> None:
    w, h, cx = 190, 262, 200
    x0, x1, top = cx - w / 2, cx + w / 2, BOTTOM - h
    for i, x in enumerate((cx - 50, cx - 16, cx + 18)):                              # filters sticking out
        a.rrect((x, top - 4 + i * 6, x + 30, top + 60), (224, 160, 90, 255), radius=6, light=False)
    a.rrect((x0, top + 30, x1, BOTTOM), (236, 234, 244, 255), radius=RADIUS)
    a.rrect((x0 + LINE / 2, top + 34, x1 - LINE / 2, top + 100), band, radius=2, light=False)
    a.line([(x0, top + 100), (x1, top + 100)])                                        # flip-top lid edge
    a.text((cx, top + 67), label, 28, PAPER)
    a.text((cx, top + 170), "20 STÜCK", 20, INK)
    a.text((cx, top + 205), "ZIGARETTEN", 16, INK, 500)


def tobacco_pouch(a: Art, body: tuple, label: str) -> None:
    cx, top = 200, BOTTOM - 220
    a.poly([(cx - 130, top + 30), (cx + 130, top + 30), (cx + 122, BOTTOM), (cx - 122, BOTTOM)], body)
    a.poly([(cx - 130, top + 30), (cx + 130, top + 30), (cx + 100, top - 6), (cx - 100, top - 6)], darker(body, 0.15), light=False)  # fold flap
    a.rrect((cx - 88, top + 80, cx + 88, top + 160), (236, 214, 170, 255), radius=RADIUS / 2, light=False)
    a.text((cx, top + 108), label, 30, INK, max_w=160)
    a.text((cx, top + 140), "30 g FEINSCHNITT", 15, INK, 600, max_w=160)


def papers(a: Art, body: tuple) -> None:
    w, h, cx = 250, 160, 200
    x0, x1, top = cx - w / 2, cx + w / 2, BOTTOM - h
    a.rrect((x0 + 30, top - 40, x1 - 30, top + 20), (250, 248, 240, 255), radius=4, light=False)   # paper leaf
    a.rrect((x0, top, x1, BOTTOM), body, radius=RADIUS)
    a.line([(x0 + 14, top + 24), (x1 - 14, top + 24)], lighter(body, 0.45), 4)
    a.text((cx, top + 78), "PAPERS", 38, PAPER)
    a.text((cx, top + 118), "SLIM", 18, lighter(body, 0.6), 600)


def lighter_shape(a: Art, body: tuple) -> None:
    w, h, cx = 104, 250, 200
    x0, x1, top = cx - w / 2, cx + w / 2, BOTTOM - h
    a.poly([(cx - 10, top + 30), (cx - 2, top + 4), (cx + 10, top + 30)], (255, 190, 80, 255), light=False)  # flame
    a.rrect((x0 + 8, top + 34, x1 - 8, top + 84), (196, 200, 214, 255), radius=RADIUS / 2)                  # metal hood
    a.ellipse((x1 - 30, top + 42, x1 - 6, top + 66), (150, 154, 170, 255))                                    # flint wheel
    a.rrect((x0, top + 76, x1, BOTTOM), body, radius=RADIUS * 1.5)
    a.text((cx, top + 170), "FEUER", 22, PAPER, angle=90)


def bar(a: Art, body: tuple, label: str, sub: str) -> None:
    """A flat bar would be ~100 px tall and vanish at 56 px, so it is drawn tilted by TILT°,
    centred so its lowest corner sits on BOTTOM."""
    TILT, w, h, cx, cy = 20, 330, 120, 200, 247
    b = Art()
    x0, x1, top, bottom = cx - w / 2, cx + w / 2, cy - h / 2, cy + h / 2
    for ex in (x0, x1 - 26):                                                           # crimped ends
        b.rrect((ex, top - 6, ex + 26, bottom + 6), darker(body, 0.12), radius=4, light=False)
        for k in range(1, 6):
            b.line([(ex + 4, top - 6 + k * 22), (ex + 22, top - 6 + k * 22)], lighter(body, 0.4), 3)
    b.rrect((x0 + 20, top, x1 - 20, bottom), body, radius=RADIUS / 2)
    b.text((cx, cy - 12), label, 34, PAPER)
    b.text((cx, cy + 26), sub, 16, (255, 214, 140, 255), 600, max_w=w - 80)
    a.img.alpha_composite(b.img.rotate(TILT, center=(s(cx), s(cy)), resample=Image.BICUBIC))


def gummy_bag(a: Art, body: tuple, label: str) -> None:
    w, h, cx = 236, 290, 200
    x0, x1, top = cx - w / 2, cx + w / 2, BOTTOM - h
    teeth = [(x0 + i * w / 12, top + (0 if i % 2 == 0 else 12)) for i in range(13)]  # crimped top seal
    a.poly(teeth + [(x1, top + 40), (x1 - 6, BOTTOM), (x0 + 6, BOTTOM), (x0, top + 40)], body)
    a.line([(x0 + 4, top + 40), (x1 - 4, top + 40)], lighter(body, 0.5), 4)
    a.ellipse((cx - 70, top + 120, cx + 70, top + 230), (255, 250, 235, 255))           # window
    for (dx, dy, col) in ((-34, 150, (240, 70, 80, 255)), (14, 142, (80, 200, 110, 255)), (-8, 188, (255, 150, 40, 255)), (36, 186, (180, 90, 230, 255))):
        a.ellipse((cx + dx - 16, top + dy - 16, cx + dx + 16, top + dy + 16), col, outline=False)   # round gummies (no bear shapes)
    a.text((cx, top + 82), label, 26, INK, max_w=w - 30)
    a.text((cx, BOTTOM - 30), "100 g", 16, INK, 600)


def chip_tube(a: Art, body: tuple, label: str) -> None:
    w, h, cx = 132, 312, 200
    x0, x1, top = cx - w / 2, cx + w / 2, BOTTOM - h
    a.rrect((x0 - 4, top, x1 + 4, top + 30), (196, 200, 214, 255), radius=RADIUS / 2)   # lid
    a.rrect((x0, top + 22, x1, BOTTOM), body, radius=RADIUS)
    a.ellipse((cx - 44, top + 110, cx + 44, top + 170), (250, 206, 110, 255))            # chip
    a.text((cx, top + 70), "CHIPS", 28, PAPER)
    a.text((cx, top + 215), label, 22, PAPER)
    a.text((cx, BOTTOM - 30), "40 g", 16, PAPER, 600)


# -- the 13 products, file names exactly as docs/PLAN.md §3 ----------------------------------
PRODUCTS = [
    ("elfbar-600-blueberry-ice.webp", lambda a: vape(a, (70, 90, 220, 255), (170, 225, 255, 255), "BLAUBEERE")),
    ("elfbar-600-watermelon.webp", lambda a: vape(a, (235, 70, 110, 255), (170, 240, 150, 255), "MELONE")),
    ("elfbar-600-cola.webp", lambda a: vape(a, (120, 55, 40, 255), (250, 200, 150, 255), "COLA")),
    ("red-bull-250.webp", lambda a: can(a, 124, 272, (60, 110, 200, 255), (210, 220, 236, 255), "ENERGY", "250 ml", INK)),
    ("coca-cola-zero-330.webp", lambda a: can(a, 156, 248, (40, 38, 48, 255), (220, 40, 55, 255), "COLA ZERO", "330 ml", PAPER)),
    ("vio-still-500.webp", lambda a: bottle(a, (150, 205, 245, 255), (236, 246, 255, 255), "WASSER", "still · 500 ml")),
    ("snickers.webp", lambda a: bar(a, (120, 70, 40, 255), "SCHOKO", "ERDNUSS · KARAMELL")),
    ("haribo-goldbaeren-100.webp", lambda a: gummy_bag(a, (250, 200, 50, 255), "FRUCHTGUMMI")),
    ("pringles-paprika-40.webp", lambda a: chip_tube(a, (220, 80, 40, 255), "PAPRIKA")),
    ("marlboro-red-20.webp", lambda a: cigarette_pack(a, (200, 40, 50, 255), "ROT")),
    ("pueblo-classic-30.webp", lambda a: tobacco_pouch(a, (70, 120, 80, 255), "TABAK")),
    ("ocb-slim-premium.webp", lambda a: papers(a, (50, 50, 64, 255))),
    ("clipper-feuerzeug.webp", lambda a: lighter_shape(a, (124, 58, 237, 255))),
]


def contact_sheet(images: list[Image.Image], path: Path) -> None:
    """All 13 on the site's --card colour, at 400 px and at the panel's 56 px."""
    card = (30, 28, 53, 255)
    cols, big, small = 13, 200, 56
    sheet = Image.new("RGBA", (cols * big + 40, big + small + 80), card)
    for i, im in enumerate(images):
        sheet.alpha_composite(im.resize((big, big), Image.LANCZOS), (20 + i * big, 20))
        tile = Image.new("RGBA", (small, small), (39, 39, 59, 255))           # --muted box, as in the panel
        tile.alpha_composite(im.resize((small - 8, small - 8), Image.LANCZOS), (4, 4))
        sheet.alpha_composite(tile, (20 + i * big + (big - small) // 2, big + 40))
    sheet.convert("RGB").save(path)


def main() -> None:
    OUT.mkdir(parents=True, exist_ok=True)
    images = []
    for name, paint in PRODUCTS:
        art = Art()
        paint(art)
        images.append(art.save(name))
        print("wrote", OUT / name)
    if "--sheet" in sys.argv:
        idx = sys.argv.index("--sheet")
        target = Path(sys.argv[idx + 1]) if len(sys.argv) > idx + 1 else HERE / "contact-sheet.png"
        contact_sheet(images, target)
        print("wrote", target)


if __name__ == "__main__":
    main()

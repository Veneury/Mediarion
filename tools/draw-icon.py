"""Draws icon.png from the same geometry as icon.svg.

Not an SVG renderer: the shapes are hand-drawn with the same numbers, at eight
times the size, then reduced. That is what gives the clean edges, and it is why
the two files have to be changed together.
"""
from PIL import Image, ImageDraw

S = 8
N = 128
NAVY = (14, 43, 69, 255)
MINT = (94, 234, 212, 255)
AMBER = (251, 191, 36, 255)

img = Image.new("RGBA", (N * S, N * S), (0, 0, 0, 0))
d = ImageDraw.Draw(img)


def px(v):
    return v * S


d.rounded_rectangle([0, 0, px(N) - 1, px(N) - 1], radius=px(28), fill=NAVY)

for end in ((64, 26), (30, 84), (98, 84)):
    d.line([px(64), px(64), px(end[0]), px(end[1])], fill=MINT, width=px(6))
    d.ellipse([px(end[0]) - px(3), px(end[1]) - px(3),
               px(end[0]) + px(3), px(end[1]) + px(3)], fill=MINT)

d.ellipse([px(64) - px(3), px(64) - px(3), px(64) + px(3), px(64) + px(3)], fill=MINT)
d.ellipse([px(64 - 17), px(64 - 17), px(64 + 17), px(64 + 17)], fill=MINT)

for cx, cy in ((64, 26), (30, 84), (98, 84)):
    d.ellipse([px(cx - 10), px(cy - 10), px(cx + 10), px(cy + 10)],
              fill=None, outline=AMBER, width=px(6))

img = img.resize((N, N), Image.LANCZOS)
img.save("icon.png", optimize=True)
print("icon.png escrito:", img.size, img.mode)

"""Generate AutoCommand app-icon candidates (gradient tile + command glyph).

Usage:  <python with Pillow+numpy> Assets/generate_icon.py
Outputs (Assets/icons/): candidate_{a,b,c}_256.png, candidate_{a,b,c}.ico,
and icon_sheet.png (all candidates at 256/48/32/16 on dark + light rows).
"""
import os
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "icons")
os.makedirs(OUT, exist_ok=True)

S = 1024  # master canvas
NAVY = (22, 41, 76)
BLUE = (37, 99, 235)
LIGHT = (244, 247, 251)
WHITE = (255, 255, 255)
INK = (22, 41, 76)


def gradient_tile(top=NAVY, bottom=BLUE, inset=56, radius=210):
    """Full-canvas image: rounded tile filled with a diagonal gradient."""
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    yy, xx = np.mgrid[0:S, 0:S]
    t = ((xx + yy) / (2 * S))[..., None]
    top_a, bot_a = np.array(top, float), np.array(bottom, float)
    grad = (top_a * (1 - t) + bot_a * t).astype(np.uint8)
    grad_img = Image.fromarray(grad, "RGB").convert("RGBA")

    mask = Image.new("L", (S, S), 0)
    ImageDraw.Draw(mask).rounded_rectangle([inset, inset, S - inset, S - inset],
                                           radius=radius, fill=255)
    img.paste(grad_img, (0, 0), mask)
    return img


def glyph_command(img, color=WHITE, stroke=88):
    d = ImageDraw.Draw(img)
    # chevron '>' then cursor '_', nudged right for optical centering
    d.line([(395, 385), (585, 565), (395, 745)], fill=color, width=stroke, joint="curve")
    d.rounded_rectangle([665, 681, 785, 681 + stroke], radius=stroke // 2, fill=color)
    return img


def shield_outline(img, color=WHITE, stroke=64):
    """Rounded-top shield with a pointed bottom, drawn as one thick outline."""
    d = ImageDraw.Draw(img)
    top, cx, w, bottom = 300, S // 2, 300, 810
    pts = []
    # left edge down, pointed bottom, right edge up, rounded top via arc
    pts += [(cx - w, top + 140)]
    for t in np.linspace(0, 1, 40):  # left side down to the point
        x = cx - w + (w * 0.28) * (t ** 2.2)
        pts.append((x, top + 140 + (bottom - top - 140) * t))
    for t in np.linspace(0, 1, 40):  # point back up the right side
        x = cx + w - (w * 0.28) * ((1 - t) ** 2.2)
        pts.append((x, top + 140 + (bottom - top - 140) * t))
    pts += [(cx + w, top + 140)]
    d.line(pts, fill=color, width=stroke, joint="curve")
    d.arc([cx - w, top - 120, cx + w, top + 160], 180, 360, fill=color, width=stroke)
    return img


def glyph_command_navy(img):
    return glyph_command(img, color=INK)


def candidate_a():
    """Recommendation: brand-gradient tile, white command glyph."""
    img = gradient_tile()
    return glyph_command(img)


def candidate_b():
    """Corporate light tile, navy glyph — matches the infographic theme."""
    img = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.rounded_rectangle([56, 56, S - 56, S - 56], radius=210,
                        fill=LIGHT, outline=(215, 223, 233), width=10)
    glyph_command(img, color=INK)
    # brand tick: blue cursor already navy; recolor cursor bar to brand blue
    d.rounded_rectangle([640, 676, 770, 676 + 88], radius=44, fill=BLUE)
    return img


def candidate_c():
    """Shield test: brand-gradient tile, white shield + navy command glyph."""
    img = gradient_tile()
    shield_outline(img)
    d = ImageDraw.Draw(img)
    d.line([(430, 440), (560, 570), (430, 700)], fill=NAVY, width=72, joint="curve")
    d.rounded_rectangle([610, 646, 700, 646 + 64], radius=32, fill=NAVY)
    return img


def downscales(img):
    return {size: img.resize((size, size), Image.LANCZOS) for size in (256, 48, 32, 16)}


def save_all(name, img):
    sizes = downscales(img)
    sizes[256].save(os.path.join(OUT, f"candidate_{name}_256.png"))
    master = sizes[256].copy()
    master.save(os.path.join(OUT, f"candidate_{name}.ico"),
                sizes=[(256, 256), (48, 48), (32, 32), (24, 24), (16, 16)])
    return sizes


def sheet(cands):
    rows, cols = 2, 3  # dark row, light row
    cell_w, cell_h = 256 + 130, 256 + 40
    W, H = cols * cell_w + 80, rows * (cell_h + 70) + 60
    board = Image.new("RGB", (W, H), (250, 251, 253))
    d = ImageDraw.Draw(board)
    for ci, (name, sizes) in enumerate(cands.items()):
        x0 = 40 + ci * cell_w
        d.text((x0, 20), f"candidate {name}", fill=(60, 60, 70))
        # dark row
        d.rounded_rectangle([x0 - 10, 50, x0 + 256 + 10, 50 + 256 + 20], 12, fill=(24, 28, 36))
        board.paste(sizes[256], (x0, 60), sizes[256])
        for i, size in enumerate((48, 32, 16)):
            board.paste(sizes[size], (x0 + 270 + i * (size + 14), 60 + 256 - size), sizes[size])
        # light row
        oy = 50 + 256 + 70
        d.rounded_rectangle([x0 - 10, oy, x0 + 256 + 10, oy + 256 + 20], 12, fill=(255, 255, 255),
                            outline=(220, 224, 230))
        board.paste(sizes[256], (x0, oy + 10), sizes[256])
        for i, size in enumerate((48, 32, 16)):
            board.paste(sizes[size], (x0 + 270 + i * (size + 14), oy + 10 + 256 - size), sizes[size])
    board.save(os.path.join(OUT, "icon_sheet.png"))


cands = {"a": candidate_a(), "b": candidate_b(), "c": candidate_c()}
all_sizes = {name: save_all(name, img) for name, img in cands.items()}
sheet(all_sizes)
print("written to", OUT)

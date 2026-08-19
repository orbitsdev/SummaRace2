"""
Rebuild the three legacy Ms. Lumi images from the pose set in Resources/UI/Lumi/.

WHY THIS EXISTS. Three screens do not read the pose pool: the race briefing
(EndlessRaceDirector Resources.Load "UI/mslumi_wave"), and Arrange + Summary (scene-wired
Art/UI/teacher_temp.png). Each has layout maths tuned to its current file's exact geometry, so
regenerating the file is safer than rewriting the layout.

THE RULE THAT MATTERS. The half-bodies are fitted to the ORIGINAL file's alpha envelope, not to a
matching head size. Head-matching was tried first and put her silhouette at 49% of the canvas
against the 44% the briefing assumes -- on screen that overlapped the START button. The envelope
is what the tuned anchors actually depend on.

Run from the repo root:  python missingassets/derive_lumi.py
Nothing is written unless every step succeeds. The .png.meta files are never touched.
"""
from PIL import Image, ImageDraw
import numpy as np, os, sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
POSES = os.path.join(ROOT, 'Assets/_Game/Resources/UI/Lumi')
WAVE  = os.path.join(ROOT, 'Assets/_Game/Resources/UI/mslumi_wave.png')
CHEER = os.path.join(ROOT, 'Assets/_Game/Resources/UI/mslumi_cheer.png')
BADGE = os.path.join(ROOT, 'Assets/_Game/Art/UI/teacher_temp.png')

# Source poses. Change these to restyle without touching any maths.
SRC_WAVE, SRC_CHEER, SRC_BADGE = 'idle_wave.png', 'cheer_hooray.png', 'idle_smile.png'

CW, CH = 512, 256      # half-body canvas: 2:1 like the originals, sized so no upscaling is needed
BS, SS = 512, 4        # badge canvas, and supersampling for a clean circle edge
GREEN = (77, 191, 102, 255)   # SwbstPalette.Wanted (0.30, 0.75, 0.40)
RING, PAD = 13, 6
HEAD_W_FRAC, HEAD_CY = 0.46, 0.38   # badge framing, chosen against the file it replaces

# Alpha envelopes of the ORIGINAL 2048x1024 files, measured once before they were replaced.
# mslumi_wave  bbox (507,48)-(1405,1023);  mslumi_cheer bbox (508,48)-(1593,1023).
ENV_WAVE  = (507 / 2048, 1406 / 2048, 48 / 1024, 1.0)
ENV_CHEER = (508 / 2048, 1594 / 2048, 48 / 1024, 1.0)

def alpha_bb(im):
    a = np.array(im.convert('RGBA'))[..., 3]
    ys, xs = np.nonzero(a > 8)
    return int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())

def hair_bb(im):
    """Same hair mask as Editor/LumiPoseNormalizer.cs -- keep the constants in step."""
    a = np.array(im.convert('RGBA')).astype(np.int16)
    r, g, b, al = a[..., 0], a[..., 1], a[..., 2], a[..., 3]
    m = ((al >= 230) & (r >= 38) & (r <= 80) & (g >= 26) & (g <= 58)
         & (b >= 20) & (b <= 52) & (r > g + 8) & (g > b + 3))
    for _ in range(2):
        n = np.zeros_like(m); c = m[1:-1, 1:-1]
        n[1:-1, 1:-1] = (c & m[:-2, 1:-1] & m[2:, 1:-1] & m[1:-1, :-2] & m[1:-1, 2:]
                         & m[:-2, :-2] & m[:-2, 2:] & m[2:, :-2] & m[2:, 2:])
        m = n
    ys, xs = np.nonzero(m)
    return int(xs.min()), int(ys.min()), int(xs.max()), int(ys.max())

def half_body(env, pose, out):
    """env = (bx0, bx1, by0, by1): the alpha envelope of the file being replaced, as fractions.

    HARDCODED ON PURPOSE. Reading it from `out` would make the script read its own previous
    output, so every re-run would drift a little and a clean checkout would give a different
    answer than a repeat run. These four numbers were measured once off the pre-F58 originals
    (see ENV_WAVE / ENV_CHEER) and are the contract the scene layouts are tuned against.
    """
    bx0, bx1, by0, by1 = env
    box_w, box_h = (bx1 - bx0) * CW, (by1 - by0) * CH
    s = Image.open(pose).convert('RGBA')
    fx0, fy0, fx1, fy1 = alpha_bb(s)
    k = min(box_w / (fx1 - fx0 + 1), box_h / (fy1 - fy0 + 1))
    sc = s.resize((round(s.width * k), round(s.height * k)), Image.LANCZOS)
    c = Image.new('RGBA', (CW, CH), (0, 0, 0, 0))
    c.alpha_composite(sc, (round((bx0 + bx1) / 2 * CW - ((fx0 + fx1 + 1) / 2) * k),
                           round(by1 * CH - (fy1 + 1) * k)))
    c.save(out)
    return f"{os.path.basename(out)}  {CW}x{CH}  scale={k:.3f}"

def badge(pose, out):
    raw = Image.open(pose).convert('RGBA'); A = np.array(raw)
    op = (A[..., 3] > 250).sum(axis=1)
    # Last fully-solid torso row. The pose ends in a straight cut above the disc's bottom edge,
    # which showed as a white crescent; repeating this row downward lets the circle do the crop.
    solid = max(y for y in range(A.shape[0]) if op[y] >= 0.9 * op.max())
    hx0, hy0, hx1, hy1 = hair_bb(raw)
    k = (HEAD_W_FRAC * BS) / (hx1 - hx0 + 1)
    oy = round(HEAD_CY * BS - ((hy0 + hy1 + 1) / 2) * k)
    need = int(np.ceil((BS - oy) / k)) + 2
    ext = np.vstack([A[:solid + 1], np.repeat(A[solid:solid + 1], max(0, need - solid - 1), axis=0)])
    fig = Image.fromarray(ext)
    fig = fig.resize((round(fig.width * k), round(fig.height * k)), Image.LANCZOS)
    R = (BS // 2 - PAD) * SS
    big = Image.new('RGBA', (BS * SS, BS * SS), (0, 0, 0, 0))
    ImageDraw.Draw(big).ellipse([BS*SS//2-R, BS*SS//2-R, BS*SS//2+R, BS*SS//2+R], fill=(255,)*4)
    disc = big.resize((BS, BS), Image.LANCZOS)
    layer = Image.new('RGBA', (BS, BS), (0, 0, 0, 0))
    layer.alpha_composite(fig, (round(0.5 * BS - ((hx0 + hx1 + 1) / 2) * k), oy))
    out_im = Image.composite(Image.alpha_composite(disc, layer),
                             Image.new('RGBA', (BS, BS), (0, 0, 0, 0)), disc.split()[3])
    ring = Image.new('RGBA', (BS * SS, BS * SS), (0, 0, 0, 0))
    ImageDraw.Draw(ring).ellipse([BS*SS//2-R, BS*SS//2-R, BS*SS//2+R, BS*SS//2+R],
                                 outline=GREEN, width=RING * SS)
    out_im.alpha_composite(ring.resize((BS, BS), Image.LANCZOS))
    out_im.save(out)
    return f"{os.path.basename(out)}  {BS}x{BS}  scale={k:.3f}"

if __name__ == '__main__':
    for m in (half_body(ENV_WAVE,  os.path.join(POSES, SRC_WAVE),  WAVE),
              half_body(ENV_CHEER, os.path.join(POSES, SRC_CHEER), CHEER),
              badge(os.path.join(POSES, SRC_BADGE), BADGE)):
        print("wrote", m)
    print("\nDone. The .png.meta files were not touched. Let Unity reimport, then check "
          "Arrange / Summary / the race briefing in a PORTRAIT render.")

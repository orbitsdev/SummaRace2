"""Measure a string the way TextMeshPro will actually measure it.

Why this exists: every readability rule in this pipeline up to now has been a proxy --
words, then characters. A character count cannot tell you that "Mr. Willy Wonka" and
"immmmmmmmmmmmmm" are wildly different widths, and it cannot tell you at what point
TMP's auto-sizer gives up and shrinks a race card's text below the size a learner has
actually been playtested reading. This reads the real per-glyph advance widths out of the
project's own TMP font assets and reproduces TMP's layout closely enough to answer:

    "at what font size does this exact string render inside this exact rect?"

Approximations (all stated, none silent):
  * kerning pairs are ignored (TMP Settings has kerning on; pair adjustments in these
    fonts are small and would only ever make text NARROWER, so every width here is a
    conservative upper bound -- a string that fits under this model fits in TMP too)
  * greedy word wrap on spaces, which is what TMP does for Latin text
  * line height from m_FaceInfo.m_LineHeight, block height = (n-1)*lineHeight +
    (ascent - descent), which is TMP's own preferred-height formula
  * auto-size resolution 0.05pt, well under any size a human can distinguish
"""
import os
import re
import struct

import paths

_CACHE = {}

# The TMP font assets in this project are m_AtlasPopulationMode: 1 (DYNAMIC), so their
# baked m_CharacterTable holds only the glyphs some scene has already used -- 69 of ASCII
# for Fredoka, 44 for Nunito. Fredoka's baked table is missing J, Z, j, z, '-', ':' and
# '(' among others, all of which appear in the stories. Measuring from the baked table
# alone would silently substitute a space width for every one of them. So the advances
# come from the SOURCE TTF (which is what TMP itself rasterises from at runtime), and the
# baked table is used only to VERIFY the TTF reader agrees with Unity's own import.
TTF_FOR = {
    "Fredoka-SemiBold SDF": os.path.join("Fredoka", "static", "Fredoka-SemiBold.ttf"),
    "Nunito-Regular SDF": os.path.join("Nunito", "static", "Nunito-Regular.ttf"),
    "Nunito-Bold SDF": os.path.join("Nunito", "static", "Nunito-Bold.ttf"),
}


def _read_ttf_advances(path):
    """unitsPerEm + {unicode: advance} from a TrueType file. cmap formats 4 and 12."""
    with open(path, "rb") as f:
        data = f.read()

    num_tables = struct.unpack(">H", data[4:6])[0]
    tables = {}
    for i in range(num_tables):
        off = 12 + i * 16
        tag = data[off:off + 4].decode("latin-1")
        t_off, t_len = struct.unpack(">II", data[off + 8:off + 16])
        tables[tag] = (t_off, t_len)

    head = tables["head"][0]
    units_per_em = struct.unpack(">H", data[head + 18:head + 20])[0]

    hhea = tables["hhea"][0]
    num_h_metrics = struct.unpack(">H", data[hhea + 34:hhea + 36])[0]

    hmtx = tables["hmtx"][0]
    adv = []
    for i in range(num_h_metrics):
        adv.append(struct.unpack(">H", data[hmtx + i * 4:hmtx + i * 4 + 2])[0])

    def glyph_advance(gid):
        return adv[gid] if gid < len(adv) else (adv[-1] if adv else 0)

    # cmap: prefer (3,10) format 12, else (3,1) format 4
    cmap = tables["cmap"][0]
    n_sub = struct.unpack(">H", data[cmap + 2:cmap + 4])[0]
    best = None
    for i in range(n_sub):
        off = cmap + 4 + i * 8
        pid, eid, sub_off = struct.unpack(">HHI", data[off:off + 8])
        fmt = struct.unpack(">H", data[cmap + sub_off:cmap + sub_off + 2])[0]
        rank = {12: 3, 4: 2, 6: 1, 0: 1}.get(fmt, 0)
        if (pid, eid) in ((3, 10), (3, 1), (0, 3), (0, 4), (0, 6)) and rank:
            if best is None or rank > best[0]:
                best = (rank, cmap + sub_off, fmt)
    if best is None:
        raise ValueError("no usable cmap in " + path)

    _, sub, fmt = best
    mapping = {}
    if fmt == 4:
        seg_x2 = struct.unpack(">H", data[sub + 6:sub + 8])[0]
        seg = seg_x2 // 2
        ends = struct.unpack(">%dH" % seg, data[sub + 14:sub + 14 + seg_x2])
        s = sub + 16 + seg_x2
        starts = struct.unpack(">%dH" % seg, data[s:s + seg_x2])
        d = s + seg_x2
        deltas = struct.unpack(">%dh" % seg, data[d:d + seg_x2])
        r = d + seg_x2
        ranges = struct.unpack(">%dH" % seg, data[r:r + seg_x2])
        for i in range(seg):
            for c in range(starts[i], min(ends[i], 0xFFFF) + 1):
                if ranges[i] == 0:
                    gid = (c + deltas[i]) & 0xFFFF
                else:
                    gi_off = r + i * 2 + ranges[i] + (c - starts[i]) * 2
                    if gi_off + 2 > len(data):
                        continue
                    gid = struct.unpack(">H", data[gi_off:gi_off + 2])[0]
                    if gid:
                        gid = (gid + deltas[i]) & 0xFFFF
                if gid:
                    mapping[c] = gid
    elif fmt == 12:
        n_groups = struct.unpack(">I", data[sub + 12:sub + 16])[0]
        for i in range(n_groups):
            g = sub + 16 + i * 12
            start, end, sgid = struct.unpack(">III", data[g:g + 12])
            for c in range(start, min(end, start + 0x10000) + 1):
                mapping[c] = sgid + (c - start)

    return units_per_em, {c: glyph_advance(g) for c, g in mapping.items()}


class Font:
    def __init__(self, name, point_size, scale, line_height, ascent, descent, advances):
        self.name = name
        self.point_size = point_size
        self.scale = scale
        self.line_height = line_height
        self.ascent = ascent
        self.descent = descent
        self.advances = advances          # unicode -> advance in font units
        vals = [v for v in advances.values() if v > 0]
        self.mean_advance = sum(vals) / float(len(vals)) if vals else point_size * 0.5

    # ---- primitive measurement, in font units at pointSize ----

    def advance(self, ch):
        a = self.advances.get(ord(ch))
        if a is None:
            a = self.advances.get(0x20, self.mean_advance)  # unmapped -> space-ish
        return a

    def width_units(self, s):
        return sum(self.advance(c) for c in s)

    def unit_scale(self, font_size, ortho=1.0):
        """TMP's own fontScale: fontSize / pointSize * faceScale * orthographicMultiplier.

        `ortho` is 1 for a screen-space canvas and 0.1 for world-space TextMeshPro under a
        PERSPECTIVE camera (TMP_Text: `orthographicMultiplier = m_isOrthographic ? 1 : 0.1f`).
        The race cards are world-space text under the 58.7 deg perspective race camera, so
        their 2.4 "font size" is 0.24 world units per em -- miss the 0.1 and every race
        measurement is out by a factor of ten.
        """
        return font_size / float(self.point_size) * self.scale * ortho


def _num(body, key, default=None):
    m = re.search(r"\b%s: (-?[\d.eE+]+)" % key, body)
    return float(m.group(1)) if m else default


def load(asset_name):
    """asset_name e.g. 'Fredoka-SemiBold SDF'."""
    if asset_name in _CACHE:
        return _CACHE[asset_name]
    path = os.path.join(paths.FONTS, asset_name + ".asset")
    with open(path, encoding="utf-8", errors="replace") as f:
        text = f.read()

    face = text.split("m_FaceInfo:", 1)[1].split("m_Material:", 1)[0]
    point_size = _num(face, "m_PointSize", 90.0)
    scale = _num(face, "m_Scale", 1.0)
    line_height = _num(face, "m_LineHeight", point_size * 1.2)
    ascent = _num(face, "m_AscentLine", point_size)
    descent = _num(face, "m_DescentLine", -point_size * 0.25)

    glyph_block = text.split("m_GlyphTable:", 1)[1].split("m_CharacterTable:", 1)[0]
    glyph_adv = {}
    for m in re.finditer(
        r"- m_Index: (\d+)\s+m_Metrics:.*?m_HorizontalAdvance: (-?[\d.eE+]+)",
        glyph_block, re.S):
        glyph_adv[int(m.group(1))] = float(m.group(2))

    char_block = text.split("m_CharacterTable:", 1)[1].split("m_AtlasTextures:", 1)[0]
    advances = {}
    for m in re.finditer(r"m_Unicode: (\d+)\s+m_GlyphIndex: (\d+)", char_block):
        uni, gi = int(m.group(1)), int(m.group(2))
        if gi in glyph_adv:
            advances[uni] = glyph_adv[gi]

    # The baked table is the reference; the TTF is the source of truth for coverage.
    baked = dict(advances)
    ttf_rel = TTF_FOR.get(asset_name)
    max_err = 0.0
    if ttf_rel:
        ttf_path = os.path.join(paths.PROJECT, "Assets", "Art", "Fonts", ttf_rel)
        upem, ttf_adv = _read_ttf_advances(ttf_path)
        full = {}
        for uni, a in ttf_adv.items():
            full[uni] = a / float(upem) * point_size
        for uni, a in baked.items():
            if uni in full:
                max_err = max(max_err, abs(full[uni] - a))
        advances = full

    font = Font(asset_name, point_size, scale, line_height, ascent, descent, advances)
    font.baked = baked
    font.ttf_agreement_error = max_err
    _CACHE[asset_name] = font
    return font


def missing_glyphs(font, s):
    """Characters the font has no glyph for -- they render as a fallback or a box."""
    return sorted({c for c in s if ord(c) not in font.advances and c not in "\n\r"})


# ---- layout ----

def wrap(font, s, font_size, width, first_line_width=None, ortho=1.0):
    """Greedy word wrap. Returns [(line_width, limit_that_line_had_to_fit), ...].

    Two things this has to get right or the whole check lies:
      * explicit newlines. Summary's reference block is five AppendLine'd rows and page
        text can carry breaks; treating a line break as an ordinary character would
        measure the whole block as one enormous line and wrap it somewhere arbitrary.
      * a per-line limit. The Reader hangs its "A. " letter with <indent=9%>, so line one
        has the full width and every wrapped line has 9% less. Comparing the widest line
        against a single width silently flags (or clears) the wrong strings.
    """
    us = font.unit_scale(font_size, ortho)
    if first_line_width is None:
        first_line_width = width
    space = font.advance(" ") * us
    out = []
    for para in s.splitlines() or [""]:
        cur = 0.0
        limit = first_line_width if not out else width
        started = False
        for word in para.split(" "):
            if word == "":
                continue
            w = font.width_units(word) * us
            add = w if not started else w + space
            if started and cur + add > limit + 1e-6:
                out.append((cur, limit))
                cur = w
                limit = width
            else:
                cur += add
            started = True
        out.append((cur, limit))
    return out


def fits(font, s, font_size, rect_w, rect_h, first_line_width=None, wrap_text=True,
         line_spacing=1.0, ortho=1.0):
    us = font.unit_scale(font_size, ortho)
    if wrap_text:
        lines = wrap(font, s, font_size, rect_w, first_line_width, ortho)
    else:
        lines = [(font.width_units(" ".join(s.splitlines())) * us, rect_w)]
    overflow = max(w - lim for w, lim in lines)
    widest = max(w for w, _ in lines)
    n = len(lines)
    block_h = ((n - 1) * font.line_height * us * line_spacing
               + (font.ascent - font.descent) * us)
    return (overflow <= 1e-6 and block_h <= rect_h + 1e-6), widest, block_h, n


def autosize(font, s, rect_w, rect_h, size_min, size_max, first_line_width=None,
             wrap_text=True, line_spacing=1.0, ortho=1.0):
    """Reproduce TMP auto-sizing: the largest size in [min, max] that fits.

    Returns (size, fits_at_that_size, lines, widest, block_height). When even `size_min`
    overflows, the size is clamped to `size_min` and fits=False -- which is exactly what
    TMP does, and is the state where text is clipped or spills out of its box.
    """
    ok, widest, h, n = fits(font, s, size_max, rect_w, rect_h, first_line_width,
                            wrap_text, line_spacing, ortho)
    if ok:
        return size_max, True, n, widest, h
    ok_min, widest, h, n = fits(font, s, size_min, rect_w, rect_h, first_line_width,
                                wrap_text, line_spacing, ortho)
    if not ok_min:
        return size_min, False, n, widest, h
    lo, hi = size_min, size_max
    while hi - lo > 0.05:
        mid = (lo + hi) * 0.5
        ok, _, _, _ = fits(font, s, mid, rect_w, rect_h, first_line_width, wrap_text,
                           line_spacing, ortho)
        if ok:
            lo = mid
        else:
            hi = mid
    ok, widest, h, n = fits(font, s, lo, rect_w, rect_h, first_line_width, wrap_text,
                            line_spacing, ortho)
    return lo, True, n, widest, h

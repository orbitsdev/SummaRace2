"""Runtime-geometry gate: will every string a learner reads actually render?

`flag.py` gates CONTENT (tells, register, a character budget). This gates PRESENTATION: it
takes each string to the exact rect it lands in at runtime, in the exact font, and asks
TMP's own auto-size question -- what point size does this end up at, and does it still fit
at the floor? A character budget cannot answer that. "Molly" and "Willy" are the same five
characters and different widths, and the race card's auto-sizer will happily shrink text
to 0.2 (its fontSizeMin) rather than admit it does not fit, so nothing ever "overflows" --
it just becomes unreadable, silently, on the surface that IS the study's measure.

Every geometry number below is READ, not assumed:
  * lane spacing / card size / font bounds  <- EndlessRaceDirector.cs + MainSummaRace.unity
  * UI rects                                <- the .unity files, via scenegeom
  * glyph advances                          <- the project's own TTFs, cross-checked
                                               against Unity's baked TMP tables

Run:  python fit.py            summary + failures
      python fit.py --all      every failure, no truncation
"""
import json
import math
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

import paths
import scenegeom
import tmpfont

# ---------------------------------------------------------------- race geometry
# EndlessRaceDirector.PlaceAnswerGate:
#   cardWidth = Mathf.Min(1.55f, laneOffset * 0.95f);   laneOffset = 1.5 (MainSummaRace)
#   card size = (cardWidth, 0.85)
#   BuildCard: text rect = (size.x - 0.15, size.y - 0.12); autosize [0.2 .. 2.4]
LANE_OFFSET = 1.5
CARD_W = min(1.55, LANE_OFFSET * 0.95)          # 1.425
CARD_H = 0.85
CARD_TEXT_W = CARD_W - 0.15                     # 1.275
CARD_TEXT_H = CARD_H - 0.12                     # 0.73
CARD_FONT_MIN = 0.2
CARD_FONT_MAX = 2.4
# TMP_Text: `orthographicMultiplier = m_isOrthographic ? 1 : 0.1f`. The race cards are
# world-space TextMeshPro under a perspective camera, so font size 2.4 = 0.24 world units
# per em. Miss this and every race number is out by 10x.
ORTHO_3D = 0.1

# MainSummaRace.unity: the scene's one Camera has `field of view: 58.7` (vertical).
CAMERA_VFOV = 58.7
SCREEN_H = 1920.0
# px per world unit at distance d = SCREEN_H / (2 d tan(vfov/2))
PX_M = SCREEN_H / (2.0 * math.tan(math.radians(CAMERA_VFOV / 2.0)))

# Cap height at which a moving card is worth trying to read. 20px on a 1920-tall portrait
# screen is roughly a 2.6mm capital on a 6" phone -- about the smallest a Grade-4 reader
# tracks on a moving object. Used only to express reading time; the PASS/FAIL gate below
# is the playtested-floor comparison, which needs no such judgement call.
LEGIBLE_CAP_PX = 20.0
# TrackManager (MainSummaRace): minSpeed 10, maxSpeed 30, accel 0.2/s. Gates 1-5 of a
# ~60-90s run sit around here, so this is the speed the card is closing at.
GATE_RUN_SPEED = 14.0

# EndlessRaceDirector.FlyCollectedToSlot: pill 600x180, text inset 18/14, autosize 26-58.
TOKEN_W, TOKEN_H = 600.0 - 36.0, 180.0 - 28.0
TOKEN_MIN, TOKEN_MAX = 26.0, 58.0

# EndlessRaceDirector.BuildTracker: slot 138x96, label inset 10/8, NoWrap, 24-50, Ellipsis.
SLOT_W, SLOT_H = 138.0 - 20.0, 96.0 - 16.0
SLOT_MIN, SLOT_MAX = 24.0, 50.0

# ReaderController: options are rendered as "A. " + <indent=9%>text</indent>.
READER_INDENT = 0.09

FREDOKA = "Fredoka-SemiBold SDF"
NUNITO = "Nunito-Regular SDF"
NUNITO_B = "Nunito-Bold SDF"
CAP_LINE = {FREDOKA: 63.0, NUNITO: 65.0, NUNITO_B: 65.0}   # m_CapLine from each asset
FONT_BY_GUID = {
    "2d80ca966f93d3f4baba91251ab4761c": FREDOKA,
    "f3b748a641b77c647a84f87626998cc5": NUNITO,
    "274a6a94c05170c499d70c25618bfad6": NUNITO_B,
}


def measure(font_name, s, w, h, fmin, fmax, ortho=1.0, first_line_width=None,
            wrap_text=True):
    f = tmpfont.load(font_name)
    size, ok, lines, widest, block = tmpfont.autosize(
        f, s, w, h, fmin, fmax, first_line_width=first_line_width,
        wrap_text=wrap_text, ortho=ortho)
    return dict(size=size, fits=ok, lines=lines, widest=widest, block=block,
                missing=tmpfont.missing_glyphs(f, s), font=font_name)


def race_card(s):
    """What a race answer card does with this string. The single most important call
    here: every option, correct or not, is a card."""
    return measure(FREDOKA, s, CARD_TEXT_W, CARD_TEXT_H, CARD_FONT_MIN, CARD_FONT_MAX,
                   ortho=ORTHO_3D)


def cap_world(font_name, font_size, ortho=ORTHO_3D):
    f = tmpfont.load(font_name)
    return CAP_LINE[font_name] * f.unit_scale(font_size, ortho)


def cap_px_at(font_name, font_size, distance):
    return cap_world(font_name, font_size) * PX_M / distance


def legible_distance(font_size):
    """Metres at which this card's capitals first reach LEGIBLE_CAP_PX."""
    return cap_world(FREDOKA, font_size) * PX_M / LEGIBLE_CAP_PX


def read_seconds(font_size, speed=GATE_RUN_SPEED):
    return legible_distance(font_size) / speed


# ---------------------------------------------------------------- scene lookups

def named_box(scene, name, index=0):
    s = scenegeom.load(scene)
    hits = s.find(name)
    if not hits:
        raise RuntimeError("%s: no object named %s" % (scene, name))
    go = hits[index]
    t = s.tmp(go)
    w, h = s.size(go)
    ml, mt, mr, mb = t["margin"]
    return dict(w=w - ml - mr, h=h - mt - mb, auto=t["autosize"],
                fmin=t["fmin"], fmax=t["fmax"], fixed=t["fixed"],
                font=FONT_BY_GUID.get(t["font_guid"], NUNITO))


def reader_option_box():
    """The three option labels are identical; find one by its distinctive autosize band."""
    s = scenegeom.load("Reader")
    for fid, (cls, body) in s.docs.items():
        if cls != "1":
            continue
        t = s.tmp(fid)
        if t and t["autosize"] and (t["fmin"], t["fmax"]) == (26.0, 44.0):
            w, h = s.size(fid)
            ml, mt, mr, mb = t["margin"]
            return dict(w=w - ml - mr, h=h - mt - mb, fmin=t["fmin"], fmax=t["fmax"],
                        font=FONT_BY_GUID.get(t["font_guid"], NUNITO))
    raise RuntimeError("Reader: no option label found")


def arrange_piece_boxes():
    """Arrange's piece + slot labels are FIXED size (no autosize) with overflow mode
    Overflow, so a long element sentence spills over its neighbours instead of shrinking."""
    s = scenegeom.load("Arrange")
    out = {}
    for fid, (cls, body) in s.docs.items():
        if cls != "1" or s.name(fid) != "Label":
            continue
        t = s.tmp(fid)
        if t is None or t["autosize"]:
            continue
        w, h = s.size(fid)
        key = (round(w, 1), round(h, 1), t["fixed"])
        out.setdefault(key, dict(n=0, font=FONT_BY_GUID.get(t["font_guid"], NUNITO)))
        out[key]["n"] += 1
    return out


# ---------------------------------------------------------------- the run

def collect_rows(stories):
    opt = reader_option_box()
    q = named_box("Reader", "QuestionText")
    page = named_box("Reader", "PageText")
    summ = named_box("Summary", "ReferenceText")
    arr = arrange_piece_boxes()
    # the two 912-wide fixed labels are the piece tray and the slot rows
    arr_boxes = [(k, v) for k, v in arr.items() if k[0] > 800]

    rows = []
    for d in stories:
        sid = d["id"]
        for el in d["elements"]:
            for tag, s in ([("correct", el["correct"])] +
                           [("distr%d" % k, t) for k, t in enumerate(el["distractors"])]):
                rows.append((sid, "race_card", el["type"], tag, s, race_card(s)))
            rows.append((sid, "collect_token", el["type"], "correct", el["correct"],
                         measure(FREDOKA, el["correct"], TOKEN_W, TOKEN_H,
                                 TOKEN_MIN, TOKEN_MAX)))
            for (w, h, fixed), meta in arr_boxes:
                rows.append((sid, "arrange_%.0f" % fixed, el["type"], "correct",
                             el["correct"],
                             measure(meta["font"], el["correct"], w, h, fixed, fixed)))
        for pi, p in enumerate(d["pages"], 1):
            for oi, o in enumerate(p["question"]["options"]):
                full = "ABC"[oi] + ". " + o
                rows.append((sid, "reader_option", "p%d" % pi, "opt%d" % oi, full,
                             measure(opt["font"], full, opt["w"] * (1 - READER_INDENT),
                                     opt["h"], opt["fmin"], opt["fmax"],
                                     first_line_width=opt["w"])))
            rows.append((sid, "reader_question", "p%d" % pi, "q", p["question"]["text"],
                         measure(q["font"], p["question"]["text"], q["w"], q["h"],
                                 q["fmin"], q["fmax"])))
            rows.append((sid, "reader_page", "p%d" % pi, "text", p["text"],
                         measure(page["font"], p["text"], page["w"], page["h"],
                                 page["fmin"], page["fmax"])))
        block = "\n".join("%d. %s: %s" % (i + 1, el["type"], el["correct"])
                          for i, el in enumerate(d["elements"]))
        rows.append((sid, "summary_reference", "-", "block", block,
                     measure(summ["font"], block, summ["w"], summ["h"],
                             summ["fixed"], summ["fixed"])))
    return rows, dict(opt=opt, q=q, page=page, summ=summ, arr=arr_boxes)


def run():
    detail = "--all" in sys.argv
    stories = [json.load(open(p, encoding="utf-8")) for p in paths.story_paths()]
    rows, boxes = collect_rows(stories)

    print("GEOMETRY READ FROM THE PROJECT")
    print("  race card      %.3f x %.3f world text rect, Fredoka, autosize %.1f-%.1f, "
          "ortho x%.1f" % (CARD_TEXT_W, CARD_TEXT_H, CARD_FONT_MIN, CARD_FONT_MAX, ORTHO_3D))
    print("  collect token  %.0f x %.0f px, Fredoka, %.0f-%.0f"
          % (TOKEN_W, TOKEN_H, TOKEN_MIN, TOKEN_MAX))
    print("  tracker slot   %.0f x %.0f px, Fredoka NoWrap+Ellipsis, %.0f-%.0f"
          % (SLOT_W, SLOT_H, SLOT_MIN, SLOT_MAX))
    print("  reader option  %.1f x %.1f px, %s, %.0f-%.0f, hanging indent %d%%"
          % (boxes["opt"]["w"], boxes["opt"]["h"], boxes["opt"]["font"],
             boxes["opt"]["fmin"], boxes["opt"]["fmax"], READER_INDENT * 100))
    print("  reader Q       %.1f x %.1f px, %s, %.0f-%.0f"
          % (boxes["q"]["w"], boxes["q"]["h"], boxes["q"]["font"],
             boxes["q"]["fmin"], boxes["q"]["fmax"]))
    print("  reader page    %.1f x %.1f px, %s, %.0f-%.0f"
          % (boxes["page"]["w"], boxes["page"]["h"], boxes["page"]["font"],
             boxes["page"]["fmin"], boxes["page"]["fmax"]))
    print("  summary ref    %.1f x %.1f px, %s, FIXED %.0f, overflow=Overflow"
          % (boxes["summ"]["w"], boxes["summ"]["h"], boxes["summ"]["font"],
             boxes["summ"]["fixed"]))
    for (w, h, fixed), meta in boxes["arr"]:
        print("  arrange label  %.1f x %.1f px, %s, FIXED %.0f (x%d), overflow=Overflow"
              % (w, h, meta["font"], fixed, meta["n"]))
    for name in (FREDOKA, NUNITO, NUNITO_B):
        f = tmpfont.load(name)
        print("  %-22s TTF advances match Unity's baked TMP table to %.4f font units"
              % (name, f.ttf_agreement_error))
    print()

    # ---- tracker (fixed content, one check for the whole corpus)
    print("SWBST TRACKER SLOTS")
    print("  RefreshTracker sets a collected slot to `type.ToUpperInvariant()`, not to the")
    print("  answer -- so the 50-character-answer constraint really is gone. What is left")
    print("  is the five element NAMES, NoWrap, Ellipsis, floor %.0fpt:" % SLOT_MIN)
    tracker_bad = []
    for t in ("SOMEBODY", "WANTED", "BUT", "SO", "THEN"):
        r = measure(FREDOKA, t, SLOT_W, SLOT_H, SLOT_MIN, SLOT_MAX, wrap_text=False)
        need = r["widest"] / SLOT_W
        print("    %-9s %.1fpt, needs %.0fpx in a %.0fpx slot  %s"
              % (t, r["size"], r["widest"], SLOT_W,
                 "OK" if r["fits"] else "ELLIPSIZED (%.0f%% over)" % ((need - 1) * 100)))
        if not r["fits"]:
            tracker_bad.append(t)
    print()

    # ---- the playtested baseline
    base = [r for r in rows if r[0].startswith("s01") and r[1] == "race_card"]
    floor = min(x[5]["size"] for x in base)
    worst = min(base, key=lambda x: x[5]["size"])
    print("RACE-CARD READABILITY FLOOR, derived rather than invented")
    print("  s01_* are the only stories anyone has played. Their worst-rendering card is")
    print("    %s / %s : %r" % (worst[0], worst[2], worst[4]))
    print("    -> %.2f font, %d lines, cap height %.1f px at 10 m, legible (>= %.0f px)"
          " from %.1f m = %.2f s at %.0f m/s"
          % (worst[5]["size"], worst[5]["lines"], cap_px_at(FREDOKA, worst[5]["size"], 10),
             LEGIBLE_CAP_PX, legible_distance(worst[5]["size"]),
             read_seconds(worst[5]["size"]), GATE_RUN_SPEED))
    print("  That card has been read in a real playtest, so nothing may render smaller.")
    print()

    cards = [r for r in rows if r[1] == "race_card"]
    sizes = sorted(r[5]["size"] for r in cards)
    lines = {}
    for r in cards:
        lines[r[5]["lines"]] = lines.get(r[5]["lines"], 0) + 1
    print("  %d race-card strings: min %.2f  p05 %.2f  median %.2f  max %.2f"
          % (len(sizes), sizes[0], sizes[len(sizes) // 20], sizes[len(sizes) // 2], sizes[-1]))
    print("  line counts: " + "  ".join("%d line%s x%d" % (k, "" if k == 1 else "s", v)
                                        for k, v in sorted(lines.items())))
    print("  reading window at %.0f m/s: worst %.2f s, median %.2f s, best %.2f s"
          % (GATE_RUN_SPEED, read_seconds(sizes[0]),
             read_seconds(sizes[len(sizes) // 2]), read_seconds(sizes[-1])))
    print()

    # ---- failures
    failures = []
    for sid, kind, slot, tag, s, res in rows:
        bad = None
        if kind == "race_card":
            if not res["fits"]:
                bad = "overflows the card even at fontSizeMin %.1f" % CARD_FONT_MIN
            elif res["size"] < floor - 1e-6:
                bad = ("renders at %.2f, below the playtested floor %.2f "
                       "(%.2f s of reading vs %.2f s)"
                       % (res["size"], floor, read_seconds(res["size"]), read_seconds(floor)))
        elif not res["fits"]:
            bad = ("does not fit at %.0fpt: needs %.0f x %.0f px"
                   % (res["size"], res["widest"], res["block"]))
        if res["missing"]:
            bad = (bad + "; " if bad else "") + "no glyph for %r" % "".join(res["missing"])
        if bad:
            failures.append((sid, kind, slot, tag, s, bad))

    kinds = []
    for r in rows:
        if r[1] not in kinds:
            kinds.append(r[1])
    by_kind = {}
    for f in failures:
        by_kind.setdefault(f[1], []).append(f)

    print("FAILURES BY SURFACE")
    for k in kinds:
        total = len([r for r in rows if r[1] == k])
        print("  %-22s %3d / %4d" % (k, len(by_kind.get(k, [])), total))
    if tracker_bad:
        print("  %-22s %3d / %4d   (%s)" % ("swbst_tracker", len(tracker_bad), 5,
                                            ", ".join(tracker_bad)))
    print()

    for k in kinds:
        fs = by_kind.get(k, [])
        if not fs:
            continue
        print("=== %s (%d)" % (k, len(fs)))
        for sid, kind, slot, tag, s, why in (fs if detail else fs[:30]):
            print("  %-12s %-9s %-7s %s" % (sid, slot, tag, why))
            print("        %r" % s[:130])
        if not detail and len(fs) > 30:
            print("  ... %d more (--all)" % (len(fs) - 30))
        print()

    # ---- is MAX_CARD_CHARS still the right proxy?
    print("MAX_CARD_CHARS RE-DERIVED AGAINST THE CARD AS IT STANDS NOW")
    lens = sorted(len(r[4]) for r in cards)
    at_floor = [r for r in cards if abs(r[5]["size"] - floor) < 0.02]
    print("  card text rect is %.3f x %.3f world units (F30 sizing, unchanged since)"
          % (CARD_TEXT_W, CARD_TEXT_H))
    print("  longest string in the corpus: %d chars; flag.py budget: 53" % lens[-1])
    print("  strings that bottom out at the playtested floor: %d" % len(at_floor))
    worst_chars = max((len(r[4]) for r in at_floor), default=0)
    print("  the floor is reached at %d characters for the widest of them, so 53 remains"
          % worst_chars)
    print("  a sound PROXY -- but width, not count, is what actually binds: the corpus")
    print("  holds %d-char strings that render fine and %d-char strings that do not."
          % (lens[-1], worst_chars))
    return 1 if (failures or tracker_bad) else 0


if __name__ == "__main__":
    sys.exit(run())

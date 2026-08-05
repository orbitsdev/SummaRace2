"""Whole-corpus content QA: everything flag.py gates, plus everything it cannot see.

flag.py is a per-element-set gate with a fixed rule list. This is the corpus-level audit:
it asks the question the F44 crisis was really about -- "is there ANY signal in these three
cards that beats guessing without reading?" -- and it asks it of every signal a learner can
actually perceive, not just the one that was caught last time.

The strategies scored below are deliberately mechanical. A Grade-4 learner will not compute
character counts; but they very quickly learn "the long one", "the big-print one", "the one
that isn't like the others". Each of those is a measurable strategy and each is scored here
against the 33.3% floor that pure guessing gets at a three-card gate.

Run:  python qa.py              headline numbers + defects
      python qa.py --json out   also dump the per-story machine-readable results
"""
import json
import os
import re
import sys
from collections import Counter, defaultdict

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

import fit
import paths

SLOTS = ("SOMEBODY", "WANTED", "BUT", "SO", "THEN")
WORLDS = {
    1: "morning_suburbs", 2: "bright_park", 3: "sunset_town", 4: "blue_hour_suburbs",
    5: "overcast_industrial", 6: "golden_fields", 7: "night_city", 8: "misty_morning",
    9: "autumn_lane", 10: "starlit_finale",
}
DIFFICULTIES = ("easy", "average", "hard")

STOP = set("""a an the and or but so then to of in on at for with from by as is are was were
be been being it its it's he she they them his her their him we you i my your our this that
these those there here not no did do does done had has have will would could can not could
also very just about into over out up down after before when while what which who whom whose
how why if than too more most some any all each other another one two three""".split())


def norm(s):
    return re.sub(r"[^a-z0-9' ]", " ", s.lower()).split()


def content_words(s):
    return [w for w in norm(s) if w not in STOP and len(w) > 2]


def load_stories():
    out = []
    for p in paths.story_paths():
        d = json.load(open(p, encoding="utf-8"))
        d["_path"] = p
        d["_file"] = os.path.splitext(os.path.basename(p))[0]
        out.append(d)
    return out


# ------------------------------------------------------------------ A. structure

def check_structure(d):
    bad = []
    sid = d["_file"]
    if d.get("id") != sid:
        bad.append("id %r != filename %r" % (d.get("id"), sid))
    m = re.match(r"s(\d\d)_(easy|average|hard)$", sid)
    if not m:
        bad.append("filename does not match sNN_<difficulty>")
        return bad
    day, diff = int(m.group(1)), m.group(2)
    if d.get("session") != day:
        bad.append("session %r != %d from the filename" % (d.get("session"), day))
    if d.get("difficulty") != diff:
        bad.append("difficulty %r != %r" % (d.get("difficulty"), diff))
    for f in ("title", "mainIdea", "heroImage"):
        if not d.get(f):
            bad.append("missing %s" % f)
    if not d.get("world"):
        bad.append("missing world")
    elif d["world"] != WORLDS[day]:
        bad.append("world %r != session %d's %r" % (d["world"], day, WORLDS[day]))

    pages = d.get("pages") or []
    if len(pages) != 5:
        bad.append("pages=%d (want 5)" % len(pages))
    for i, p in enumerate(pages, 1):
        if not p.get("text"):
            bad.append("p%d empty text" % i)
        if not p.get("narration"):
            bad.append("p%d missing narration path" % i)
        q = p.get("question") or {}
        opts = q.get("options") or []
        if not q.get("text"):
            bad.append("p%d empty question" % i)
        if len(opts) != 3:
            bad.append("p%d options=%d" % (i, len(opts)))
        elif len({o.strip().lower() for o in opts}) != 3:
            bad.append("p%d duplicate options" % i)
        ci = q.get("correctIndex")
        if not isinstance(ci, int) or not 0 <= ci < len(opts):
            bad.append("p%d correctIndex=%r out of range" % (i, ci))

    els = d.get("elements") or []
    if len(els) != 5:
        bad.append("elements=%d (want 5)" % len(els))
    else:
        got = tuple(e.get("type") for e in els)
        if got != SLOTS:
            bad.append("element order %s != S-W-B-S-T" % (got,))
    for e in els:
        ds = e.get("distractors") or []
        if not e.get("correct"):
            bad.append("%s empty correct" % e.get("type"))
        if len(ds) != 2:
            bad.append("%s distractors=%d" % (e.get("type"), len(ds)))
        trio = [e.get("correct", "")] + list(ds)
        if len({t.strip().lower() for t in trio}) != len(trio):
            bad.append("%s duplicate option among the three cards" % e.get("type"))

    mi = d.get("mission")
    if not mi:
        bad.append("mission missing/null")
    else:
        for f in ("playerSpeed", "checkpointSpacing", "dangerPerSecond"):
            if mi.get(f) is None:
                bad.append("mission.%s missing" % f)
        if mi.get("startingDanger") is None:
            bad.append("mission.startingDanger missing")
    return bad


def check_assets(d):
    """heroImage and narration are Resources paths -- do the files exist on disk?"""
    bad = []
    res = os.path.join(paths.PROJECT, "Assets", "_Game", "Resources")
    hero = d.get("heroImage")
    if hero:
        hits = [e for e in (".png", ".jpg", ".jpeg")
                if os.path.exists(os.path.join(res, hero.replace("/", os.sep) + e))]
        if not hits:
            bad.append("heroImage %r resolves to no file" % hero)
    for i, p in enumerate(d.get("pages") or [], 1):
        n = p.get("narration")
        if not n:
            continue
        hits = [e for e in (".ogg", ".mp3", ".wav")
                if os.path.exists(os.path.join(res, n.replace("/", os.sep) + e))]
        if not hits:
            bad.append("p%d narration %r resolves to no file" % (i, n))
    return bad


# ------------------------------------------------------- B. tells a learner can use

def strategy_scores(stories):
    """Expected score of each no-reading strategy across all 150 gates.

    Ties are scored as 1/k so the number is the true expected value, not a best case.
    """
    strategies = {
        "widest card (most characters)": lambda t: [len(x) for x in t],
        "narrowest card (fewest characters)": lambda t: [-len(x) for x in t],
        "biggest print (largest rendered font)": lambda t: [fit.race_card(x)["size"] for x in t],
        "smallest print": lambda t: [-fit.race_card(x)["size"] for x in t],
        "fewest lines of text": lambda t: [-fit.race_card(x)["lines"] for x in t],
        "most words": lambda t: [len(x.split()) for x in t],
    }
    totals = {k: 0.0 for k in strategies}
    n = 0
    per_story = defaultdict(lambda: defaultdict(float))
    for d in stories:
        for e in d["elements"]:
            trio = [e["correct"]] + list(e["distractors"])
            n += 1
            for name, key in strategies.items():
                vals = key(trio)
                best = max(vals)
                winners = [i for i, v in enumerate(vals) if v == best]
                hit = (1.0 / len(winners)) if 0 in winners else 0.0
                totals[name] += hit
                per_story[d["_file"]][name] += hit
    return {k: v / n for k, v in totals.items()}, per_story, n


def permutation_null(stories, key, trials=4000, seed=12345):
    """What would this strategy score if `correct` were a random one of the three?

    The right null is NOT a flat 1/3. Ties, and the fact that these three strings are not
    exchangeable in length by construction, both move it. Permuting WHICH card is correct
    while holding the three strings fixed gives the exact null for "this signal carries no
    information about correctness", which is the only claim that matters.
    """
    import random
    rng = random.Random(seed)
    trios = [[e["correct"]] + list(e["distractors"]) for d in stories for e in d["elements"]]
    vals = [key(t) for t in trios]
    n = len(trios)
    scores = []
    for _ in range(trials):
        hit = 0.0
        for v in vals:
            best = max(v)
            winners = [i for i, x in enumerate(v) if x == best]
            truth = rng.randrange(3)
            if truth in winners:
                hit += 1.0 / len(winners)
        scores.append(hit / n)
    scores.sort()
    mean = sum(scores) / len(scores)
    sd = (sum((x - mean) ** 2 for x in scores) / len(scores)) ** 0.5
    return mean, sd, scores


def length_margins(stories):
    rows = []
    for d in stories:
        for e in d["elements"]:
            c = len(e["correct"])
            ds = [len(x) for x in e["distractors"]]
            rows.append((d["_file"], e["type"], c - max(ds), c - sum(ds) / 2.0, c, ds))
    return rows


# ------------------------------------------------- C. answer-position balance

def position_balance(stories):
    reader = Counter()
    for d in stories:
        for p in d["pages"]:
            reader[p["question"]["correctIndex"]] += 1
    return reader


# ------------------------------------------------- D. race reused from the Reader

def reader_reuse(stories):
    """A race distractor that is verbatim one of the Reader's own wrong options makes the
    race passable from memory of the Reader instead of from comprehension."""
    same_page = 0
    anywhere = 0
    total = 0
    detail = []
    for d in stories:
        page_opts = []
        for p in d["pages"]:
            q = p["question"]
            page_opts.append([o.strip().lower() for j, o in enumerate(q["options"])
                              if j != q["correctIndex"]])
        all_wrong = {o for row in page_opts for o in row}
        for i, e in enumerate(d["elements"]):
            for x in e["distractors"]:
                total += 1
                k = x.strip().lower()
                if i < len(page_opts) and k in page_opts[i]:
                    same_page += 1
                    detail.append((d["_file"], e["type"], "same page", x))
                elif k in all_wrong:
                    anywhere += 1
                    detail.append((d["_file"], e["type"], "other page", x))
    return same_page, anywhere, total, detail


# ------------------------------------------------- E. slot appropriateness

WANT_RE = re.compile(r"^to\s+\w", re.I)
BUT_MARKERS = re.compile(
    r"\b(but|could ?n[o']t|couldn't|did ?n[o']t|didn't|was ?n[o']t|wasn't|were ?n[o']t|"
    r"no |not |never|refused|quit|stopped|broke|lost|missing|gone|too |so hard|hard to|"
    r"afraid|scared|angry|upset|sad|unhappy|tired|sick|hurt|problem|trouble|struggl|"
    r"unfair|forgot|failed|ran out|left|complain|argu|fight|fought|worried|nervous|"
    r"unable|blocked|stuck|difficult|only|without|hid|hiding|took|stole|teased|bull)", re.I)
CHAR_RE = re.compile(r"^(?:[A-Z][a-z]+|The |A |An |His |Her |Their |Mr\.|Mrs\.|Ms\.)")


def slot_shape(stories):
    """The failure mode that matters is NOT 'the correct answer looks wrong for its slot'.
    It is 'the correct answer is the ONLY one that looks right for its slot' -- a learner who
    understands the framework can then eliminate two cards without understanding the story.
    """
    findings = []
    counts = Counter()
    for d in stories:
        for e in d["elements"]:
            trio = [e["correct"]] + list(e["distractors"])
            t = e["type"]
            if t == "WANTED":
                marks = [bool(WANT_RE.match(x)) for x in trio]
            elif t == "BUT":
                marks = [bool(BUT_MARKERS.search(x)) for x in trio]
            elif t == "SOMEBODY":
                marks = [bool(CHAR_RE.match(x)) for x in trio]
            else:
                continue
            counts[t] += 1
            if marks[0] and not marks[1] and not marks[2]:
                findings.append((d["_file"], t, "correct is the ONLY slot-shaped option", trio))
            elif not marks[0] and (marks[1] or marks[2]):
                findings.append((d["_file"], t, "correct is NOT slot-shaped but a distractor is", trio))
    return findings, counts


# ------------------------------------------------- F. factual grounding

def grounding(stories):
    """Content-word overlap of each option with its own story's pages.

    A `correct` line that is poorly grounded in the passage is a candidate for the
    s09_hard-style error (an answer that is not what the story says). A DISTRACTOR that is
    better grounded than the correct answer is the mirror-image threat.
    """
    rows = []
    for d in stories:
        body = " ".join(p["text"] for p in d["pages"])
        vocab = set(content_words(body))
        for e in d["elements"]:
            def score(x):
                cw = content_words(x)
                if not cw:
                    return 1.0, []
                miss = [w for w in cw
                        if w not in vocab and w.rstrip("s") not in vocab
                        and w + "s" not in vocab and w.rstrip("ed") not in vocab
                        and w.rstrip("ing") not in vocab]
                return 1.0 - len(miss) / float(len(cw)), miss
            cs, cmiss = score(e["correct"])
            ds = [score(x)[0] for x in e["distractors"]]
            rows.append((d["_file"], e["type"], cs, ds, cmiss, e["correct"]))
    return rows


# ------------------------------------------------- G. spelling / typography

TYPO_PATTERNS = [
    (re.compile(r"\s{2,}"), "double space"),
    (re.compile(r"\s+[,.;!?]"), "space before punctuation"),
    (re.compile(r"\b(\w+)\s+\1\b", re.I), "repeated word"),
    (re.compile(r"[a-z]\.[A-Z]"), "missing space after full stop"),
    (re.compile(r"\bi\b"), "lowercase standalone 'i'"),
    (re.compile(r"[‘’“”]"), "curly quote (renders, but mixes with straight quotes)"),
    # A single letter standing alone is how "Baba Yag a" looked. Possessive 's and the
    # contraction in "Molly's" are NOT that, so exclude anything touching an apostrophe --
    # without this the rule fires on every possessive in the corpus and buries the signal.
    (re.compile(r"(?<![\w'])[b-hj-z](?![\w'])"), "stray single letter (a word split by a space?)"),
]


def typography(stories):
    hits = []
    vocab = Counter()
    fields = []
    for d in stories:
        for p in d["pages"]:
            fields.append((d["_file"], "page text", p["text"]))
            fields.append((d["_file"], "question", p["question"]["text"]))
            for o in p["question"]["options"]:
                fields.append((d["_file"], "reader option", o))
        for e in d["elements"]:
            fields.append((d["_file"], "%s correct" % e["type"], e["correct"]))
            for x in e["distractors"]:
                fields.append((d["_file"], "%s distractor" % e["type"], x))
        fields.append((d["_file"], "title", d["title"]))
        fields.append((d["_file"], "mainIdea", d["mainIdea"]))

    for sid, where, text in fields:
        for rx, why in TYPO_PATTERNS:
            m = rx.search(text)
            if m:
                hits.append((sid, where, why, text[max(0, m.start() - 25):m.end() + 25]))
        for w in re.findall(r"[A-Za-z']+", text):
            vocab[w.lower()] += 1
    return hits, vocab


# ------------------------------------------------------------------ report

def main():
    stories = load_stories()
    print("STORIES: %d\n" % len(stories))

    print("=" * 78)
    print("A. STRUCTURE + ASSET RESOLUTION")
    print("=" * 78)
    struct_bad = {}
    for d in stories:
        bad = check_structure(d) + check_assets(d)
        if bad:
            struct_bad[d["_file"]] = bad
    if struct_bad:
        for sid, bad in sorted(struct_bad.items()):
            print("  %s" % sid)
            for b in bad:
                print("     - %s" % b)
    else:
        print("  all %d stories: 5 pages, 5 elements in S-W-B-S-T order, 3 distinct options,"
              % len(stories))
        print("  correctIndex in range, world set and matching its session, mission non-null,")
        print("  every heroImage and all 150 narration paths resolve to a real file.")
    print()

    print("=" * 78)
    print("B. CAN A GATE BE PASSED WITHOUT READING?")
    print("=" * 78)
    scores, per_story, n = strategy_scores(stories)
    print("  %d gates. Guessing scores 33.3%%. Every score below is an EXPECTED value:" % n)
    print("  a tie between k cards is credited 1/k, so nothing is flattered by a best case.")
    print()
    print("  IMPORTANT, and it changes what 'the widest card' means: in the shipping race")
    print("  every card is the SAME physical size. EndlessRaceDirector.PlaceAnswerGate")
    print("  builds all three at `new Vector2(cardWidth, 0.85f)` with cardWidth =")
    print("  min(1.55, laneOffset*0.95) = 1.425 for every lane. So the F44-era cue -- a")
    print("  visibly wider card -- no longer physically exists. What a longer string now")
    print("  produces is SMALLER, DENSER TEXT on an identically sized card, which is why")
    print("  'smallest print' is scored here alongside the raw character count.")
    print()
    strat_keys = {
        "widest card (most characters)": lambda t: [len(x) for x in t],
        "narrowest card (fewest characters)": lambda t: [-len(x) for x in t],
        "biggest print (largest rendered font)": lambda t: [fit.race_card(x)["size"] for x in t],
        "smallest print": lambda t: [-fit.race_card(x)["size"] for x in t],
        "fewest lines of text": lambda t: [-fit.race_card(x)["lines"] for x in t],
        "most words": lambda t: [len(x.split()) for x in t],
    }
    print("    %-40s %7s %9s %7s" % ("strategy", "score", "null", "z"))
    for k, v in sorted(scores.items(), key=lambda kv: -kv[1]):
        mu, sd, _ = permutation_null(stories, strat_keys[k])
        z = (v - mu) / sd if sd else 0.0
        mark = "  <-- carries real signal" if abs(z) >= 2.5 else ""
        print("    %-40s %6.1f%% %8.1f%% %+7.1f%s" % (k, v * 100, mu * 100, z, mark))
    print()

    margins = length_margins(stories)
    m_long = [r[2] for r in margins]
    m_mean = [r[3] for r in margins]
    widest = sum(1 for r in margins if r[2] > 0)
    print("  character margin, correct minus its WIDEST distractor:")
    print("    mean %+.1f   median %+.0f   min %+d   max %+d"
          % (sum(m_long) / len(m_long), sorted(m_long)[len(m_long) // 2],
             min(m_long), max(m_long)))
    print("    correct is strictly the widest card in %d of %d sets (%.1f%%)"
          % (widest, len(margins), 100.0 * widest / len(margins)))
    print("    sets breaching flag.py's +/-6 margin: %d"
          % sum(1 for r in margins if abs(r[2]) > 6))
    print("    mean of (correct - mean distractor): %+.1f" % (sum(m_mean) / len(m_mean)))
    worst = sorted(margins, key=lambda r: -abs(r[2]))[:8]
    print("    widest absolute margins:")
    for sid, t, dlong, dmean, c, ds in worst:
        print("      %-12s %-9s %+3d  (correct %d vs %s)" % (sid, t, dlong, c, ds))
    print()

    ranks = Counter()
    for d in stories:
        for e in d["elements"]:
            lens = [len(e["correct"])] + [len(x) for x in e["distractors"]]
            ranks[sum(1 for x in lens[1:] if x > lens[0])] += 1
    print("  where `correct` sits by length among its three cards (0 = the longest):")
    for r in range(3):
        print("      rank %d: %3d / %d  (%.1f%%)   [balanced = 33.3%%]"
              % (r, ranks[r], len(margins), 100.0 * ranks[r] / len(margins)))
    print()

    # The Reader is logged data too (SessionLogService records the first answer per page),
    # so the same tell has to be absent there.
    r_hit = 0.0
    r_n = 0
    for d in stories:
        for p in d["pages"]:
            q = p["question"]
            lens = [len(o) for o in q["options"]]
            best = max(lens)
            winners = [i for i, v in enumerate(lens) if v == best]
            r_n += 1
            if q["correctIndex"] in winners:
                r_hit += 1.0 / len(winners)
    print("  the same test on the READER's own options (also logged first-answer data):")
    print("      'tap the longest option' scores %.1f%% of %d questions (guessing 33.3%%)"
          % (100.0 * r_hit / r_n, r_n))
    print()

    per_story_margin = defaultdict(list)
    for sid, t, dlong, dmean, c, ds in margins:
        per_story_margin[sid].append(dlong)
    print("  per-story mean margin (worst 8):")
    ranked = sorted(per_story_margin.items(),
                    key=lambda kv: -abs(sum(kv[1]) / len(kv[1])))
    for sid, vals in ranked[:8]:
        print("      %-12s %+.1f  (max |margin| %d)"
              % (sid, sum(vals) / len(vals), max(abs(v) for v in vals)))
    print()

    print("=" * 78)
    print("C. ANSWER-POSITION BALANCE")
    print("=" * 78)
    reader = position_balance(stories)
    tot = sum(reader.values())
    print("  Reader, correctIndex as stored in the JSON:")
    for i in range(3):
        print("    index %d: %3d / %d  (%.1f%%)" % (i, reader[i], tot, 100.0 * reader[i] / tot))
    print("  ReaderController.ShuffleDisplayOrder Fisher-Yates shuffles the three buttons on")
    print("  every page, so the stored index is not what the learner sees -- the file-order")
    print("  bias above is neutralised at runtime and does NOT need a content fix.")
    print("  EndlessRaceDirector.PlaceAnswerGate shuffles lanes the same way, so race")
    print("  position carries no information either.")
    print()

    print("=" * 78)
    print("D. RACE DISTRACTORS REUSED FROM THE READER")
    print("=" * 78)
    same, other, total, detail = reader_reuse(stories)
    print("  %d race distractors. Verbatim reuse of a Reader wrong option:" % total)
    print("    from the SAME page   : %d (%.1f%%)" % (same, 100.0 * same / total))
    print("    from another page    : %d (%.1f%%)" % (other, 100.0 * other / total))
    if detail:
        for row in detail[:25]:
            print("      %-12s %-9s %-11s %r" % row)
        if len(detail) > 25:
            print("      ... %d more" % (len(detail) - 25))
    print()

    print("=" * 78)
    print("E. SLOT SHAPE (is the correct card the only slot-appropriate one?)")
    print("=" * 78)
    findings, counts = slot_shape(stories)
    print("  checked %s" % ", ".join("%s x%d" % (k, v) for k, v in sorted(counts.items())))
    only = [f for f in findings if "ONLY" in f[2]]
    notshaped = [f for f in findings if "NOT" in f[2]]
    print("  correct is the only slot-shaped card : %d" % len(only))
    print("  correct is not slot-shaped, distractor is : %d" % len(notshaped))
    for f in (only + notshaped)[:30]:
        print("    %-12s %-9s %s" % (f[0], f[1], f[2]))
        print("        correct : %r" % f[3][0])
        print("        distr   : %r | %r" % (f[3][1], f[3][2]))
    if len(only + notshaped) > 30:
        print("    ... %d more" % (len(only + notshaped) - 30))
    print()

    print("=" * 78)
    print("F. FACTUAL GROUNDING OF `correct` IN ITS OWN PAGES")
    print("=" * 78)
    g = grounding(stories)
    low = sorted([r for r in g if r[2] < 0.75], key=lambda r: r[2])
    beaten = [r for r in g if max(r[3]) > r[2] + 0.15]
    print("  content-word overlap with the passage: mean %.2f"
          % (sum(r[2] for r in g) / len(g)))
    print("  correct lines grounded below 0.75: %d  (read these by hand)" % len(low))
    for sid, t, cs, ds, miss, c in low[:30]:
        print("    %-12s %-9s %.2f  distractors %s  unmatched=%s"
              % (sid, t, cs, ["%.2f" % x for x in ds], miss))
        print("        %r" % c)
    if len(low) > 30:
        print("    ... %d more" % (len(low) - 30))
    print("  a DISTRACTOR better grounded than the correct answer: %d" % len(beaten))
    print()

    print("=" * 78)
    print("G. TYPOGRAPHY / SPELLING")
    print("=" * 78)
    hits, vocab = typography(stories)
    if hits:
        for sid, where, why, ctx in hits[:40]:
            print("  %-12s %-18s %-38s %r" % (sid, where, why, ctx.strip()))
        if len(hits) > 40:
            print("  ... %d more" % (len(hits) - 40))
    else:
        print("  no pattern hits (double spaces, split words, repeated words, "
              "punctuation spacing)")
    hapax = sorted(w for w, c in vocab.items() if c == 1 and len(w) > 3)
    print("\n  %d distinct word forms; %d appear exactly once corpus-wide." % (len(vocab), len(hapax)))
    print("  Once-only forms are where a typo hides. Listed for eyeball review:")
    for i in range(0, len(hapax), 12):
        print("    " + " ".join(hapax[i:i + 12]))
    print()

    if "--json" in sys.argv:
        out = sys.argv[sys.argv.index("--json") + 1]
        blob = {
            "strategies": scores,
            "per_story_margin": {k: sum(v) / len(v) for k, v in per_story_margin.items()},
            "per_story_max_margin": {k: max(abs(x) for x in v) for k, v in per_story_margin.items()},
            "per_story_strategy": {k: {kk: vv / 5.0 for kk, vv in v.items()}
                                   for k, v in per_story.items()},
            "structure": struct_bad,
            "reader_reuse": detail,
            "slot_findings": [(f[0], f[1], f[2]) for f in findings],
            "grounding_low": [(r[0], r[1], r[2], r[5]) for r in low],
            "typography": hits,
        }
        json.dump(blob, open(out, "w", encoding="utf-8"), indent=1)
        print("wrote %s" % out)
    return 0


if __name__ == "__main__":
    sys.exit(main())

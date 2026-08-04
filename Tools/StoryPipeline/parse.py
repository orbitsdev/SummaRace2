"""Parse the researcher's stories docx into structured per-story records.

Word glues headings to body text both across runs and inside a single run, so
we split on run boundaries first and then again on known markers, producing a
token stream we walk with a small state machine. Content tokens accumulate into
whichever field is open, which makes fragmented passages reassemble themselves.

Emits parsed.json (intermediate, for review) and prints an anomaly report.
"""
import json
import re
import sys
import io

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

RUN_SEP = "\x01"
DIFFS = ("EASY", "AVERAGE", "HARD")
SLOTS = ("SOMEBODY", "WANTED", "BUT", "SO", "THEN")
DASH = r"[–—\-:]"

# Order matters: longer / more specific patterns first.
MARKERS = [
    ("DAY",        re.compile(r"^DAY\s*(\d+)\s*$", re.I)),
    ("DIFF",       re.compile(r"^(EASY|AVERAGE|HARD)\b", re.I)),
    ("TITLE",      re.compile(r"^Title\s*:\s*(.*)$", re.I)),
    ("PASSAGE",    re.compile(r"^(?:Original|Shortened)\s+Passage\s*:?\s*(.*)$", re.I)),
    ("SWBST_HEAD", re.compile(r"^SWBST\s+Analysis\s*:?\s*(.*)$", re.I)),
    ("SWBST_LINE", re.compile(r"^(Somebody|Wanted|But|So|Then)\s*" + DASH + r"\s*(.*)$", re.I)),
    ("PAGES_HEAD", re.compile(r"^5\s*PAGES\s*:?\s*(.*)$", re.I)),
    # the page number is occasionally missing ("Page" alone) -> infer from order
    ("PAGE",       re.compile(r"^Page\s*(\d*)\s*:?\s*(.*)$", re.I)),
    ("QUESTION",   re.compile(r"^Processing\s+Question\s*:?\s*(.*)$", re.I)),
    ("QTEXT",      re.compile(r"^\U0001f449\s*(.*)$")),
    ("OPTION",     re.compile(r"^([ABC])\s*[.)]\s*(.*)$")),
    ("MAINIDEA",   re.compile(r"^Main\s+Idea\s*:?\s*(.*)$", re.I)),
]

# Same lookaheads, used to break markers glued inside one run.
SPLIT_RE = re.compile(
    r"(?=(?:EASY|AVERAGE|HARD)Title\s*:)"
    r"|(?=Title\s*:)"
    r"|(?=(?:Original|Shortened)\s+Passage)"
    r"|(?=SWBST\s+Analysis)"
    r"|(?=(?:Somebody|Wanted|But|So|Then)\s*[–—]\s*)"
    r"|(?=5\s*PAGES\s*:)"
    r"|(?=Page\s*\d)"
    r"|(?=Processing\s+Question)"
    r"|(?=\U0001f449)"
    r"|(?=\b[ABC]\.\s)"
    # options are sometimes glued straight onto the previous one ("A. MomB. Amelia"),
    # leaving no word boundary before the letter
    r"|(?<=[a-z])(?=[ABC]\.\s)"
    r"|(?=Main\s+Idea)",
    re.I,
)

# Page bodies are often labelled "Text:" / "Text".
TEXT_LABEL = re.compile(r"^Text\s*:?\s*", re.I)

CHECK = re.compile(r"[✅✔]|\(correct\)", re.I)


def tokenize():
    raw = open("runs.txt", encoding="utf-8").read()
    tokens = []
    for line in raw.split("\n"):
        for run in line.split(RUN_SEP):
            for piece in SPLIT_RE.split(run):
                piece = piece.strip()
                if not piece:
                    continue
                # "EASYTitle: X" -> "EASY" + "Title: X"
                m = re.match(r"^(EASY|AVERAGE|HARD)(Title\s*:.*)$", piece, re.I)
                if m:
                    tokens.append(m.group(1))
                    tokens.append(m.group(2).strip())
                    continue
                tokens.append(piece)
    return merge_day_headers(tokens)


def merge_day_headers(tokens):
    """"DAY" and its number often land in separate runs ("DAY" then "6")."""
    out = []
    skip = False
    for i, t in enumerate(tokens):
        if skip:
            skip = False
            continue
        if re.fullmatch(r"DAY", t, re.I) and i + 1 < len(tokens) \
                and re.fullmatch(r"\d{1,2}", tokens[i + 1].strip()):
            out.append("DAY " + tokens[i + 1].strip())
            skip = True
            continue
        out.append(t)
    return out


def classify(tok):
    for kind, rx in MARKERS:
        m = rx.match(tok)
        if m:
            return kind, m
    return "TEXT", None


def clean(s):
    s = CHECK.sub("", s)
    s = re.sub(r"\s+", " ", s)
    s = s.replace("’", "'").replace("‘", "'")
    s = s.replace("“", '"').replace("”", '"')
    return s.strip()


def parse():
    tokens = tokenize()
    stories = []
    cur = None
    day = None
    field = None          # which text field open tokens append to
    page = None           # page dict currently being filled
    awaiting_qtext = False

    def flush():
        if cur is not None:
            stories.append(cur)

    for tok in tokens:
        kind, m = classify(tok)

        if kind == "DAY":
            day = int(m.group(1))
            continue

        if kind == "DIFF" and re.match(r"^(EASY|AVERAGE|HARD)\s*$", tok, re.I):
            flush()
            cur = {
                "day": day,
                "difficulty": tok.strip().lower(),
                "title": "",
                "passage": "",
                "swbst": {},
                "pages": [],
                "mainIdea": "",
            }
            field, page, awaiting_qtext = None, None, False
            continue

        if cur is None:
            continue

        if kind == "TITLE":
            cur["title"] = clean(m.group(1))
            field = "title"
            continue

        if kind == "PASSAGE":
            cur["passage"] = clean(m.group(1))
            field = "passage"
            continue

        if kind == "SWBST_HEAD":
            field = None
            rest = clean(m.group(1))
            if rest:
                sub = SPLIT_RE.split(rest)
                for s in sub:
                    mm = MARKERS[5][1].match(s.strip())
                    if mm:
                        cur["swbst"][mm.group(1).upper()] = clean(mm.group(2))
            continue

        if kind == "SWBST_LINE":
            slot = m.group(1).upper()
            cur["swbst"][slot] = clean(m.group(2))
            field = ("swbst", slot)
            continue

        if kind == "PAGES_HEAD":
            field = None
            continue

        if kind == "PAGE":
            n = int(m.group(1)) if m.group(1) else len(cur["pages"]) + 1
            page = {"n": n, "text": clean(TEXT_LABEL.sub("", m.group(2))),
                    "question": "", "options": [], "correct": -1}
            cur["pages"].append(page)
            field = "page"
            awaiting_qtext = False
            continue

        if kind == "QUESTION":
            field = None
            awaiting_qtext = True
            rest = clean(m.group(1))
            if rest and page is not None:
                # question text sometimes trails the header on the same run
                rest = re.sub(r"^\U0001f449\s*", "", rest)
                page["question"] = rest
                awaiting_qtext = False
                field = "question"
            continue

        if kind == "QTEXT":
            if page is not None:
                page["question"] = clean(m.group(1))
                field = "question"
                awaiting_qtext = False
            continue

        if kind == "OPTION":
            if page is not None:
                text = clean(m.group(2))
                if CHECK.search(tok):
                    page["correct"] = len(page["options"])
                page["options"].append(text)
                field = "option"
            continue

        if kind == "MAINIDEA":
            cur["mainIdea"] = clean(m.group(1))
            field = "mainidea"
            page = None
            continue

        # ---- plain text: append to whatever is open ----
        text = clean(tok)
        if not text:
            continue
        if awaiting_qtext and page is not None:
            page["question"] = text
            awaiting_qtext = False
            field = "question"
        elif field == "passage":
            cur["passage"] = (cur["passage"] + " " + text).strip()
        elif field == "title" and not cur["title"]:
            cur["title"] = text
        elif isinstance(field, tuple) and field[0] == "swbst":
            cur["swbst"][field[1]] = (cur["swbst"][field[1]] + " " + text).strip()
        elif field == "page" and page is not None:
            if re.fullmatch(r"Text\s*:?", text, re.I):
                continue          # bare "Text" label on its own run
            if not page["text"]:
                text = TEXT_LABEL.sub("", text)
            page["text"] = (page["text"] + " " + text).strip()
        elif field == "question" and page is not None:
            page["question"] = (page["question"] + " " + text).strip()
        elif field == "option" and page is not None and page["options"]:
            if CHECK.search(tok):
                page["correct"] = len(page["options"]) - 1
            page["options"][-1] = (page["options"][-1] + " " + text).strip()
        elif field == "mainidea":
            cur["mainIdea"] = (cur["mainIdea"] + " " + text).strip()

    flush()
    assign_days(stories)
    return stories


def assign_days(stories):
    """Day comes from the document's own DAY headings; blocks also run strictly
    easy->average->hard three per day, so check the two agree."""
    expected = ["easy", "average", "hard"]
    for i, s in enumerate(stories):
        ordinal_day = i // 3 + 1
        if s["day"] != ordinal_day:
            print("  ?? order: block %d has DAY %s, ordinal says %d"
                  % (i, s["day"], ordinal_day))
        if s["difficulty"] != expected[i % 3]:
            print("  ?? order: block %d is %s, expected %s"
                  % (i, s["difficulty"], expected[i % 3]))


def report(stories):
    print("parsed stories:", len(stories))
    print()
    bad = 0
    for s in stories:
        problems = []
        if not s["title"]:
            problems.append("no title")
        if not s["passage"]:
            problems.append("no passage")
        missing = [k for k in SLOTS if k not in s["swbst"] or not s["swbst"][k]]
        if missing:
            problems.append("swbst missing " + ",".join(missing))
        if len(s["pages"]) != 5:
            problems.append("pages=%d" % len(s["pages"]))
        for p in s["pages"]:
            if len(p["options"]) != 3:
                problems.append("p%d opts=%d" % (p["n"], len(p["options"])))
            if p["correct"] < 0:
                problems.append("p%d no check" % p["n"])
            if not p["question"]:
                problems.append("p%d no question" % p["n"])
            if not p["text"]:
                problems.append("p%d no text" % p["n"])
        if not s["mainIdea"]:
            problems.append("no mainIdea")

        tag = "day%-2d %-8s" % (s["day"] or 0, s["difficulty"])
        if problems:
            bad += 1
            print("  !! %s  %s" % (tag, "; ".join(problems)))
        else:
            print("  ok %s  %s" % (tag, s["title"][:48]))
    print()
    print("clean: %d / %d" % (len(stories) - bad, len(stories)))


if __name__ == "__main__":
    st = parse()
    json.dump(st, open("parsed.json", "w", encoding="utf-8"),
              ensure_ascii=False, indent=1)
    report(st)

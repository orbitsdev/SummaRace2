"""Re-extract the stories docx preserving run boundaries.

Word splits a single visual line into multiple <w:r> runs (heading + body often
share one paragraph), so paragraph-only extraction glues "EASY" to "Title:" to
the passage. Emitting a marker between runs lets the parser split reliably.
"""
import re
import sys
import io

sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding="utf-8", errors="replace")

SRC = "stories_x/word/document.xml"
RUN_SEP = "\x01"

ENT = {"&amp;": "&", "&lt;": "<", "&gt;": ">", "&quot;": '"', "&apos;": "'"}


def unescape(s):
    for k, v in ENT.items():
        s = s.replace(k, v)
    return s


def main():
    xml = open(SRC, encoding="utf-8").read()
    paras = re.findall(r"<w:p[ >].*?</w:p>", xml, re.S)

    lines = []
    for p in paras:
        runs = re.findall(r"<w:r[ >].*?</w:r>", p, re.S)
        parts = []
        for r in runs:
            t = "".join(re.findall(r"<w:t[^>]*>(.*?)</w:t>", r, re.S))
            t = unescape(t)
            if t.strip():
                parts.append(t.strip())
        if parts:
            lines.append(RUN_SEP.join(parts))

    out = "\n".join(lines)
    open("runs.txt", "w", encoding="utf-8").write(out)
    print("paragraphs:", len(paras), "non-empty lines:", len(lines))

    # Structural survey: where does each DAY / difficulty block start?
    marks = []
    for i, ln in enumerate(lines):
        flat = ln.replace(RUN_SEP, "")
        if re.match(r"^DAY\s*\d+\s*$", flat, re.I):
            marks.append((i, "DAY", flat))
        for d in ("EASY", "AVERAGE", "HARD"):
            # difficulty appears as its own run at the start of a block
            if ln.split(RUN_SEP)[0].strip().upper() == d:
                marks.append((i, "DIFF", d))
    print("\n=== block markers (%d) ===" % len(marks))
    for i, kind, val in marks:
        print(f"{i:5d}  {kind:5s}  {val}")


main()

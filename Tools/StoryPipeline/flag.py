"""Flag element sets whose distractors need an editorial pass.

A research instrument must not let the correct answer be identifiable by style
rather than comprehension, so we look for tells: infinitive mismatch, subject
mismatch, and length disparity. Race cards also have to stay short enough to
read at speed.
"""
import json
import glob
import os
import re
import sys

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

STORIES = r"C:\Users\User\Documents\2026\GAME\SummaRace2\Assets\_Game\Resources\Stories"
# s01_* are excluded from GENERATION (emit.py) because they are the hand-checked reference,
# but they are NOT exempt from the gate. They used to be, and that is exactly how the worst
# length tell in the corpus stayed invisible: when the character-based rule was finally
# applied, all 8 sets corpus-wide where `correct` beat its widest distractor by more than 6
# characters were s01 -- the one session every learner plays first. A gate with a hole in it
# reports the hole as clean.
GOLD = set()

# Bare subject pronouns only. Possessive determiners ("their house", "his
# classmates") head a noun phrase and pattern with names, not with pronouns.
PRONOUNS = {"he", "she", "they", "it"}
MAX_CARD_WORDS = 12

# Race answer cards are a fixed 1.55 x 0.85 world units with font autosize capped
# at 2.4 (F30), and the learner reads them while running. The only stories ever
# playtested are s01_*, whose longest option is 53 characters, so 52 is "no wider
# than a card we know is readable". Every option is a card, correct or not.
MAX_CARD_CHARS = 52

# How much longer (in characters) `correct` may be than its longest distractor before
# the card itself gives the answer away, and how far it may sit from the distractor
# mean. Both are deliberately tight: at the study's three-card gate, a consistent
# width difference is a free 50 percentage points.
LONGEST_MARGIN = 6
MEAN_SPREAD = 10


def words(s):
    return len(s.split())


def first(s):
    m = re.match(r"[A-Za-z']+", s)
    return m.group(0).lower() if m else ""


def flags(correct, distractors):
    out = []
    c_to = correct.lower().startswith("to ")
    d_to = [d.lower().startswith("to ") for d in distractors]
    if c_to != all(d_to):
        out.append("infinitive-mismatch")

    c_pron = first(correct) in PRONOUNS
    d_pron = [first(d) in PRONOUNS for d in distractors]
    if any(p != c_pron for p in d_pron):
        out.append("subject-mismatch")

    # LENGTH IS THE TELL THAT MATTERS, and it must be measured in characters on the
    # card, not in words. The old rule was words with a +/-5 tolerance, which fired on
    # 1 of 150 sets and reported the corpus clean -- while in fact `correct` was the
    # strictly longest of the three cards in 121 of 150 (80.7%), mean +10.8 characters.
    # A learner who never reads and simply takes the widest card scored ~85% of race
    # gates against 33% for guessing, which means the race could not distinguish
    # comprehension from card-width perception at all. Gate on the shape of the cards.
    cl = len(correct)
    dl = [len(d) for d in distractors]
    mean_d = sum(dl) / float(len(dl))
    if cl > max(dl) + LONGEST_MARGIN:
        out.append("length-tell:longest(%d vs %s)" % (cl, dl))
    elif cl < min(dl) - LONGEST_MARGIN:
        out.append("length-tell:shortest(%d vs %s)" % (cl, dl))
    if abs(cl - mean_d) > MEAN_SPREAD:
        out.append("length-spread(%d vs mean %.0f)" % (cl, mean_d))

    cw = words(correct)
    if cw > MAX_CARD_WORDS:
        out.append("too-long-for-card(%d)" % cw)

    over = [len(s) for s in [correct] + list(distractors) if len(s) > MAX_CARD_CHARS]
    if over:
        out.append("over-card-budget(%s > %d)" % (", ".join(str(n) for n in over), MAX_CARD_CHARS))
    return out


def main():
    total = 0
    flagged = 0
    for path in sorted(glob.glob(os.path.join(STORIES, "*.json"))):
        sid = os.path.splitext(os.path.basename(path))[0]
        if sid in GOLD:
            continue
        d = json.load(open(path, encoding="utf-8"))
        lines = []
        for i, el in enumerate(d["elements"]):
            total += 1
            f = flags(el["correct"], el["distractors"])
            if not f:
                continue
            flagged += 1
            q = d["pages"][i]["question"]["text"]
            lines.append("  %-8s [%s]" % (el["type"], ", ".join(f)))
            lines.append("     Q: %s" % q)
            lines.append("     correct : %s" % el["correct"])
            lines.append("     distr   : %s | %s" % tuple(el["distractors"]))
        if lines:
            print("=== %s  %s" % (sid, d["title"]))
            print("\n".join(lines))
    print()
    print("element sets: %d   flagged: %d   clean: %d" % (total, flagged, total - flagged))


if __name__ == "__main__":
    main()

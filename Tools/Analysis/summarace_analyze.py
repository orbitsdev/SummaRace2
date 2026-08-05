#!/usr/bin/env python3
"""
SummaRace study-data analyser.

Turns a folder of exported tablet logs (`export_*.jsonl` + `export_*_learners.json`)
into the CSV tables a thesis chapter needs.

Standard library only. No pip install. Python 3.8+.

    python summarace_analyze.py --in <folder of exports> --out <folder for tables>

Read Tools/Analysis/README.md first. It is written for the researcher, not the coder.

DESIGN RULES (do not relax these without thinking about what they protect)
  1. INPUT IS NEVER TOUCHED. Every input file is opened read-only. The output folder
     must be a different folder; the script refuses to run otherwise.
  2. NOTHING IS DROPPED SILENTLY. A line that will not parse stops the run with the
     file name, the line number and the reason. Rows removed by deduplication are
     counted and reported. Every exclusion appears in the printed report.
  3. THE REPORT SAYS WHAT IT READ. Files, lines, learners, runs, kept, skipped, why.

Schema: written against SessionLog schema version 3
(Assets/_Game/Scripts/Data/SaveModels.cs, written by Core/SessionLogService.cs).
"""

import argparse
import csv
import json
import math
import os
import statistics
import sys
from collections import defaultdict, OrderedDict

SCRIPT_SCHEMA = 3

# ---------------------------------------------------------------------------
# The five SWBST slots, in the fixed order the app writes them.
# Race lists (raceFirstPickCorrect / raceFirstOutcome / raceWrongPicks) are index
# 0..4 in this order. Reading answers are placed by readingPageIndices, and the
# story files use page i for slot i (a CONTENT convention -- see --stories).
# ---------------------------------------------------------------------------
SLOTS = [
    ("e1", "SOMEBODY"),
    ("e2", "WANTED"),
    ("e3", "BUT"),
    ("e4", "SO"),
    ("e5", "THEN"),
]
SLOT_KEYS = [k for k, _ in SLOTS]
SLOT_NAMES = [n for _, n in SLOTS]

DIFFICULTIES = ["easy", "average", "hard"]

OUT_FILES = OrderedDict([
    ("runs", "01_runs.csv"),
    ("learner_session", "02_learner_session.csv"),
    ("contrast", "03_swbst_contrast.csv"),
    ("growth", "04_growth_by_session.csv"),
    ("learners", "05_learner_summary.csv"),
    ("picks", "06_race_picks.csv"),
    ("summaries", "07_summaries.csv"),
    ("quality", "08_data_quality.csv"),
    ("alignment", "09_story_alignment_audit.csv"),
    ("distractors", "10_distractor_frequency.csv"),
    ("bad", "00_bad_rows.csv"),
    ("report", "00_report.txt"),
])

# ---------------------------------------------------------------------------
# WHICH SCHEMA CARRIES WHICH FIELD.
#
# Rows are only ever appended to; a build in the field that was never re-flashed
# keeps writing its own schema. So a field can be ABSENT, and absent is NOT zero:
# reporting "0 pauses" for a build that never counted pauses would be a fabricated
# number in a thesis. Everywhere below, a field the row does not carry is written
# as an EMPTY CELL (Excel/SPSS read that as missing), never as 0/false, and a
# companion *_captured column says so explicitly.
# ---------------------------------------------------------------------------
SCHEMA2_FIELDS = [
    "schemaVersion", "appVersion", "deviceId", "deviceModel", "rowWrittenIso",
    "session", "difficulty", "narrationOn", "lastPhase", "readingPageIndices",
    "readingSeconds", "raceSeconds", "arrangeSeconds", "summarySeconds",
    "raceRunSeconds", "raceWrongPicks", "raceFirstOutcome", "arrangeSolved",
]
SCHEMA3_FIELDS = ["racePicks", "racePauseCount", "racePausedSeconds", "abandonReason"]

# Names that look like a tester rather than a Grade-4 learner.
TEST_NAME_HINTS = [
    "test", "tester", "asdf", "qwer", "zxcv", "aaa", "bbb", "xxx", "demo",
    "sample", "dummy", "teacher", "admin", "me", "abc", "123", "player",
    "unnamed", "learner",
]


# ===========================================================================
# small helpers
# ===========================================================================

def die(message, code=2):
    sys.stderr.write("\n" + "!" * 72 + "\n")
    sys.stderr.write("STOPPED: " + message.rstrip() + "\n")
    sys.stderr.write("!" * 72 + "\n\n")
    sys.exit(code)


def as_bool(value):
    if isinstance(value, bool):
        return value
    if isinstance(value, (int, float)):
        return value != 0
    if isinstance(value, str):
        return value.strip().lower() in ("true", "1", "yes")
    return False


def as_int(value, default=0):
    try:
        if value is None or value == "":
            return default
        return int(value)
    except (TypeError, ValueError):
        return default


def as_float(value, default=0.0):
    try:
        if value is None or value == "":
            return default
        return float(value)
    except (TypeError, ValueError):
        return default


def as_list(value):
    return value if isinstance(value, list) else []


def numeric_only(values):
    """Drops blanks. A blank means the build never recorded that field, so it must
    be EXCLUDED from the average -- never coerced to 0, which would silently drag a
    mean towards zero and put an invented number in the thesis."""
    kept = []
    for value in values:
        if value is None or value == "":
            continue
        try:
            kept.append(float(value))
        except (TypeError, ValueError):
            continue
    return kept


def mean_or_blank(values):
    values = numeric_only(values)
    if not values:
        return ""
    return round(statistics.fmean(values), 4)


def sd_or_blank(values):
    values = numeric_only(values)
    if len(values) < 2:
        return ""
    return round(statistics.stdev(values), 4)


def sum_or_blank(values, digits=2):
    values = numeric_only(values)
    if not values:
        return ""
    return round(sum(values), digits)


def minutes(seconds_values):
    total = sum_or_blank(seconds_values)
    return "" if total == "" else round(total / 60.0, 2)


def pct(numerator, denominator):
    if not denominator:
        return ""
    return round(100.0 * numerator / denominator, 1)


def iso_sort_key(text):
    """ISO-8601 UTC strings sort correctly as plain strings; blanks sort last."""
    return text if text else "￿"


# ---------------------------------------------------------------------------
# Paired t-test, standard library only.
# Regularised incomplete beta by continued fraction (Lentz), the usual recipe.
# Used ONLY for the Reader-vs-Race manipulation check; everything else is
# descriptive. Report it as such.
# ---------------------------------------------------------------------------

def _betacf(a, b, x):
    tiny = 1e-30
    qab, qap, qam = a + b, a + 1.0, a - 1.0
    c = 1.0
    d = 1.0 - qab * x / qap
    if abs(d) < tiny:
        d = tiny
    d = 1.0 / d
    h = d
    for m in range(1, 300):
        m2 = 2 * m
        aa = m * (b - m) * x / ((qam + m2) * (a + m2))
        d = 1.0 + aa * d
        if abs(d) < tiny:
            d = tiny
        c = 1.0 + aa / c
        if abs(c) < tiny:
            c = tiny
        d = 1.0 / d
        h *= d * c
        aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2))
        d = 1.0 + aa * d
        if abs(d) < tiny:
            d = tiny
        c = 1.0 + aa / c
        if abs(c) < tiny:
            c = tiny
        d = 1.0 / d
        delta = d * c
        h *= delta
        if abs(delta - 1.0) < 3e-12:
            break
    return h


def betainc(a, b, x):
    if x <= 0.0:
        return 0.0
    if x >= 1.0:
        return 1.0
    front = math.exp(
        math.lgamma(a + b) - math.lgamma(a) - math.lgamma(b)
        + a * math.log(x) + b * math.log(1.0 - x)
    )
    if x < (a + 1.0) / (a + b + 2.0):
        return front * _betacf(a, b, x) / a
    return 1.0 - math.exp(
        math.lgamma(a + b) - math.lgamma(a) - math.lgamma(b)
        + b * math.log(1.0 - x) + a * math.log(x)
    ) * _betacf(b, a, 1.0 - x) / b


def paired_t(pairs):
    """pairs = [(before, after), ...]. Returns dict or None if too few pairs."""
    diffs = [a - b for a, b in pairs]
    n = len(diffs)
    if n < 2:
        return None
    m = statistics.fmean(diffs)
    sd = statistics.stdev(diffs)
    if sd == 0:
        return {"n": n, "mean_diff": round(m, 4), "sd_diff": 0.0,
                "t": "", "df": n - 1, "p_two_tailed": "", "cohen_dz": ""}
    t = m / (sd / math.sqrt(n))
    df = n - 1
    p = betainc(df / 2.0, 0.5, df / (df + t * t))
    return {
        "n": n,
        "mean_diff": round(m, 4),
        "sd_diff": round(sd, 4),
        "t": round(t, 4),
        "df": df,
        "p_two_tailed": ("%.6f" % p),
        "cohen_dz": round(m / sd, 4),
    }


# ===========================================================================
# reading the export folder
# ===========================================================================

class BadRow(Exception):
    pass


def read_jsonl(path, problems):
    """Yields (line_number, row_dict). Records unparseable lines in `problems`."""
    rows = []
    with open(path, "r", encoding="utf-8-sig", errors="replace") as handle:
        lines = handle.readlines()
    total = len(lines)
    for index, raw in enumerate(lines, start=1):
        text = raw.strip()
        if not text:
            continue
        try:
            row = json.loads(text)
        except json.JSONDecodeError as error:
            hint = ""
            if index == total:
                hint = (" -- this is the LAST line of the file, so it is most likely a "
                        "half-written row from a tablet that was switched off mid-save. "
                        "Re-run with --skip-bad-rows to continue without it; you lose at "
                        "most one play-through.")
            problems.append({
                "file": os.path.basename(path),
                "line": index,
                "reason": "line is not valid JSON: %s%s" % (error.msg, hint),
                "text": text[:400],
            })
            continue
        if not isinstance(row, dict):
            problems.append({
                "file": os.path.basename(path), "line": index,
                "reason": "line parsed but is not a JSON object (a row must be {...})",
                "text": text[:400],
            })
            continue
        row["_srcFile"] = os.path.basename(path)
        row["_srcLine"] = index
        rows.append(row)
    return rows


def read_roster(path, problems):
    entries = []
    try:
        with open(path, "r", encoding="utf-8-sig", errors="replace") as handle:
            data = json.load(handle)
    except (json.JSONDecodeError, OSError) as error:
        problems.append({
            "file": os.path.basename(path), "line": 0,
            "reason": "roster file could not be read: %s" % error, "text": "",
        })
        return entries
    for entry in as_list(data.get("learners")):
        if not isinstance(entry, dict):
            continue
        entries.append({
            "learnerId": str(entry.get("learnerId", "")),
            # The researcher's own id from the child's paper booklet (e.g. "P07"). THIS is the
            # column the results chapter joins on: learnerId is a device guid that appears
            # nowhere on paper, and displayName was typed by a nine-year-old. Absent on rosters
            # written before schema 4.
            "participantCode": str(entry.get("participantCode", "")),
            "displayName": str(entry.get("displayName", "")),
            "unlockedSession": as_int(entry.get("unlockedSession")),
            "storiesCompleted": as_int(entry.get("storiesCompleted")),
            "rosterFile": os.path.basename(path),
        })
    return entries


def collect_inputs(folder):
    """Every .jsonl is data; every *_learners.json is a roster."""
    if not os.path.isdir(folder):
        die("the input folder does not exist:\n    %s\n\n"
            "Point --in at the folder where you copied the tablet exports." % folder)
    data_files, roster_files, ignored = [], [], []
    for name in sorted(os.listdir(folder)):
        path = os.path.join(folder, name)
        if not os.path.isfile(path):
            continue
        lower = name.lower()
        if lower.endswith(".jsonl"):
            data_files.append(path)
        elif lower.endswith("_learners.json"):
            roster_files.append(path)
        elif lower.endswith(".json"):
            # Could be a roster named differently. Peek before ignoring it.
            try:
                with open(path, "r", encoding="utf-8-sig", errors="replace") as handle:
                    head = handle.read(400)
                if '"learners"' in head:
                    roster_files.append(path)
                else:
                    ignored.append(name)
            except OSError:
                ignored.append(name)
        else:
            ignored.append(name)
    return data_files, roster_files, ignored


# ===========================================================================
# validation of one row
# ===========================================================================

REQUIRED_KEYS = ["runId", "learnerId", "storyId", "startedIso"]


def validate_row(row):
    """Raises BadRow with a plain-English reason. Structural faults only --
    'this row cannot be analysed', not 'this row is odd'. Odd goes to quality."""
    for key in REQUIRED_KEYS:
        if key not in row:
            raise BadRow("row has no '%s' field, so it cannot be identified. "
                         "Is this really a SummaRace export?" % key)
    if not str(row.get("runId", "")).strip():
        raise BadRow("runId is empty -- the row cannot be de-duplicated against "
                     "its own safety snapshots, so it is unsafe to analyse.")
    for key in ("readingFirstCorrect", "readingFirstChoices", "readingPageIndices",
                "raceFirstPickCorrect", "raceFirstOutcome", "raceWrongPicks"):
        if key in row and not isinstance(row[key], list):
            raise BadRow("'%s' should be a list but is %s."
                         % (key, type(row[key]).__name__))
    # The reading lists are positional -- element k of each describes the same
    # answer -- so unequal lengths mean the answers cannot be matched to a page.
    # Only compare the lists the row actually CARRIES: readingPageIndices arrived
    # with schema 2, and a schema-1 row legitimately has the other two only.
    reading_lists = {k: len(as_list(row.get(k)))
                     for k in ("readingPageIndices", "readingFirstChoices",
                               "readingFirstCorrect")
                     if k in row and row[k] is not None}
    if len(set(reading_lists.values())) > 1:
        raise BadRow(
            "the reading lists have different lengths (%s). They are positional -- "
            "element k of each describes the same answer -- so unequal lengths mean "
            "the answers cannot be matched to their pages."
            % ", ".join("%s=%d" % item for item in sorted(reading_lists.items())))


# ===========================================================================
# deduplication
# ===========================================================================

def dedupe(rows, quality):
    """One row per runId.

    RULE (SessionLog.runId, SessionLogService.WriteRow): keep the row with
    isPartial == false if one exists, otherwise the row with the latest
    rowWrittenIso. The app writes a mid-run snapshot whenever it is backgrounded,
    so one play-through legitimately appears as several lines -- and if the same
    tablet was exported twice (say at session 5 and again at session 10) the
    second export repeats every row of the first. Both are handled here.
    """
    by_run = defaultdict(list)
    for row in rows:
        by_run[str(row["runId"])].append(row)

    kept, dropped = [], 0
    overlap = defaultdict(int)          # file-pair -> how many runs they share
    for run_id, group in by_run.items():
        finals = [r for r in group if not as_bool(r.get("isPartial"))]
        pool = finals if finals else group
        pool = sorted(pool, key=lambda r: iso_sort_key(str(r.get("rowWrittenIso", ""))))
        winner = pool[-1]
        winner["_rowsForThisRun"] = len(group)
        winner["_partialSnapshots"] = sum(1 for r in group if as_bool(r.get("isPartial")))
        kept.append(winner)
        dropped += len(group) - 1

        if len(finals) > 1:
            # Two "final" rows for one run should be impossible: SessionLogService
            # nulls the log after writing. In practice it means the same tablet was
            # exported twice and BOTH files were copied in -- harmless, and the whole
            # point of de-duplicating by runId. Two final rows that actually DIFFER is
            # a different matter and is reported one by one.
            signatures = {json.dumps(
                {k: v for k, v in r.items() if not k.startswith("_")},
                sort_keys=True) for r in finals}
            files = tuple(sorted({str(r.get("_srcFile")) for r in finals}))
            if len(signatures) == 1:
                overlap[files] += 1
            else:
                quality.append({
                    "severity": "SERIOUS",
                    "kind": "two DIFFERENT final rows for the same runId",
                    "who": str(winner.get("learnerId", "")),
                    "what": run_id,
                    "detail": "%d final rows, %d of them distinct, from: %s. The later "
                              "rowWrittenIso was kept. This should be impossible -- check "
                              "whether a file was edited by hand."
                              % (len(finals), len(signatures), ", ".join(files)),
                })

    for files, count in sorted(overlap.items(), key=lambda kv: -kv[1]):
        quality.append({
            "severity": "note",
            "kind": "the same tablet was exported more than once",
            "who": "",
            "what": " + ".join(files),
            "detail": "%d play-through(s) appear identically in these files, so an earlier "
                      "export is contained in a later one. De-duplicated by runId; nothing "
                      "was counted twice and nothing was lost." % count,
        })
    kept.sort(key=lambda r: (str(r.get("learnerId", "")),
                             iso_sort_key(str(r.get("startedIso", "")))))
    return kept, dropped


# ===========================================================================
# derived row
# ===========================================================================

def derive(row, options):
    """Flattens one deduplicated run into the wide analysis row.

    A field the row does not carry becomes an EMPTY CELL, never 0. See the
    SCHEMA2_FIELDS / SCHEMA3_FIELDS note above -- this is the difference between
    "not measured by that build" and "measured, and the answer was zero", and
    getting it wrong would put invented numbers in the thesis.
    """
    out = OrderedDict()

    def present(key):
        return key in row and row[key] is not None

    def num(key, cast=as_float, digits=2):
        """Value if the row carries the field, else blank."""
        if not present(key):
            return ""
        value = cast(row[key])
        return round(value, digits) if cast is as_float else value

    learner_id = str(row.get("learnerId", ""))
    story_id = str(row.get("storyId", ""))

    out["learnerId"] = learner_id
    # Schema 4 stamps the participant code on every row, so the export joins to the paper
    # pretest/posttest even if the companion roster file was never pulled off the tablet. Taken
    # from the row when present; otherwise filled from the roster below, exactly like the name.
    out["participantCode"] = str(row.get("participantCode", ""))
    out["displayName"] = ""            # filled from the roster later
    out["runId"] = str(row.get("runId", ""))
    out["storyId"] = story_id
    out["session"] = as_int(row.get("session")) or session_from_story_id(story_id)
    out["difficulty"] = str(row.get("difficulty", "")) or difficulty_from_story_id(story_id)
    out["isReplay"] = int(as_bool(row.get("isReplay")))
    out["attemptNo"] = ""              # filled after sorting per learner+story

    finished = str(row.get("finishedIso", ""))
    out["completed"] = int(bool(finished))
    out["abandoned"] = int(not finished)
    out["abandonReason"] = str(row.get("abandonReason", "")) if present("abandonReason") else ""
    out["lastPhase"] = str(row.get("lastPhase", "")) if present("lastPhase") else ""

    out["startedIso"] = str(row.get("startedIso", ""))
    out["finishedIso"] = finished
    out["rowWrittenIso"] = str(row.get("rowWrittenIso", ""))
    out["startedDate"] = out["startedIso"][:10]

    # ---- reading (support present) ----
    pages = [as_int(v, -1) for v in as_list(row.get("readingPageIndices"))]
    choices = [as_int(v, -1) for v in as_list(row.get("readingFirstChoices"))]
    correct = [as_bool(v) for v in as_list(row.get("readingFirstCorrect"))]

    # readingPageIndices arrived with schema 2. Without it the answer lists are
    # still positional, so position k IS page k -- but say so rather than pretend
    # the field was there.
    out["read_pages_captured"] = int(present("readingPageIndices"))
    if not pages:
        pages = list(range(len(correct)))

    read_by_slot = {}
    choice_by_slot = {}
    for position, page in enumerate(pages):
        if 0 <= page < len(SLOT_KEYS):
            if position < len(correct):
                read_by_slot[page] = correct[position]
            if position < len(choices):
                choice_by_slot[page] = choices[position]

    out["read_answered"] = len(correct)
    out["read_n_correct"] = sum(1 for v in correct if v)
    out["read_acc"] = round(out["read_n_correct"] / len(correct), 4) if correct else ""
    out["narrationOn"] = int(as_bool(row.get("narrationOn"))) if present("narrationOn") else ""

    # ---- race (support removed) ----
    # raceFirstOutcome (schema 2) is the authoritative per-gate verdict, because it
    # separates "chose a distractor" (a comprehension error) from "never touched a
    # card" (an attention/motor event). raceFirstPickCorrect collapses both into
    # false, so it is only the fallback for a schema-1 row.
    has_outcomes = present("raceFirstOutcome")
    outcomes = [str(v) for v in as_list(row.get("raceFirstOutcome"))]
    outcomes += [""] * (5 - len(outcomes))
    first_correct = [as_bool(v) for v in as_list(row.get("raceFirstPickCorrect"))]
    out["race_outcome_captured"] = int(has_outcomes)

    if not has_outcomes and len(first_correct) == 5:
        # Degrade honestly: we know correct vs not-correct, and NOT which kind of
        # not-correct. Everything wrong/missed stays blank rather than guessing.
        outcomes = ["correct" if ok else "notcorrect" for ok in first_correct]

    has_wrong_picks = present("raceWrongPicks")
    wrong_picks = [as_int(v) for v in as_list(row.get("raceWrongPicks"))]
    wrong_picks += [0] * (5 - len(wrong_picks))

    n_correct = sum(1 for o in outcomes[:5] if o == "correct")
    n_wrong = sum(1 for o in outcomes[:5] if o == "wrong")
    n_missed = sum(1 for o in outcomes[:5] if o == "missed")
    n_reached = sum(1 for o in outcomes[:5] if o)

    out["race_gates_resolved"] = n_reached
    out["race_n_correct"] = n_correct
    out["race_n_wrong"] = n_wrong if has_outcomes else ""
    out["race_n_missed"] = n_missed if has_outcomes else ""
    out["race_acc"] = round(n_correct / 5.0, 4) if n_reached == 5 else ""
    # Comprehension-only accuracy: a gate the learner drove past says nothing about
    # whether they knew the answer, so it is excluded rather than counted wrong.
    out["race_acc_excl_missed"] = (round(n_correct / (n_correct + n_wrong), 4)
                                   if (has_outcomes and (n_correct + n_wrong)) else "")
    out["race_missed_rate"] = (round(n_missed / 5.0, 4)
                               if (has_outcomes and n_reached == 5) else "")
    out["race_total_wrong_picks"] = sum(wrong_picks[:5]) if has_wrong_picks else ""

    # THE contrast the study is about, per run.
    if out["read_acc"] != "" and out["race_acc"] != "":
        out["support_removal_drop"] = round(out["read_acc"] - out["race_acc"], 4)
    else:
        out["support_removal_drop"] = ""
    if out["read_acc"] != "" and out["race_acc_excl_missed"] != "":
        out["support_removal_drop_excl_missed"] = round(
            out["read_acc"] - out["race_acc_excl_missed"], 4)
    else:
        out["support_removal_drop_excl_missed"] = ""

    # ---- per-slot columns: the Reader/Race contrast element by element ----
    for index, (key, name) in enumerate(SLOTS):
        read_value = read_by_slot.get(index)
        out["read_%s_correct" % key] = "" if read_value is None else int(read_value)
        out["read_%s_choice" % key] = choice_by_slot.get(index, "")
        outcome = outcomes[index] if index < len(outcomes) else ""
        out["race_%s_outcome" % key] = outcome
        out["race_%s_correct" % key] = (
            1 if outcome == "correct"
            else (0 if outcome in ("wrong", "missed", "notcorrect") else ""))
        out["race_%s_wrongpicks" % key] = (
            wrong_picks[index] if (has_wrong_picks and index < len(wrong_picks)) else "")
        if read_value is None or outcome == "":
            out["drop_%s" % key] = ""
        else:
            out["drop_%s" % key] = int(read_value) - (1 if outcome == "correct" else 0)

    # ---- arrange ----
    # arrangeSolved arrived with schema 2. Without it, "solved unaided" is not
    # recoverable: a schema-1 row only says whether the assist fired.
    solved = as_bool(row.get("arrangeSolved"))
    assisted = as_bool(row.get("arrangeAssisted"))
    has_solved = present("arrangeSolved")
    out["arrangeAttempts"] = num("arrangeAttempts", as_int)
    out["arrangeSolved"] = int(solved) if has_solved else ""
    out["arrangeAssisted"] = int(assisted)
    out["arrange_unaided"] = int(solved and not assisted) if has_solved else ""
    out["arrange_first_try"] = (int(solved and not assisted
                                    and as_int(row.get("arrangeAttempts")) == 1)
                                if has_solved else "")

    # ---- summary ----
    text = row.get("summaryText") or ""
    if not isinstance(text, str):
        text = str(text)
    out["summary_words"] = len(text.split()) if text.strip() else 0
    out["summary_chars"] = len(text)
    out["summary_written"] = int(bool(text.strip()))
    out["nudgeCount"] = as_int(row.get("nudgeCount"))

    # ---- outcome + time ----
    out["starsEarned"] = as_int(row.get("starsEarned"))
    out["timesCaught"] = as_int(row.get("timesCaught"))

    total = as_float(row.get("totalSeconds"))
    out["totalSeconds"] = round(total, 2)
    out["readingSeconds"] = num("readingSeconds")
    out["raceSeconds"] = num("raceSeconds")
    out["raceRunSeconds"] = num("raceRunSeconds")
    out["arrangeSeconds"] = num("arrangeSeconds")
    out["summarySeconds"] = num("summarySeconds")
    out["phase_clocks_captured"] = int(present("readingSeconds"))

    # Pause accounting arrived with schema 3. A blank here means the build never
    # counted pauses -- it does NOT mean the race was never paused.
    has_pause = present("racePausedSeconds")
    paused = as_float(row.get("racePausedSeconds"))
    out["racePauseCount"] = num("racePauseCount", as_int)
    out["racePausedSeconds"] = num("racePausedSeconds")
    out["pause_captured"] = int(has_pause)
    out["raceSeconds_net"] = (round(max(0.0, as_float(row.get("raceSeconds")) - paused), 2)
                              if (present("raceSeconds") and has_pause) else "")
    out["totalMinutes"] = round(total / 60.0, 2)
    # Durations are REAL elapsed time with no idle detection: a tablet put down
    # mid-story counts that waiting as effort. Flag, never silently trim.
    out["time_outlier"] = int(total > options.time_cap)

    # ---- provenance ----
    out["schemaVersion"] = as_int(row.get("schemaVersion"))
    out["appVersion"] = str(row.get("appVersion", ""))
    out["deviceId"] = str(row.get("deviceId", ""))
    out["deviceModel"] = str(row.get("deviceModel", ""))
    out["isPartialRowKept"] = int(as_bool(row.get("isPartial")))
    out["rowsForThisRun"] = as_int(row.get("_rowsForThisRun"), 1)
    out["srcFile"] = str(row.get("_srcFile", ""))
    out["srcLine"] = as_int(row.get("_srcLine"))

    # racePicks (schema 3) is the misconception data. Blank captured flag = the
    # build never recorded which card was taken; 0 picks with the flag set = the
    # learner genuinely touched nothing.
    has_picks = present("racePicks")
    out["picks_captured"] = int(has_picks)
    out["n_race_picks"] = len(as_list(row.get("racePicks"))) if has_picks else ""

    out["_racePicks"] = as_list(row.get("racePicks")) if has_picks else []
    out["_picksCaptured"] = has_picks
    out["_summaryText"] = text
    return out


def session_from_story_id(story_id):
    try:
        return int(story_id[1:3])
    except (ValueError, IndexError):
        return 0


def difficulty_from_story_id(story_id):
    if "_" in story_id:
        return story_id.split("_", 1)[1]
    return ""


# ===========================================================================
# aggregate tables
# ===========================================================================

def analysis_set(runs, options):
    """The rows a learning analysis should use: completed, and (by default) first
    attempts only. Replays are excluded per GDD 8.3 -- a second run at the same
    story is practice, not a first measurement."""
    keep = [r for r in runs if r["completed"] == 1]
    if not options.include_replays:
        keep = [r for r in keep if r["isReplay"] == 0]
    if not options.include_time_outliers:
        keep = [r for r in keep if r["time_outlier"] == 0]
    return keep


def build_contrast(runs):
    """THE table. Reader (text visible) vs Race (text gone), per SWBST slot.

    Reported three ways because they answer different questions:
      read_acc                 -- can they find the slot WITH the text
      race_acc                 -- can they find it WITHOUT the text (missed counts as failure)
      race_acc_excl_missed     -- ... counting only gates where they actually chose a card
    """
    rows = []

    def block(label, subset):
        if not subset:
            return
        for index, (key, name) in enumerate(SLOTS):
            read_values = [r["read_%s_correct" % key] for r in subset
                           if r["read_%s_correct" % key] != ""]
            outcomes = [r["race_%s_outcome" % key] for r in subset
                        if r["race_%s_outcome" % key] != ""]
            n_correct = sum(1 for o in outcomes if o == "correct")
            n_wrong = sum(1 for o in outcomes if o == "wrong")
            n_missed = sum(1 for o in outcomes if o == "missed")
            race_acc = (n_correct / len(outcomes)) if outcomes else None
            race_excl = (n_correct / (n_correct + n_wrong)) if (n_correct + n_wrong) else None
            read_acc = (statistics.fmean(read_values)) if read_values else None

            pairs = [(r["read_%s_correct" % key], 1 if r["race_%s_outcome" % key] == "correct" else 0)
                     for r in subset
                     if r["read_%s_correct" % key] != "" and r["race_%s_outcome" % key] != ""]
            test = paired_t(pairs) if len(pairs) >= 2 else None

            rows.append(OrderedDict([
                ("grouping", label),
                ("slot", key),
                ("slot_name", name),
                ("n_runs", len(subset)),
                ("read_n", len(read_values)),
                ("read_acc", "" if read_acc is None else round(read_acc, 4)),
                ("race_n", len(outcomes)),
                ("race_acc", "" if race_acc is None else round(race_acc, 4)),
                ("race_acc_excl_missed", "" if race_excl is None else round(race_excl, 4)),
                ("race_n_correct", n_correct),
                ("race_n_wrong", n_wrong),
                ("race_n_missed", n_missed),
                ("race_wrong_rate", "" if not outcomes else round(n_wrong / len(outcomes), 4)),
                ("race_missed_rate", "" if not outcomes else round(n_missed / len(outcomes), 4)),
                ("drop_read_minus_race",
                 "" if (read_acc is None or race_acc is None) else round(read_acc - race_acc, 4)),
                ("paired_n", "" if not test else test["n"]),
                ("paired_mean_diff", "" if not test else test["mean_diff"]),
                ("paired_t", "" if not test else test["t"]),
                ("paired_df", "" if not test else test["df"]),
                ("paired_p", "" if not test else test["p_two_tailed"]),
                ("paired_cohen_dz", "" if not test else test["cohen_dz"]),
            ]))

    block("ALL", runs)
    for session in range(1, 11):
        block("session %02d" % session, [r for r in runs if r["session"] == session])
    for difficulty in DIFFICULTIES:
        block("difficulty %s" % difficulty, [r for r in runs if r["difficulty"] == difficulty])
    return rows


def build_growth(runs):
    """Does performance improve from session 1 to session 10?

    One row per session (and per session x difficulty), learner-level means first
    so a learner who played more stories does not weigh more heavily.
    """
    rows = []

    def block(label, subset):
        if not subset:
            return
        by_learner = defaultdict(list)
        for run in subset:
            by_learner[run["learnerId"]].append(run)

        def learner_means(field):
            values = []
            for _, group in by_learner.items():
                got = [g[field] for g in group if g[field] != ""]
                if got:
                    values.append(statistics.fmean(got))
            return values

        read_means = learner_means("read_acc")
        race_means = learner_means("race_acc")
        drop_means = learner_means("support_removal_drop")
        star_means = learner_means("starsEarned")
        word_means = learner_means("summary_words")
        unaided = learner_means("arrange_unaided")
        missed = learner_means("race_missed_rate")
        total_minutes = learner_means("totalMinutes")

        rows.append(OrderedDict([
            ("grouping", label),
            ("n_learners", len(by_learner)),
            ("n_runs", len(subset)),
            ("read_acc_mean", mean_or_blank(read_means)),
            ("read_acc_sd", sd_or_blank(read_means)),
            ("race_acc_mean", mean_or_blank(race_means)),
            ("race_acc_sd", sd_or_blank(race_means)),
            ("drop_mean", mean_or_blank(drop_means)),
            ("drop_sd", sd_or_blank(drop_means)),
            ("race_missed_rate_mean", mean_or_blank(missed)),
            ("stars_mean", mean_or_blank(star_means)),
            ("arrange_unaided_rate", mean_or_blank(unaided)),
            ("summary_words_mean", mean_or_blank(word_means)),
            ("total_minutes_mean", mean_or_blank(total_minutes)),
        ]))

    block("ALL", runs)
    for session in range(1, 11):
        block("session %02d" % session, [r for r in runs if r["session"] == session])
    for difficulty in DIFFICULTIES:
        block("difficulty %s" % difficulty, [r for r in runs if r["difficulty"] == difficulty])
    for session in range(1, 11):
        for difficulty in DIFFICULTIES:
            block("session %02d %s" % (session, difficulty),
                  [r for r in runs if r["session"] == session and r["difficulty"] == difficulty])
    return rows


def build_learner_session(runs):
    rows = []
    by_key = defaultdict(list)
    for run in runs:
        by_key[(run["learnerId"], run["displayName"], run["session"])].append(run)

    for (learner_id, name, session), group in sorted(by_key.items(), key=lambda kv: (kv[0][0], kv[0][2])):
        done = {g["difficulty"] for g in group}
        rows.append(OrderedDict([
            ("learnerId", learner_id),
            ("displayName", name),
            ("session", session),
            ("stories_completed", len(group)),
            ("easy_done", int("easy" in done)),
            ("average_done", int("average" in done)),
            ("hard_done", int("hard" in done)),
            ("read_acc_mean", mean_or_blank([g["read_acc"] for g in group if g["read_acc"] != ""])),
            ("race_acc_mean", mean_or_blank([g["race_acc"] for g in group if g["race_acc"] != ""])),
            ("drop_mean", mean_or_blank([g["support_removal_drop"] for g in group
                                         if g["support_removal_drop"] != ""])),
            ("race_missed_rate_mean", mean_or_blank([g["race_missed_rate"] for g in group
                                                     if g["race_missed_rate"] != ""])),
            ("stars_mean", mean_or_blank([g["starsEarned"] for g in group])),
            ("stars_total", sum(g["starsEarned"] for g in group)),
            ("arrange_unaided_rate", mean_or_blank([g["arrange_unaided"] for g in group])),
            ("arrange_attempts_mean", mean_or_blank([g["arrangeAttempts"] for g in group])),
            ("summary_words_mean", mean_or_blank([g["summary_words"] for g in group])),
            ("nudge_mean", mean_or_blank([g["nudgeCount"] for g in group])),
            ("minutes_total", sum_or_blank([g["totalMinutes"] for g in group])),
            ("reading_minutes", minutes([g["readingSeconds"] for g in group])),
            ("race_minutes", minutes([g["raceSeconds"] for g in group])),
            ("arrange_minutes", minutes([g["arrangeSeconds"] for g in group])),
            ("summary_minutes", minutes([g["summarySeconds"] for g in group])),
        ]))
    return rows


def build_learner_summary(runs, all_runs, roster_by_id):
    rows = []
    by_learner = defaultdict(list)
    for run in runs:
        by_learner[run["learnerId"]].append(run)
    all_by_learner = defaultdict(list)
    for run in all_runs:
        all_by_learner[run["learnerId"]].append(run)

    known = set(by_learner) | set(all_by_learner) | set(roster_by_id)
    for learner_id in sorted(known):
        group = by_learner.get(learner_id, [])
        every = all_by_learner.get(learner_id, [])
        info = roster_by_id.get(learner_id, {})
        sessions = sorted({g["session"] for g in group})
        missing = [s for s in range(1, 11) if s not in sessions]

        early = [g for g in group if g["session"] <= 3]
        late = [g for g in group if g["session"] >= 8]

        rows.append(OrderedDict([
            ("learnerId", learner_id),
            ("displayName", info.get("displayName", "")),
            ("roster_unlockedSession", info.get("unlockedSession", "")),
            ("roster_storiesCompleted", info.get("storiesCompleted", "")),
            ("in_roster", int(bool(info))),
            ("runs_total", len(every)),
            ("runs_analysed", len(group)),
            ("runs_abandoned", sum(1 for g in every if g["abandoned"])),
            ("runs_replay", sum(1 for g in every if g["isReplay"])),
            ("sessions_with_data", len(sessions)),
            ("sessions_missing", " ".join(str(s) for s in missing)),
            ("first_seen", min([g["startedIso"] for g in every], default="")),
            ("last_seen", max([g["startedIso"] for g in every], default="")),
            ("devices_used", " ".join(sorted({g["deviceId"] for g in every if g["deviceId"]}))),
            ("app_versions", " ".join(sorted({g["appVersion"] for g in every if g["appVersion"]}))),
            ("read_acc_mean", mean_or_blank([g["read_acc"] for g in group if g["read_acc"] != ""])),
            ("race_acc_mean", mean_or_blank([g["race_acc"] for g in group if g["race_acc"] != ""])),
            ("drop_mean", mean_or_blank([g["support_removal_drop"] for g in group
                                         if g["support_removal_drop"] != ""])),
            ("race_acc_first3", mean_or_blank([g["race_acc"] for g in early if g["race_acc"] != ""])),
            ("race_acc_last3", mean_or_blank([g["race_acc"] for g in late if g["race_acc"] != ""])),
            ("race_acc_gain", ""),
            ("stars_mean", mean_or_blank([g["starsEarned"] for g in group])),
            ("arrange_unaided_rate", mean_or_blank([g["arrange_unaided"] for g in group])),
            ("summary_words_mean", mean_or_blank([g["summary_words"] for g in group])),
            ("minutes_total", sum_or_blank([g["totalMinutes"] for g in every])),
        ]))
        last_row = rows[-1]
        if last_row["race_acc_first3"] != "" and last_row["race_acc_last3"] != "":
            last_row["race_acc_gain"] = round(
                last_row["race_acc_last3"] - last_row["race_acc_first3"], 4)
    return rows


def build_picks(runs):
    """One row per card touched in the race (schema 3 `racePicks`).

    This is the misconception data: WHICH wrong idea the learner took, not merely
    that they were wrong. `option` is an index into the story JSON's own list --
    0 = correct, 1 = distractors[0], 2 = distractors[1]; -1 = the raiser could not say.
    """
    rows = []
    for run in runs:
        for order, pick in enumerate(run["_racePicks"]):
            if not isinstance(pick, dict):
                continue
            element = as_int(pick.get("element"), -1)
            rows.append(OrderedDict([
                ("learnerId", run["learnerId"]),
                ("displayName", run["displayName"]),
                ("runId", run["runId"]),
                ("storyId", run["storyId"]),
                ("session", run["session"]),
                ("difficulty", run["difficulty"]),
                ("pick_order", order),
                ("slot", SLOT_KEYS[element] if 0 <= element < 5 else ""),
                ("slot_name", SLOT_NAMES[element] if 0 <= element < 5 else ""),
                ("option_index", as_int(pick.get("option"), -1)),
                ("option_kind", option_kind(as_int(pick.get("option"), -1))),
                ("card_text", str(pick.get("text", ""))),
                ("lane", as_int(pick.get("lane"), -1)),
                ("correct", int(as_bool(pick.get("correct")))),
                ("is_represent", int(as_bool(pick.get("represent")))),
                ("at_seconds", round(as_float(pick.get("atSeconds")), 2)),
            ]))
    return rows


def option_kind(index):
    return {0: "correct", 1: "distractor_1", 2: "distractor_2"}.get(index, "unknown")


def first_free_pick(run):
    """Per SWBST slot, the learner's FIRST freely-chosen card in that run.

    A re-presented card (`represent: true`) only ever appears AFTER a wrong pick or
    a missed gate -- it is a single gold card, not a choice among three -- so it is
    never a first encounter and must be excluded from any "what did they choose"
    analysis. Returns {element: pick}.
    """
    chosen = {}
    for pick in run["_racePicks"]:
        if not isinstance(pick, dict) or as_bool(pick.get("represent")):
            continue
        element = as_int(pick.get("element"), -1)
        if 0 <= element < 5 and element not in chosen:
            chosen[element] = pick
    return chosen


def build_distractors(runs):
    """WHICH wrong idea the learner took -- the qualitative half of the measure.

    `raceFirstOutcome` says a learner was wrong at BUT. This says they thought the
    BUT was "the swing chain snapped in half". The two distractors at each slot are
    authored to fail in different ways, so this is the difference between a visible
    misconception and an invisible one. Schema 3 only (`racePicks`).

    Two groupings in one file:
      story        -- one row per story x slot x card. The teaching-level table:
                      which specific wrong idea pulled learners in.
      slot_overall -- one row per slot x option kind, with an early (sessions 1-5)
                      / late (6-10) split, so a shift over the study is visible.
    """
    usable = [r for r in runs if r["_picksCaptured"]]
    rows = []
    if not usable:
        return rows

    by_card = defaultdict(lambda: {"first": 0, "all": 0, "learners": set()})
    by_slot = defaultdict(lambda: {"early": 0, "late": 0, "learners": set()})
    slot_totals = defaultdict(int)
    story_slot_totals = defaultdict(int)
    slot_era_totals = defaultdict(int)

    for run in usable:
        firsts = first_free_pick(run)
        for element, pick in firsts.items():
            index = as_int(pick.get("option"), -1)
            key = (run["storyId"], run["session"], run["difficulty"], element,
                   index, str(pick.get("text", "")))
            by_card[key]["first"] += 1
            by_card[key]["learners"].add(run["learnerId"])
            story_slot_totals[(run["storyId"], element)] += 1
            slot_totals[element] += 1

            era = "early" if run["session"] <= 5 else "late"
            slot_key = (element, index)
            by_slot[slot_key][era] += 1
            by_slot[slot_key]["learners"].add(run["learnerId"])
            slot_era_totals[(element, era)] += 1

        for pick in run["_racePicks"]:
            if not isinstance(pick, dict) or as_bool(pick.get("represent")):
                continue
            element = as_int(pick.get("element"), -1)
            if not 0 <= element < 5:
                continue
            index = as_int(pick.get("option"), -1)
            key = (run["storyId"], run["session"], run["difficulty"], element,
                   index, str(pick.get("text", "")))
            by_card[key]["all"] += 1

    for key, counts in sorted(by_card.items(), key=lambda kv: (kv[0][0], kv[0][3], -kv[1]["first"])):
        story_id, session, difficulty, element, index, text = key
        total = story_slot_totals[(story_id, element)]
        rows.append(OrderedDict([
            ("grouping", "story"),
            ("storyId", story_id),
            ("session", session),
            ("difficulty", difficulty),
            ("slot", SLOT_KEYS[element]),
            ("slot_name", SLOT_NAMES[element]),
            ("option_index", index),
            ("option_kind", option_kind(index)),
            ("card_text", text),
            ("n_first_picks", counts["first"]),
            ("pct_of_first_picks_at_slot", pct(counts["first"], total)),
            ("n_learners", len(counts["learners"])),
            ("n_picks_incl_repeats", counts["all"]),
            ("n_early_1to5", ""),
            ("n_late_6to10", ""),
            ("pct_early", ""),
            ("pct_late", ""),
        ]))

    for (element, index), counts in sorted(by_slot.items()):
        early_total = slot_era_totals[(element, "early")]
        late_total = slot_era_totals[(element, "late")]
        rows.append(OrderedDict([
            ("grouping", "slot_overall"),
            ("storyId", "ALL"),
            ("session", ""),
            ("difficulty", ""),
            ("slot", SLOT_KEYS[element]),
            ("slot_name", SLOT_NAMES[element]),
            ("option_index", index),
            ("option_kind", option_kind(index)),
            ("card_text", ""),
            ("n_first_picks", counts["early"] + counts["late"]),
            ("pct_of_first_picks_at_slot",
             pct(counts["early"] + counts["late"], slot_totals[element])),
            ("n_learners", len(counts["learners"])),
            ("n_picks_incl_repeats", ""),
            ("n_early_1to5", counts["early"]),
            ("n_late_6to10", counts["late"]),
            ("pct_early", pct(counts["early"], early_total)),
            ("pct_late", pct(counts["late"], late_total)),
        ]))
    return rows


def build_summaries(runs):
    rows = []
    for run in runs:
        text = run["_summaryText"]
        rows.append(OrderedDict([
            ("learnerId", run["learnerId"]),
            ("displayName", run["displayName"]),
            ("session", run["session"]),
            ("difficulty", run["difficulty"]),
            ("storyId", run["storyId"]),
            ("startedDate", run["startedDate"]),
            ("nudgeCount", run["nudgeCount"]),
            ("word_count", run["summary_words"]),
            ("char_count", run["summary_chars"]),
            ("read_acc", run["read_acc"]),
            ("race_acc", run["race_acc"]),
            ("stars", run["starsEarned"]),
            ("rubric_score", ""),      # blank column for the coder to fill in
            ("coder_notes", ""),
            ("summaryText", text),
        ]))
    rows.sort(key=lambda r: (r["learnerId"], r["session"], r["difficulty"]))
    return rows


# ===========================================================================
# data quality
# ===========================================================================

def build_quality(all_runs, analysed, roster_by_id, roster_dupes, options, extra):
    issues = list(extra)

    def add(severity, kind, who, what, detail):
        issues.append({"severity": severity, "kind": kind, "who": who,
                       "what": what, "detail": detail})

    # --- provenance constants that must not vary ---
    versions = defaultdict(int)
    schemas = defaultdict(int)
    for run in all_runs:
        versions[run["appVersion"]] += 1
        schemas[run["schemaVersion"]] += 1
    if len(versions) > 1:
        add("CHECK", "more than one appVersion in the data", "", "",
            "two builds were in the field, which is a threat to internal validity: "
            + ", ".join("%s=%d rows" % (k or "(blank)", v) for k, v in sorted(versions.items())))
    if len(schemas) > 1:
        add("CHECK", "more than one schemaVersion in the data", "", "",
            ", ".join("v%s=%d rows" % (k, v) for k, v in sorted(schemas.items()))
            + " -- rows from an older build lack the newer columns, which appear blank")
    for schema in schemas:
        if schema and schema != SCRIPT_SCHEMA:
            add("CHECK", "schemaVersion is not the one this script was written for", "",
                "v%s" % schema,
                "this script targets schema %d. Older rows are readable but some columns "
                "will be blank; a NEWER schema means the app gained fields this script "
                "does not know about -- check Documentation/SummaRace_Data_Dictionary.md."
                % SCRIPT_SCHEMA)

    # --- fields a build in the field simply did not record ---
    # Blank is "not measured", which is NOT zero. Say how many rows are affected so
    # nobody averages a column that half the data never carried.
    for flag, label, needs in (
            ("picks_captured", "racePicks (which card was chosen)", "schema 3"),
            ("pause_captured", "racePauseCount / racePausedSeconds", "schema 3"),
            ("race_outcome_captured", "raceFirstOutcome (wrong vs missed)", "schema 2"),
            ("phase_clocks_captured", "the four phase clocks", "schema 2"),
            ("read_pages_captured", "readingPageIndices", "schema 2")):
        missing = [r for r in all_runs if r.get(flag) == 0]
        if missing and len(missing) != len(all_runs):
            add("CHECK", "a measure is missing from SOME rows", "", label,
                "%d of %d runs do not carry %s (it needs %s). Those cells are BLANK, "
                "not 0 -- do not fill them in, and say n for this measure separately."
                % (len(missing), len(all_runs), label, needs))
        elif missing:
            add("SERIOUS", "a measure is missing from EVERY row", "", label,
                "no run carries %s (it needs %s). Any analysis that depends on it "
                "cannot be done with this data." % (label, needs))

    # --- position bias: is the race passable without reading? ---
    # Lane is randomised per gate, so if first picks pile into one lane the learners
    # are steering by habit rather than choosing an answer, and race accuracy is
    # partly measuring a motor preference. This is checkable only because `lane` is
    # logged; a surface cue that the log cannot see (card width) was exactly the
    # F44 finding, so the one cue it CAN see is worth testing every time.
    lane_counts = defaultdict(int)
    for run in all_runs:
        if not run["_picksCaptured"]:
            continue
        for pick in first_free_pick(run).values():
            lane = as_int(pick.get("lane"), -1)
            if 0 <= lane <= 2:
                lane_counts[lane] += 1
    lane_total = sum(lane_counts.values())
    if lane_total >= 100:
        share = {lane: lane_counts[lane] / lane_total for lane in (0, 1, 2)}
        worst = max(share.values())
        detail = "left %.1f%%, centre %.1f%%, right %.1f%% of %d first picks" % (
            100 * share[0], 100 * share[1], 100 * share[2], lane_total)
        if worst > 0.45:
            add("CHECK", "learners' first picks favour one lane", "",
                "%.1f%% in one lane" % (100 * worst),
                detail + " -- lanes are randomised, so an even split is expected (~33%% "
                "each). A strong preference means some picks are steering habit rather "
                "than an answer, which inflates or deflates race accuracy. Report it.")
        else:
            add("note", "lane use is balanced (no position bias)", "", "",
                detail + " -- close enough to the 33%/33%/33% expected under "
                "randomised lanes, so position carried no information")

    caught = sum(run["timesCaught"] for run in all_runs)
    if caught:
        add("CHECK", "timesCaught is not zero", "", str(caught),
            "the patrol is designed never to catch the learner (GDD D7). A non-zero value "
            "means the build in the field was not the study build.")

    # --- rows that cannot be attributed ---
    for run in all_runs:
        if not run["learnerId"]:
            add("SERIOUS", "row with an empty learnerId", "", run["runId"],
                "%s line %d -- this row cannot be attributed to a child and cannot be "
                "used. The app blocks these, so it came from an editor session or an "
                "older build." % (run["srcFile"], run["srcLine"]))
        elif roster_by_id and run["learnerId"] not in roster_by_id:
            add("CHECK", "learnerId is in the data but in no roster", run["learnerId"],
                run["runId"],
                "%s -- you have their play data but not their name. Did a roster file get "
                "left behind on a tablet?" % run["srcFile"])

    # --- learners on the roster who never played ---
    played = {run["learnerId"] for run in all_runs}
    for learner_id, info in roster_by_id.items():
        if learner_id not in played:
            add("CHECK", "learner on the roster with no play data at all", learner_id,
                info.get("displayName", ""),
                "either they never used the tablet, or their .jsonl was not copied off it")

    for name, ids in roster_dupes.items():
        add("CHECK", "the same displayName appears under different learnerIds", "", name,
            "ids: " + ", ".join(sorted(ids))
            + " -- either two children share a name (fine, but note it) or one child was "
              "given a second profile and their data is split in two")

    # --- missing sessions ---
    by_learner = defaultdict(set)
    for run in analysed:
        by_learner[run["learnerId"]].add(run["session"])
    for learner_id in sorted(played):
        if not learner_id:
            continue
        sessions = by_learner.get(learner_id, set())
        missing = [s for s in range(1, 11) if s not in sessions]
        if missing:
            add("CHECK", "learner is missing sessions", learner_id,
                roster_by_id.get(learner_id, {}).get("displayName", ""),
                "no completed first-attempt run for session(s): "
                + ", ".join(str(s) for s in missing))

    # --- partial / abandoned ---
    for run in all_runs:
        if run["abandoned"]:
            add("note", "abandoned run (never reached Results)", run["learnerId"], run["runId"],
                "%s, stopped in phase '%s'%s -- excluded from the analysis tables"
                % (run["storyId"], run["lastPhase"],
                   ", reason '%s'" % run["abandonReason"] if run["abandonReason"] else ""))
        if run["isPartialRowKept"]:
            add("CHECK", "only a mid-run snapshot survives for this run", run["learnerId"],
                run["runId"],
                "no final row was ever written -- the tablet was probably killed while the "
                "app was in the background. The row is incomplete by definition.")

    # --- looks like test traffic rather than a real child ---
    for learner_id in sorted(played):
        info = roster_by_id.get(learner_id, {})
        name = (info.get("displayName") or "").strip().lower()
        if name and any(hint == name or (len(hint) > 3 and hint in name)
                        for hint in TEST_NAME_HINTS):
            add("CHECK", "learner name looks like test traffic", learner_id,
                info.get("displayName", ""),
                "decide whether to exclude this profile before analysing")

    for run in all_runs:
        model = (run["deviceModel"] or "").lower()
        if model and not looks_like_a_tablet(model):
            add("CHECK", "row came from something that is not an Android tablet",
                run["learnerId"], run["runId"],
                "deviceModel = '%s' -- this looks like the Unity Editor / a desktop, so it "
                "is developer testing, not a child" % run["deviceModel"])
        if run["deviceId"] == "unknown":
            add("note", "device could not identify itself", run["learnerId"], run["runId"],
                "deviceId = 'unknown'; you cannot tell this tablet apart from another")
        if run["completed"] and run["totalSeconds"] < options.min_plausible_seconds:
            add("CHECK", "completed run was implausibly fast", run["learnerId"], run["runId"],
                "%s finished in %.0f s. Reading five pages, racing, ordering and writing a "
                "sentence cannot be done that fast -- almost certainly a tester tapping "
                "through." % (run["storyId"], run["totalSeconds"]))
        if run["time_outlier"]:
            add("note", "run longer than the time cap", run["learnerId"], run["runId"],
                "%.0f s (cap %.0f s). Durations include interruptions -- the tablet may "
                "simply have been put down." % (run["totalSeconds"], options.time_cap))
        if run["completed"] and run["summary_written"] == 0:
            add("note", "completed run with an empty summary sentence", run["learnerId"],
                run["runId"], "%s -- the app never blocks a blank submission" % run["storyId"])
        if options.study_start and run["startedIso"] and run["startedIso"] < options.study_start:
            add("CHECK", "run happened before the study started", run["learnerId"], run["runId"],
                "started %s, study starts %s -- pilot or developer data"
                % (run["startedIso"], options.study_start))
        if options.study_end and run["startedIso"] and run["startedIso"] > options.study_end:
            add("CHECK", "run happened after the study ended", run["learnerId"], run["runId"],
                "started %s, study ends %s" % (run["startedIso"], options.study_end))

    # --- one child, two tablets ---
    devices = defaultdict(set)
    for run in all_runs:
        if run["learnerId"] and run["deviceId"]:
            devices[run["learnerId"]].add(run["deviceId"])
    for learner_id, used in devices.items():
        if len(used) > 1:
            add("note", "learner played on more than one tablet", learner_id,
                roster_by_id.get(learner_id, {}).get("displayName", ""),
                "devices: " + ", ".join(sorted(used)))

    order = {"SERIOUS": 0, "CHECK": 1, "identical duplicate": 2, "note": 3}
    issues.sort(key=lambda i: (order.get(i["severity"], 9), i["kind"], i["who"]))
    return issues


def looks_like_a_tablet(model):
    desktop_hints = ["windows", "editor", "macbook", "imac", "system product",
                     "to be filled", "default string", "linux", "vmware", "virtualbox",
                     "desktop", "pc"]
    return not any(hint in model for hint in desktop_hints)


# ===========================================================================
# story alignment audit (optional, needs the story JSON folder)
# ===========================================================================

def build_alignment(stories_dir):
    """The Reader-vs-Race contrast per SWBST slot rests on one assumption the app
    does NOT enforce: that the Reader's question on page k asks about SWBST slot k.
    Nothing in the log can check that -- only the story content can. This dumps all
    150 page/slot pairs side by side so a human can sign each one off before any
    slot-level claim is made."""
    rows = []
    if not os.path.isdir(stories_dir):
        return rows, "story folder not found: %s" % stories_dir
    files = sorted(f for f in os.listdir(stories_dir) if f.lower().endswith(".json"))
    for name in files:
        path = os.path.join(stories_dir, name)
        try:
            with open(path, "r", encoding="utf-8-sig") as handle:
                story = json.load(handle)
        except (OSError, json.JSONDecodeError) as error:
            rows.append(OrderedDict([
                ("storyId", name), ("slot", ""), ("slot_name", ""),
                ("page_question", "COULD NOT READ: %s" % error),
                ("page_correct_answer", ""), ("element_type", ""),
                ("element_correct", ""), ("aligned_yes_no", "?"),
            ]))
            continue
        pages = as_list(story.get("pages"))
        elements = as_list(story.get("elements"))
        for index in range(max(len(pages), len(elements))):
            page = pages[index] if index < len(pages) else {}
            element = elements[index] if index < len(elements) else {}
            question = (page.get("question") or {}) if isinstance(page, dict) else {}
            options = as_list(question.get("options"))
            correct_index = as_int(question.get("correctIndex"), -1)
            answer = options[correct_index] if 0 <= correct_index < len(options) else ""
            rows.append(OrderedDict([
                ("storyId", str(story.get("id", name))),
                ("slot", SLOT_KEYS[index] if index < 5 else "e%d" % (index + 1)),
                ("slot_name", SLOT_NAMES[index] if index < 5 else ""),
                ("page_question", str(question.get("text", ""))),
                ("page_correct_answer", str(answer)),
                ("element_type", str(element.get("type", "")) if isinstance(element, dict) else ""),
                ("element_correct", str(element.get("correct", "")) if isinstance(element, dict) else ""),
                ("aligned_yes_no", ""),   # for the researcher / adviser to fill in
            ]))
    return rows, ""


# ===========================================================================
# writing
# ===========================================================================

def write_csv(path, rows, columns=None):
    if not rows:
        columns = columns or []
    else:
        columns = columns or [k for k in rows[0].keys() if not k.startswith("_")]
    # utf-8-sig so Excel opens accented names correctly by double-click.
    with open(path, "w", encoding="utf-8-sig", newline="") as handle:
        writer = csv.DictWriter(handle, fieldnames=columns, extrasaction="ignore")
        writer.writeheader()
        for row in rows:
            writer.writerow(row)
    return len(rows)


class Reporter(object):
    def __init__(self):
        self.lines = []

    def say(self, text=""):
        print(text)
        self.lines.append(text)

    def rule(self, title=""):
        self.say("")
        self.say("=" * 74)
        if title:
            self.say(title)
            self.say("=" * 74)

    def save(self, path):
        with open(path, "w", encoding="utf-8") as handle:
            handle.write("\n".join(self.lines) + "\n")


# ===========================================================================
# main
# ===========================================================================

def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Turn SummaRace tablet exports into thesis tables.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="Example:\n"
               "  python summarace_analyze.py --in exports --out analysis_out\n")
    parser.add_argument("--in", dest="input_dir", required=True,
                        help="folder holding the copied export_*.jsonl and "
                             "export_*_learners.json files")
    parser.add_argument("--out", dest="output_dir", default="analysis_out",
                        help="folder to write the CSV tables into (created if needed). "
                             "Must not be the input folder.")
    parser.add_argument("--stories", dest="stories_dir", default="",
                        help="optional: path to Assets/_Game/Resources/Stories, to write "
                             "the story alignment audit (09_...csv)")
    parser.add_argument("--skip-bad-rows", action="store_true",
                        help="continue past lines that will not parse instead of stopping. "
                             "They are still written to 00_bad_rows.csv and counted.")
    parser.add_argument("--include-replays", action="store_true",
                        help="include repeat plays of a story in the analysis tables "
                             "(default: first attempts only, per GDD 8.3)")
    parser.add_argument("--include-time-outliers", action="store_true",
                        help="include runs longer than --time-cap in the analysis tables")
    parser.add_argument("--time-cap", type=float, default=1800.0,
                        help="seconds above which a run is treated as interrupted rather "
                             "than as effort (default 1800 = 30 minutes)")
    parser.add_argument("--min-plausible-seconds", type=float, default=90.0,
                        help="a completed run faster than this is flagged as test traffic "
                             "(default 90)")
    parser.add_argument("--study-start", default="",
                        help="optional ISO date, e.g. 2026-09-01. Runs before it are flagged.")
    parser.add_argument("--study-end", default="",
                        help="optional ISO date. Runs after it are flagged.")
    options = parser.parse_args(argv)

    input_dir = os.path.abspath(options.input_dir)
    output_dir = os.path.abspath(options.output_dir)
    if input_dir == output_dir:
        die("--out must be a DIFFERENT folder from --in.\n"
            "The exported logs are irreplaceable and this script never writes near them.")
    if os.path.commonpath([input_dir]) == os.path.commonpath([input_dir, output_dir]) \
            and output_dir.startswith(input_dir + os.sep):
        die("--out is inside --in (%s).\nChoose an output folder outside the export folder "
            "so the raw data can never be mixed with generated tables." % output_dir)

    report = Reporter()
    report.rule("SummaRace study-data analyser")
    report.say("input folder : %s" % input_dir)
    report.say("output folder: %s" % output_dir)
    report.say("schema this script understands: version %d" % SCRIPT_SCHEMA)

    # ---------------- read ----------------
    data_files, roster_files, ignored = collect_inputs(input_dir)
    if not data_files:
        die("no .jsonl files found in\n    %s\n\n"
            "You are looking for files named export_YYYYMMDD_HHMM.jsonl, copied off each "
            "tablet. If you only see export_..._learners.json, you copied the rosters but "
            "not the data." % input_dir)

    report.rule("1. What was read")
    problems = []
    raw_rows = []
    for path in data_files:
        rows = read_jsonl(path, problems)
        raw_rows.extend(rows)
        report.say("  data   %-44s %6d rows" % (os.path.basename(path), len(rows)))
    roster_entries = []
    for path in roster_files:
        entries = read_roster(path, problems)
        roster_entries.extend(entries)
        report.say("  roster %-44s %6d learners" % (os.path.basename(path), len(entries)))
    for name in ignored:
        report.say("  ignored (not an export): %s" % name)

    # ---------------- validate ----------------
    good_rows = []
    for row in raw_rows:
        try:
            validate_row(row)
            good_rows.append(row)
        except BadRow as error:
            problems.append({
                "file": row.get("_srcFile", "?"), "line": row.get("_srcLine", 0),
                "reason": str(error),
                "text": json.dumps({k: v for k, v in row.items()
                                    if not k.startswith("_")})[:400],
            })

    os.makedirs(output_dir, exist_ok=True)

    if problems:
        bad_path = os.path.join(output_dir, OUT_FILES["bad"])
        write_csv(bad_path, problems, ["file", "line", "reason", "text"])
        report.say("")
        report.say("  %d line(s) could not be used. They are listed in %s"
                   % (len(problems), OUT_FILES["bad"]))
        for problem in problems[:10]:
            report.say("     %s line %s: %s" % (problem["file"], problem["line"],
                                                problem["reason"][:200]))
        if len(problems) > 10:
            report.say("     ... and %d more (see the file)" % (len(problems) - 10))
        if not options.skip_bad_rows:
            die("%d line(s) in the export could not be read.\n\n"
                "They are listed with the reason in:\n    %s\n\n"
                "Nothing has been analysed. Look at that file first -- a play-through is "
                "irreplaceable and this script will not quietly throw one away.\n"
                "If you have looked and you accept losing them, re-run with "
                "--skip-bad-rows." % (len(problems), bad_path))
        report.say("  --skip-bad-rows was given, so the run continues without them.")

    report.say("")
    report.say("  usable log lines: %d" % len(good_rows))

    # ---------------- roster ----------------
    roster_by_id = {}
    roster_dupes = defaultdict(set)
    for entry in roster_entries:
        learner_id = entry["learnerId"]
        if not learner_id:
            continue
        if learner_id in roster_by_id and \
                roster_by_id[learner_id]["displayName"] != entry["displayName"]:
            roster_dupes[entry["displayName"]].add(learner_id)
        roster_by_id[learner_id] = entry
    by_name = defaultdict(set)
    for learner_id, entry in roster_by_id.items():
        if entry["displayName"].strip():
            by_name[entry["displayName"].strip()].add(learner_id)
    for name, ids in by_name.items():
        if len(ids) > 1:
            roster_dupes[name] = ids
    report.say("  learners on the rosters: %d" % len(roster_by_id))

    # ---------------- dedupe ----------------
    quality_extra = []
    kept_rows, dropped = dedupe(good_rows, quality_extra)
    report.rule("2. Deduplication (one row per play-through)")
    report.say("  RULE: group by runId; keep the row with isPartial=false if there is one,")
    report.say("        otherwise the latest rowWrittenIso. The app writes a safety snapshot")
    report.say("        each time it is backgrounded, and a tablet exported twice repeats")
    report.say("        every earlier row -- both collapse here.")
    report.say("")
    report.say("  log lines in      : %d" % len(good_rows))
    report.say("  play-throughs out : %d" % len(kept_rows))
    report.say("  duplicate/snapshot lines folded away: %d" % dropped)

    # ---------------- derive ----------------
    runs = [derive(row, options) for row in kept_rows]
    for run in runs:
        info = roster_by_id.get(run["learnerId"])
        run["displayName"] = info["displayName"] if info else ""
        # The row's own participant code wins: it was written at the moment the run happened,
        # whereas the roster reflects the tablet whenever the export was taken. Fall back to the
        # roster for rows written before schema 4, so an export that mixes both still joins.
        if not run.get("participantCode") and info:
            run["participantCode"] = info.get("participantCode", "")

    attempts = defaultdict(int)
    for run in sorted(runs, key=lambda r: (r["learnerId"], r["storyId"], iso_sort_key(r["startedIso"]))):
        key = (run["learnerId"], run["storyId"])
        attempts[key] += 1
        run["attemptNo"] = attempts[key]

    analysed = analysis_set(runs, options)

    report.rule("3. Which runs the analysis tables use")
    report.say("  play-throughs total                     : %d" % len(runs))
    report.say("  ... abandoned (empty finishedIso)        : %d"
               % sum(1 for r in runs if r["abandoned"]))
    report.say("  ... replays of an already-finished story : %d"
               % sum(1 for r in runs if r["isReplay"]))
    report.say("  ... longer than the %.0f s time cap      : %d"
               % (options.time_cap, sum(1 for r in runs if r["time_outlier"])))
    report.say("")
    report.say("  ANALYSED (completed%s%s)          : %d"
               % ("" if options.include_replays else ", first attempts only",
                  "" if options.include_time_outliers else ", within the time cap",
                  len(analysed)))
    report.say("  Every run, including the excluded ones, is still in %s -- filter it"
               % OUT_FILES["runs"])
    report.say("  yourself with the completed / isReplay / time_outlier columns.")

    # ---------------- tables ----------------
    written = OrderedDict()
    written[OUT_FILES["runs"]] = write_csv(
        os.path.join(output_dir, OUT_FILES["runs"]), runs)
    written[OUT_FILES["learner_session"]] = write_csv(
        os.path.join(output_dir, OUT_FILES["learner_session"]),
        build_learner_session(analysed))
    contrast = build_contrast(analysed)
    written[OUT_FILES["contrast"]] = write_csv(
        os.path.join(output_dir, OUT_FILES["contrast"]), contrast)
    written[OUT_FILES["growth"]] = write_csv(
        os.path.join(output_dir, OUT_FILES["growth"]), build_growth(analysed))
    written[OUT_FILES["learners"]] = write_csv(
        os.path.join(output_dir, OUT_FILES["learners"]),
        build_learner_summary(analysed, runs, roster_by_id))
    written[OUT_FILES["picks"]] = write_csv(
        os.path.join(output_dir, OUT_FILES["picks"]), build_picks(runs),
        ["learnerId", "displayName", "runId", "storyId", "session", "difficulty",
         "pick_order", "slot", "slot_name", "option_index", "option_kind", "card_text",
         "lane", "correct", "is_represent", "at_seconds"])
    written[OUT_FILES["summaries"]] = write_csv(
        os.path.join(output_dir, OUT_FILES["summaries"]), build_summaries(analysed))
    distractors = build_distractors(analysed)
    written[OUT_FILES["distractors"]] = write_csv(
        os.path.join(output_dir, OUT_FILES["distractors"]), distractors,
        ["grouping", "storyId", "session", "difficulty", "slot", "slot_name",
         "option_index", "option_kind", "card_text", "n_first_picks",
         "pct_of_first_picks_at_slot", "n_learners", "n_picks_incl_repeats",
         "n_early_1to5", "n_late_6to10", "pct_early", "pct_late"])

    issues = build_quality(runs, analysed, roster_by_id, roster_dupes, options, quality_extra)
    written[OUT_FILES["quality"]] = write_csv(
        os.path.join(output_dir, OUT_FILES["quality"]), issues,
        ["severity", "kind", "who", "what", "detail"])

    if options.stories_dir:
        rows, error = build_alignment(options.stories_dir)
        if error:
            report.say("")
            report.say("  alignment audit skipped: %s" % error)
        else:
            written[OUT_FILES["alignment"]] = write_csv(
                os.path.join(output_dir, OUT_FILES["alignment"]), rows)

    # ---------------- headline numbers ----------------
    report.rule("4. The headline: Reader (text visible) vs Race (text gone)")
    overall = [row for row in contrast if row["grouping"] == "ALL"]
    if overall:
        report.say("  slot        read_acc  race_acc  race_excl_missed   drop   wrong  missed")
        for row in overall:
            report.say("  %-10s %8s  %8s  %16s  %5s  %6d  %6d"
                       % (row["slot_name"], row["read_acc"], row["race_acc"],
                          row["race_acc_excl_missed"], row["drop_read_minus_race"],
                          row["race_n_wrong"], row["race_n_missed"]))
        report.say("")
        report.say("  read_acc         = correct WITH the story text on screen")
        report.say("  race_acc         = correct WITHOUT it (a missed gate counts as a failure)")
        report.say("  race_excl_missed = ... counting only gates where a card was actually chosen")
        report.say("  drop             = read_acc - race_acc: the support-removal cost")

        whole_pairs = [(r["read_acc"], r["race_acc"]) for r in analysed
                       if r["read_acc"] != "" and r["race_acc"] != ""]
        test = paired_t(whole_pairs)
        if test:
            report.say("")
            report.say("  Whole-story paired comparison (one pair per analysed run):")
            report.say("    n = %d, mean(read - race) = %.3f, SD = %.3f"
                       % (test["n"], test["mean_diff"], test["sd_diff"]))
            report.say("    t(%s) = %s, p (two-tailed) = %s, Cohen's dz = %s"
                       % (test["df"], test["t"], test["p_two_tailed"], test["cohen_dz"]))
            report.say("    NOTE: runs are nested within learners, so this is a descriptive")
            report.say("    manipulation check, not the study's inferential test. For a")
            report.say("    per-learner test use 05_learner_summary.csv (one row per child).")

        missed_total = sum(row["race_n_missed"] for row in overall)
        resolved_total = sum(row["race_n"] for row in overall)
        if resolved_total:
            rate = missed_total / resolved_total
            report.say("")
            report.say("  Handling noise: %.1f%% of race gates were MISSED (no card touched)."
                       % (100 * rate))
            if rate > 0.15:
                report.say("  >>> WARNING: above 15%, the race is partly measuring dexterity,")
                report.say("      not comprehension. Report race_acc_excl_missed alongside")
                report.say("      race_acc, and say so in the limitations.")

    report.rule("5. Which wrong idea did they take? (schema 3 racePicks)")
    picks_rows = [r for r in analysed if r["_picksCaptured"]]
    if not picks_rows:
        report.say("  No run in this data carries racePicks, so the misconception-level")
        report.say("  analysis is not possible. That needs a schema 3 build.")
    else:
        report.say("  %d of %d analysed runs carry the per-card record."
                   % (len(picks_rows), len(analysed)))
        report.say("")
        report.say("  slot        chose correct   chose distractor 1   chose distractor 2")
        overall_rows = [r for r in distractors if r["grouping"] == "slot_overall"]
        for key, name in SLOTS:
            block = {r["option_kind"]: r for r in overall_rows if r["slot"] == key}
            report.say("  %-10s %13s %20s %20s"
                       % (name,
                          "%s%%" % block.get("correct", {}).get("pct_of_first_picks_at_slot", "-"),
                          "%s%%" % block.get("distractor_1", {}).get("pct_of_first_picks_at_slot", "-"),
                          "%s%%" % block.get("distractor_2", {}).get("pct_of_first_picks_at_slot", "-")))
        report.say("")
        report.say("  Percentages are of the learner's FIRST freely-chosen card at that")
        report.say("  gate; re-presented gold cards are excluded (they only ever follow a")
        report.say("  wrong pick or a missed gate, so they are not a choice).")
        report.say("")
        report.say("  The story-level rows of %s name the exact card." % OUT_FILES["distractors"])
        report.say("  Sort by n_first_picks to find the wrong ideas that pulled learners in --")
        report.say("  a distractor taken by many children is either a real misconception worth")
        report.say("  discussing in the paper, or a badly written option worth reporting as a")
        report.say("  limitation. The data cannot tell those apart; only reading the card can.")

        strong = sorted([r for r in distractors
                         if r["grouping"] == "story" and r["option_kind"].startswith("distractor")
                         and r["pct_of_first_picks_at_slot"] != ""],
                        key=lambda r: -r["pct_of_first_picks_at_slot"])[:8]
        if strong:
            report.say("")
            report.say("  Most-chosen distractors across all stories:")
            for row in strong:
                report.say("    %-14s %-9s %5s%% (n=%d)  %s"
                           % (row["storyId"], row["slot_name"],
                              row["pct_of_first_picks_at_slot"], row["n_first_picks"],
                              row["card_text"][:46]))

    report.rule("6. Growth: session 1 -> session 10")
    growth = build_growth(analysed)
    report.say("  grouping      learners  runs  read_acc  race_acc   drop  stars  words")
    for row in growth:
        if row["grouping"].startswith("session") and len(row["grouping"]) == 10:
            report.say("  %-12s %8d %5d %9s %9s %6s %6s %6s"
                       % (row["grouping"], row["n_learners"], row["n_runs"],
                          row["read_acc_mean"], row["race_acc_mean"], row["drop_mean"],
                          row["stars_mean"], row["summary_words_mean"]))

    # ---------------- quality ----------------
    report.rule("7. Data quality")
    counts = defaultdict(int)
    for issue in issues:
        counts[issue["severity"]] += 1
    if not issues:
        report.say("  nothing flagged.")
    for severity in ("SERIOUS", "CHECK", "identical duplicate", "note"):
        if counts.get(severity):
            report.say("  %-20s %d" % (severity, counts[severity]))
    kinds = defaultdict(int)
    for issue in issues:
        kinds[(issue["severity"], issue["kind"])] += 1
    report.say("")
    for (severity, kind), count in sorted(kinds.items(),
                                          key=lambda kv: (kv[0][0] != "SERIOUS", -kv[1])):
        report.say("  [%s] %-58s x%d" % (severity[:8], kind[:58], count))
    report.say("")
    report.say("  Full detail with learner ids: %s" % OUT_FILES["quality"])
    if counts.get("SERIOUS"):
        report.say("")
        report.say("  >>> There are SERIOUS items. Read them before you analyse anything.")

    # ---------------- caveats that must be said every time ----------------
    report.rule("8. Read this before quoting any number")
    report.say("  * A BLANK cell means the field was not recorded by the build that")
    report.say("    produced that row. It does NOT mean zero. Never fill a blank with 0,")
    report.say("    and report n separately for any measure that has blanks -- the")
    report.say("    *_captured columns in 01_runs.csv say which rows carry what.")
    report.say("  * raceFirstOutcome (correct/wrong/missed) is the authoritative per-gate")
    report.say("    verdict. raceFirstPickCorrect collapses 'chose a distractor' and")
    report.say("    'never touched a card' into the same false; use it only as a fallback")
    report.say("    for a row that has no raceFirstOutcome (outcome 'notcorrect' marks")
    report.say("    exactly those rows).")
    report.say("  * timesCaught is always 0 by design (GDD D7). It is a constant, not a")
    report.say("    measure. Do not model it.")
    report.say("  * starsEarned is computed from race first-picks alone (5/5=3*, 4/5=2*,")
    report.say("    else 1*). It carries no information the race columns do not already")
    report.say("    carry, and it ignores reading, arranging and the summary. It is a")
    report.say("    reward, not a score.")
    report.say("  * Durations are real elapsed time with no idle detection. A tablet put")
    report.say("    down mid-story counts that waiting as effort. time_outlier flags the")
    report.say("    long ones; racePausedSeconds is the part the learner deliberately paused.")
    report.say("  * nudgeCount is not a summary quality score. The app never grades the")
    report.say("    sentence -- the paper rubric does. 07_summaries.csv has empty")
    report.say("    rubric_score / coder_notes columns for that.")
    report.say("  * The per-slot Reader/Race contrast assumes the Reader's question on")
    report.say("    page k is about SWBST slot k. The app does not enforce this; it is a")
    report.say("    convention in the story files. Run with --stories to dump all 150")
    report.say("    page/slot pairs (09_...csv) and sign them off before making any")
    report.say("    slot-level claim. The whole-story contrast does not depend on it.")
    report.say("  * These logs are process data and a manipulation check. The paper")
    report.say("    pretest/posttest scored with the Summary Writing Rubric is the study's")
    report.say("    outcome measure.")

    report.rule("9. Files written")
    for name, count in written.items():
        report.say("  %-32s %6d rows" % (name, count))
    report.say("")
    report.say("  Nothing in the input folder was changed.")

    report.save(os.path.join(output_dir, OUT_FILES["report"]))
    print("\nReport also saved to %s"
          % os.path.join(output_dir, OUT_FILES["report"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())

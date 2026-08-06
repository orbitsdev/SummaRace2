#!/usr/bin/env python3
"""
Makes a realistic FAKE SummaRace export so the whole analysis pipeline can be
rehearsed before a single real child touches a tablet.

    python make_test_data.py --out fake_exports

Then:

    python summarace_analyze.py --in fake_exports --out fake_analysis

If that works end to end, the real thing will too -- and if a variable you need
for the thesis turns out to be missing, you find out NOW, while there is still
time to add it, instead of in October with 40 tablets already wiped.

Standard library only.

WHAT IT SIMULATES (on purpose -- these are the messes you WILL actually get)
  * 40 learners across 10 tablets, 4 children per tablet
  * a learning curve: race accuracy climbs across the ten sessions
  * the support-removal gap: reading (text visible) beats racing (text gone)
  * mid-run safety snapshots (isPartial rows) from backgrounding
  * abandoned runs with an empty finishedIso
  * a tablet exported TWICE, so every early row appears in both files
  * one learner moved to a spare tablet mid-study
  * one developer test profile that must be spotted and excluded
  * a learner who missed sessions 6 and 7 (absent from school)
  * runs interrupted by the app being backgrounded -- a locked screen, a child called
    away -- so the phase clocks carry idle time and the schema 6 backgrounded clocks
    are there to net it back out
  * one tablet still on an older build, whose rows lack the newest fields entirely
    (the analyser must call that NOT CAPTURED, never 0)
  * the SWBST ordering errors real learners make -- mostly But/So confusions

Add --corrupt to also write a half-written final line, so you can rehearse what
the analyser does when a tablet dies mid-save. (It stops and tells you.)
"""

import argparse
import datetime
import json
import os
import random
import uuid

SLOT_NAMES = ["SOMEBODY", "WANTED", "BUT", "SO", "THEN"]
DIFFICULTIES = ["easy", "average", "hard"]
APP_VERSION = "1.0.0"
DEVICE_MODELS = ["samsung SM-T295", "samsung SM-T500", "Lenovo TB-X306X",
                 "HUAWEI AGS3-W09", "realme RMP2105"]

FIRST_NAMES = ["Maria", "Juan", "Angel", "Sofia", "Miguel", "Althea", "Gabriel",
               "Nicole", "Joshua", "Kyla", "Rafael", "Danica", "Christian", "Jasmine",
               "Paolo", "Ericka", "Andrei", "Trisha", "Lorenzo", "Bianca", "Marco",
               "Precious", "Jerome", "Camille", "Vincent", "Shaira", "Emmanuel",
               "Alyssa", "Carlo", "Reyna", "Diego", "Mikaela", "Ryan", "Charlize",
               "Nathaniel", "Aubrey", "Julius", "Kristine", "Adrian", "Faith"]

SUMMARY_TEMPLATES = [
    "{who} wanted {want} but {but} so {so} and then {then}.",
    "{who} wanted {want}. But {but}. So {so}. Then {then}.",
    "The story is about {who} who wanted {want} but {but} so {so} then {then}.",
    "{who} wanted {want} but {but}.",                      # incomplete attempt
    "{who} was in the story and it was fun.",              # weak attempt
]
WHO = ["Molly", "Duncan", "Lucy", "Omar", "Grandfather", "the beginner", "the animals",
       "Jayden", "Shawn", "Marusia"]
WANT = ["a turn on the swing", "to write a letter", "to visit the zoo",
        "to learn about the past", "to skateboard", "to help people"]
BUT = ["her friend would not get off", "the crayons were angry", "they had too much to see",
       "it was very hard", "they faced danger"]
SO = ["she used an I message", "he made a new picture", "they visited the animals",
      "they practised every day", "they worked bravely"]
THEN = ["she got her turn", "everyone was happy", "they made good memories",
        "they learned the basics", "they were honoured"]


def iso(when):
    return when.strftime("%Y-%m-%dT%H:%M:%S.%f0Z")


def device_token(seed):
    return "%012x" % (abs(hash("dev" + str(seed))) % (16 ** 12))


ARRANGE_MAX_ATTEMPTS = 4        # GameRules.ArrangeMaxAttempts -- then the assist fires
CORRECT_BOARD = [0, 1, 2, 3, 4]


def make_arrange(rng, ability):
    """Simulates the Arrange screen and returns (orders, solved, assisted, attempts).

    `orders` is what schema 5 logs: one 5-character string per VERIFY press, position
    = slot, character = the element placed in it. "01234" is correct, "01324" is the
    But/So swap.

    The wrong first boards are deliberately NOT uniform noise. Real Grade-4 learners
    confuse the middle of SWBST -- the problem (But) with the consequence (So) -- far
    more often than they misplace Somebody, so the fake data carries that shape and
    the confusion table has a real pattern to find. Everything after attempt 1 follows
    the app's own rule: correct slots lock and are pre-filled, the rest come back to
    the pool, and after ARRANGE_MAX_ATTEMPTS failures the assist finishes it.
    """
    board = list(CORRECT_BOARD)
    if rng.random() >= min(0.90, 0.22 + ability):
        roll = rng.random()
        if roll < 0.42:                                  # THE systematic one: But <-> So
            board[2], board[3] = board[3], board[2]
        elif roll < 0.60:                                # So <-> Then
            board[3], board[4] = board[4], board[3]
        elif roll < 0.72:                                # Somebody <-> Wanted
            board[0], board[1] = board[1], board[0]
        elif roll < 0.86:                                # both middle errors at once
            board[2], board[3] = board[3], board[2]
            board[3], board[4] = board[4], board[3]
        else:                                            # no idea at all
            rng.shuffle(board)

    orders = ["".join(str(e) for e in board)]
    attempts = 1
    locked = [i for i in range(5) if board[i] == i]

    while len(locked) < 5 and attempts < ARRANGE_MAX_ATTEMPTS:
        attempts += 1
        free = [i for i in range(5) if i not in locked]
        pieces = list(free)          # the elements still in the pool ARE the free slots
        if rng.random() >= 0.50 + 0.35 * ability:
            rng.shuffle(pieces)
            if pieces == free and len(pieces) > 1:       # a "wrong" try must be wrong
                pieces = pieces[1:] + pieces[:1]
        for slot, element in zip(free, pieces):
            board[slot] = element
        orders.append("".join(str(e) for e in board))
        locked = [i for i in range(5) if board[i] == i]

    solved = len(locked) == 5
    # The assist places the rest FOR the learner and logs no order of its own -- the
    # last entry above stays the last thing the child actually built.
    return orders, solved, not solved, attempts


def make_summary(rng, quality):
    """quality 0..1 -- better learners write fuller sentences."""
    if quality > 0.75:
        template = rng.choice(SUMMARY_TEMPLATES[:3])
    elif quality > 0.45:
        template = rng.choice(SUMMARY_TEMPLATES[:4])
    else:
        template = rng.choice(SUMMARY_TEMPLATES[3:])
    return template.format(who=rng.choice(WHO), want=rng.choice(WANT), but=rng.choice(BUT),
                           so=rng.choice(SO), then=rng.choice(THEN))


def make_run(rng, learner, session, difficulty, when, device_id, device_model,
             ability, abandon=False):
    """One play-through. Returns (final_row, [partial_rows])."""
    story_id = "s%02d_%s" % (session, difficulty)
    run_id = uuid.uuid4().hex

    # Learning curve + difficulty penalty, bounded so nobody is perfect or hopeless.
    growth = 0.030 * (session - 1)
    penalty = {"easy": 0.0, "average": 0.06, "hard": 0.13}[difficulty]
    # Reading has the text on screen, so it is easier than racing from memory.
    read_p = min(0.97, max(0.25, ability + growth - penalty + 0.16))
    race_p = min(0.95, max(0.15, ability + growth - penalty))

    reading_pages, reading_choices, reading_correct = [], [], []
    n_pages = 5 if not abandon else rng.randint(1, 4)
    for page in range(n_pages):
        ok = rng.random() < read_p
        reading_pages.append(page)
        # 0-based index into the story JSON's own options list.
        reading_choices.append(0 if ok else rng.choice([1, 2]))
        reading_correct.append(ok)

    first_correct, outcomes, wrong_picks, picks = [], [], [0] * 5, []
    clock = 120.0 + rng.random() * 40
    resolved = 5 if not abandon else 0
    if abandon and n_pages == 5:
        resolved = rng.randint(0, 3)

    for element in range(5):
        if element >= resolved:
            outcomes.append("")
            continue
        roll = rng.random()
        if roll < race_p:
            outcome = "correct"
        elif roll < race_p + 0.08:      # drove past the whole gate
            outcome = "missed"
        else:
            outcome = "wrong"
        outcomes.append(outcome)

        if outcome == "correct":
            picks.append({"element": element, "option": 0, "text": "correct card",
                          "lane": rng.randint(0, 2), "correct": True,
                          "represent": False, "atSeconds": round(clock, 2)})
        elif outcome == "wrong":
            taken = rng.choice([1, 2])
            wrong_picks[element] = 1 + (1 if rng.random() < 0.18 else 0)
            for _ in range(wrong_picks[element]):
                picks.append({"element": element, "option": taken,
                              "text": "distractor %d for %s" % (taken, SLOT_NAMES[element]),
                              "lane": rng.randint(0, 2), "correct": False,
                              "represent": False, "atSeconds": round(clock, 2)})
                clock += 2.5
            # the correct card is re-presented after a wrong pick
            picks.append({"element": element, "option": 0, "text": "correct card",
                          "lane": 1, "correct": True, "represent": True,
                          "atSeconds": round(clock, 2)})
        else:  # missed -> the gate is re-presented and usually taken
            if rng.random() < 0.8:
                picks.append({"element": element, "option": 0, "text": "correct card",
                              "lane": 1, "correct": True, "represent": True,
                              "atSeconds": round(clock, 2)})
        clock += 9 + rng.random() * 6

    if resolved == 5:
        for element in range(5):
            first_correct.append(outcomes[element] == "correct")

    n_right = sum(1 for o in outcomes if o == "correct")
    stars = 3 if n_right >= 5 else (2 if n_right == 4 else 1)

    reading_seconds = round(rng.uniform(110, 300) + (1 - ability) * 90, 2)
    race_run = round(clock - 100, 2)
    race_seconds = round(race_run + rng.uniform(12, 30), 2)
    arrange_orders, arrange_solved, assisted, arrange_attempts = make_arrange(rng, ability)
    arrange_seconds = round(rng.uniform(40, 70) * arrange_attempts, 2)
    summary_seconds = round(rng.uniform(35, 150), 2)
    pause_count = 1 if rng.random() < 0.12 else 0
    paused_seconds = round(rng.uniform(15, 120), 2) if pause_count else 0.0

    # Schema 6: the app was backgrounded -- screen locked, child called out of the room,
    # a notification pulled through. THE PHASE CLOCK KEEPS RUNNING, which is the whole
    # point: the idle seconds are added INTO the phase they happened in, and the matching
    # backgrounded field is what lets the analyser take them back out again. Reading is
    # weighted heaviest because it is the longest phase and the one a child is most likely
    # to be interrupted during -- and it is the phase whose duration the study reports.
    bg = {"reading": 0.0, "race": 0.0, "arrange": 0.0, "summary": 0.0}
    bg_count = 0
    if rng.random() < 0.22:
        bg_count = 1 if rng.random() < 0.8 else 2
        for _ in range(bg_count):
            phase = rng.choices(list(bg), weights=[6, 1, 2, 3])[0]
            # A glance at a notification, or a whole break. Both happen in a classroom.
            away = rng.uniform(6, 45) if rng.random() < 0.6 else rng.uniform(120, 900)
            bg[phase] += away
    if abandon:
        # An abandoned run never reached those phases, and the row writes 0.0 for their
        # clocks -- so it must not claim backgrounded seconds inside them either.
        bg["race"] = bg["arrange"] = bg["summary"] = 0.0
    bg = {k: round(v, 2) for k, v in bg.items()}
    bg_total = round(sum(bg.values()), 2)
    bg_count = min(bg_count, sum(1 for v in bg.values() if v > 0)) if bg_total else 0
    reading_seconds = round(reading_seconds + bg["reading"], 2)
    race_seconds = round(race_seconds + bg["race"], 2)
    arrange_seconds = round(arrange_seconds + bg["arrange"], 2)
    summary_seconds = round(summary_seconds + bg["summary"], 2)

    total = reading_seconds + race_seconds + arrange_seconds + summary_seconds + rng.uniform(5, 20)
    quality = min(1.0, ability + growth)

    row = {
        "runId": run_id,
        "isPartial": False,
        "learnerId": learner["id"],
        "participantCode": learner.get("participantCode", ""),
        "storyId": story_id,
        "startedIso": iso(when),
        "finishedIso": "" if abandon else iso(when + datetime.timedelta(seconds=total)),
        "totalSeconds": round(total, 2),
        "readingFirstChoices": reading_choices,
        "readingFirstCorrect": reading_correct,
        "raceFirstPickCorrect": first_correct,
        "timesCaught": 0,
        "arrangeAttempts": 0 if abandon else arrange_attempts,
        "arrangeAssisted": False if abandon else assisted,
        "nudgeCount": 0 if abandon else rng.choice([0, 0, 0, 1, 1, 2]),
        "summaryText": "" if abandon else make_summary(rng, quality),
        "starsEarned": 0 if abandon else stars,
        "isReplay": False,
        "schemaVersion": 5,
        "appVersion": APP_VERSION,
        "deviceId": device_id,
        "deviceModel": device_model,
        "rowWrittenIso": iso(when + datetime.timedelta(seconds=total + 0.2)),
        "session": session,
        "difficulty": difficulty,
        "narrationOn": rng.random() < 0.7,
        "lastPhase": "complete" if not abandon else rng.choice(["reading", "race"]),
        "readingPageIndices": reading_pages,
        "readingSeconds": reading_seconds,
        "raceSeconds": 0.0 if abandon else race_seconds,
        "arrangeSeconds": 0.0 if abandon else arrange_seconds,
        "summarySeconds": 0.0 if abandon else summary_seconds,
        "raceRunSeconds": 0.0 if abandon else race_run,
        "raceWrongPicks": wrong_picks,
        "raceFirstOutcome": outcomes,
        "arrangeSolved": False if abandon else arrange_solved,
        "racePicks": picks,
        "racePauseCount": pause_count,
        "racePausedSeconds": paused_seconds,
        "abandonReason": ("race_left_by_learner" if abandon and rng.random() < 0.5 else ""),
        # Schema 5. An abandoned run never reached Arrange, so the list is EMPTY --
        # which is a different fact from the field being absent (an older build), and
        # the analyser has to keep them apart.
        "arrangeOrders": [] if abandon else arrange_orders,
    }

    partials = []
    if rng.random() < 0.18:       # the app was backgrounded mid-run
        snapshot = json.loads(json.dumps(row))
        snapshot["isPartial"] = True
        snapshot["finishedIso"] = ""
        snapshot["starsEarned"] = 0
        snapshot["summaryText"] = ""
        snapshot["arrangeAttempts"] = 0
        snapshot["arrangeSolved"] = False
        snapshot["arrangeOrders"] = []
        snapshot["raceFirstPickCorrect"] = []
        snapshot["lastPhase"] = "race"
        snapshot["rowWrittenIso"] = iso(when + datetime.timedelta(seconds=total * 0.4))
        snapshot["totalSeconds"] = round(total * 0.4, 2)
        partials.append(snapshot)
    return row, partials


def main():
    parser = argparse.ArgumentParser(description="Generate fake SummaRace exports.")
    parser.add_argument("--out", default="fake_exports", help="folder to write into")
    parser.add_argument("--learners", type=int, default=40)
    parser.add_argument("--tablets", type=int, default=10)
    parser.add_argument("--seed", type=int, default=20260806)
    parser.add_argument("--corrupt", action="store_true",
                        help="also write a half-written final line, to rehearse the "
                             "'tablet died mid-save' failure")
    parser.add_argument("--pristine", action="store_true",
                        help="skip the deliberate messes (double export, test profile, "
                             "abandoned runs) and write a clean dataset")
    options = parser.parse_args()

    rng = random.Random(options.seed)
    os.makedirs(options.out, exist_ok=True)

    per_tablet = max(1, options.learners // options.tablets)
    study_start = datetime.datetime(2026, 9, 7, 8, 30)

    learners, tablets = [], []
    name_pool = FIRST_NAMES[:]
    rng.shuffle(name_pool)
    for tablet_index in range(options.tablets):
        tablet = {
            "deviceId": device_token(tablet_index),
            "deviceModel": DEVICE_MODELS[tablet_index % len(DEVICE_MODELS)],
            "learners": [],
        }
        for _ in range(per_tablet):
            if not name_pool:
                break
            name = name_pool.pop()
            learner = {
                "id": str(uuid.uuid4()),
                # The researcher's booklet id -- schema 4 stamps this on every row AND the
                # roster, and it is the column the results chapter joins on.
                "participantCode": "P%02d" % (len(learners) + 1),
                "displayName": "%s %s." % (name, rng.choice("BCDGLMNPRSTV")),
                "ability": rng.uniform(0.30, 0.72),
                "tablet": tablet_index,
            }
            learners.append(learner)
            tablet["learners"].append(learner)
        tablets.append(tablet)

    # A developer test profile that the analyser must flag.
    if not options.pristine:
        # No participantCode on purpose: an unfinished install looks exactly like this,
        # and the analyser has to surface it rather than quietly joining to nothing.
        tester = {"id": str(uuid.uuid4()), "displayName": "test", "participantCode": "",
                  "ability": 0.9, "tablet": 0}
        learners.append(tester)
        tablets[0]["learners"].append(tester)
    else:
        tester = None

    # ONE TABLET WAS NEVER RE-FLASHED and still runs the older build, so its rows have
    # no arrangeOrders at all. This is not decoration: it is the exact case the analyser
    # must report as NOT CAPTURED rather than as "those children made no ordering
    # errors", and the only way to see that working is to have it in the rehearsal data.
    stale_tablet = options.tablets - 1 if options.tablets > 1 else None

    # Who missed which sessions.
    absentee = learners[3]
    missed_sessions = {6, 7}
    # Who was moved to a spare tablet after session 5.
    moved = learners[7]

    rows_by_tablet = {index: [] for index in range(options.tablets)}

    for learner in learners:
        is_tester = tester is not None and learner is tester
        for session in range(1, 11):
            if learner is absentee and session in missed_sessions:
                continue
            if is_tester and session > 2:
                continue
            day = study_start + datetime.timedelta(days=(session - 1) * 3)
            for order, difficulty in enumerate(DIFFICULTIES):
                when = day + datetime.timedelta(minutes=25 * order + rng.randint(0, 8))
                tablet_index = learner["tablet"]
                if learner is moved and session > 5:
                    tablet_index = (learner["tablet"] + 1) % options.tablets
                tablet = tablets[tablet_index]

                abandon = (not options.pristine) and rng.random() < 0.035
                row, partials = make_run(
                    rng, learner, session, difficulty, when,
                    tablet["deviceId"], tablet["deviceModel"], learner["ability"],
                    abandon=abandon)
                if is_tester:
                    # Testers tap straight through; the analyser should notice.
                    row["totalSeconds"] = round(rng.uniform(25, 55), 2)
                    row["deviceModel"] = "System manufacturer System Product Name (Windows)"
                if (not options.pristine) and tablet_index == stale_tablet:
                    for stale in [row] + partials:
                        stale["schemaVersion"] = 4
                        stale.pop("arrangeOrders", None)
                for extra in partials:
                    rows_by_tablet[tablet_index].append(extra)
                rows_by_tablet[tablet_index].append(row)

                # A learner who replays a story they already finished.
                if (not options.pristine) and rng.random() < 0.02:
                    replay, _ = make_run(
                        rng, learner, session, difficulty,
                        when + datetime.timedelta(minutes=40),
                        tablet["deviceId"], tablet["deviceModel"], learner["ability"])
                    replay["isReplay"] = True
                    rows_by_tablet[tablet_index].append(replay)

    stamp = "20261019_1540"
    written = []
    for index, tablet in enumerate(tablets):
        rows = sorted(rows_by_tablet[index], key=lambda r: r["rowWrittenIso"])
        name = "export_%s_tablet%02d.jsonl" % (stamp, index + 1)
        path = os.path.join(options.out, name)
        with open(path, "w", encoding="utf-8", newline="\n") as handle:
            for row in rows:
                handle.write(json.dumps(row, ensure_ascii=False) + "\n")
        written.append((name, len(rows)))

        roster = {"learners": [
            {"learnerId": learner["id"],
             "participantCode": learner.get("participantCode", ""),
             "displayName": learner["displayName"],
             "unlockedSession": 10,
             "storiesCompleted": sum(1 for r in rows
                                     if r["learnerId"] == learner["id"]
                                     and r["finishedIso"] and not r["isPartial"])}
            for learner in tablet["learners"]]}
        # A learner moved to a spare tablet also has rows on the spare tablet's file
        # but is not on that tablet's roster -- the analyser should flag it.
        roster_name = "export_%s_tablet%02d_learners.json" % (stamp, index + 1)
        with open(os.path.join(options.out, roster_name), "w", encoding="utf-8") as handle:
            json.dump(roster, handle, indent=2, ensure_ascii=False)
        written.append((roster_name, len(roster["learners"])))

    # THE MESS THAT WILL ACTUALLY HAPPEN: tablet 1 was exported at session 5 too,
    # and both files were copied into the same folder. Every early row appears twice.
    if not options.pristine:
        early = [r for r in sorted(rows_by_tablet[0], key=lambda r: r["rowWrittenIso"])
                 if r["session"] <= 5]
        name = "export_20260921_1105_tablet01.jsonl"
        with open(os.path.join(options.out, name), "w", encoding="utf-8", newline="\n") as handle:
            for row in early:
                handle.write(json.dumps(row, ensure_ascii=False) + "\n")
        written.append((name, len(early)))

    if options.corrupt:
        name = "export_%s_tablet01.jsonl" % stamp
        path = os.path.join(options.out, name)
        with open(path, "a", encoding="utf-8", newline="\n") as handle:
            handle.write('{"runId":"deadbeef","learnerId":"x","storyId":"s03_ea')

    print("Wrote fake export to: %s" % os.path.abspath(options.out))
    for name, count in written:
        print("  %-46s %5d" % (name, count))
    print("\nLearners: %d   Tablets: %d" % (len(learners), options.tablets))
    if not options.pristine:
        print("\nDeliberate messes included, so the analyser has something to catch:")
        print("  * tablet 1 exported twice -- early rows appear in two files")
        print("  * a 'test' profile that tapped straight through on a desktop")
        print("  * %s missed sessions 6 and 7" % absentee["displayName"])
        print("  * %s was moved to a spare tablet after session 5" % moved["displayName"])
        print("  * some abandoned runs, some replays, some mid-run snapshots")
        if stale_tablet is not None:
            print("  * tablet %02d was never re-flashed: schema 4 rows with NO arrangeOrders"
                  % (stale_tablet + 1))
    if options.corrupt:
        print("  * a half-written final line on tablet 1 (the analyser should STOP)")
    print("\nNow run:")
    print("  python summarace_analyze.py --in %s --out analysis_out" % options.out)


if __name__ == "__main__":
    main()

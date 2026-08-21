# Researcher email — ready to send

Drafted 2026-08-21 for **Task 12**. Everything below is verified against the build and the
story files; the two numbers in point 6 come from `Documentation/SummaRace_Critique.md` and the
anti-carryover measurements. Edit the greeting/sign-off to taste and send.

**Why this is urgent:** six of the seven points need *her* decision, not ours. The build is
otherwise finished, so this is the item most likely to sit idle while everything else waits.

---

**Subject:** SummaRace — content sign-off + 6 quick decisions before we build the study APK

---

Hi Ma'am,

The app is content-complete and I'm about to build the version the 40 tablets will run. Before
I freeze the content, I need your sign-off on a few things — most are one-word answers, and
I've given my recommendation for each so you can just reply "agree" where you do.

**The one big ask first: may I freeze the content?**

Turning your 30 stories into the app surfaced a problem I had to fix, and I want you to see
exactly what I changed before it's locked.

In the racing stage the learner picks one of three answers with the story text hidden. I
measured the three options and found the *correct* one was the longest in 121 of the 150 sets
— so a learner could score about 85% by always tapping the longest card, without reading
anything. The same pattern was in the reading questions (correct was longest in 124 of 150,
worth about 81%). That would have meant the app measured "spots the longest option" rather
than "can summarise", which would undermine the comparison with the control group.

I rewrote the **wrong** options only — about 130 strings — so all three are now similar in
length. Both exploits now score at chance (32–33%). **I did not change any of your correct
answers, and I did not change your passages.** The only exceptions are seven correct lines
that were factually wrong about their own story (e.g. one said Marusia *found* the flower when
in your passage she *tells Baba Yaga where it is*), which I corrected back toward your text.

Full detail: `SummaRace_Content_QA_Report.md` and `SummaRace_Story_Alignment_Audit.md`.

**Could you confirm you're happy with the content as it stands, so I can freeze it?** After the
freeze, any change means rebuilding and re-installing all 40 tablets.

---

**Then six quick ones:**

**1. Day 1 Easy — "The Playground" questions.** Your document gives Day 1 Easy a passage and a
main idea, but not the five page-questions the other days have. I wrote those five following
your own pattern (Who is the main character? = Somebody, What did she want? = Wanted, and so
on). They're the first questions every learner in the study will see, so I'd like your
explicit OK on them rather than assuming.

**2. Sessions 5 and 6 both use "In Grandfather's Day".** They're different passages, but they
share a title and they disagree about who the story is about — session 5's answer is
*Grandfather*, session 6's is *Sharr and Kaze*. A learner meets both a week apart.
*My recommendation:* leave them as they are and record it as a limitation — renaming one is a
content change this close to the study, and the passages themselves are fine. Happy to retitle
one if you prefer (e.g. "Sharr and Kaze Ask About the Past").

**3. Session 3 Easy — "Emma's Favorite Restaurant".** You flagged a question about its
difficulty label. Could you confirm whether it should stay Easy, or move? It's a one-line
change on my side either way.

**4. The pretest story.** "The Playground" is both the pretest passage and session 1 Easy, so
every learner in the experimental group meets the pretest text again on day 1. If that's
deliberate, no action. If not, I can swap session 1 Easy for another passage.

**5. Three sentences in Chapter 3 no longer match what the app does.** I'd rather you correct
the thesis than have me change a working app to match a sentence — but tell me either way:

  - *p.28* — "a patrol actively attempts to catch the player."
    The patrol never catches anyone; catching is disabled by design, and the app records zero
    catches for every run. It appears briefly after a wrong answer as visible pressure and is
    then left behind. Suggested wording: **"the patrol pressures the runner after wrong
    choices; it never catches."**

  - *p.29* — "no explicit SWBST guide is displayed" at the summary stage.
    The five collected parts *are* listed on that screen (they're in your own prototype
    screens too). Suggested wording: **"the five story parts remain visible as a reference."**
    If you'd rather the claim stand as written, I can hide the list — but I'd advise against
    it: it's the scaffold that makes the writing stage doable for a struggling reader.

  - *p.29* — "the system automatically evaluates the correctness, coherence, and completeness"
    of the summary. It doesn't, deliberately — a program can't score a child's sentence
    reliably, and your paper rubric is the actual outcome measure. What it does is check the
    sentence is long enough, is one sentence, and mentions the main character, then nudge at
    most twice and always accept. Every sentence is stored word-for-word for you to score.
    Suggested wording: **"guided checks and reflective prompts; sentences are recorded verbatim
    for rubric scoring."**

**6. One limitation you'll want to cite.** Because the reading stage teaches the same five
parts the racing stage then asks about, a learner could in principle pass the race by
remembering rather than by understanding. I measured it and reduced it as far as it can go: it
started at 84%, and it's now **59.3%** — but **all 25 remaining cases are the "Somebody" slot,
and every one is a character's name**. You can't reword a name, and remembering who a story
was about *is* part of the recall being tested. **Excluding names, the race scores 49.2%
against a 33.3% chance baseline.** I'd suggest citing the 49.2% figure and noting the naming
residual, rather than the raw number.

---

Nothing here blocks me from building — I can start the moment you confirm the freeze in the
first section. Points 1–6 I can apply afterwards if any need changing, as long as it's before
the tablets go out.

Thank you,
Brian

---

## Quick reply template (paste this back with your answers)

```
FREEZE:  agree / changes needed (list them)
1. Day-1 Easy questions:        OK / revise
2. Duplicate "Grandfather" title: record as limitation / retitle one
3. s03 Easy difficulty label:   keep Easy / move to ____
4. Pretest = session 1 Easy:    intentional / swap it
5. Chapter 3 wording:           I'll correct the thesis / change the app instead
6. Limitation figure:           cite 49.2% / cite 59.3% / discuss
```

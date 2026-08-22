# Draft email — five sign-off points where the app and the thesis text differ

*Prepared 2026-08-22. Send from the developer to the thesis researchers. Each point is written
so it can be answered with one line.*

---

**Subject:** SummaRace — five small wording/sign-off points before the study build

Dear team,

The app is nearly ready for the study. While checking the build against the thesis text
(pp. 27–29 and the session descriptions), I found five places where the two do not say quite
the same thing. None of them needs new development — each just needs a one-line decision from
you, so the thesis and the app describe the same instrument when the panel compares them.

**1. The patrol never actually catches the player.**
The thesis says a patrol "actively attempts to catch the player." In the app the patrol chases
and visibly closes in after a wrong answer, but it can never catch the learner — this is the
locked never-punish design decision (D7): a wrong answer slows the runner and brings the chaser
close, but there is no capture, no game-over, and the logged `timesCaught` is always 0.
*Recommendation:* reword the thesis to "a patrol pursues the player but never catches them,"
which matches both the app and the never-punish rationale already in the design chapter.
→ **OK to reword? (yes/no)**

**2. The Summary screen does show a SWBST scaffold.**
The thesis (p. 29) says "no explicit SWBST guide or visual scaffold is displayed" during the
summary task. The app currently shows a five-part SWBST reference card above the writing box.
The trade-off: the card is the last rung of the fading-support ladder (it supports weaker
writers and reduces blank submissions), but keeping it means the in-app summary is not a fully
unsupported production task the way the thesis describes. Either resolution is a small change —
softening the sentence, or removing the card from the app.
*Recommendation:* your call; I can implement either.
→ **Soften the thesis sentence, or remove the card? (one word)**

**3. The app does not really "evaluate" the summary.**
The thesis (p. 29) says the system "automatically evaluates the correctness, coherence, and
completeness" of the summary. What the app actually does is three light checks — is it more
than a few words, is it one sentence, does it name the Somebody — with at most two gentle
nudges, after which any text is accepted. It never grades the sentence; your paper rubric is
the real measure, and the app stores the sentence verbatim for that purpose.
*Recommendation:* soften to "the system applies light completeness prompts; summaries are
scored afterwards with the Summary Writing Rubric."
→ **OK to soften? (yes/no)**

**4. There are no countdown timers in the app.**
The session descriptions (all ten) mention "timed challenges," and the Definition of Terms
defines the game as a "time-limited activity." The shipped app has no countdown clocks
anywhere: a visible timer would pressure the slowest readers on exactly the measure the study
depends on (first-pick accuracy in the race), and it would conflict with the never-punish
design. The race is still naturally paced — the runner keeps moving, and the child's race time
is recorded and shown after the run — so the spirit of "timed" is there without a clock on
screen.
*Recommendation:* reword to "paced challenges" (or "self-paced with a moving-game element")
in the boilerplate and the Definition of Terms.
→ **OK to reword? (yes/no)**

**5. The pretest story reappears as the first in-app story — deliberate?**
The pretest/posttest passage "The Playground" (Appendix A) is also the app's first story
(Session 1, easy). That means every experimental-group learner re-reads the pretest passage in
their first session, which the control group does not. If that is intended (e.g. as a bridging
familiar text), no change is needed — it just deserves a sentence in the methods/limitations.
If not, I can swap Session 1 easy for a different passage before the build is frozen.
→ **Deliberate — keep? (yes/no)**

**FYI, no action needed:** the app records per-play-through process data (reading answers,
race picks, arrange orders, the typed summary, and timings) exactly as described in
`Documentation/SummaRace_Data_Dictionary.md` (schema 6, regenerated today). Learners are
pseudonymised throughout: rows carry only your participant code and a random id, never a name.

Thank you — one line per point is genuinely all I need.

Best regards,
[Developer]

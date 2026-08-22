using System.Collections;
using PrimeTween;
using SummaRace.Constants;
using SummaRace.Core;
using SummaRace.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SummaRace.Features.Results
{
    /// <summary>
    /// Stars + praise + main-idea reveal (TDD §10.4). Stars come from
    /// first-pick race accuracy; finishing always earns at least one.
    /// </summary>
    public class ResultsController : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Image[] starImages = new Image[3];
        [SerializeField] private TMP_Text praiseText;
        private SummaRace.UI.MsLumiReactor _lumi;
        [SerializeField] private GameObject mainIdeaPanel;
        [SerializeField] private TMP_Text mainIdeaText;
        [SerializeField] private Button nextButton;

        [Header("Story treasure (chest + 5 SWBST gems)")]
        [SerializeField] private RectTransform treasureRow;
        [SerializeField] private Sprite chipSprite;

        [Header("Labels (set from GameText)")]
        [SerializeField] private TMP_Text mainIdeaHeader;
        [SerializeField] private TMP_Text nextButtonLabel;

        // Sprite is already golden — off = dark silhouette, on = full color.
        //
        // MEASURED against what is actually behind it, not picked by eye. The three stars sit on
        // ResultsPanel (0.05..0.95 x 0.30..0.90), whose teal samples at sRGB 54,107,125 =
        // relative luminance 0.1278. The old 0.35/0.35/0.38 grey lands at 0.1018, which is
        // 1.17:1 against that panel — an unlit star was very nearly the panel itself. So a
        // ONE-STAR result read as a single star sitting slightly off-centre rather than as "one
        // of three", and the two empty slots that give the earned one its meaning were invisible.
        // On the study's headline measure, that is the difference between a child seeing what
        // they got and a child seeing nothing at all.
        //
        // This is the panel's own hue taken to 25% — a recessed slot rather than a neutral
        // near-black hole (see Theme.Wood on why near-black fills read as holes in this game's
        // art) — and it measures 3.02:1, clear of WCAG's 3.0 non-text floor while staying
        // unmistakably unlit. DARKER rather than lighter on purpose: the sprite is gold, so a
        // lighter off state would read as "lit but pale", the one thing this must never be
        // confusable with. Nothing about how stars are COUNTED changes here.
        private static readonly Color StarOff = new Color(0.05f, 0.10f, 0.12f);
        private static readonly Color StarOn = Color.white;

        // The learner's own sentence, shown back to them (see BuildSummaryCard). Cream card,
        // warm-brown ink: the same pairing the mission briefing and the Main Idea card use, so
        // it reads as part of this screen rather than as a notice pinned to it.
        private static readonly Color SummaryCardFill = Theme.Alpha(Theme.Cream, 1f);
        private static readonly Color SummaryInk = new Color(0.278f, 0.196f, 0.129f, 1f);

        // ---- geometry, in canvas fractions of the 1080x1920 portrait reference ----
        // Results is a full screen: the ONLY vertical band with nothing in it runs from the top
        // of the continue button's ring (0.31 x 1920 + 7 px of sizeDelta = 602.2 px) to the
        // bottom of the Main Idea card (0.37 x 1920 = 710.4 px) — 108.2 px. The card takes
        // 606.0..707.0 px of it, leaving 3.8 px clear below and 3.4 px clear above, and sits
        // inside the results panel's own width (0.05..0.95) so it reads as part of that card.
        private const float SummaryCardMinX = 0.06f;
        private const float SummaryCardMaxX = 0.94f;
        private const float SummaryCardMinY = 0.315625f;   // 606.0 px
        private const float SummaryCardMaxY = 0.368229f;   // 707.0 px

        // Padding inside the card, reference px. 12 x 3 was chosen by measuring, not by eye:
        // at 926.4 x 95.0 the worst case the input field can produce (GameRules.SummaryMaxChars
        // = 200 characters plus this caption and its quotes) auto-sizes to 23.2 pt on three
        // lines, and the same length in capitals to 22.1 pt. More vertical padding costs type
        // size directly; more horizontal padding costs almost none.
        private const float SummaryPadX = 12f;
        private const float SummaryPadY = 3f;

        /// <summary>Matches the Main Idea card's fixed 34 pt, so the two read as one screen.
        /// Any summary up to ~95 characters — which is nearly all of them — renders at this
        /// size; only a maximal sentence auto-sizes below it.</summary>
        private const float SummaryFontMax = 34f;

        /// <summary>Low on purpose. It is not a design target — the measured worst case is
        /// 22 pt — it is the floor that guarantees nothing is ever CLIPPED, including the
        /// keyboard-mash a learner can submit after two nudges (200 characters with no spaces
        /// needs about 17 pt once TMP breaks the word). Clipping a child's own words on a
        /// celebration screen is the one outcome this must not have.</summary>
        private const float SummaryFontMin = 14f;

        private StoryData _story;

        /// <summary>Set by a tap anywhere during the reveal. It does NOT abandon the reveal — it
        /// collapses every wait to zero so the same beats all still run, in order, and the
        /// coroutine still reaches the finally that hands back the only exit.</summary>
        private bool _skipRequested;

        /// <summary>The full-screen tap catcher that sets <see cref="_skipRequested"/>. Destroyed
        /// by the reveal's finally, BEFORE the continue button is shown, so it can never sit on
        /// top of the exit and eat the tap aimed at it.</summary>
        private GameObject _skipCatcher;

        private void Start()
        {
            _story = SummaRace.Core.GameManager.Instance != null ? SummaRace.Core.GameManager.Instance.CurrentStory : null;
                        if (_story == null) _story = StoryLoader.Load("s01_easy"); // editor-direct fallback
            if (_story == null)
            {
                // No story means no stars, no main idea and — because the continue button's
                // listener is wired below — no way off this screen. Leave for the cards
                // rather than sitting on a blank result (TDD §13).
                Debug.LogError("Results: no story — returning to Story Select.");
                SceneLoader.Go(SceneNames.StorySelect);
                return;
            }

            // See ArrangeController: the celebration screen was silent apart from its own star
            // and coin stings, which is the worst screen in the game to have no bed under. The
            // stings are PlayOneShot on the SFX source, so they still land on top of the loop.
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMusic(AudioKeys.MusicMenu);

            int stars = SummaRace.Core.GameManager.Instance != null ? SummaRace.Core.GameManager.Instance.CalculateStars() : 1;

            if (titleText != null)
            {
                titleText.text = GameText.ResultsCleared;
                SummaRace.UI.TitleBannerSkin.Apply(titleText);
            }
            if (mainIdeaHeader != null) mainIdeaHeader.text = GameText.MainIdeaHeader;
            if (praiseText != null) praiseText.text = "";
            EnsureLumiBadge();
            if (mainIdeaPanel != null) mainIdeaPanel.SetActive(false);
            if (mainIdeaText != null) mainIdeaText.text = _story.mainIdea;

            foreach (var star in starImages)
                if (star != null) star.color = StarOff;

            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(false);
                nextButton.onClick.AddListener(OnNextMission);
            }

            // NextButtonFrame is a SEPARATE Canvas child on the button's exact rect (0.22..0.78 x
            // 0.22..0.31) — a ring drawn behind it, not its parent — so hiding only the button
            // left that ring on screen, empty, for the whole ~4.4s reveal. The screen spent its
            // longest stretch showing a conspicuous slot with nothing in it, which reads as
            // something failing to load rather than as something still to come.
            SetNextFrameActive(false);

            // The reveal is ~4.4s and a learner sits through it thirty times across the study,
            // by which point they know exactly what it is going to say. A tap now completes it.
            // Built BEFORE the coroutine starts so even the first beat is skippable, and torn
            // down by the coroutine's finally.
            BuildSkipCatcher();

            if (SummaRace.Core.GameManager.Instance != null) SummaRace.Core.GameManager.Instance.CompleteStory(stars);

            // Label AFTER CompleteStory, and from the same test the button routes on: this run
            // only counts toward the session once it is recorded, so before that call the third
            // story of a session still reads as unfinished. Mid-session the button goes back to
            // the three story cards of the SAME session — calling that "NEXT MISSION" told the
            // learner they were leaving for a new mission when the mission had not changed.
            //
            // THE END OF THE GAME had no marker of any kind. After the last story of session 10
            // the headline still read "Mission Cleared!" exactly like the other twenty-nine, and
            // the button still said NEXT MISSION while pointing at a map with no next mission on
            // it — ten sessions of work ending on a label that was simply untrue. The final state
            // is decided from the same recorded progress, in the same place, so the headline, the
            // label and the destination cannot drift apart.
            bool allMissionsDone = IsFinalMissionDone();

            if (nextButtonLabel != null)
                nextButtonLabel.text = allMissionsDone ? GameText.ResultsJourneyDoneLabel
                                     : IsSessionDone() ? GameText.NextMissionLabel
                                     : GameText.ResultsNextStoryLabel;

            // Re-assigned rather than folded into the block above because TitleBannerSkin.Apply
            // has already run there and only ever touches colour, never the string.
            if (allMissionsDone && titleText != null) titleText.text = GameText.ResultsAllMissionsCleared;

            StartCoroutine(RevealRoutine(stars));
        }

        /// <summary>
        /// Punches a sibling of this screen's own canvas, found by name, if it is there.
        ///
        /// By name rather than by a serialized field because every one of these already exists
        /// in the scene and wiring three more fields means three more things that can be left
        /// unassigned by whoever next edits Results. Null-safe at every step: a missing canvas,
        /// a missing child or a renamed object costs the flourish and nothing else. That matters
        /// more here than elsewhere - this runs inside the reveal coroutine, and a throw in
        /// there would strand the learner on a screen whose only exit appears at the end.
        /// </summary>
        private void PunchByName(string path, float strength, float seconds)
        {
            var t = FindByName(path);
            if (t == null) return;
            Tween.StopAll(onTarget: t);
            t.localScale = Vector3.one;
            Tween.PunchScale(t, Vector3.one * strength, seconds);
        }

        private IEnumerator RevealRoutine(int stars)
        {
            // The continue button is the only way off this screen, and it is hidden until the reveal
            // ends — so anything that stops the reveal early used to trap the learner here with
            // no exit at all. The finally hands the button back however this routine ends.
            try
            {
                yield return WaitOrSkip(0.6f);

                // The fanfare starts WITH the first star rather than after the praise. It used
                // to begin four beats late, so three stars landed under the menu loop and the
                // victory sting arrived once the celebration had already peaked. This is the
                // ONLY place it starts — it used to be started a second time after the praise,
                // which restarted the sting on top of itself two beats in.
                if (AudioManager.Instance != null) AudioManager.Instance.PlayMusic(AudioKeys.MusicVictory, false);

                int lit = starImages != null ? Mathf.Min(stars, starImages.Length) : 0;
                for (int i = 0; i < lit; i++)
                {
                    if (starImages[i] != null)
                    {
                        starImages[i].color = StarOn;
                        // ESCALATING, not repeating. Every star used to land with the identical
                        // punch, the identical sound and the identical pause, so a 3-star run
                        // read as the same event three times rather than as something building
                        // to a third. Each one now hits harder, holds longer and sounds higher,
                        // and the gaps shorten, so the last star is the loudest moment on the
                        // screen - which is what it is.
                        Tween.PunchScale(starImages[i].transform,
                                         Vector3.one * (0.45f + 0.20f * i), 0.4f + 0.08f * i);
                    }
                    RevealSfx(AudioKeys.SfxStar, 1f + 0.08f * i);
                    // A star landing is one of the two moments GDD §11.4 asks to be felt. It also
                    // matters more than it sounds: a classroom tablet is usually muted, so for a
                    // learner with the sound off this is the only channel the celebration has
                    // besides the animation. Suppressed on a skip for the same reason the sounds
                    // are: three taps fired inside one frame is one long buzz, not three stars.
                    if (!_skipRequested) SummaRace.Core.Haptics.Play(SummaRace.Core.Haptics.Medium);

                    // The trophy sits in the banner through the whole reveal and never moves,
                    // on the screen that is entirely about having earned it. It reacts once, to
                    // the last star. Found by name and null-checked: no trophy in a scene simply
                    // means no punch, never a throw inside the reveal - and a throw here would
                    // strand the learner, which is what the try/finally around this exists for.
                    if (i == lit - 1) PunchByName("TitleBanner/TrophyIcon", 0.30f, 0.5f);

                    yield return WaitOrSkip(0.45f - 0.06f * i);
                }

                yield return RevealTreasure();
                // Five gems fly into a chest that did nothing about it.
                PunchByName("TreasureChest", 0.35f, 0.5f);

                if (praiseText != null)
                {
                    praiseText.text = SummaRace.Core.Praise.ForStars(stars);
                    // It used to arrive by plain assignment - no motion, no sound - on the one
                    // sentence the screen most wants read. Scale from zero, not a punch: it is
                    // appearing, not reacting.
                    praiseText.transform.localScale = Vector3.zero;
                    Tween.Scale(praiseText.transform, Vector3.one, 0.35f, Ease.OutBack);
                    RevealSfx(AudioKeys.SfxPop);
                }

                // THE STORY'S NAME MOVED HERE when the banner took the headline. It rides the
                // line that already existed under the praise rather than a new one under the
                // title: measured on the scene, TitleBanner's bottom edge is at y 0.872 and the
                // star row's top at 0.860 - a 23px gap on a 1920 reference, where SubtitleLine
                // needs 62. A subtitle up there would have landed on the stars, which is exactly
                // the collision F56h had to fix once already in this same band.
                //
                // D3's race time joins it when a real run produced one. Played direct in the
                // editor there is no race result, and the line is then just the story's name -
                // which is why the title is added unconditionally and the time appended, rather
                // than the whole line being gated on the run.
                var race = SummaRace.Core.GameManager.Instance != null
                    ? SummaRace.Core.GameManager.Instance.LastRaceResult : null;
                if (praiseText != null && _story != null && !string.IsNullOrEmpty(_story.title))
                {
                    string sub = "\u201C" + _story.title + "\u201D";
                    if (race != null && race.runSeconds > 0f)
                        sub += "   " + GameText.ResultsRaceTime(Mathf.RoundToInt(race.runSeconds));
                    SummaRace.UI.SubtitleLine.Add(praiseText, sub);
                }
                // Ms. Lumi reacts on the same beat as the praise, so the line has a face saying
                // it. Null-safe: no badge object or no badge art leaves the screen unchanged.
                if (_lumi != null) _lumi.Celebrate();

                yield return WaitOrSkip(0.8f);
                if (mainIdeaPanel != null) mainIdeaPanel.SetActive(true);

                // Last beat, after the story's own main idea: the learner's sentence beside it.
                // Deliberately not before — the reveal builds from what the app gave them to
                // what they made of it, and their words are the thing to end on.
                yield return WaitOrSkip(0.55f);
                BuildSummaryCard();
            }
            finally
            {
                // The catcher goes FIRST and unconditionally. It is a full-screen raycast target,
                // so leaving it up for even one more statement would put an invisible sheet over
                // the continue button — the exact trap the try/finally exists to prevent, just
                // wearing a different shape. Destroy on an already-destroyed object is a no-op,
                // so this is safe on scene teardown too.
                if (_skipCatcher != null) { Destroy(_skipCatcher); _skipCatcher = null; }

                // The empty ring comes back with the button it belongs to.
                SetNextFrameActive(true);

                if (nextButton != null)
                {
                    nextButton.gameObject.SetActive(true);
                    // It used to appear with no motion and no sound at the end of a celebration.
                    // The punch goes on the FRAME, never on the button: NextButton carries
                    // ButtonSquash, and two tweens driving one localScale leave it wherever the
                    // last one wrote. The SetActive is deliberately not gated on any of this -
                    // the exit must be handed back however this routine ends.
                    if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxPop);
                    PunchByName("NextButtonFrame", 0.22f, 0.4f);
                }
            }
        }


        /// <summary>A child of this screen's own canvas, by path, or null. Same reasoning as
        /// PunchByName: everything this looks for already exists in the scene, and a null here
        /// must only ever cost a flourish — never throw inside the reveal.</summary>
        private Transform FindByName(string path)
        {
            var root = ResolveSceneCanvas();
            return root == null ? null : root.Find(path);
        }

        /// <summary>Shows or hides the ring drawn behind the continue button. Separate from the
        /// button because the two are siblings, not parent and child.</summary>
        private void SetNextFrameActive(bool visible)
        {
            var frame = FindByName("NextButtonFrame");
            if (frame != null) frame.gameObject.SetActive(visible);
        }

        /// <summary>
        /// A wait that a tap can collapse to nothing.
        ///
        /// Deliberately NOT "stop the coroutine": every beat after the skip still runs, in the
        /// same order, so a skipped reveal ends with exactly the same screen as a watched one —
        /// all the stars lit, all the gems placed, the praise, the main idea and the learner's
        /// own sentence — and, critically, still falls through to the finally that hands back the
        /// only exit. Nothing here can leave the reveal half-built.
        ///
        /// Unscaled time on purpose: Results is the last screen of the loop and a timeScale left
        /// at zero by anything upstream must not be able to freeze the reveal, because the exit
        /// does not exist until the reveal ends.
        /// </summary>
        private IEnumerator WaitOrSkip(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                if (_skipRequested) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>Reveal sounds, muted once the learner has skipped: a skipped reveal fires
        /// every beat inside one frame, and eight stings at once is noise, not a celebration.</summary>
        private void RevealSfx(string key, float pitch = 1f)
        {
            if (_skipRequested) return;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(key, pitch);
        }

        /// <summary>
        /// A full-screen, fully transparent tap target that completes the reveal.
        ///
        /// A transparent Image is still a raycast target in uGUI, so this needs no art. It is
        /// safe to cover the whole screen for the duration: while the reveal runs, the continue
        /// button is hidden and nothing else on Results is interactive — and the reveal's finally
        /// destroys this before the button comes back, so the exit is never underneath it.
        ///
        /// Built in code, like the treasure gems and the summary card, so it ships without a
        /// scene edit; with no canvas to build on the reveal is simply not skippable, which is
        /// the behaviour that already existed.
        /// </summary>
        private void BuildSkipCatcher()
        {
            if (_skipCatcher != null) return;
            var root = ResolveSceneCanvas();
            if (root == null) return;

            _skipCatcher = new GameObject("RevealSkipCatcher",
                                          typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            _skipCatcher.transform.SetParent(root, false);
            var rect = (RectTransform)_skipCatcher.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();

            var image = _skipCatcher.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);   // invisible, still hit-tested
            image.raycastTarget = true;

            var button = _skipCatcher.AddComponent<Button>();
            button.transition = Selectable.Transition.None;   // nothing to highlight; it is not a control
            button.targetGraphic = image;
            button.onClick.AddListener(OnSkipReveal);
        }

        /// <summary>The learner tapping through the celebration. It answers with a click so the
        /// tap is plainly registered — a screen that simply jumps reads as a glitch.</summary>
        private void OnSkipReveal()
        {
            if (_skipRequested) return;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);
            _skipRequested = true;
        }

        /// <summary>
        /// Puts Ms. Lumi on the results celebration. She is on every other learning screen
        /// (Reader, race briefing, Arrange, Summary) and was missing from the one that
        /// congratulates - so the run ended with nobody in it.
        ///
        /// Built rather than scene-wired because this scene has no avatar object at all, and
        /// it is named TeacherAvatar so the existing MsLumiReactor.AttachBadge finds it by the
        /// same name it uses on Arrange and Summary - including its guards, which bail when
        /// the badge pools are empty rather than adding a component that would do nothing.
        ///
        /// Placement is measured against this scene, not copied from the others: the title
        /// band here is a banner with a trophy in it (x 0.12-0.88), and the Results title has
        /// already had to be fixed once for crossing that trophy, so the top-left corner the
        /// other screens use is not free. The left margin beside the stars is: Star_0 begins
        /// at x 0.14 and the star row spans y 0.70-0.86, so a square at x 0.015-0.135,
        /// y 0.745-0.813 sits clear of it, on the panel, right beside the celebration.
        /// </summary>
        private void EnsureLumiBadge()
        {
            if (_lumi != null) return;
            if (GameObject.Find(SummaRace.UI.MsLumiReactor.BadgeObjectName) == null)
            {
                var root = ResolveSceneCanvas();
                if (root == null) return;

                var go = new GameObject(SummaRace.UI.MsLumiReactor.BadgeObjectName,
                                        typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(root, false);
                var rect = (RectTransform)go.transform;
                rect.anchorMin = new Vector2(0.015f, 0.745f);
                rect.anchorMax = new Vector2(0.135f, 0.813f);
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;

                var img = go.GetComponent<Image>();
                img.preserveAspect = true;
                img.raycastTarget = false;
                go.AddComponent<SummaRace.UI.UIFloat>();
            }

            // AttachBadge sets her resting pose and the cheer pool. It returns null when there
            // is no badge art, which leaves the empty Image above drawing nothing.
            _lumi = SummaRace.UI.MsLumiReactor.AttachBadge();
            if (_lumi == null) return;

            // headPixels stays 0, so ApplyPose swaps the sprite without touching the
            // RectTransform above - the framing measured for this scene is the framing kept.
        }

        /// <summary>
        /// The story's 5 SWBST "gems" pop in above the chest — full color when the
        /// first race pick was right, dimmed otherwise. Closes the Boot treasure metaphor.
        /// </summary>
        private IEnumerator RevealTreasure()
        {
            if (treasureRow == null) yield break;
            var result = SummaRace.Core.GameManager.Instance != null ? SummaRace.Core.GameManager.Instance.LastRaceResult : null;

            // One gem per SWBST element. Driven off the story's own count rather than a literal
            // 5 so a short story (or a race result from a run that ended early) shortens the row
            // instead of throwing — this screen has no other exit, so nothing here may throw.
            int gems = _story.elements != null ? Mathf.Min(5, _story.elements.Length) : 0;

            // The count came from the story but the row was still cut into literal fifths, so
            // the two could disagree — a short row would have sat bunched against the left of
            // the chest with a gap where the missing gems used to be. One number now drives both.
            float slotWidth = 1f / Mathf.Max(1, gems);

            for (int i = 0; i < gems; i++)
            {
                // NOT earned when there is no race result, which is the opposite of what this
                // used to say. CalculateStars answers 1 to the same null (GameManager:100), so
                // playing Results directly in the editor showed ONE star above FIVE full-colour
                // gems — a screen contradicting itself about the same run, and the two readings
                // are the study's headline measure and its per-element breakdown. Dimmed gems
                // are also the honest reading of "no run happened": a gem is lit by a correct
                // FIRST pick, and there were none. Same answer for an element the run never
                // reached, which is how CountFirstPickCorrect already scores it.
                bool earned = result != null && result.firstPickCorrect != null
                    && i < result.firstPickCorrect.Length && result.firstPickCorrect[i];

                var chip = new GameObject("Gem_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                chip.transform.SetParent(treasureRow, false);
                var rt = (RectTransform)chip.transform;
                rt.anchorMin = new Vector2(i * slotWidth + 0.015f, 0f);
                rt.anchorMax = new Vector2((i + 1) * slotWidth - 0.015f, 1f);
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                var img = chip.GetComponent<Image>();
                if (chipSprite != null) { img.sprite = chipSprite; img.type = Image.Type.Sliced; }
                img.color = earned
                    ? SwbstPalette.ForIndex(i)
                    : Color.Lerp(SwbstPalette.ForIndex(i), new Color(0.6f, 0.6f, 0.6f), 0.65f);

                var letterGo = new GameObject("Letter", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                letterGo.transform.SetParent(chip.transform, false);
                var lrt = (RectTransform)letterGo.transform;
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
                var letter = letterGo.GetComponent<TextMeshProUGUI>();
                // An element with no type still gets its gem — a blank chip, never an exception.
                var type = _story.elements[i] != null ? _story.elements[i].type : null;
                letter.text = string.IsNullOrEmpty(type) ? string.Empty : type.Substring(0, 1);
                letter.alignment = TextAlignmentOptions.Center;
                letter.enableAutoSizing = true;
                letter.fontSizeMax = 46; letter.fontSizeMin = 10;
                letter.fontStyle = FontStyles.Bold;
                letter.color = earned ? Color.white : new Color(1f, 1f, 1f, 0.6f);

                chip.transform.localScale = Vector3.zero;
                Tween.Scale(chip.transform, Vector3.one, 0.3f, Ease.OutBack);
                RevealSfx(AudioKeys.SfxCoin);
                yield return WaitOrSkip(0.16f);
            }
        }

        /// <summary>
        /// Shows the learner the sentence they just wrote. Four screens produce something and
        /// this was the only one whose product the learner never saw again: SummaryController
        /// stores it on GameManager and, until now, nothing read it back.
        /// <para>
        /// It is a CELEBRATION and nothing else. No score, no tick, no comparison with the
        /// reference SWBST parts, no praise attached to it — the app does not grade a summary
        /// (GDD D7; the paper rubric is the study's outcome measure), and a child who wrote
        /// very little is entitled to see it presented exactly as warmly as a child who wrote a
        /// lot. An empty or whitespace-only sentence builds NOTHING rather than an empty card,
        /// because the Summary screen deliberately accepts almost anything, and a blank box on
        /// the results screen would read as the failure the rest of the app refuses to hand out.
        /// </para>
        /// Built in code, like the treasure gems above and SummaryController's DONE TYPING chip,
        /// so it ships without a scene edit and degrades to nothing if it cannot find a canvas.
        /// </summary>
        private void BuildSummaryCard()
        {
            var manager = SummaRace.Core.GameManager.Instance;
            string written = manager != null ? manager.LastSummaryText : null;
            // Editor-direct play has no GameManager and therefore no sentence (TDD §13), which
            // lands in the same branch as a learner who submitted nothing: show no card.
            if (string.IsNullOrWhiteSpace(written)) return;

            var root = ResolveSceneCanvas();
            if (root == null) return;

            var cardGo = new GameObject("YourSummaryCard", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            cardGo.transform.SetParent(root, false);
            var rect = (RectTransform)cardGo.transform;
            rect.anchorMin = new Vector2(SummaryCardMinX, SummaryCardMinY);
            rect.anchorMax = new Vector2(SummaryCardMaxX, SummaryCardMaxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();

            var image = cardGo.GetComponent<Image>();
            // The gem chips' own 9-sliced sprite, already wired in this scene — a card here and
            // a chip there being the same shape is why the row of gems and this read as one
            // screen. With nothing wired it degrades to a flat cream rectangle, not to nothing.
            if (chipSprite != null) { image.sprite = chipSprite; image.type = Image.Type.Sliced; }
            image.color = SummaryCardFill;
            image.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(cardGo.transform, false);
            var textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(SummaryPadX, SummaryPadY);
            textRect.offsetMax = new Vector2(-SummaryPadX, -SummaryPadY);

            var label = textGo.GetComponent<TextMeshProUGUI>();
            // Borrow the Main Idea card's own face (Nunito body) rather than loading one: same
            // scene, already resident, and the two cards then match without a serialized field
            // that nobody would remember to wire.
            var borrowed = mainIdeaText != null ? mainIdeaText.font
                         : praiseText != null ? praiseText.font : null;
            if (borrowed != null) label.font = borrowed;

            // RICH TEXT OFF. This string contains a child's unfiltered typing, and TMP would
            // read a stray "<" as the start of a tag and silently eat everything up to the next
            // ">". A learner who writes "the dog was <this> big" must see what they wrote.
            label.richText = false;
            label.text = string.Format(GameText.ResultsYourSummary, written.Trim());
            label.color = SummaryInk;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.enableAutoSizing = true;
            label.fontSizeMin = SummaryFontMin;
            label.fontSizeMax = SummaryFontMax;
            // Ellipsis, not the default Overflow. This band was measured against a 200-character
            // cap and GameRules.SummaryMaxChars is now 320, so a long sentence would otherwise
            // paint straight out of the card and across its neighbours. The child's words are
            // logged in full either way - this is only the echo, and an echo may abbreviate.
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;

            cardGo.transform.localScale = Vector3.zero;
            Tween.Scale(cardGo.transform, Vector3.one, 0.32f, Ease.OutBack);
            RevealSfx(AudioKeys.SfxPop);
        }

        /// <summary>
        /// The canvas this scene's own UI lives on. A blind FindAnyObjectByType would happily
        /// return SceneLoader's persistent FadeCanvas ([Core], DontDestroyOnLoad, alpha 0) and
        /// the card would be built invisible on the loading overlay. Same idiom, and same trap,
        /// as SummaryController.ResolveSceneCanvas.
        /// </summary>
        private Transform ResolveSceneCanvas()
        {
            var canvas = treasureRow != null ? treasureRow.GetComponentInParent<Canvas>() : null;
            if (canvas == null && nextButton != null) canvas = nextButton.GetComponentInParent<Canvas>();
            if (canvas == null && titleText != null) canvas = titleText.GetComponentInParent<Canvas>();
            if (canvas != null) return canvas.rootCanvas.transform;

            foreach (var candidate in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (candidate.gameObject.scene == gameObject.scene)
                    return candidate.rootCanvas.transform;

            Debug.LogWarning("Results: no scene canvas found — the learner's summary is not shown.");
            return null;
        }

        private void OnNextMission()
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(AudioKeys.SfxClick);

            // Mid-session there are still stories to pick, so go back to the three cards.
            // After the third one, the session is done — return to the map, which celebrates
            // it on arrival (GDD §3.1).
            //
            // The FINAL state deliberately keeps this destination. The Session Map is the only
            // screen in the game that shows the whole journey at once, so ten cleared stops IS
            // the payoff view; sending the last run to MainMenu instead would hide the thing that
            // was just finished behind a start button. What changes at the end is the headline
            // and the label (see Start) — the map is still the right place to arrive, and it is
            // never a dead end.
            SceneLoader.Go(IsSessionDone() ? SceneNames.SessionMap : SceneNames.StorySelect);
        }

        /// <summary>Was that the last story of the session? One test drives both where the
        /// continue button goes and what it is called, so the label and the destination
        /// cannot drift apart.</summary>
        /// <summary>
        /// Whether this was the last unplayed story of its session. Drives both the onward
        /// button's label and its destination, so the two cannot disagree.
        ///
        /// TOTALLY defensive, because of WHERE it is called from: Start() runs it while the exit
        /// button is hidden and before RevealRoutine's try/finally exists to hand the exit back.
        /// Anything that throws in here strands the learner on a screen with no way off and no
        /// timeout. IsSessionComplete walks CurrentLearner.progress, and the null-conditional in
        /// there guards the LEARNER, not the list - an older or hand-edited profiles.json with a
        /// null progress list was a hard NRE in exactly that window.
        /// </summary>
        private static bool IsSessionDone()
        {
            try
            {
                var gm = SummaRace.Core.GameManager.Instance;
                return gm != null && gm.CurrentStory != null
                    && gm.IsSessionComplete(gm.CurrentStory.session);
            }
            catch (System.Exception e)
            {
                // False is the safe answer: it routes to Story Select rather than the map, which
                // is never a dead end from here.
                Debug.LogWarning("[Results] session-complete check failed: " + e.Message);
                return false;
            }
        }

        /// <summary>
        /// Was that the last story of the LAST session — i.e. is the whole ten-session journey
        /// finished? Drives the one state the screen never had.
        ///
        /// Built on IsSessionDone rather than beside it so the two can only ever agree: the final
        /// state is strictly the session-complete state plus "and there is no session after this
        /// one". Session numbers are 1..GameRules.SessionCount, and the comparison is >= rather
        /// than == so a story JSON carrying an out-of-range session still resolves to "the end"
        /// instead of silently falling back to NEXT MISSION and pointing at nothing.
        ///
        /// Same defensiveness as IsSessionDone, and for the same reason: this runs in Start while
        /// the exit is hidden and before RevealRoutine's finally exists to hand it back. False is
        /// the safe answer — it is exactly the behaviour that shipped before this existed.
        /// </summary>
        private static bool IsFinalMissionDone()
        {
            try
            {
                var gm = SummaRace.Core.GameManager.Instance;
                return gm != null && gm.CurrentStory != null
                    && gm.CurrentStory.session >= GameRules.SessionCount
                    && IsSessionDone();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Results] final-mission check failed: " + e.Message);
                return false;
            }
        }
    }
}

namespace SummaRace.Tests.EditMode
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using SummaRace.Constants;
    using UnityEngine;

    /// <summary>
    /// The loading tips are the S-W-B-S-T definitions, and they are now also spoken aloud.
    /// `AudioKeys.VoLoadingTips[i]` must be the clip for `GameText.LoadingTips[i]` — the
    /// pairing is positional and nothing in the type system enforces it.
    ///
    /// `ResourceContractTests` cannot catch this: it reflects over `const string` fields, and
    /// `VoLoadingTips` is a `static readonly string[]`, so every clip in it is invisible to that
    /// check. And a mismatch does not throw — `SceneLoader` bounds-checks the index, so a short
    /// or reordered array degrades to silence or, worse, to the wrong definition being READ ALOUD
    /// over the right one on screen. A learner who cannot read the tip is exactly the learner the
    /// narration was added for, so they are the one person who cannot detect the error.
    ///
    /// `ArrangeController` indexes `LoadingTips` by SWBST element for its hint, on the same
    /// positional assumption, which is why the length is asserted against the framework's five
    /// slots rather than against whatever the arrays happen to contain.
    /// </summary>
    public class NarrationArrayTests
    {
        private const int SwbstSlots = 5;

        [Test]
        public void SpokenLoadingTipsMatchTheWrittenOnesOneForOne()
        {
            Assert.IsNotNull(GameText.LoadingTips, "GameText.LoadingTips is null.");
            Assert.IsNotNull(AudioKeys.VoLoadingTips, "AudioKeys.VoLoadingTips is null.");

            Assert.AreEqual(SwbstSlots, GameText.LoadingTips.Length,
                "LoadingTips is the S-W-B-S-T definition list — ArrangeController indexes it by " +
                "element, so it must have exactly one entry per slot.");

            Assert.AreEqual(GameText.LoadingTips.Length, AudioKeys.VoLoadingTips.Length,
                "Every written loading tip needs its spoken counterpart at the SAME index. A " +
                "shorter VoLoadingTips does not throw — the caller bounds-checks it — so the " +
                "tail of the list would silently stop being narrated.");
        }

        [Test]
        public void EverySpokenLoadingTipHasAClip()
        {
            var failures = new List<string>();

            for (int i = 0; i < AudioKeys.VoLoadingTips.Length; i++)
            {
                var key = AudioKeys.VoLoadingTips[i];
                if (string.IsNullOrEmpty(key))
                {
                    failures.Add("VoLoadingTips[" + i + "] is empty.");
                    continue;
                }
                // AudioManager.PlayVoice builds exactly this path.
                if (Resources.Load<AudioClip>("Audio/" + key) == null)
                    failures.Add("VoLoadingTips[" + i + "] = '" + key +
                                 "' has no clip at Resources/Audio/" + key +
                                 " — tip " + i + " would play as silence.");
            }

            StoryContentTests.AssertNoFailures(failures, "loading tips that would not be spoken");
        }
    }
}

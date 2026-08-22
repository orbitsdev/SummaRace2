using System.Collections;
using System.Collections.Generic;
using SummaRace.Data;
using UnityEngine;

namespace SummaRace.Core
{
    /// <summary>
    /// Plays music and SFX by key. Clips live in Resources/Audio named per AudioKeys —
    /// swapping a sound is replacing a file, never touching code (TDD §7.4).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        // Holds a null for a key with no clip too — see GetClip. Values may be null on purpose.
        private readonly Dictionary<string, AudioClip> _cache = new();
        private AudioSource _musicSource;
        private AudioSource _sfxSource;
        private AudioSource _voiceSource;
        private float _musicVolume = 0.8f;
        private float _sfxVolume = 1f;
        // Defaults mirror AppSettings so an un-configured manager still sounds like the game.
        private float _voiceVolume = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.loop = true;
            _musicSource.playOnAwake = false;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;

            _voiceSource = gameObject.AddComponent<AudioSource>();
            _voiceSource.playOnAwake = false;
        }

        /// <summary>Plays a story narration clip from a full Resources path (e.g. "Stories/Narration/s01_easy_p1").</summary>
        public void PlayNarration(string resourcePath)
        {
            StopNarration();
            if (string.IsNullOrEmpty(resourcePath)) return;
            var clip = Resources.Load<AudioClip>(resourcePath);
            if (clip == null)
            {
                Debug.LogWarning($"AudioManager: narration '{resourcePath}' not found."); // missing clip = silent page, never an error state
                return;
            }
            _voiceSource.clip = clip;
            // The narration is the accessibility support the study depends on, so it is the one
            // channel that gets its own level: AppSettings.narrationVolume was a declared setting
            // with no reader at all (this line hard-coded 1f), which meant a researcher who turned
            // the voice down still got it at full volume over a quieter mix.
            _voiceSource.volume = _voiceVolume;
            _voiceSource.Play();
        }

        public void StopNarration()
        {
            // Anything still waiting its turn dies with the line that is speaking. Without
            // this a tip queued during a scene change would surface seconds later, over the
            // next screen — SceneLoader calls this on every load precisely so the voice
            // cannot follow the learner out of a screen.
            _voiceQueue.Clear();
            if (_voicePump != null) { StopCoroutine(_voicePump); _voicePump = null; }
            if (_voiceSource != null && _voiceSource.isPlaying) _voiceSource.Stop();
        }

        // ---------- instructional narration ----------
        // Interface lines (Arrange/Summary/loading tip/race briefing) read aloud in the same
        // voice as the story pages. They share the story's AudioSource ON PURPOSE: one voice
        // channel is the only way two lines can never talk over each other, and it means
        // PlayNarration — which starts with StopNarration — always outranks an instruction.
        // Nothing scored is ever spoken here; see AudioKeys' vo_* block.

        private readonly Queue<string> _voiceQueue = new();
        private Coroutine _voicePump;

        /// <summary>The learner's VOICE ON/OFF choice (Reader toggle, PrefKeys.NarrationOn).
        /// Read live rather than cached — it can be changed mid-story, and a teacher who
        /// turns the voice off means off on every screen, not just the Reader.</summary>
        public static bool NarrationEnabled =>
            PlayerPrefs.GetInt(SummaRace.Constants.PrefKeys.NarrationOn, 1) == 1;

        /// <summary>
        /// Speaks an instructional line by AudioKeys key (Resources/Audio). Silent when the
        /// VOICE toggle is off, and silent — never an error — when the clip is missing, so
        /// call sites are safe before the audio exists.
        /// </summary>
        /// <param name="queue">
        /// true = wait for whatever is speaking to finish instead of cutting it off. That is
        /// the normal case for a screen that opens while the loading overlay's tip is still
        /// talking: the tip was started ~a second earlier and interrupting it mid-sentence
        /// teaches the learner nothing and sounds broken.
        /// </param>
        public void PlayVoice(string key, bool queue = false)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (!NarrationEnabled) { StopNarration(); return; }

            if (!queue)
            {
                StopNarration();
                Speak(GetClip(key));
                return;
            }

            _voiceQueue.Enqueue(key);
            if (_voicePump == null && isActiveAndEnabled) _voicePump = StartCoroutine(PumpVoiceQueue());
        }

        private IEnumerator PumpVoiceQueue()
        {
            while (_voiceQueue.Count > 0)
            {
                while (_voiceSource != null && _voiceSource.isPlaying) yield return null;
                Speak(GetClip(_voiceQueue.Dequeue()));
                yield return null; // let Play() register before the wait above re-tests it
            }
            _voicePump = null;
        }

        private void Speak(AudioClip clip)
        {
            if (clip == null || _voiceSource == null) return; // missing clip = silence
            if (_voiceSource.isPlaying) _voiceSource.Stop();
            _voiceSource.clip = clip;
            _voiceSource.volume = _voiceVolume;
            _voiceSource.Play();
        }

        public void PlaySfx(string key) => PlaySfx(key, 1f);

        /// <summary>
        /// A one-shot at a given pitch. Three stars that land on a rising three-note run read as
        /// something BUILDING; three identical clips read as the same event happening three
        /// times. It is the cheapest escalation available on the payoff screen and it costs no
        /// asset.
        ///
        /// Pitch is set on the shared source rather than restored afterwards, because
        /// PlayOneShot follows its source's pitch for the whole of the clip - resetting on the
        /// next line would cancel the effect before it was audible. Every caller comes through
        /// here and the no-argument overload passes 1f, so the source is always left carrying an
        /// explicit value rather than whatever the last caller happened to want.
        ///
        /// The one visible consequence: a still-ringing one-shot shifts pitch if a differently
        /// pitched sound starts over it. Today the only non-1f caller is the Results star run,
        /// where the clips are short, 0.35-0.45s apart and deliberately ascending, so the
        /// overlap is the effect rather than a defect.
        /// </summary>
        public void PlaySfx(string key, float pitch)
        {
            var clip = GetClip(key);
            if (clip == null) return;
            _sfxSource.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            _sfxSource.PlayOneShot(clip, _sfxVolume);
        }

        public void PlayMusic(string key, bool loop = true)
        {
            var clip = GetClip(key);
            if (clip == null) return;
            if (_musicSource.clip == clip && _musicSource.isPlaying)
            {
                // Same track already running: never restart it (that is what lets StorySelect and
                // the session map re-assert the menu loop for free). Do honour a changed loop
                // flag, though — asking for a one-shot sting while it happens to be looping
                // otherwise left it looping for ever.
                _musicSource.loop = loop;
                return;
            }

            _musicSource.clip = clip;
            _musicSource.loop = loop;
            _musicSource.volume = _musicVolume;
            _musicSource.Play();
        }

        public void StopMusic() => _musicSource.Stop();

        public void SetVolumes(AppSettings settings)
        {
            if (settings == null) return;
            _musicVolume = settings.musicVolume;
            _sfxVolume = settings.sfxVolume;
            _voiceVolume = settings.narrationVolume;
            _musicSource.volume = _musicVolume;
            // A page already being read follows the new level immediately rather than only from
            // the next page — the volume was changed to be heard now.
            if (_voiceSource != null) _voiceSource.volume = _voiceVolume;
        }

        private AudioClip GetClip(string key)
        {
            if (_cache.TryGetValue(key, out var cached)) return cached;

            var clip = Resources.Load<AudioClip>("Audio/" + key);
            if (clip == null)
                Debug.LogWarning($"AudioManager: clip '{key}' not found in Resources/Audio.");

            // Cache the miss as well as the hit. A missing key is usually one the game asks for
            // over and over (footsteps fire several times a second in the race), and each miss
            // was costing a fresh Resources.Load plus a console line — on the 2GB floor device
            // that is a per-frame cost for a sound that will never exist.
            _cache[key] = clip;
            return clip;
        }
    }
}

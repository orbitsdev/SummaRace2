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
            if (_voiceSource != null && _voiceSource.isPlaying) _voiceSource.Stop();
        }

        public void PlaySfx(string key)
        {
            var clip = GetClip(key);
            if (clip != null) _sfxSource.PlayOneShot(clip, _sfxVolume);
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

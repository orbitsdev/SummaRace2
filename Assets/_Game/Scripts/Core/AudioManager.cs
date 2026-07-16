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

        private readonly Dictionary<string, AudioClip> _cache = new();
        private AudioSource _musicSource;
        private AudioSource _sfxSource;
        private float _musicVolume = 0.8f;
        private float _sfxVolume = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.loop = true;
            _musicSource.playOnAwake = false;

            _sfxSource = gameObject.AddComponent<AudioSource>();
            _sfxSource.playOnAwake = false;
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
            if (_musicSource.clip == clip && _musicSource.isPlaying) return;

            _musicSource.clip = clip;
            _musicSource.loop = loop;
            _musicSource.volume = _musicVolume;
            _musicSource.Play();
        }

        public void StopMusic() => _musicSource.Stop();

        public void SetVolumes(AppSettings settings)
        {
            _musicVolume = settings.musicVolume;
            _sfxVolume = settings.sfxVolume;
            _musicSource.volume = _musicVolume;
        }

        private AudioClip GetClip(string key)
        {
            if (_cache.TryGetValue(key, out var cached)) return cached;

            var clip = Resources.Load<AudioClip>("Audio/" + key);
            if (clip == null)
            {
                Debug.LogWarning($"AudioManager: clip '{key}' not found in Resources/Audio.");
                return null;
            }
            _cache[key] = clip;
            return clip;
        }
    }
}

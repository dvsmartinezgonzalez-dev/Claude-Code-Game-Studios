using System.Collections;
using UnityEngine;

namespace BoltSort.Audio
{
    /// <summary>
    /// DDOL singleton for all game audio. Reads bs.music_on / bs.sfx_on from
    /// PlayerPrefs on Awake and applies immediately. Clips loaded from
    /// Resources/Audio/ — place WAV or OGG files there to register them.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        private const string MusicKey = "bs.music_on";
        private const string SfxKey   = "bs.sfx_on";

        // Integer 0–8 volume steps (Settings popup). 0 = muted.
        private const string MusicVolKey = "MusicVolume";
        private const string SfxVolKey   = "SFXVolume";
        private const float  MusicBaseVolume = 0.20f;
        private const float  SfxBaseVolume   = 1.0f;

        public static AudioManager Instance { get; private set; }

        private AudioSource _musicSource;
        private AudioSource _sfxSource;

        private bool  _musicEnabled = true;
        private bool  _sfxEnabled   = true;
        private float _sfxScalar    = 1.0f;   // 0..1 multiplier derived from SFXVolume step

        // Cached clips — loaded from Resources/Audio/ in Awake
        private AudioClip _boltPick;
        private AudioClip _boltPlace;
        private AudioClip _boltInvalid;
        private AudioClip _levelWin;
        private AudioClip _buttonTap;
        private AudioClip _bgmMain;
        private AudioClip _sceneWhoosh;

        // New real-asset clips
        private AudioClip _tubeCompleted;
        private AudioClip _nextLevel;
        private AudioClip _coinSfx;
        private AudioClip _extraBonus;
        private AudioClip _startLevel;
        private AudioClip _buttonSound;
        private AudioClip _boltsortTitle;
        private AudioClip _thunder;        // Undo thunder effect SFX

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            CreateAudioSources();
            LoadClips();

            int musicStep = PlayerPrefs.GetInt(MusicVolKey, PlayerPrefs.GetInt(MusicKey, 1) == 1 ? 8 : 0);
            int sfxStep   = PlayerPrefs.GetInt(SfxVolKey,   PlayerPrefs.GetInt(SfxKey,   1) == 1 ? 8 : 0);
            ApplyMusicStep(musicStep);
            ApplySfxStep(sfxStep);
        }

        // ── 0–8 step volume (Settings popup) ────────────────────────────────────────

        /// <summary>Maps a 0–8 step to a linear scalar: 0 → -80 dB (mute), 1–8 → -20…0 dB.</summary>
        private static float StepToScalar(int step)
        {
            if (step <= 0) return 0f;
            float db = Mathf.Lerp(-20f, 0f, (step - 1) / 7f);
            return Mathf.Pow(10f, db / 20f);
        }

        private void ApplyMusicStep(int step)
        {
            _musicEnabled = step > 0;
            if (_musicSource != null) _musicSource.volume = MusicBaseVolume * StepToScalar(step);
            if (_musicSource != null)
            {
                if (_musicEnabled) { if (!_musicSource.isPlaying && _musicSource.clip != null) _musicSource.Play(); }
                else _musicSource.Pause();
            }
        }

        private void ApplySfxStep(int step)
        {
            _sfxEnabled = step > 0;
            _sfxScalar  = SfxBaseVolume * StepToScalar(step);
        }

        /// <summary>Sets music volume to a 0–8 step, persists it, and applies immediately.</summary>
        public void SetMusicVolume(int step)
        {
            step = Mathf.Clamp(step, 0, 8);
            PlayerPrefs.SetInt(MusicVolKey, step);
            PlayerPrefs.SetInt(MusicKey, step > 0 ? 1 : 0);
            PlayerPrefs.Save();
            ApplyMusicStep(step);
        }

        /// <summary>Sets SFX volume to a 0–8 step, persists it, and applies immediately.</summary>
        public void SetSFXVolume(int step)
        {
            step = Mathf.Clamp(step, 0, 8);
            PlayerPrefs.SetInt(SfxVolKey, step);
            PlayerPrefs.SetInt(SfxKey, step > 0 ? 1 : 0);
            PlayerPrefs.Save();
            ApplySfxStep(step);
        }

        private void CreateAudioSources()
        {
            _musicSource            = gameObject.AddComponent<AudioSource>();
            _musicSource.loop       = true;
            _musicSource.volume     = 0.20f;
            _musicSource.playOnAwake = false;

            _sfxSource              = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop         = false;
            _sfxSource.volume       = 1.0f;
            _sfxSource.playOnAwake  = false;
        }

        private void LoadClips()
        {
            _boltPick       = Resources.Load<AudioClip>("Audio/bolt_pick");
            _boltPlace      = Resources.Load<AudioClip>("Audio/bolt_place");
            _boltInvalid    = Resources.Load<AudioClip>("Audio/bolt_invalid");
            _levelWin       = Resources.Load<AudioClip>("Audio/level_win");
            _sceneWhoosh    = Resources.Load<AudioClip>("Audio/scene_whoosh") ?? GenerateWhoosh();

            _tubeCompleted  = Resources.Load<AudioClip>("Audio/tube_completed");
            _nextLevel      = Resources.Load<AudioClip>("Audio/next_level");
            _coinSfx        = Resources.Load<AudioClip>("Audio/coin");
            _extraBonus     = Resources.Load<AudioClip>("Audio/extra_bonus");
            _startLevel     = Resources.Load<AudioClip>("Audio/start_level");
            _buttonSound    = Resources.Load<AudioClip>("Audio/button_sound_2");
            _boltsortTitle  = Resources.Load<AudioClip>("Audio/boltsort_title");
            _thunder        = Resources.Load<AudioClip>("Audio/thunder");

            // Real music loop replaces procedural fallback
            _bgmMain        = Resources.Load<AudioClip>("Audio/music_loop") ?? GenerateBGM();
            // button_tap is now button_sound_2; keep old name working
            _buttonTap      = _buttonSound ?? Resources.Load<AudioClip>("Audio/button_tap");
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void PlaySFX(AudioClip clip)
        {
            if (!_sfxEnabled || clip == null) return;
            _sfxSource.PlayOneShot(clip, _sfxScalar);
        }

        public void PlaySFX(string clipName)
        {
            if (!_sfxEnabled) return;
            (AudioClip clip, float vol) = clipName switch
            {
                "bolt_pick"       => (_boltPick,      1.00f),
                "bolt_place"      => (_boltPlace,     1.00f),
                "bolt_invalid"    => (_boltInvalid,   1.00f),
                "level_win"       => (_levelWin,      1.00f),
                "button_tap"      => (_buttonTap,     1.00f),
                "scene_whoosh"    => (_sceneWhoosh,   1.00f),
                "tube_completed"  => (_tubeCompleted, 1.00f),
                "next_level"      => (_nextLevel,     1.00f),
                "coin"            => (_coinSfx,       1.00f),
                "extra_bonus"     => (_extraBonus,    1.00f),
                "start_level"     => (_startLevel,    0.82f),
                "button_sound"    => (_buttonSound,   1.00f),
                "boltsort_title"  => (_boltsortTitle, 1.00f),
                "thunder"         => (_thunder,       1.00f),
                _                 => (null,           1.00f),
            };
            if (clip != null) _sfxSource.PlayOneShot(clip, vol * _sfxScalar);
        }

        public void PlayMusic(AudioClip clip = null)
        {
            AudioClip target = clip ?? _bgmMain;
            if (target == null) return;
            if (_musicSource.clip == target && _musicSource.isPlaying) return;
            _musicSource.clip = target;
            if (_musicEnabled) _musicSource.Play();
        }

        public void StopMusic() => _musicSource.Stop();

        public void SetMusicEnabled(bool enabled)
        {
            _musicEnabled = enabled;
            if (enabled)
            {
                if (!_musicSource.isPlaying && _musicSource.clip != null)
                    _musicSource.Play();
            }
            else
            {
                _musicSource.Pause();
            }
        }

        public void SetSFXEnabled(bool enabled) => _sfxEnabled = enabled;

        /// <summary>Plays next_level after 0.75s (avoids overlap), then coin after 80% of next_level's duration.</summary>
        public void PlayVictorySequence()
        {
            if (!_sfxEnabled) return;
            const float startDelay = 0.75f;
            StartCoroutine(PlayDelayedScaled(_nextLevel, startDelay, 1.3f));
            float coinDelay = startDelay + (_nextLevel != null ? _nextLevel.length * 0.80f : 1.0f);
            StartCoroutine(PlayDelayed(_coinSfx, coinDelay));
        }

        private IEnumerator PlayDelayed(AudioClip clip, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_sfxEnabled && clip != null) _sfxSource.PlayOneShot(clip, _sfxScalar);
        }

        private IEnumerator PlayDelayedScaled(AudioClip clip, float delay, float volumeScale)
        {
            yield return new WaitForSeconds(delay);
            if (_sfxEnabled && clip != null) _sfxSource.PlayOneShot(clip, volumeScale * _sfxScalar);
        }

        // Procedural BGM: soft ambient pad using layered sine waves in C major (8-second loop)
        private static AudioClip GenerateBGM()
        {
            const int   sampleRate = 44100;
            const float duration   = 8.0f;
            int         samples    = (int)(sampleRate * duration);
            var         clip       = AudioClip.Create("bgm_proc", samples, 1, sampleRate, false);
            var         data       = new float[samples];

            // C major chord tones (Hz): root, major third, fifth, octave
            float[] freqs  = { 65.41f, 98.00f, 130.81f, 164.81f, 196.00f };
            float[] amps   = { 0.28f,  0.18f,  0.22f,   0.14f,   0.12f   };

            for (int i = 0; i < samples; i++)
            {
                float t    = (float)i / sampleRate;
                float norm = (float)i / samples; // 0..1
                // Gentle amplitude envelope: smooth ramp in/out for seamless loop
                float env = norm < 0.05f ? norm / 0.05f
                          : norm > 0.95f ? (1f - norm) / 0.05f
                          : 1f;
                // Slow tremolo (0.25 Hz) adds subtle life
                float tremolo = 1f + 0.04f * Mathf.Sin(2f * Mathf.PI * 0.25f * t);

                float sample = 0f;
                for (int f = 0; f < freqs.Length; f++)
                {
                    // Slight detuning per partial for warmth
                    float detune = 1f + (f % 2 == 0 ? 0.0015f : -0.0015f);
                    sample += amps[f] * Mathf.Sin(2f * Mathf.PI * freqs[f] * detune * t);
                }
                data[i] = Mathf.Clamp(sample * env * tremolo, -1f, 1f);
            }
            clip.SetData(data, 0);
            return clip;
        }

        // Procedural whoosh: bandpass-filtered white noise, 300ms, fade in/out
        private static AudioClip GenerateWhoosh()
        {
            const int   sampleRate = 44100;
            const float duration   = 0.30f;
            int samples = (int)(sampleRate * duration);
            var clip    = AudioClip.Create("scene_whoosh_proc", samples, 1, sampleRate, false);
            var data    = new float[samples];

            // Biquad lowpass at 800 Hz, Q=0.7 (approximates 200–800 Hz bandpass)
            float w0    = 2f * Mathf.PI * 800f / sampleRate;
            float cosW0 = Mathf.Cos(w0), sinW0 = Mathf.Sin(w0);
            float alph  = sinW0 / (2f * 0.7f);
            float b0    = (1f - cosW0) * 0.5f;
            float b1    = 1f - cosW0;
            float b2    = b0;
            float a0    = 1f + alph;
            float a1    = -2f * cosW0 / a0;
            float a2    = (1f - alph) / a0;
            b0 /= a0; b1 /= a0; b2 /= a0;

            float x1 = 0f, x2 = 0f, y1 = 0f, y2 = 0f;
            for (int i = 0; i < samples; i++)
            {
                float t   = (float)i / samples;
                float env = Mathf.Sin(t * Mathf.PI) * 0.55f;
                float x0  = UnityEngine.Random.Range(-1f, 1f) * env;
                float y0  = b0 * x0 + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x0; y2 = y1; y1 = y0;
                data[i] = Mathf.Clamp(y0, -1f, 1f);
            }
            clip.SetData(data, 0);
            return clip;
        }
    }
}

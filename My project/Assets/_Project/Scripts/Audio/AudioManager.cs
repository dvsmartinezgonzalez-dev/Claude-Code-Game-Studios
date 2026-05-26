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

        public static AudioManager Instance { get; private set; }

        private AudioSource _musicSource;
        private AudioSource _sfxSource;

        private bool _musicEnabled = true;
        private bool _sfxEnabled   = true;

        // Cached clips — loaded from Resources/Audio/ in Awake
        private AudioClip _boltPick;
        private AudioClip _boltPlace;
        private AudioClip _boltInvalid;
        private AudioClip _levelWin;
        private AudioClip _buttonTap;
        private AudioClip _bgmMain;

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

            _musicEnabled = PlayerPrefs.GetInt(MusicKey, 1) == 1;
            _sfxEnabled   = PlayerPrefs.GetInt(SfxKey,   1) == 1;
        }

        private void CreateAudioSources()
        {
            _musicSource            = gameObject.AddComponent<AudioSource>();
            _musicSource.loop       = true;
            _musicSource.volume     = 0.4f;
            _musicSource.playOnAwake = false;

            _sfxSource              = gameObject.AddComponent<AudioSource>();
            _sfxSource.loop         = false;
            _sfxSource.volume       = 1.0f;
            _sfxSource.playOnAwake  = false;
        }

        private void LoadClips()
        {
            _boltPick    = Resources.Load<AudioClip>("Audio/bolt_pick");
            _boltPlace   = Resources.Load<AudioClip>("Audio/bolt_place");
            _boltInvalid = Resources.Load<AudioClip>("Audio/bolt_invalid");
            _levelWin    = Resources.Load<AudioClip>("Audio/level_win");
            _buttonTap   = Resources.Load<AudioClip>("Audio/button_tap");
            _bgmMain     = Resources.Load<AudioClip>("Audio/bgm_main");
        }

        // ── Public API ────────────────────────────────────────────────────────────

        public void PlaySFX(AudioClip clip)
        {
            if (!_sfxEnabled || clip == null) return;
            _sfxSource.PlayOneShot(clip);
        }

        public void PlaySFX(string clipName)
        {
            if (!_sfxEnabled) return;
            AudioClip clip = clipName switch
            {
                "bolt_pick"    => _boltPick,
                "bolt_place"   => _boltPlace,
                "bolt_invalid" => _boltInvalid,
                "level_win"    => _levelWin,
                "button_tap"   => _buttonTap,
                _              => null,
            };
            if (clip != null) _sfxSource.PlayOneShot(clip);
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
    }
}

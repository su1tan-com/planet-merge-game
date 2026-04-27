using UnityEngine;

/// <summary>
/// Handles background music and sound effects.
/// Music clip loaded from Resources/Audio/Midnight_Geometry.
/// Settings (music on/off, sound on/off) are saved in PlayerPrefs.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    // ── State ─────────────────────────────────────────────────────
    public bool MusicEnabled { get; private set; } = true;
    public bool SoundEnabled { get; private set; } = true;

    private AudioSource _music;

    private const string KeyMusic = "PlanetMerge_Music";
    private const string KeySound = "PlanetMerge_Sound";

    // ─────────────────────────────────────────────────────────────
    /// <summary>Auto-creates the AudioManager on scene load — no scene setup needed.</summary>
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (Instance == null)
        {
            var go = new GameObject("AudioManager");
            go.AddComponent<AudioManager>();
        }
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Load saved preferences (default = enabled)
        MusicEnabled = PlayerPrefs.GetInt(KeyMusic, 1) == 1;
        SoundEnabled = PlayerPrefs.GetInt(KeySound, 1) == 1;
    }

    void Start()
    {
        _music              = gameObject.AddComponent<AudioSource>();
        _music.loop         = true;
        _music.volume       = 0.45f;
        _music.playOnAwake  = false;

        var clip = Resources.Load<AudioClip>("Audio/Midnight_Geometry");
        if (clip != null)
        {
            _music.clip = clip;
            if (MusicEnabled) _music.Play();
        }
        else
        {
            Debug.LogWarning("AudioManager: could not find Resources/Audio/Midnight_Geometry");
        }
    }

    // ─────────────────────────────────────────────────────────────
    #region Public API

    public void SetMusic(bool on)
    {
        MusicEnabled = on;
        PlayerPrefs.SetInt(KeyMusic, on ? 1 : 0);
        PlayerPrefs.Save();

        if (on)  { if (!_music.isPlaying) _music.Play(); }
        else       _music.Pause();
    }

    /// <summary>Pause music during game over / win (doesn't change the user's enabled preference).</summary>
    public void PauseMusic()  { if (_music != null) _music.Pause(); }

    /// <summary>Resume music on restart (only if user hasn't disabled it).</summary>
    public void ResumeMusic() { if (_music != null && MusicEnabled && !_music.isPlaying) _music.Play(); }

    public void SetSound(bool on)
    {
        SoundEnabled = on;
        PlayerPrefs.SetInt(KeySound, on ? 1 : 0);
        PlayerPrefs.Save();
    }

    /// <summary>Play a one-shot clip at the given position (respects SoundEnabled).</summary>
    public void PlaySFX(AudioClip clip, float volume = 1f)
    {
        if (!SoundEnabled || clip == null) return;
        AudioSource.PlayClipAtPoint(clip, Camera.main ? Camera.main.transform.position : Vector3.zero, volume);
    }

    #endregion
}

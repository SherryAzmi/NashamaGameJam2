using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[Serializable]
public class MusicTrack
{
    public string trackName;
    public AudioClip clip;
}

[Serializable]
public class DecisiveActionSound
{
    public DecisiveAction action;
    public AudioClip clip;
}

// Single place to assign every sound in the game. Unlike SaveManager/
// GameProgressManager, this one is NOT auto-created in code - it needs
// Inspector-assigned AudioClip references, which only exist on a real
// GameObject placed in a scene. Add one "AudioManager" GameObject with
// this script to IntroScene (the very first scene that loads), assign
// every clip there, and Awake()'s DontDestroyOnLoad keeps it alive for
// every other scene for the rest of the session. Every scene/script that
// needs a sound just calls AudioManager.Instance.PlayX() - nothing else to
// wire per-scene. Button clicks specifically need zero manual wiring at
// all: every Button in every loaded scene is auto-hooked to play the click
// sound (see HookButtonsInScene below).
[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    private static AudioManager instance;
    public static AudioManager Instance => instance;

    [Header("One-shot SFX - assign once here")]
    public AudioClip clickSfx;
    public AudioClip jordanGoalSfx;
    public AudioClip opponentGoalSfx;
    public AudioClip trainingSfx;

    [Header("Music playlist - assign once here")]
    [Tooltip("Played in order; when one track ends the next one starts automatically (wrapping back to the first).")]
    public List<MusicTrack> musicTracks = new List<MusicTrack>();

    [Header("Decision sounds - one per action, assign once here")]
    [Tooltip("One clip per DecisiveAction (Shoot, Pass, Dribble, Through Ball, Long Ball, Tackle, Block, Press, Cover). Plays the instant the player picks that option, regardless of success/fail.")]
    public List<DecisiveActionSound> decisiveActionSounds = new List<DecisiveActionSound>();

    private const string MasterVolumeKey = "NASHAMA_MASTER_VOLUME";
    private const string MusicVolumeKey = "NASHAMA_MUSIC_VOLUME";
    private const string SfxVolumeKey = "NASHAMA_SFX_VOLUME";
    private const string TrackIndexKey = "NASHAMA_TRACK_INDEX";

    private AudioSource musicSource;
    private AudioSource sfxSource;
    private bool musicShouldBeActive;

    public float MasterVolume { get; private set; } = 1f;
    public float MusicVolume { get; private set; } = 0.7f;
    public float SfxVolume { get; private set; } = 1f;
    public int CurrentTrackIndex { get; private set; }

    // Settings panel listens to this to refresh sliders/track list without
    // every caller needing to know who else might care.
    public event Action OnSettingsChanged;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.loop = false;
        musicSource.playOnAwake = false;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        LoadSettings();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        HookButtonsInScene(SceneManager.GetActiveScene());

        if (musicTracks.Count > 0)
        {
            PlayTrack(Mathf.Clamp(CurrentTrackIndex, 0, musicTracks.Count - 1));
        }
    }

    private void Update()
    {
        // No AudioSource.loop here on purpose - when the current track
        // finishes playing, isPlaying drops to false and this advances to
        // the next one, wrapping back to the start of the list.
        if (musicShouldBeActive && musicSource != null && !musicSource.isPlaying)
        {
            PlayTrack(CurrentTrackIndex + 1);
        }
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        HookButtonsInScene(scene);
    }

    // Finds every Button in the given scene (including inactive ones, e.g.
    // panels that start hidden) and wires the click sound onto it exactly
    // once, marked via a tiny tag component so re-scans (additive loads,
    // dynamically spawned UI) never double-subscribe the same button.
    private void HookButtonsInScene(Scene scene)
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (Button button in buttons)
        {
            if (button == null || button.gameObject.scene != scene)
            {
                continue;
            }

            if (button.GetComponent<ClickSoundHooked>() != null)
            {
                continue;
            }

            button.onClick.AddListener(PlayClick);
            button.gameObject.AddComponent<ClickSoundHooked>();
        }
    }

    public void PlayClick()
    {
        PlaySfx(clickSfx);
    }

    public void PlayGoal(MatchSide scoringSide)
    {
        PlaySfx(scoringSide == MatchSide.Home ? jordanGoalSfx : opponentGoalSfx);
    }

    public void PlayTraining()
    {
        PlaySfx(trainingSfx);
    }

    public void PlayDecisiveAction(DecisiveAction action)
    {
        foreach (DecisiveActionSound entry in decisiveActionSounds)
        {
            if (entry.action == action)
            {
                PlaySfx(entry.clip);
                return;
            }
        }
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null || sfxSource == null)
        {
            return;
        }

        sfxSource.PlayOneShot(clip, MasterVolume * SfxVolume);
    }

    public void PlayTrack(int index)
    {
        if (musicTracks.Count == 0)
        {
            musicShouldBeActive = false;
            return;
        }

        index = ((index % musicTracks.Count) + musicTracks.Count) % musicTracks.Count;
        CurrentTrackIndex = index;

        MusicTrack track = musicTracks[index];

        if (track.clip != null)
        {
            musicSource.clip = track.clip;
            musicSource.volume = MasterVolume * MusicVolume;
            musicSource.Play();
            musicShouldBeActive = true;
        }
        else
        {
            musicShouldBeActive = false;
        }

        PlayerPrefs.SetInt(TrackIndexKey, index);
        PlayerPrefs.Save();
        OnSettingsChanged?.Invoke();
    }

    public string GetTrackName(int index)
    {
        if (index < 0 || index >= musicTracks.Count)
        {
            return "";
        }

        return string.IsNullOrWhiteSpace(musicTracks[index].trackName)
            ? "TRACK " + (index + 1)
            : musicTracks[index].trackName;
    }

    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        ApplyMusicVolume();
        PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
        PlayerPrefs.Save();
        OnSettingsChanged?.Invoke();
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        ApplyMusicVolume();
        PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
        PlayerPrefs.Save();
        OnSettingsChanged?.Invoke();
    }

    public void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
        PlayerPrefs.Save();
        OnSettingsChanged?.Invoke();
    }

    private void ApplyMusicVolume()
    {
        if (musicSource != null)
        {
            musicSource.volume = MasterVolume * MusicVolume;
        }
    }

    private void LoadSettings()
    {
        MasterVolume = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        MusicVolume = PlayerPrefs.GetFloat(MusicVolumeKey, 0.7f);
        SfxVolume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
        CurrentTrackIndex = PlayerPrefs.GetInt(TrackIndexKey, 0);
    }
}

// Empty marker so HookButtonsInScene never wires the same Button twice.
public class ClickSoundHooked : MonoBehaviour
{
}

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Lives in HomeScene. One settings panel: master/music/sfx volume sliders,
// a list of music tracks to pick from, and the "New Career" reset (wipes
// the save and every persisted manager via CampaignRestartService, behind
// a confirmation step so it can't be hit by accident).
public class SettingsPanelController : MonoBehaviour
{
    [Header("Panel")]
    public GameObject settingsPanel;
    public Button openSettingsButton;
    public Button closeSettingsButton;

    [Header("Volume sliders (0-1)")]
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;

    [Header("Track list")]
    [Tooltip("Parent that track buttons get spawned into.")]
    public Transform trackListContent;
    [Tooltip("Prefab with a Button component and a TMP_Text child for the track name.")]
    public GameObject trackButtonPrefab;
    public TMP_Text nowPlayingText;

    [Header("New career (delete save, start over)")]
    public Button newCareerButton;
    public GameObject confirmRestartPanel;
    public Button confirmRestartButton;
    public Button cancelRestartButton;

    private void Start()
    {
        if (openSettingsButton != null)
        {
            openSettingsButton.onClick.AddListener(OpenSettings);
        }

        if (closeSettingsButton != null)
        {
            closeSettingsButton.onClick.AddListener(CloseSettings);
        }

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
        }

        if (newCareerButton != null)
        {
            newCareerButton.onClick.AddListener(ShowRestartConfirm);
        }

        if (confirmRestartButton != null)
        {
            confirmRestartButton.onClick.AddListener(ConfirmRestart);
        }

        if (cancelRestartButton != null)
        {
            cancelRestartButton.onClick.AddListener(HideRestartConfirm);
        }

        if (settingsPanel != null)
        {
            settingsPanel.SetActive(false);
        }

        if (confirmRestartPanel != null)
        {
            confirmRestartPanel.SetActive(false);
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.OnSettingsChanged += RefreshFromAudioManager;
        }

        BuildTrackList();
        RefreshFromAudioManager();
    }

    private void OnDestroy()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.OnSettingsChanged -= RefreshFromAudioManager;
        }
    }

    private void OpenSettings()
    {
        settingsPanel.SetActive(true);
    }

    private void CloseSettings()
    {
        settingsPanel.SetActive(false);
    }

    private void OnMasterVolumeChanged(float value)
    {
        AudioManager.Instance?.SetMasterVolume(value);
    }

    private void OnMusicVolumeChanged(float value)
    {
        AudioManager.Instance?.SetMusicVolume(value);
    }

    private void OnSfxVolumeChanged(float value)
    {
        AudioManager.Instance?.SetSfxVolume(value);
    }

    private void BuildTrackList()
    {
        if (trackListContent == null || trackButtonPrefab == null || AudioManager.Instance == null)
        {
            return;
        }

        for (int i = trackListContent.childCount - 1; i >= 0; i--)
        {
            Destroy(trackListContent.GetChild(i).gameObject);
        }

        List<MusicTrack> tracks = AudioManager.Instance.musicTracks;

        for (int i = 0; i < tracks.Count; i++)
        {
            int trackIndex = i;

            GameObject buttonObject = Instantiate(trackButtonPrefab, trackListContent);
            buttonObject.SetActive(true);
            TMP_Text label = buttonObject.GetComponentInChildren<TMP_Text>();

            if (label != null)
            {
                label.text = AudioManager.Instance.GetTrackName(trackIndex);
            }

            Button button = buttonObject.GetComponent<Button>();

            if (button != null)
            {
                button.onClick.AddListener(() => SelectTrack(trackIndex));
            }
        }
    }

    private void SelectTrack(int trackIndex)
    {
        AudioManager.Instance?.PlayTrack(trackIndex);
    }

    private void RefreshFromAudioManager()
    {
        if (AudioManager.Instance == null)
        {
            return;
        }

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.MasterVolume);
        }

        if (musicVolumeSlider != null)
        {
            musicVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.MusicVolume);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.SetValueWithoutNotify(AudioManager.Instance.SfxVolume);
        }

        if (nowPlayingText != null)
        {
            nowPlayingText.text = "NOW PLAYING: " + AudioManager.Instance.GetTrackName(AudioManager.Instance.CurrentTrackIndex);
        }
    }

    private void ShowRestartConfirm()
    {
        confirmRestartPanel.SetActive(true);
    }

    private void HideRestartConfirm()
    {
        confirmRestartPanel.SetActive(false);
    }

    // Wipes the save file and every persisted manager, then reloads
    // HomeScene as if the game had just been installed fresh.
    private void ConfirmRestart()
    {
        CampaignRestartService.RestartWholeCampaign();
    }
}

using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Slider))]
public class LocalMusicVolumeSlider : MonoBehaviour
{
    [Tooltip("Assign the GameObject that has the AudioSource (AudioManager).")]
    public GameObject audioManager;

    [Tooltip("Name of PlayerPrefs key to save/load volume (optional).")]
    public string prefsKey = "MusicVolume";

    [Range(0f, 1f)]
    public float defaultVolume = 1f;

    private Slider slider;
    private AudioSource audioSource;

    void Awake()
    {
        slider = GetComponent<Slider>();
        if (slider == null)
        {
            Debug.LogError("LocalMusicVolumeSlider requires a Slider component.");
            enabled = false;
            return;
        }

        if (audioManager == null)
        {
            Debug.LogError("AudioManager GameObject not assigned on LocalMusicVolumeSlider.");
            enabled = false;
            return;
        }

        audioSource = audioManager.GetComponent<AudioSource>();
        if (audioSource == null)
        {
            Debug.LogError("Assigned AudioManager does not have an AudioSource component.");
            enabled = false;
            return;
        }

        // Load saved volume or use default
        float saved = PlayerPrefs.HasKey(prefsKey) ? PlayerPrefs.GetFloat(prefsKey) : defaultVolume;
        saved = Mathf.Clamp01(saved);

        // Initialize slider and audio source
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = saved;
        audioSource.volume = saved;

        // Subscribe to slider change
        slider.onValueChanged.AddListener(OnSliderChanged);
    }

    void OnDestroy()
    {
        if (slider != null)
            slider.onValueChanged.RemoveListener(OnSliderChanged);
    }

    private void OnSliderChanged(float value)
    {
        if (audioSource != null)
            audioSource.volume = Mathf.Clamp01(value);

        if (!string.IsNullOrEmpty(prefsKey))
            PlayerPrefs.SetFloat(prefsKey, audioSource.volume);
    }
}

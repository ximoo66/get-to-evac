using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlayerEndScreenUI : MonoBehaviour
{
    [Header("Death UI")]
    [SerializeField] private GameObject deathRoot;
    [SerializeField] private Image deathImage;
    [SerializeField] private TMP_Text deathText;

    [Header("Victory UI")]
    [SerializeField] private GameObject victoryRoot;
    [SerializeField] private Image victoryImage;
    [SerializeField] private TMP_Text victoryText;

    [Header("Audio (local UI)")]
    [SerializeField] private AudioSource uiAudioSource;   // 2D AudioSource on the Canvas
    [SerializeField] private AudioClip deathClip;
    [SerializeField] private AudioClip victoryClip;

    private void Awake()
    {
        HideAll();
    }

    public void HideAll()
    {
        if (deathRoot != null) deathRoot.SetActive(false);
        if (victoryRoot != null) victoryRoot.SetActive(false);
    }

    public void ShowDeath(string message)
    {
        HideAll();

        if (deathRoot != null) deathRoot.SetActive(true);
        if (deathText != null) deathText.text = message;

        Play(deathClip);
    }

    public void ShowVictory(string message)
    {
        HideAll();

        if (victoryRoot != null) victoryRoot.SetActive(true);
        if (victoryText != null) victoryText.text = message;

        Play(victoryClip);
    }

    private void Play(AudioClip clip)
    {
        if (uiAudioSource == null) return;
        if (clip == null) return;

        uiAudioSource.PlayOneShot(clip);
    }
}

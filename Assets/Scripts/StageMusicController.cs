using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio; // optional if you use AudioMixer

[RequireComponent(typeof(AudioSource))]
public class StageMusicController : MonoBehaviour
{
    [System.Serializable]
    public class StageAudio
    {
        public int stageIndex;

        [Header("Music")]
        public AudioClip musicToPlay;

        [Header("Stage Intro SFX")]
        public List<AudioClip> introSounds = new List<AudioClip>();

        [Tooltip("Delay between each sound")]
        public float delayBetweenSounds = 0.4f;

        [Tooltip("Play sounds in random order")]
        public bool randomizeOrder = false;
    }

    [Header("Default Music")]
    [SerializeField] private AudioClip introMusic;

    [Header("Per Stage Audio")]
    [SerializeField] private List<StageAudio> stageAudios = new List<StageAudio>();

    private ZombieSpawner spawner;

    private AudioSource audioSource;
    private Coroutine musicRoutine;

    // PUBLIC volume so your slider can set this (0..1). Changes are applied to audioSource.volume.
    [Range(0f, 1f)]
    public float baseMusicVolume = 1f;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.volume = Mathf.Clamp01(baseMusicVolume);
    }

    private void OnEnable()
    {
        ZombieSpawner.OnStageStarted += HandleStageStarted;
    }

    private void OnDisable()
    {
        ZombieSpawner.OnStageStarted -= HandleStageStarted;
    }

    private void Start()
    {
        PlayMusic(introMusic);
        spawner = FindFirstObjectByType<ZombieSpawner>();

        if (spawner != null)
        {
            spawner.CurrentStageIndex.OnValueChanged += OnStageIndexChanged;

            if (spawner.CurrentStageIndex.Value >= 0)
                OnStageIndexChanged(-1, spawner.CurrentStageIndex.Value);
        }
    }

    // Call this from your slider script to change volume locally
    public void SetVolume(float v)
    {
        baseMusicVolume = Mathf.Clamp01(v);
        audioSource.volume = baseMusicVolume;
    }

    void HandleStageStarted(ZombieSpawner.StageInfo info)
    {
        StageAudio config = stageAudios.Find(s => s.stageIndex == info.Index);
        if (config == null) return;

        if (musicRoutine != null)
        {
            StopCoroutine(musicRoutine);
            musicRoutine = null;
        }

        musicRoutine = StartCoroutine(PlayStageSequence(config));
    }

    IEnumerator PlayStageSequence(StageAudio config)
    {
        List<AudioClip> sounds = new List<AudioClip>(config.introSounds);

        if (config.randomizeOrder)
            Shuffle(sounds);

        foreach (var clip in sounds)
        {
            if (clip == null) continue;

            audioSource.PlayOneShot(clip);
            yield return new WaitForSeconds(Mathf.Max(0.01f, config.delayBetweenSounds));
        }

        if (config.musicToPlay != null)
            yield return StartCoroutine(FadeTo(config.musicToPlay, 1.5f));

        musicRoutine = null;
    }

    // --------------------------------------------------
    // MUSIC CONTROL
    // --------------------------------------------------

    void PlayMusic(AudioClip clip)
    {
        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.volume = baseMusicVolume;
        audioSource.Play();
    }

    IEnumerator FadeTo(AudioClip newClip, float duration)
    {
        duration = Mathf.Max(0.01f, duration);
        float startVol = baseMusicVolume;

        // fade out towards 0 while preserving baseMusicVolume (so slider can still update)
        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            float frac = t / duration;
            audioSource.volume = Mathf.Lerp(startVol, 0f, frac);
            yield return null;
        }

        audioSource.clip = newClip;
        audioSource.Play();

        for (float t = 0; t < duration; t += Time.deltaTime)
        {
            float frac = t / duration;
            audioSource.volume = Mathf.Lerp(0f, startVol, frac);
            yield return null;
        }

        audioSource.volume = startVol;
    }

    // --------------------------------------------------
    // UTIL
    // --------------------------------------------------

    void Shuffle(List<AudioClip> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rand = Random.Range(i, list.Count);
            (list[i], list[rand]) = (list[rand], list[i]);
        }
    }

    void OnStageIndexChanged(int oldValue, int newValue)
    {
        var info = new ZombieSpawner.StageInfo(newValue, $"Stage {newValue}", 0);
        HandleStageStarted(info);
    }

    private void OnDestroy()
    {
        if (spawner != null)
            spawner.CurrentStageIndex.OnValueChanged -= OnStageIndexChanged;
    }
}

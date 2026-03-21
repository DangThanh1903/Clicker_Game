using System.Collections.Generic;
using UnityEngine;

public class SoundEffectController : MonoBehaviour
{
    public static SoundEffectController Ins { get; private set; }

    private const string SFXVolumeKey = "SFXVolume";
    private const float MinPitch = 0.1f;
    private const float MaxPitch = 3f;

    [Header("Audio Source")]
    [SerializeField] private AudioSource sfxSource;

    [Header("Clips")]
    [SerializeField] private List<AudioClip> clips;
    private Dictionary<string, AudioClip> clipDict;

    [Header("Settings")]
    [Range(0f, 1f)] public float sfxVolume = 1f;
    public bool randomizePitch = true;
    [Range(0.8f, 1.2f)] public float pitchRange = 0.1f;

    private void Awake()
    {
        if (Ins != null && Ins != this)
        {
            Destroy(gameObject);
            return;
        }
        Ins = this;
        DontDestroyOnLoad(gameObject);

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        sfxVolume = PlayerPrefs.GetFloat(SFXVolumeKey, 1f);

        clipDict = new Dictionary<string, AudioClip>();
        foreach (var clip in clips)
        {
            if (clip != null && !clipDict.ContainsKey(clip.name))
                clipDict.Add(clip.name, clip);
        }
    }

    public void SetVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);

        // Save
        PlayerPrefs.SetFloat(SFXVolumeKey, sfxVolume);
        PlayerPrefs.Save();
    }

    public bool PlaySFX(string key)
    {
        return PlaySFX(key, 1f, applyRandomPitchOffset: true);
    }

    public bool PlaySFX(string key, float basePitch, bool applyRandomPitchOffset)
    {
        if (!clipDict.TryGetValue(key, out var clip))
        {
            Debug.LogWarning($"SFX '{key}' not found!");
            return false;
        }

        sfxSource.pitch = ResolvePitch(basePitch, applyRandomPitchOffset);

        sfxSource.PlayOneShot(clip, sfxVolume);
        return true;
    }

    public void PlaySFX(AudioClip clip)
    {
        PlaySFX(clip, 1f, applyRandomPitchOffset: true);
    }

    public void PlaySFX(AudioClip clip, float basePitch, bool applyRandomPitchOffset)
    {
        if (clip == null) return;

        sfxSource.pitch = ResolvePitch(basePitch, applyRandomPitchOffset);

        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    private float ResolvePitch(float basePitch, bool applyRandomPitchOffset)
    {
        float pitch = Mathf.Clamp(basePitch, MinPitch, MaxPitch);
        if (applyRandomPitchOffset && randomizePitch)
            pitch += Random.Range(-pitchRange, pitchRange);
        return Mathf.Clamp(pitch, MinPitch, MaxPitch);
    }
}

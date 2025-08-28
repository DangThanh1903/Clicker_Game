using System.Collections.Generic;
using UnityEngine;

public class SoundEffectController : MonoBehaviour
{
    public static SoundEffectController Ins { get; private set; }

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

        clipDict = new Dictionary<string, AudioClip>();
        foreach (var clip in clips)
        {
            if (clip != null && !clipDict.ContainsKey(clip.name))
                clipDict.Add(clip.name, clip);
        }
    }

    public bool PlaySFX(string key)
    {
        if (!clipDict.TryGetValue(key, out var clip))
        {
            Debug.LogWarning($"SFX '{key}' not found!");
            return false;
        }

        if (randomizePitch)
        {
            float pitch = 1f + Random.Range(-pitchRange, pitchRange);
            sfxSource.pitch = pitch;
        }
        else
        {
            sfxSource.pitch = 1f;
        }

        sfxSource.PlayOneShot(clip, sfxVolume);
        return true;
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;

        if (randomizePitch)
        {
            float pitch = 1f + Random.Range(-pitchRange, pitchRange);
            sfxSource.pitch = pitch;
        }
        else
        {
            sfxSource.pitch = 1f;
        }

        sfxSource.PlayOneShot(clip, sfxVolume);
    }

    public void SetVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
    }
}

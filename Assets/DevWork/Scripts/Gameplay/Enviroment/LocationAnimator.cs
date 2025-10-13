using UnityEngine;
using UniRx;
using System;
using System.Collections;

#if DOTWEEN_ENABLED
using DG.Tweening;
#endif

public class LocationAnimator : MonoBehaviour
{
    [Header("Groups")]
    [Tooltip("All transforms that count as 'land' (will move UP to original local position).")]
    public Transform[] landGroup;
    [Tooltip("All other props (will move DOWN to original local position).")]
    public Transform[] otherGroup;

    [Header("Movement (local space)")]
    [Min(0f)] public float spawnTime   = 0.45f;
    [Min(0f)] public float despawnTime = 0.35f;
    [Tooltip("How far to offset land BELOW at spawn (local Y).")]
    public float landOffsetY   = 2.0f;
    [Tooltip("How far to offset others ABOVE at spawn (local Y).")]
    public float otherOffsetY  = 2.0f;

    [Header("Optional Fade (if present)")]
    public CanvasGroup fadeGroup;
    [Range(0f,1f)] public float fadeInPercent  = 0.8f;  // portion of spawnTime used to fade in
    [Range(0f,1f)] public float fadeOutPercent = 0.9f;  // portion of despawnTime used to fade out

    [Header("VFX / SFX (optional)")]
    public ParticleSystem[] spawnVFX;
    public ParticleSystem[] despawnVFX;
    public AudioSource audioSource;
    public AudioClip spawnSfx;
    public AudioClip despawnSfx;

    // Cache original local positions
    private Vector3[] _landOriginals;
    private Vector3[] _otherOriginals;

    void Awake()
    {
        // Cache originals once
        _landOriginals  = new Vector3[landGroup?.Length  ?? 0];
        _otherOriginals = new Vector3[otherGroup?.Length ?? 0];

        for (int i = 0; i < _landOriginals.Length;  i++) if (landGroup[i])  _landOriginals[i]  = landGroup[i].localPosition;
        for (int i = 0; i < _otherOriginals.Length; i++) if (otherGroup[i]) _otherOriginals[i] = otherGroup[i].localPosition;

        // Optional auto-detect CanvasGroup
        if (!fadeGroup) fadeGroup = GetComponent<CanvasGroup>();
    }

    // ---------- SPAWN ----------
    public IObservable<Unit> PlaySpawn()
    {
        var done = new AsyncSubject<Unit>();

        // Set start positions
        for (int i = 0; i < _landOriginals.Length; i++)
            if (landGroup[i]) landGroup[i].localPosition = _landOriginals[i] + new Vector3(0f, -Mathf.Abs(landOffsetY), 0f);

        for (int i = 0; i < _otherOriginals.Length; i++)
            if (otherGroup[i]) otherGroup[i].localPosition = _otherOriginals[i] + new Vector3(0f,  Mathf.Abs(otherOffsetY), 0f);

        if (fadeGroup) fadeGroup.alpha = 0f;

        PlayVFX(spawnVFX);
        PlaySfx(spawnSfx);

#if DOTWEEN_ENABLED
        var seq = DOTween.Sequence();

        // Land up
        for (int i = 0; i < _landOriginals.Length; i++)
            if (landGroup[i]) seq.Join(landGroup[i].DOLocalMove(_landOriginals[i], spawnTime).SetEase(Ease.OutCubic));

        // Others down
        for (int i = 0; i < _otherOriginals.Length; i++)
            if (otherGroup[i]) seq.Join(otherGroup[i].DOLocalMove(_otherOriginals[i], spawnTime).SetEase(Ease.OutCubic));

        // Fade
        if (fadeGroup) seq.Join(fadeGroup.DOFade(1f, spawnTime * Mathf.Clamp01(fadeInPercent)));

        seq.OnComplete(() => { done.OnNext(Unit.Default); done.OnCompleted(); });
#else
        StartCoroutine(CoSpawn(done));
#endif
        return done;
    }

    // ---------- DESPAWN ----------
    public IObservable<Unit> PlayDespawn()
    {
        var done = new AsyncSubject<Unit>();

        PlayVFX(despawnVFX);
        PlaySfx(despawnSfx);

#if DOTWEEN_ENABLED
        var seq = DOTween.Sequence();

        // Reverse directions:
        // Land goes back DOWN, Others go back UP by the same offsets
        for (int i = 0; i < _landOriginals.Length; i++)
            if (landGroup[i])
                seq.Join(landGroup[i].DOLocalMove(_landOriginals[i] + new Vector3(0f, -Mathf.Abs(landOffsetY), 0f), despawnTime).SetEase(Ease.InCubic));

        for (int i = 0; i < _otherOriginals.Length; i++)
            if (otherGroup[i])
                seq.Join(otherGroup[i].DOLocalMove(_otherOriginals[i] + new Vector3(0f,  Mathf.Abs(otherOffsetY), 0f), despawnTime).SetEase(Ease.InCubic));

        if (fadeGroup) seq.Join(fadeGroup.DOFade(0f, despawnTime * Mathf.Clamp01(fadeOutPercent)));

        seq.OnComplete(() => { done.OnNext(Unit.Default); done.OnCompleted(); });
#else
        StartCoroutine(CoDespawn(done));
#endif
        return done;
    }

#if !DOTWEEN_ENABLED
    private IEnumerator CoSpawn(AsyncSubject<Unit> done)
    {
        float t = 0f;

        while (t < spawnTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / spawnTime);
            float ease = 1f - Mathf.Pow(1f - k, 3f); // OutCubic

            // Land up
            for (int i = 0; i < _landOriginals.Length; i++)
                if (landGroup[i])
                    landGroup[i].localPosition = Vector3.Lerp(
                        _landOriginals[i] + new Vector3(0f, -Mathf.Abs(landOffsetY), 0f),
                        _landOriginals[i],
                        ease
                    );

            // Others down
            for (int i = 0; i < _otherOriginals.Length; i++)
                if (otherGroup[i])
                    otherGroup[i].localPosition = Vector3.Lerp(
                        _otherOriginals[i] + new Vector3(0f,  Mathf.Abs(otherOffsetY), 0f),
                        _otherOriginals[i],
                        ease
                    );

            if (fadeGroup) fadeGroup.alpha = Mathf.Lerp(0f, 1f, Mathf.Clamp01(k / Mathf.Max(0.0001f, fadeInPercent)));
            yield return null;
        }

        done.OnNext(Unit.Default); done.OnCompleted();
    }

    private IEnumerator CoDespawn(AsyncSubject<Unit> done)
    {
        float t = 0f;

        while (t < despawnTime)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / despawnTime);
            float ease = k * k * k; // InCubic

            // Land down
            for (int i = 0; i < _landOriginals.Length; i++)
                if (landGroup[i])
                    landGroup[i].localPosition = Vector3.Lerp(
                        _landOriginals[i],
                        _landOriginals[i] + new Vector3(0f, -Mathf.Abs(landOffsetY), 0f),
                        ease
                    );

            // Others up
            for (int i = 0; i < _otherOriginals.Length; i++)
                if (otherGroup[i])
                    otherGroup[i].localPosition = Vector3.Lerp(
                        _otherOriginals[i],
                        _otherOriginals[i] + new Vector3(0f,  Mathf.Abs(otherOffsetY), 0f),
                        ease
                    );

            if (fadeGroup) fadeGroup.alpha = 1f - Mathf.Clamp01(k / Mathf.Max(0.0001f, fadeOutPercent));
            yield return null;
        }

        done.OnNext(Unit.Default); done.OnCompleted();
    }
#endif

    private void PlayVFX(ParticleSystem[] list)
    {
        if (list == null) return;
        foreach (var ps in list) if (ps) ps.Play();
    }

    private void PlaySfx(AudioClip clip)
    {
        if (!audioSource || !clip) return;
        audioSource.PlayOneShot(clip);
    }
}

using UnityEngine;
using Lean.Pool;
using System.Collections;

[RequireComponent(typeof(CanvasGroup))]
public class AutoFadeDespawn : MonoBehaviour
{
    [Header("Timing")]
    public float lifetime = 5f;        // Tổng thời gian tồn tại
    public float fadeDuration = 0.5f;  // Thời gian mờ dần trước khi despawn
    public bool useUnscaledTime = true;// Bỏ qua Time.timeScale khi pause

    CanvasGroup cg;
    Coroutine co;

    void Awake()
    {
        cg = GetComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        if (co != null) StopCoroutine(co);
        cg.alpha = 1f;
        co = StartCoroutine(Run());
    }

    void OnDisable()
    {
        if (co != null) { StopCoroutine(co); co = null; }
        if (cg) cg.alpha = 1f; // reset alpha cho lần spawn sau
    }

    IEnumerator Run()
    {
        float dt() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        // Giữ nguyên trước khi fade
        float hold = Mathf.Max(0f, lifetime - Mathf.Max(0.01f, fadeDuration));
        for (float t = 0f; t < hold; t += dt()) yield return null;

        // Fade dần
        if (fadeDuration > 0f)
        {
            for (float t = 0f; t < fadeDuration; t += dt())
            {
                cg.alpha = 1f - (t / fadeDuration);
                yield return null;
            }
        }

        LeanPool.Despawn(gameObject);
    }

    // Gọi thủ công nếu muốn despawn ngay lập tức
    public void DespawnNow() => LeanPool.Despawn(gameObject);
}

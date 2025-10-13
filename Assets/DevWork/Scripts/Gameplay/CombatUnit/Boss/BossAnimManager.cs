using UnityEngine;
using DG.Tweening;
using System;

public class BossAnimManager : MonoBehaviour
{
    [Header("Visual root (only visuals, not collider)")]
    public Transform modelRoot;

    [Header("Skills")]
    public BossSkillDef normalSkill;
    public BossSkillDef specialSkill;

    public event Action<string> OnSkillFired; // moment to apply effects (e.g., debuff/heal)

    private Sequence _playing;
    private float _nextNormal;
    private float _nextSpecial;
    [Serializable] public struct LocalPose { public Vector3 pos, scale; public Quaternion rot; }
    private LocalPose _basePose;

    void OnDisable() => Kill();
    void Awake()
    {
        CacheBasePoseFromCurrent();
    }

    public void CacheBasePoseFromCurrent()
    {
        _basePose.pos = modelRoot.localPosition;
        _basePose.scale = modelRoot.localScale;
        _basePose.rot = modelRoot.localRotation;
    }

    public bool TryPlayNormal()
    {
        if (!normalSkill) return false;
        if (Time.time < _nextNormal) return false;

        Play(normalSkill);
        _nextNormal = Time.time + Mathf.Max(0f, normalSkill.cooldown);
        return true;
    }

    public bool TryPlaySpecial()
    {
        if (!specialSkill) return false;
        if (Time.time < _nextSpecial) return false;

        Play(specialSkill);
        _nextSpecial = Time.time + Mathf.Max(0f, specialSkill.cooldown);
        return true;
    }

    private void Play(BossSkillDef def)
    {
        Kill(); // ensure clean state
        _playing = def.Build(modelRoot, _basePose, () => OnSkillFired?.Invoke(def.skillId));
        _playing.Play();
    }

    private void Kill()
    {
        if (_playing != null) { _playing.Kill(); _playing = null; }
        if (!modelRoot) return;

        modelRoot.DOKill();
        // reset to cached base pose (NOT zeros)
        modelRoot.localPosition = _basePose.pos;
        modelRoot.localRotation = _basePose.rot;
        modelRoot.localScale    = _basePose.scale;
    }

    /// <summary>Optional helper to reset CDs (e.g., on spawn).</summary>
    public void ResetCooldowns(float normalReadyIn = 0f, float specialReadyIn = 0f)
    {
        _nextNormal = Time.time + normalReadyIn;
        _nextSpecial = Time.time + specialReadyIn;
    }
}

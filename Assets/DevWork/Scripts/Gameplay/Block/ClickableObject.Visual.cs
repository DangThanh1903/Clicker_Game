using DG.Tweening;
using UnityEngine;

public partial class ClickableObject
{
    private void OnAppear()
    {
        blockSpawnTime = Time.unscaledTime;
        KillDeathFlowTween();
        animCtrl?.StopAll();

        CacheBaseAliveScale();
        transform.localScale = GetAliveScaleForHealth(CurrentHealth.Value);

        if (animCtrl != null)
            animCtrl.PlaySpawn(() => animCtrl.TryPlayIdle(), playAnimation: true);
    }

    private void OnDisappear()
    {
        StopAura();
        isDyingEffect = true;
        breakFinalized = false;
        KillDeathFlowTween();
        float timeToBreak = Mathf.Max(0f, Time.unscaledTime - blockSpawnTime);
        AnalyticsManager.Ins?.TrackBlockBreak(blockName, GetLocationString(), timeToBreak);

        RunGrowThenExplodeFlow();
    }

    private void UpdateCrackVisual(float currentHP)
    {
        if (crackMeshRenderer == null)
            return;

        float healthPercent = currentHP / MaxHealth;
        int crackIndex = Mathf.FloorToInt((1f - healthPercent) * crackLevels);
        crackIndex = Mathf.Clamp(crackIndex, 0, crackLevels - 1);

        crackMeshRenderer.GetPropertyBlock(crackPropertyBlock);
        crackPropertyBlock.SetFloat(CrackIndexID, crackIndex);
        crackMeshRenderer.SetPropertyBlock(crackPropertyBlock);

        if (!isDyingEffect && MaxHealth > 0f)
            transform.localScale = GetAliveScaleForHealth(currentHP);
    }

    public float GetDestroyBlockAnimTime() => 0f;

    private void PlayHittingSound()
    {
        float hitPitch = ResolveHitPitch();
        if (!SoundEffectController.Ins.PlaySFX(blockName + "Breaking", hitPitch, applyRandomPitchOffset: false))
            SoundEffectController.Ins.PlaySFX("Hit", hitPitch, applyRandomPitchOffset: false);
    }

    private void PlayBreakedSound()
    {
        float value = UnityEngine.Random.Range(0f, 1f);
        if (value <= 0.9f)
            SoundEffectController.Ins.PlaySFX("Break");
        else if (value <= 0.95f)
            SoundEffectController.Ins.PlaySFX("Fart");
        else
            SoundEffectController.Ins.PlaySFX("Ack");
    }

    private void CacheBaseAliveScale()
    {
        if (baseBlockScale > 0.001f)
            baseAliveScale = Vector3.one * baseBlockScale;
        else if (authoredBaseScale.sqrMagnitude > 0.0001f)
            baseAliveScale = authoredBaseScale;
        else
            baseAliveScale = Vector3.one;

        if (!isDyingEffect)
            transform.localScale = baseAliveScale;
    }

    private void KillDeathFlowTween()
    {
        deathFlowTween?.Kill();
        deathFlowTween = null;
    }

    private Vector3 GetAliveScaleForHealth(float currentHP)
    {
        if (MaxHealth <= 0f)
            return baseAliveScale;

        float hp = Mathf.Clamp(currentHP, 0f, MaxHealth);
        float damage01 = 1f - (hp / MaxHealth);
        float curveT = Mathf.Pow(Mathf.Clamp01(damage01), Mathf.Max(1f, nearDeathGrowthExponent));
        float minMul = Mathf.Clamp(fullHealthScaleMultiplier, 0.05f, growNearDeathMaxScale);
        float maxMul = Mathf.Max(minMul, growNearDeathMaxScale);
        float scaleMul = Mathf.Lerp(minMul, maxMul, curveT);
        return baseAliveScale * scaleMul;
    }

    private void RunGrowThenExplodeFlow()
    {
        KillDeathFlowTween();
        Vector3 burstScale = GetAliveScaleForHealth(CurrentHealth.Value) * Mathf.Max(1f, growThenExplodeBurstScale);
        deathFlowTween = transform
            .DOScale(burstScale, Mathf.Max(0.01f, growThenExplodeBurstDuration))
            .SetEase(growThenExplodeBurstEase)
            .OnComplete(() =>
            {
                animCtrl?.PlayDeath();
                FinalizeBreak();
            });
    }

    private float ResolveHitPitch()
    {
        if (!scaleHitPitchByRemainingHealth || MaxHealth <= 0f || CurrentHealth == null)
            return 1f;

        float hp01 = Mathf.Clamp01(CurrentHealth.Value / MaxHealth);
        float nearDeath01 = 1f - hp01;
        float curved = Mathf.Pow(nearDeath01, Mathf.Max(0.1f, hitPitchCurvePower));
        return Mathf.Lerp(hitPitchAtFullHealth, hitPitchAtZeroHealth, curved);
    }

    private void UpdateAuraFromBlock()
    {
        if (!enableAura || blockUVDatabase == null || string.IsNullOrWhiteSpace(blockName))
        {
            StopAura();
            return;
        }

        if (!TryResolveAuraColor(out Color auraColor))
        {
            StopAura();
            return;
        }

        float blockWeight = Mathf.Max(0f, BlockWeight);
        float glowIntensity = blockUVDatabase.GetGlowIntensity(blockName);
        bool shouldShowAura = blockWeight <= auraWeightThreshold;
        if (!shouldShowAura)
        {
            StopAura();
            return;
        }

        if (!ResolveAuraView())
        {
            StopAura();
            return;
        }

        float glowScore = Mathf.Clamp01(glowIntensity / Mathf.Max(0.0001f, auraGlowAtMaxIntensity));
        float intensity = Mathf.Lerp(auraMinIntensity, auraMaxIntensity, glowScore);

        auraView.SetState(true, auraColor, intensity);
    }

    private bool ResolveAuraView()
    {
        if (auraView != null)
            return true;

        auraView = GetComponentInChildren<BlockAuraController>(true);
        if (auraView == null)
            return false;

        auraView.Hide();
        return true;
    }

    private void StopAura()
    {
        if (ResolveAuraView())
            auraView.Hide();
    }

    private bool TryResolveAuraColor(out Color auraColor)
    {
        auraColor = currentOutlineColor;
        auraColor.a = Mathf.Clamp01(auraColor.a);
        return auraColor.a > 0.0001f && auraColor.maxColorComponent >= auraMinimumVisibleColor;
    }
}

using System.Collections;
using Lean.Pool;
using UnityEngine;

public sealed class PlayerCombatVfxService
{
    private readonly MonoBehaviour coroutineHost;
    private static readonly int AttackTriggerHash = Animator.StringToHash("Attack");

    private GameObject activeHoldBeamObject;
    private HoldBeamVFX activeHoldBeam;

    private GameObject activeIdlePetObject;
    private GameObject activeIdlePetPrefab;
    private IIdlePetAttackFeedback activeIdlePetFeedback;
    private Animator activeIdlePetAnimatorFallback;

    public PlayerCombatVfxService(MonoBehaviour host)
    {
        coroutineHost = host;
    }

    public void OnHold(Pickaxe equippedPickaxe, Transform holdBeamOrigin, Vector3 holdPoint)
    {
        EnsureHoldBeam(equippedPickaxe, holdBeamOrigin);
        UpdateHoldBeamPositions(holdPoint);
    }

    public bool UpdateHoldBeamLifecycle(bool isDead, bool isHoldState, bool pointerHeld, float combatNow, float lastHoldUpdateTime)
    {
        if (activeHoldBeamObject == null)
            return false;

        bool invalidHold =
            isDead ||
            !isHoldState ||
            !pointerHeld;

        // Small tolerance avoids despawn/spawn jitter when one raycast frame is missed.
        if (!invalidHold && combatNow - lastHoldUpdateTime > 0.15f)
            invalidHold = true;

        if (!invalidHold)
            return false;

        StopHoldBeam();
        return true;
    }

    public void HandleStateChanged(Pickaxe equippedPickaxe, ClickerState state, bool isDead, Transform idlePetAnchor, Transform fallbackAnchor)
    {
        if (state is not HoldState)
            StopHoldBeam();

        RefreshIdlePetVisual(equippedPickaxe, state is IdleState, isDead, idlePetAnchor, fallbackAnchor);
    }

    public void HandleEquippedPickaxeCleared()
    {
        StopHoldBeam();
        StopIdlePetVisual();
    }

    public void NotifyIdleDamageDealt(bool isDead, bool isIdleState, float damage, Vector3 targetWorldPosition)
    {
        if (isDead || !isIdleState)
            return;
        if (activeIdlePetObject == null)
            return;

        if (activeIdlePetFeedback == null && activeIdlePetAnimatorFallback == null)
            CacheIdlePetFeedbackRefs();

        if (activeIdlePetFeedback != null)
        {
            activeIdlePetFeedback.PlayIdleAttack(Mathf.Max(0f, damage), targetWorldPosition);
            return;
        }

        if (activeIdlePetAnimatorFallback != null)
            activeIdlePetAnimatorFallback.SetTrigger(AttackTriggerHash);
    }

    public void ResetImmediate()
    {
        StopHoldBeam(immediate: true);
        StopIdlePetVisual(immediate: true);
    }

    private void RefreshIdlePetVisual(
        Pickaxe equippedPickaxe,
        bool isIdleState,
        bool isDead,
        Transform idlePetAnchor,
        Transform fallbackAnchor)
    {
        bool shouldShow =
            !isDead &&
            equippedPickaxe != null &&
            equippedPickaxe.Type != ItemType.None &&
            isIdleState &&
            equippedPickaxe.IdlePetVisualPrefab != null;

        if (!shouldShow)
        {
            StopIdlePetVisual();
            return;
        }

        GameObject prefab = equippedPickaxe.IdlePetVisualPrefab;
        Transform anchor = idlePetAnchor != null ? idlePetAnchor : fallbackAnchor;
        if (anchor == null)
        {
            StopIdlePetVisual();
            return;
        }

        if (activeIdlePetObject == null || activeIdlePetPrefab != prefab)
        {
            StopIdlePetVisual(immediate: true);
            activeIdlePetObject = LeanPool.Spawn(prefab, anchor.position, anchor.rotation, anchor);

            // Ensure pooled instance starts exactly at anchor local origin.
            Transform spawnedPetTransform = activeIdlePetObject.transform;
            if (spawnedPetTransform.parent != anchor)
                spawnedPetTransform.SetParent(anchor, false);
            spawnedPetTransform.localPosition = Vector3.zero;
            spawnedPetTransform.localEulerAngles = equippedPickaxe.IdlePetSpawnLocalEuler;

            activeIdlePetPrefab = prefab;
            CacheIdlePetFeedbackRefs();
            RefreshIdlePetLookYawBase();
        }

        if (activeIdlePetObject == null)
            return;

        Transform petTransform = activeIdlePetObject.transform;
        if (petTransform.parent != anchor)
            petTransform.SetParent(anchor, false);
        petTransform.localPosition = Vector3.zero;
        petTransform.localEulerAngles = equippedPickaxe.IdlePetSpawnLocalEuler;
        RefreshIdlePetLookYawBase();
    }

    private void StopIdlePetVisual(bool immediate = false)
    {
        if (activeIdlePetObject == null)
        {
            activeIdlePetPrefab = null;
            activeIdlePetFeedback = null;
            activeIdlePetAnimatorFallback = null;
            return;
        }

        GameObject petToDespawn = activeIdlePetObject;
        activeIdlePetObject = null;
        activeIdlePetPrefab = null;
        activeIdlePetFeedback = null;
        activeIdlePetAnimatorFallback = null;

        if (immediate)
            LeanPool.Despawn(petToDespawn);
        else
            LeanPool.Despawn(petToDespawn);
    }

    private void CacheIdlePetFeedbackRefs()
    {
        activeIdlePetFeedback = null;
        activeIdlePetAnimatorFallback = null;

        if (activeIdlePetObject == null)
            return;

        var petBehaviours = activeIdlePetObject.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < petBehaviours.Length; i++)
        {
            if (activeIdlePetFeedback == null && petBehaviours[i] is IIdlePetAttackFeedback feedback)
                activeIdlePetFeedback = feedback;
        }

        if (activeIdlePetFeedback == null)
            activeIdlePetAnimatorFallback = activeIdlePetObject.GetComponentInChildren<Animator>(true);
    }

    private void RefreshIdlePetLookYawBase()
    {
        if (activeIdlePetFeedback is IdlePetAttackFeedback feedback)
            feedback.RefreshLookYawBaseFromCurrentPose();
    }

    private void EnsureHoldBeam(Pickaxe equippedPickaxe, Transform holdBeamOrigin)
    {
        if (activeHoldBeamObject != null) return;
        if (equippedPickaxe == null) return;
        if (equippedPickaxe.HoldBeamVfxPrefab == null) return;

        activeHoldBeamObject = LeanPool.Spawn(equippedPickaxe.HoldBeamVfxPrefab);
        activeHoldBeam = activeHoldBeamObject.GetComponent<HoldBeamVFX>();
        Vector3 start = GetHoldBeamStartPosition(equippedPickaxe, holdBeamOrigin);

        if (activeHoldBeam != null)
        {
            activeHoldBeam.Begin(start);
        }
        else
        {
            Debug.LogWarning("[PlayerCombatVfxService] Hold beam prefab is missing HoldBeamVFX component.");
        }
    }

    private void UpdateHoldBeamPositions(Vector3 endPoint)
    {
        if (activeHoldBeamObject == null)
            return;

        if (activeHoldBeam != null)
            activeHoldBeam.SetEndPoint(endPoint);
    }

    private Vector3 GetHoldBeamStartPosition(Pickaxe equippedPickaxe, Transform holdBeamOrigin)
    {
        Transform origin = holdBeamOrigin;

        if (origin == null && Camera.main != null)
            origin = Camera.main.transform;

        if (origin == null)
            return Vector3.zero;

        Vector3 offset = equippedPickaxe != null ? equippedPickaxe.HoldBeamStartOffset : Vector3.zero;
        return origin.position + origin.TransformDirection(offset);
    }

    private void StopHoldBeam(bool immediate = false)
    {
        if (activeHoldBeamObject == null)
        {
            activeHoldBeam = null;
            return;
        }

        GameObject beamToStop = activeHoldBeamObject;
        HoldBeamVFX beamVfx = activeHoldBeam;

        activeHoldBeamObject = null;
        activeHoldBeam = null;

        if (immediate || beamVfx == null)
        {
            LeanPool.Despawn(beamToStop);
            return;
        }

        beamVfx.EndBeam();
        float delay = beamVfx.EndDespawnDelay;
        if (delay <= 0f)
        {
            LeanPool.Despawn(beamToStop);
            return;
        }

        if (coroutineHost != null)
            coroutineHost.StartCoroutine(DespawnHoldBeamAfterDelay(beamToStop, delay));
        else
            LeanPool.Despawn(beamToStop);
    }

    private static IEnumerator DespawnHoldBeamAfterDelay(GameObject beamObject, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        if (beamObject != null)
            LeanPool.Despawn(beamObject);
    }
}

using Lean.Pool;
using UnityEngine;

public sealed class PlayerCombatVfxService
{
    private static readonly int AttackTriggerHash = Animator.StringToHash("Attack");

    private GameObject activePetObject;
    private GameObject activePetPrefab;
    private IPetAutoAttackFeedback activePetFeedback;
    private Animator activePetAnimatorFallback;

    public PlayerCombatVfxService() { }

    public void HandleStateChanged(
        PetItem equippedPet,
        bool isAutoCombatMode,
        bool isDead,
        Transform petVisualAnchor,
        Transform fallbackAnchor)
    {
        RefreshPetVisual(equippedPet, isAutoCombatMode, isDead, petVisualAnchor, fallbackAnchor);
    }

    public void NotifyAutoAttackDamageDealt(bool isDead, bool isAutoCombatMode, float damage, Vector3 targetWorldPosition)
    {
        if (isDead || !isAutoCombatMode)
            return;
        if (activePetObject == null)
            return;

        if (activePetFeedback == null && activePetAnimatorFallback == null)
            CachePetFeedbackRefs();

        if (activePetFeedback != null)
        {
            activePetFeedback.PlayAutoAttack(Mathf.Max(0f, damage), targetWorldPosition);
            return;
        }

        if (activePetAnimatorFallback != null)
            activePetAnimatorFallback.SetTrigger(AttackTriggerHash);
    }

    public void ResetImmediate()
    {
        StopPetVisual(immediate: true);
    }

    private void RefreshPetVisual(
        PetItem equippedPet,
        bool isAutoCombatMode,
        bool isDead,
        Transform petVisualAnchor,
        Transform fallbackAnchor)
    {
        bool shouldShow =
            !isDead &&
            equippedPet != null &&
            equippedPet.Type == ItemType.Pet &&
            isAutoCombatMode &&
            equippedPet.PetVisualPrefab != null;

        if (!shouldShow)
        {
            StopPetVisual();
            return;
        }

        GameObject prefab = equippedPet.PetVisualPrefab;
        Transform anchor = petVisualAnchor != null ? petVisualAnchor : fallbackAnchor;
        if (anchor == null)
        {
            StopPetVisual();
            return;
        }

        if (activePetObject == null || activePetPrefab != prefab)
        {
            StopPetVisual(immediate: true);
            activePetObject = LeanPool.Spawn(prefab, anchor.position, anchor.rotation, anchor);

            // Ensure pooled instance starts exactly at anchor local origin.
            Transform spawnedPetTransform = activePetObject.transform;
            if (spawnedPetTransform.parent != anchor)
                spawnedPetTransform.SetParent(anchor, false);
            spawnedPetTransform.localPosition = Vector3.zero;
            spawnedPetTransform.localEulerAngles = equippedPet.PetSpawnLocalEuler;

            activePetPrefab = prefab;
            CachePetFeedbackRefs();
            RefreshPetLookYawBase();
        }

        if (activePetObject == null)
            return;

        Transform petTransform = activePetObject.transform;
        if (petTransform.parent != anchor)
            petTransform.SetParent(anchor, false);
        petTransform.localPosition = Vector3.zero;
        petTransform.localEulerAngles = equippedPet.PetSpawnLocalEuler;
        RefreshPetLookYawBase();
    }

    private void StopPetVisual(bool immediate = false)
    {
        if (activePetObject == null)
        {
            activePetPrefab = null;
            activePetFeedback = null;
            activePetAnimatorFallback = null;
            return;
        }

        GameObject petToDespawn = activePetObject;
        activePetObject = null;
        activePetPrefab = null;
        activePetFeedback = null;
        activePetAnimatorFallback = null;

        if (immediate)
            LeanPool.Despawn(petToDespawn);
        else
            LeanPool.Despawn(petToDespawn);
    }

    private void CachePetFeedbackRefs()
    {
        activePetFeedback = null;
        activePetAnimatorFallback = null;

        if (activePetObject == null)
            return;

        var petBehaviours = activePetObject.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < petBehaviours.Length; i++)
        {
            if (activePetFeedback == null && petBehaviours[i] is IPetAutoAttackFeedback feedback)
                activePetFeedback = feedback;
        }

        if (activePetFeedback == null)
            activePetAnimatorFallback = activePetObject.GetComponentInChildren<Animator>(true);
    }

    private void RefreshPetLookYawBase()
    {
        if (activePetFeedback is IdlePetAttackFeedback feedback)
            feedback.RefreshLookYawBaseFromCurrentPose();
    }
}

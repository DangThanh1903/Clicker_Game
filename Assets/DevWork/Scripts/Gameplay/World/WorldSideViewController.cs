using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class WorldSideViewController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private Transform sideViewAnchor;

    [Header("Timing")]
    [SerializeField, Min(0.01f)] private float transitionDuration = 0.35f;
    [SerializeField] private Ease transitionEase = Ease.OutCubic;
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool rotateOnly = true;
    [SerializeField] private bool useFixedYawOffset = true;
    [SerializeField] private float sideYawDegrees = 40f;
    [SerializeField] private bool lookAtAnchorPosition = true;

    [Header("State")]
    [SerializeField] private bool startInCombatMode = true;
    [SerializeField] private bool cacheCombatPoseOnEnter = true;

    private Vector3 cachedCombatPosition;
    private Quaternion cachedCombatRotation;
    private bool hasCachedCombatPose;
    private Tween activeTransition;

    private void Start()
    {
        if (startInCombatMode)
            WorldViewModeRuntime.SetMode(WorldViewMode.Combat);
    }

    private void OnDisable()
    {
        if (activeTransition != null)
        {
            activeTransition.Kill(false);
            activeTransition = null;
        }

        WorldViewModeRuntime.SetMode(WorldViewMode.Combat);
    }

    public void ToggleSideView()
    {
        if (WorldViewModeRuntime.CurrentMode == WorldViewMode.SideView)
        {
            ExitSideView();
            return;
        }

        if (WorldViewModeRuntime.CurrentMode == WorldViewMode.Combat)
            EnterSideView();
    }

    public void EnterSideView()
    {
        Transform cam = ResolveCameraTransform();
        if (cam == null)
            return;

        if (WorldViewModeRuntime.CurrentMode == WorldViewMode.SideView)
            return;

        if (cacheCombatPoseOnEnter || !hasCachedCombatPose)
            CacheCombatPose(cam);

        KillActiveTransition();
        WorldViewModeRuntime.SetMode(WorldViewMode.Transition);

        Sequence seq = DOTween.Sequence().SetUpdate(useUnscaledTime);
        if (!rotateOnly && sideViewAnchor != null)
            seq.Join(cam.DOMove(sideViewAnchor.position, transitionDuration).SetEase(transitionEase));

        seq.Join(cam.DORotateQuaternion(GetSideViewRotation(cam), transitionDuration).SetEase(transitionEase));
        activeTransition = seq.OnComplete(() =>
        {
            activeTransition = null;
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            WorldViewModeRuntime.SetMode(WorldViewMode.SideView);
        });
    }

    public void ExitSideView()
    {
        Transform cam = ResolveCameraTransform();
        if (cam == null)
        {
            WorldViewModeRuntime.SetMode(WorldViewMode.Combat);
            return;
        }

        if (!hasCachedCombatPose)
        {
            WorldViewModeRuntime.SetMode(WorldViewMode.Combat);
            return;
        }

        KillActiveTransition();
        WorldViewModeRuntime.SetMode(WorldViewMode.Transition);

        Sequence seq = DOTween.Sequence().SetUpdate(useUnscaledTime);
        if (!rotateOnly)
            seq.Join(cam.DOMove(cachedCombatPosition, transitionDuration).SetEase(transitionEase));

        seq.Join(cam.DORotateQuaternion(cachedCombatRotation, transitionDuration).SetEase(transitionEase));
        activeTransition = seq.OnComplete(() =>
        {
            activeTransition = null;
            WorldViewModeRuntime.SetMode(WorldViewMode.Combat);
        });
    }

    private Transform ResolveCameraTransform()
    {
        if (cameraTransform != null && cameraTransform.gameObject.activeInHierarchy)
            return cameraTransform;

        Camera cam = Camera.main;
        if (cam != null)
            cameraTransform = cam.transform;

        return cameraTransform;
    }

    private void CacheCombatPose(Transform cam)
    {
        cachedCombatPosition = cam.position;
        cachedCombatRotation = cam.rotation;
        hasCachedCombatPose = true;
    }

    private Quaternion GetSideViewRotation(Transform cam)
    {
        if (useFixedYawOffset)
            return cachedCombatRotation * Quaternion.Euler(0f, sideYawDegrees, 0f);

        if (!lookAtAnchorPosition || sideViewAnchor == null || cam == null)
            return sideViewAnchor != null ? sideViewAnchor.rotation : (cam != null ? cam.rotation : Quaternion.identity);

        Vector3 dir = sideViewAnchor.position - cam.position;
        if (dir.sqrMagnitude <= 0.0001f)
            return sideViewAnchor.rotation;

        return Quaternion.LookRotation(dir.normalized, Vector3.up);
    }

    private void KillActiveTransition()
    {
        if (activeTransition == null)
            return;

        activeTransition.Kill(false);
        activeTransition = null;
    }
}

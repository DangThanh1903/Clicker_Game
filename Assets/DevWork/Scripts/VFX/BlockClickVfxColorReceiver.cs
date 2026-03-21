using UnityEngine;

[DisallowMultipleComponent]
public sealed class BlockClickVfxColorReceiver : MonoBehaviour
{
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [SerializeField] private Material targetMaterial;

    private bool warnedMissingMaterialBinding;
    private bool warnedMissingColorProperties;

    public void ApplyColor(Color color)
    {
        if (targetMaterial == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!warnedMissingMaterialBinding)
            {
                warnedMissingMaterialBinding = true;
                DevLog.Log("[BlockClickVfxColorReceiver] Missing target material binding.", this);
            }
#endif
            return;
        }

        bool hasColor = targetMaterial.HasProperty(ColorId);
        bool hasEmission = targetMaterial.HasProperty(EmissionColorId);
        if (!hasColor && !hasEmission)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!warnedMissingColorProperties)
            {
                warnedMissingColorProperties = true;
                DevLog.Log("[BlockClickVfxColorReceiver] Target material has neither _Color nor _EmissionColor.", this);
            }
#endif
            return;
        }

        color.a = 1f;
        if (hasColor)
            targetMaterial.SetColor(ColorId, color);
        if (hasEmission)
            targetMaterial.SetColor(EmissionColorId, color);
    }
}

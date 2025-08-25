using Lean.Pool; // optional, not required
using UnityEngine;
using UnityEngine.UI;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering.Universal;
#endif

[DisallowMultipleComponent]
public class PerObjectPixelizer : MonoBehaviour
{
    [Header("Hook these up")]
    public Camera mainCamera;           // your real camera
    public RawImage overlay;            // full-screen RawImage in a Canvas

    [Header("Settings")]
    [Range(2, 24)] public int pixelScale = 8; // 8 = each 8x8 screen pixels become one “big pixel”
    public string pixelizedLayerName = "Pixelized";
    public Color clearColor = new Color(0,0,0,0);

    Camera pixCam;
    RenderTexture rt;
    int lastW, lastH, lastScale;

    void Awake()
    {
        if (!mainCamera) mainCamera = Camera.main;

        // Create the pixelizer camera as a child of main (so it matches pose)
        var go = new GameObject("PixelizerCamera");
        go.transform.SetParent(mainCamera.transform, false);
        pixCam = go.AddComponent<Camera>();

        // Match projection
        pixCam.CopyFrom(mainCamera);
        pixCam.cullingMask = LayerMask.GetMask(pixelizedLayerName); // only pixelized layer
        pixCam.clearFlags = CameraClearFlags.SolidColor;
        pixCam.backgroundColor = clearColor;
        pixCam.depth = mainCamera.depth + 1; // not rendering to screen anyway

        // Make sure it doesn't render directly to screen
        pixCam.targetTexture = null;

        // URP camera extras
        #if UNITY_RENDER_PIPELINE_UNIVERSAL
        var urp = pixCam.GetUniversalAdditionalCameraData();
        urp.renderPostProcessing = false;
        urp.antialiasing = AntialiasingMode.None;
        urp.requiresColorTexture = false;
        urp.requiresDepthTexture = false;
        #endif

        if (!overlay)
        {
            // Auto-create a fullscreen Canvas + RawImage overlay (optional convenience)
            var canvasGo = new GameObject("PixelizerCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000; // top-most

            overlay = new GameObject("PixelizerOverlay", typeof(RawImage)).GetComponent<RawImage>();
            overlay.transform.SetParent(canvas.transform, false);
            var rt = overlay.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        RebuildRT();
    }

    void Update()
    {
        // Keep FOV & projection in sync (if you change main cam at runtime)
        if (pixCam && mainCamera)
        {
            pixCam.fieldOfView = mainCamera.fieldOfView;
            pixCam.orthographic = mainCamera.orthographic;
            pixCam.orthographicSize = mainCamera.orthographicSize;
            pixCam.nearClipPlane = mainCamera.nearClipPlane;
            pixCam.farClipPlane = mainCamera.farClipPlane;
        }

        // Recreate RT on resolution or scale change
        if (Screen.width != lastW || Screen.height != lastH || pixelScale != lastScale)
            RebuildRT();
    }

    void RebuildRT()
    {
        if (rt != null)
        {
            pixCam.targetTexture = null;
            rt.Release();
            Destroy(rt);
        }

        int w = Mathf.Max(1, Screen.width  / Mathf.Max(1, pixelScale));
        int h = Mathf.Max(1, Screen.height / Mathf.Max(1, pixelScale));

        rt = new RenderTexture(w, h, 0)
        {
            filterMode = FilterMode.Point,
            antiAliasing = 1,
            useMipMap = false,
            autoGenerateMips = false,
            wrapMode = TextureWrapMode.Clamp
        };

        pixCam.backgroundColor = clearColor;
        pixCam.targetTexture = rt;
        if (overlay) overlay.texture = rt;

        lastW = Screen.width; lastH = Screen.height; lastScale = pixelScale;
    }

    void OnDestroy()
    {
        if (rt != null)
        {
            pixCam.targetTexture = null;
            rt.Release();
            Destroy(rt);
        }
    }
}

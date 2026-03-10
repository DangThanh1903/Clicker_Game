using Sirenix.OdinInspector;
using UnityEngine;
using UniRx;
using System;
using System.Collections.Generic;
using System.Collections;
using DG.Tweening;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ClickableObject : MonoBehaviour, IDamagable
{
    private static readonly Vector3[][] CubeFaces =
    {
        new[] { new Vector3(-0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f,  0.5f), new Vector3(-0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f,  0.5f) }, // Front
        new[] { new Vector3( 0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, -0.5f), new Vector3( 0.5f,  0.5f, -0.5f), new Vector3(-0.5f,  0.5f, -0.5f) }, // Back
        new[] { new Vector3(-0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f,  0.5f), new Vector3(-0.5f,  0.5f, -0.5f), new Vector3( 0.5f,  0.5f, -0.5f) }, // Top
        new[] { new Vector3(-0.5f, -0.5f, -0.5f), new Vector3( 0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f,  0.5f) }, // Bottom
        new[] { new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f,  0.5f), new Vector3(-0.5f,  0.5f, -0.5f), new Vector3(-0.5f,  0.5f,  0.5f) }, // Left
        new[] { new Vector3( 0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f, -0.5f), new Vector3( 0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f, -0.5f) }  // Right
    };
    private static readonly int[] CubeTriangles =
    {
         0,  1,  2,  2,  1,  3,
         4,  5,  6,  6,  5,  7,
         8,  9, 10, 10,  9, 11,
        12, 13, 14, 14, 13, 15,
        16, 17, 18, 18, 17, 19,
        20, 21, 22, 22, 21, 23
    };
    private static Camera cachedMainCamera;
    private static int cachedMainCameraFrame = -1;

    [Header("Information")]
    [ReadOnly, SerializeField]
    private string blockName;
    public string BlockName => blockName;
    public float MaxHealth { get; private set; }
    public ReactiveProperty<float> CurrentHealth { get; private set; } = new ReactiveProperty<float>();
    public float BlockWeight;
    private static readonly int CrackIndexID = Shader.PropertyToID("_CrackIndex");
    private static readonly int ColorID = Shader.PropertyToID("_Color");
    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int ScaleID = Shader.PropertyToID("_Scale");
    private MeshRenderer cubeRenderer;
    private float accumulatedHoldTime = 0f;
    private readonly float timeHoldReset = 0.1f;
    private readonly float timeIdleReset = 1f;
    bool isDyingEffect;
    private bool breakFinalized;

    private bool isMouseHeld = false;

    [Header("Settings")]
    [SerializeField] private int crackLevels = 9;

    [Header("Atlas Settings")]
    [SerializeField] private int atlasColumns = 6;
    [SerializeField] private int atlasRows = 10;
    [SerializeField] private int blockColumns = 3;
    [SerializeField] private int blockRows = 2;
    private bool flipY = true;
    private Vector2Int[] faceTiles = new Vector2Int[6]
    {
    new Vector2Int(0, 0), // Back
    new Vector2Int(1, 0), // Front
    new Vector2Int(2, 0), // Top
    new Vector2Int(0, 1), // Under
    new Vector2Int(1, 1), // Left
    new Vector2Int(2, 1)  // Right
    };

    [Header("Material & Atlas")]
    public Texture2D textureAtlas;
    public UnityEngine.Material cubeMaterial;
    public BlockUVDatabase blockUVDatabase;
    public Texture2D AtlasTexture => textureAtlas;
    public int AtlasColumns => atlasColumns;
    public int AtlasRows => atlasRows;
    public bool AtlasFlipY => flipY;
    [Header("Cracking layer")]
    [SerializeField] private MeshRenderer crackMeshRenderer;
    private MaterialPropertyBlock crackPropertyBlock;
    private MaterialPropertyBlock outlinePropertyBlock;
    [Header("Outline")]
    [SerializeField, Min(0), Tooltip("Material slot index (0-based). Used as fallback when auto-detect cannot find outline material.")]
    private int outlineMaterialIndex = 2;
    [Header("Animation")]
    [SerializeField] private BlockAnimationController animCtrl;
    [SerializeField, Min(1f)] private float baseBlockScale = 2.5f;

    [Header("Death Flow - Grow Then Explode")]
    [SerializeField, Range(0.2f, 1f)] private float fullHealthScaleMultiplier = 0.82f;
    [SerializeField, Min(1f)] private float growNearDeathMaxScale = 1.2f;
    [SerializeField, Min(1f)] private float nearDeathGrowthExponent = 2.2f;
    [SerializeField, Min(1f)] private float growThenExplodeBurstScale = 1.32f;
    [SerializeField, Min(0.01f)] private float growThenExplodeBurstDuration = 0.1f;
    [SerializeField] private Ease growThenExplodeBurstEase = Ease.OutBack;

    private Vector2 onClickPos;
    private float blockSpawnTime;
    private bool isReady;
    private Vector3 authoredBaseScale;
    private Vector3 baseAliveScale;
    private Mesh generatedCubeMesh;
    private MeshFilter meshFilter;
    private readonly Vector2[] cubeUvBuffer = new Vector2[24];
    private Tween deathFlowTween;

    // Internal click buffer and stream
    private readonly Subject<long> clickStream = new Subject<long>();
    private readonly List<long> clickBuffer = new List<long>();
    private const long ClickWindowMs = 1000;
    private CompositeDisposable runtimeSubs;
    void Awake()
    {
        crackPropertyBlock = new MaterialPropertyBlock();
        outlinePropertyBlock = new MaterialPropertyBlock();
        cubeRenderer = GetComponent<MeshRenderer>();
        meshFilter = GetComponent<MeshFilter>();
        if (animCtrl == null) animCtrl = GetComponent<BlockAnimationController>();
        authoredBaseScale = transform.localScale;
        baseAliveScale = Vector3.one * baseBlockScale;
    }

    void OnEnable()
    {
        if (isReady)
            ListenRuntime();
    }

    void OnDisable()
    {
        KillDeathFlowTween();
        runtimeSubs?.Dispose();
        runtimeSubs = null;
        clickBuffer.Clear();

        // If pooled/disabled during death, finalize once so discovery + drops still fire.
        if (isDyingEffect && !breakFinalized)
            FinalizeBreak();
    }

    void OnDestroy()
    {
        if (generatedCubeMesh != null)
        {
            Destroy(generatedCubeMesh);
            generatedCubeMesh = null;
        }
    }

    void Update()
    {
        HandleClickDetection();
    }
    #region SETUP ---------------------------------------------------------------------------------------------
    void ListenRuntime()
    {
        runtimeSubs?.Dispose();
        runtimeSubs = new CompositeDisposable();

        // ⏲️ Reactive CPS update every second
        Observable.Interval(TimeSpan.FromSeconds(1))
            .Subscribe(_ =>
            {
                StatsManager.Ins.Set(StatType.ClickPerTick, GetRecentHitCount());
            })
            .AddTo(runtimeSubs);

        // Push click timestamp into buffer
        clickStream.Subscribe(time =>
        {
            RecordClickTimestamp(time);
        }).AddTo(runtimeSubs);

        // Cracking listen
        CurrentHealth
            .DistinctUntilChanged()
            .Subscribe(newHealth =>
            {
                UpdateCrackVisual(newHealth);
                if (newHealth < MaxHealth)
                    PlayHittingSound();
                if (newHealth <= 0f)
                    OnDisappear();
            })
            .AddTo(runtimeSubs);
    }

    public void SetClickableBlock(string name)
    {
        blockName = name;
        DataSaver.Ins.currentBlock = name;
        MaxHealth = blockUVDatabase.GetHealth(name);
        CurrentHealth.Value = blockUVDatabase.GetHealth(name);
        BlockWeight = blockUVDatabase.GetWeight(name);
        isDyingEffect = false;
        breakFinalized = false;
        accumulatedHoldTime = 0f;
        isReady = true;
        ListenRuntime();
        GenerateCube();
        ApplyOutlineColorFromDatabase();
        OnAppear();
    }
    public void SetClickableBlockByCondition(BlockSpawnLocation blockSpawnLocation, TimeState timeState, NormalWeatherName normalWeatherName, SpecialWeatherName specialWeatherName)
    {
        SetClickableBlock(
            blockUVDatabase.GetRandomBlockByConditions(
            blockSpawnLocation,
            timeState,
            normalWeatherName,
            specialWeatherName,
            StatsManager.Ins.Get(StatType.Lucky)).blockName
        );
    }
    #endregion
    #region CLICK_LOGIC -------------------------------------------------------------------------------------
    // Click logic
    public void HandleClickDetection()
    {
        var player = PlayerController.Instance;
        if (player == null) return;
        player.OnUpdate(this);

        var ui = UIManager.Ins;
        if (ui == null || !ui.IsBlockCanClick()) return;
        if (isDyingEffect) return;
        if (PopupController.Instance != null && PopupController.Instance.IsAnyPopupOpen()) return;

        bool mouseDown = Input.GetMouseButtonDown(0);
        bool mouseHeld = Input.GetMouseButton(0);

        if (mouseHeld)
        {
            if (!isMouseHeld)
                isMouseHeld = true;

            var cam = ResolveMainCamera();
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform)
            {
                onClickPos = GetUIPosition(cam, hit.point);
                if (mouseDown)
                    player.OnClick(this);
                player.OnHold(this, hit.point);
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            if (isMouseHeld)
            {
                isMouseHeld = false;
                StatsManager.Ins.Set(StatType.HoldedTime, 0);
            }
        }
    }

    /// <summary>
    /// Convert world pos -> RectTransform anchored position
    /// </summary>
    private Vector2 GetUIPosition(Camera cam, Vector3 worldPos)
    {
        Vector2 screenPos = cam.WorldToScreenPoint(worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            Toaster.Ins.canvas.transform as RectTransform,
            screenPos,
            Toaster.Ins.canvas.worldCamera,
            out Vector2 localPoint
        );
        return localPoint;
    }


    public void HandleClick()
    {
        float finalDamage = StatsManager.Ins.Get(StatType.NormalPower);
        float power = PlayerController.Instance != null
            ? PlayerController.Instance.ApplyStaminaToFinalDamage(finalDamage)
            : finalDamage;
        TakeDamage(power, "click");
    }
    public void HandleHold()
    {
        var player = PlayerController.Instance;
        float dt = player != null && player.UseUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        accumulatedHoldTime += dt;
        StatsManager.Ins.Add(StatType.HoldedTime, dt);
        if (accumulatedHoldTime >= timeHoldReset)
        {
            float manaMul = player != null
                ? player.GetHoldDamageMultiplier()
                : 1f;
            float power = StatsManager.Ins.Get(StatType.HoldPower) * manaMul * timeHoldReset;
            TakeDamage(power, "hold", timeHoldReset);
            accumulatedHoldTime = 0f;
        }
    }

    public void HandleIdle()
    {
        float idleMul = PlayerController.Instance != null
            ? PlayerController.Instance.GetIdleDamageMultiplier()
            : 1f;
        float power = StatsManager.Ins.Get(StatType.IdlePower) * idleMul * timeIdleReset;
        TakeDamage(power, "idle", timeIdleReset);
        PlayerController.Instance?.NotifyIdleDamageDealt(power, transform.position);
    }

    void TakeDamage(float power, string source, float timeReset = 1)
    {
        if (power <= 0f)
            return;

        long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        clickStream.OnNext(time);

        CurrentHealth.Value = Mathf.Max(0, CurrentHealth.Value - power);

        StatsManager.Ins.Add(StatType.Clicks, 1 * timeReset);
        StatsManager.Ins.Add(StatType.TotalDamageDealed, power);

        AnalyticsManager.Ins?.TrackBlockClick(blockName, GetLocationString(), power, source);

        Toaster.Show($"-{power:F1}", null, 0.2f, onClickPos);

        animCtrl?.PlayClick();
    }

    public int GetRecentHitCount(float windowSeconds = 1f)
    {
        if (clickBuffer.Count == 0)
            return 0;

        long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        long windowMs = Math.Max(50L, (long)Mathf.RoundToInt(Mathf.Max(0.05f, windowSeconds) * 1000f));
        TrimClickBuffer(now, windowMs);
        return clickBuffer.Count;
    }

    void RecordClickTimestamp(long timeMs)
    {
        clickBuffer.Add(timeMs);
        TrimClickBuffer(timeMs, ClickWindowMs);
    }

    void TrimClickBuffer(long nowMs, long windowMs)
    {
        if (windowMs <= 0)
            windowMs = ClickWindowMs;

        int removeCount = 0;
        for (int i = 0; i < clickBuffer.Count; i++)
        {
            if (nowMs - clickBuffer[i] <= windowMs)
                break;
            removeCount++;
        }

        if (removeCount > 0)
            clickBuffer.RemoveRange(0, removeCount);
    }
    void HandleItemDrop()
    {
        string dropBlockName = blockName;
        float luck = StatsManager.Ins != null ? StatsManager.Ins.Get(StatType.Lucky) : 0f;
        var drops = blockUVDatabase.GetDropResultsByName(dropBlockName, luck);
        StartCoroutine(HandleItemDrop_Co(dropBlockName, drops));
    }

    IEnumerator HandleItemDrop_Co(string dropBlockName, List<ItemDropResult> drops)
    {
        if (drops.Count == 0)
        {
            Debug.Log("There is no item drop");
            GameDebugHandler.LogStaticKey(
                "UI_Debug",
                "block_drops_none",
                new { block = dropBlockName }
            );
            yield break;
        }

        if (InventoryController.Instance == null)
        {
            Debug.LogWarning("InventoryController.Instance is null, cannot add drop.");
            yield break;
        }

        string dropName = "";
        int countTemp = 0;

        foreach (var result in drops)
        {
            Item item = null;
            yield return ResolveDropItem_Co(result.drop, resolved => item = resolved);

            if (item == null)
            {
                Debug.LogWarning($"⚠️ Null item in drop list for block: {blockName}");
                continue;
            }

            InventoryController.Instance.TryAddItemToInventory(new InventoryItem(item, result.amount));

            QuestSignals.CollectItem(item.itemName, result.amount);
            var pos = Toaster.GetRandomAnchoredPosition();
            bool rainbow = item.rarity == Rarity.Exclusive;
            Toaster.Show($"x{result.amount}", item.icon, 1.6f, pos, rainbow);

            var itemId = Game.Discovery.BlockDiscoveryService.GetItemId(item);
            Game.Discovery.BlockDiscoveryService.Ins?.DiscoverDrop(dropBlockName, itemId);

            dropName += (countTemp == 0) ? result.amount + " " + item.GetColoredName() : ", " + result.amount + " " + item.itemName;
            countTemp++;
        }

        if (countTemp == 0)
        {
            GameDebugHandler.LogStaticKey(
                "UI_Debug",
                "block_drops_none",
                new { block = blockName }
            );
            yield break;
        }

        GameDebugHandler.LogStaticKey(
            "UI_Debug",
            "block_drops",
            new { block = dropBlockName, items = dropName }
        );
    }

    IEnumerator ResolveDropItem_Co(ItemDrop drop, Action<Item> onResolved)
    {
        if (drop == null)
        {
            onResolved?.Invoke(null);
            yield break;
        }

        if (drop.item != null)
        {
            onResolved?.Invoke(drop.item);
            yield break;
        }

        string address = drop.GetItemAddress();
        if (string.IsNullOrEmpty(address))
        {
            Debug.LogWarning($"[Drop] Missing address for block '{blockName}'.");
            onResolved?.Invoke(null);
            yield break;
        }

        AsyncOperationHandle<Item> handle = Addressables.LoadAssetAsync<Item>(address);
        yield return handle;

        Item item = null;
        if (handle.Status == AsyncOperationStatus.Succeeded)
            item = handle.Result;
        else
        {
            Debug.LogWarning($"[Drop] Failed to load Addressable Item '{address}' for block '{blockName}'. Status={handle.Status}");
        }

        Addressables.Release(handle);
        onResolved?.Invoke(item);
    }

    #endregion

    #region CUBE_ANIM -------------------------------------------------------------------------------------
    void OnAppear()
    {
        blockSpawnTime = Time.unscaledTime;
        KillDeathFlowTween();
        animCtrl?.StopAll();

        CacheBaseAliveScale();
        transform.localScale = GetAliveScaleForHealth(CurrentHealth.Value);

        if (animCtrl != null)
        {
            animCtrl.PlaySpawn(playAnimation: false);
            animCtrl.TryPlayIdle();
        }
    }


    void OnDisappear()
    {
        isDyingEffect = true;
        breakFinalized = false;
        KillDeathFlowTween();
        float timeToBreak = Mathf.Max(0f, Time.unscaledTime - blockSpawnTime);
        AnalyticsManager.Ins?.TrackBlockBreak(blockName, GetLocationString(), timeToBreak);

        StatsManager.Ins.Add(StatType.TotalBlockBreaked, 1);
        RunGrowThenExplodeFlow();
    }

    void FinalizeBreak()
    {
        if (breakFinalized) return;
        breakFinalized = true;

        UpdateCrackVisual(MaxHealth); // reset crack
        Game.Discovery.BlockDiscoveryService.Ins?.DiscoverBlock(blockName);
        HandleItemDrop();
        isDyingEffect = false;
        PlayBreakedSound();

        BlockManager.Ins.OnBlockBroken();
    }
    // Crack animation
    void UpdateCrackVisual(float currentHP)
    {
        if (crackMeshRenderer == null) return;

        // Compute crack index
        float healthPercent = currentHP / MaxHealth;
        int crackIndex = Mathf.FloorToInt((1f - healthPercent) * crackLevels);
        crackIndex = Mathf.Clamp(crackIndex, 0, crackLevels - 1);

        // Apply to shader
        crackMeshRenderer.GetPropertyBlock(crackPropertyBlock);
        crackPropertyBlock.SetFloat(CrackIndexID, crackIndex);
        crackMeshRenderer.SetPropertyBlock(crackPropertyBlock);

        if (!isDyingEffect && MaxHealth > 0f)
        {
            transform.localScale = GetAliveScaleForHealth(currentHP);
        }
    }
    public float GetDestroyBlockAnimTime() => 0f;

    #endregion

    #region SOUND -----------------------------------------------------------------------------------------
    void PlayHittingSound()
    {
        if (!SoundEffectController.Ins.PlaySFX(blockName + "Breaking"))
            SoundEffectController.Ins.PlaySFX("Hit");
    }
    void PlayBreakedSound()
    {
        float value = UnityEngine.Random.Range(0f, 1f);
        if (value <= 0.9)
            SoundEffectController.Ins.PlaySFX("Break");
        else if (value <= 0.95)
            SoundEffectController.Ins.PlaySFX("Fart");
        else
        {
            SoundEffectController.Ins.PlaySFX("Ack");
        }
    }
    #endregion

    #region TEXTURE -------------------------------------------------------------------------------------
    void GenerateCube()
    {
        if (faceTiles.Length != 6)
        {
            Debug.LogError("Please assign 6 tile coordinates (one per face).");
            return;
        }

        EnsureGeneratedCubeMesh();
        UpdateCubeUv();

        if (cubeRenderer == null) cubeRenderer = gameObject.AddComponent<MeshRenderer>();
        ApplyBaseMaterialPreserveSlots();

        if (cubeMaterial != null && textureAtlas != null && cubeMaterial.mainTexture != textureAtlas)
        {
            cubeMaterial.mainTexture = textureAtlas;
        }
    }

    void ApplyBaseMaterialPreserveSlots()
    {
        if (cubeRenderer == null || cubeMaterial == null)
            return;

        var mats = cubeRenderer.sharedMaterials;
        if (mats == null || mats.Length == 0)
        {
            cubeRenderer.sharedMaterial = cubeMaterial;
            return;
        }

        if (mats[0] == cubeMaterial)
            return;

        mats[0] = cubeMaterial;
        cubeRenderer.sharedMaterials = mats;
    }

    void ApplyOutlineColorFromDatabase()
    {
        if (cubeRenderer == null || blockUVDatabase == null)
            return;

        if (!TryGetOutlineSlot(out int slotIndex, out var outlineMat))
            return;

        Color outlineColor = blockUVDatabase.GetOutlineColor(blockName);
        cubeRenderer.GetPropertyBlock(outlinePropertyBlock, slotIndex);
        if (TryGetColorPropertyId(outlineMat, out int colorPropertyId))
            outlinePropertyBlock.SetColor(colorPropertyId, outlineColor);
        if (outlineMat != null && outlineMat.HasProperty(ScaleID))
        {
            float baseScale = outlineMat.GetFloat(ScaleID);
            float appliedScale = outlineColor.a <= 0.001f ? 0f : baseScale;
            outlinePropertyBlock.SetFloat(ScaleID, appliedScale);
        }
        cubeRenderer.SetPropertyBlock(outlinePropertyBlock, slotIndex);
    }

    bool TryGetOutlineSlot(out int slotIndex, out Material outlineMaterial)
    {
        slotIndex = -1;
        outlineMaterial = null;

        if (cubeRenderer == null)
            return false;

        var mats = cubeRenderer.sharedMaterials;
        if (mats == null || mats.Length == 0)
            return false;

        for (int i = 0; i < mats.Length; i++)
        {
            var mat = mats[i];
            if (mat == null)
                continue;

            string shaderName = mat.shader != null ? mat.shader.name : string.Empty;
            bool looksLikeOutline =
                mat.name.IndexOf("outline", StringComparison.OrdinalIgnoreCase) >= 0 ||
                shaderName.IndexOf("outline", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!looksLikeOutline)
                continue;

            if (TryGetColorPropertyId(mat, out _))
            {
                slotIndex = i;
                outlineMaterial = mat;
                return true;
            }
        }

        if (outlineMaterialIndex >= 0 &&
            outlineMaterialIndex < mats.Length &&
            TryGetColorPropertyId(mats[outlineMaterialIndex], out _))
        {
            slotIndex = outlineMaterialIndex;
            outlineMaterial = mats[outlineMaterialIndex];
            return true;
        }

        return false;
    }

    bool TryGetColorPropertyId(Material mat, out int colorPropertyId)
    {
        colorPropertyId = ColorID;

        if (mat == null)
            return false;

        if (mat.HasProperty(ColorID))
        {
            colorPropertyId = ColorID;
            return true;
        }

        if (mat.HasProperty(BaseColorID))
        {
            colorPropertyId = BaseColorID;
            return true;
        }

        return false;
    }

    void EnsureGeneratedCubeMesh()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>() ?? gameObject.AddComponent<MeshFilter>();

        if (generatedCubeMesh == null)
        {
            generatedCubeMesh = new Mesh { name = "GeneratedCube" };

            var verts = new Vector3[24];
            for (int face = 0; face < CubeFaces.Length; face++)
            {
                int vi = face * 4;
                verts[vi + 0] = CubeFaces[face][0];
                verts[vi + 1] = CubeFaces[face][1];
                verts[vi + 2] = CubeFaces[face][2];
                verts[vi + 3] = CubeFaces[face][3];
            }

            generatedCubeMesh.vertices = verts;
            generatedCubeMesh.triangles = CubeTriangles;
            generatedCubeMesh.RecalculateNormals();
        }

        if (meshFilter.sharedMesh != generatedCubeMesh)
            meshFilter.sharedMesh = generatedCubeMesh;
    }

    void UpdateCubeUv()
    {
        if (generatedCubeMesh == null)
            return;

        float tileSizeX = 1f / atlasColumns;
        float tileSizeY = 1f / atlasRows;
        Vector2 tileScale = new Vector2(tileSizeX, tileSizeY);
        Vector2Int mapOffset = SetMapByName(blockName);

        for (int i = 0; i < 6; i++)
        {
            int vi = i * 4;
            Vector2Int tile = faceTiles[i] + mapOffset;
            if (flipY)
                tile.y = atlasRows - 1 - tile.y;

            Vector2 uvOffset = new Vector2(tile.x * tileSizeX, tile.y * tileSizeY);

            cubeUvBuffer[vi + 0] = uvOffset + Vector2.Scale(new Vector2(0f, 0f), tileScale);
            cubeUvBuffer[vi + 1] = uvOffset + Vector2.Scale(new Vector2(1f, 0f), tileScale);
            cubeUvBuffer[vi + 2] = uvOffset + Vector2.Scale(new Vector2(0f, 1f), tileScale);
            cubeUvBuffer[vi + 3] = uvOffset + Vector2.Scale(new Vector2(1f, 1f), tileScale);
        }

        generatedCubeMesh.uv = cubeUvBuffer;
    }

    private Vector2Int SetMapByID(int index)
    {
        return new Vector2Int(
                blockColumns * (index % (atlasColumns / blockColumns)),
                blockRows * (index / (atlasColumns / blockColumns))
            );
    }
    private Vector2Int SetMapByName(string name)
    {
        return SetMapByID(blockUVDatabase.GetAtlasIndex(name));
    }

    public bool TryGetRandomFaceTile(out Vector2Int tile)
    {
        tile = Vector2Int.zero;

        if (faceTiles == null || faceTiles.Length == 0)
            return false;

        int faceIndex = UnityEngine.Random.Range(0, faceTiles.Length);
        tile = faceTiles[faceIndex] + SetMapByName(blockName);
        return true;
    }

    #endregion
    #region HELPER --------------------------------------------------------------------------------------------
    static Camera ResolveMainCamera()
    {
        if (cachedMainCamera != null && cachedMainCamera.isActiveAndEnabled)
            return cachedMainCamera;

        if (cachedMainCameraFrame == Time.frameCount)
            return cachedMainCamera;

        cachedMainCameraFrame = Time.frameCount;
        cachedMainCamera = Camera.main;
        return cachedMainCamera;
    }

    string GetLocationString()
    {
        return DataSaver.Ins != null && DataSaver.Ins.currentLocation.HasValue
            ? DataSaver.Ins.currentLocation.Value.ToString()
            : "unknown";
    }

    void CacheBaseAliveScale()
    {
        // Use configured uniform scale, but fallback to authored scale if config is invalid.
        if (baseBlockScale > 0.001f)
            baseAliveScale = Vector3.one * baseBlockScale;
        else if (authoredBaseScale.sqrMagnitude > 0.0001f)
            baseAliveScale = authoredBaseScale;
        else
            baseAliveScale = Vector3.one;

        if (!isDyingEffect)
            transform.localScale = baseAliveScale;
    }

    void KillDeathFlowTween()
    {
        deathFlowTween?.Kill();
        deathFlowTween = null;
    }

    Vector3 GetAliveScaleForHealth(float currentHP)
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

    void RunGrowThenExplodeFlow()
    {
        KillDeathFlowTween();
        Vector3 burstScale = GetAliveScaleForHealth(CurrentHealth.Value) * Mathf.Max(1f, growThenExplodeBurstScale);
        deathFlowTween = transform
            .DOScale(burstScale, Mathf.Max(0.01f, growThenExplodeBurstDuration))
            .SetEase(growThenExplodeBurstEase)
            .OnComplete(() =>
            {
                // Death anim here is used for fragment spawn; do not wait to keep next block immediate.
                animCtrl?.PlayDeath();
                FinalizeBreak();
            });
    }
    #endregion
}






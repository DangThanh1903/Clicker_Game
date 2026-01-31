using Sirenix.OdinInspector;
using UnityEngine;
using UniRx;
using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class ClickableObject : MonoBehaviour, IDamagable
{
    [Header("Information")]
    [ReadOnly, SerializeField]
    private string blockName;
    public string BlockName => blockName;
    public float MaxHealth { get; private set; }
    public ReactiveProperty<float> CurrentHealth { get; private set; } = new ReactiveProperty<float>();
    public float BlockWeight;
    private static readonly int CrackIndexID = Shader.PropertyToID("_CrackIndex");
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
    [Header("Cracking layer")]
    [SerializeField] private MeshRenderer crackMeshRenderer;
    private MaterialPropertyBlock propertyBlock;
    [Header("Animation")]
    [SerializeField] private BlockAnimationController animCtrl;
    private Vector2 onClickPos;
    private float blockSpawnTime;

    // 📦 Internal click buffer and stream
    private readonly Subject<long> clickStream = new Subject<long>();
    private readonly List<long> clickBuffer = new List<long>();
    private CompositeDisposable runtimeSubs;
    void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        cubeRenderer = GetComponent<MeshRenderer>();
        if (animCtrl == null) animCtrl = GetComponent<BlockAnimationController>();
    }

    void OnEnable()
    {
        ListenRuntime();
    }

    void OnDisable()
    {
        runtimeSubs?.Dispose();
        runtimeSubs = null;
        clickBuffer.Clear();

        // If pooled/disabled during death, finalize once so discovery + drops still fire.
        if (isDyingEffect && !breakFinalized)
            FinalizeBreak();
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
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                // Filter old clicks
                clickBuffer.RemoveAll(timestamp => now - timestamp > 1000);

                // 👇 Set to StatManager
                StatsManager.Ins.Set(StatType.ClickPerTick, clickBuffer.Count);
            })
            .AddTo(runtimeSubs);

        // Push click timestamp into buffer
        clickStream.Subscribe(time =>
        {
            clickBuffer.Add(time);
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
        GenerateCube();
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

        // Mouse Down
        if (Input.GetMouseButtonDown(0))
        {
            var cam = Camera.main;
            if (cam == null) return;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform)
            {
                onClickPos = GetUIPosition(hit.point);
                player.OnClick(this);
            }
        }

        // Mouse Held
        if (Input.GetMouseButton(0))
        {
            if (!isMouseHeld)
            {
                isMouseHeld = true;
            }

            var cam = Camera.main;
            if (cam == null) return;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform)
            {
                onClickPos = GetUIPosition(hit.point);
                player.OnHold(this);
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
    private Vector2 GetUIPosition(Vector3 worldPos)
    {
        Vector2 screenPos = Camera.main.WorldToScreenPoint(worldPos);
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
        float power = StatsManager.Ins.Get(StatType.NormalPower);
        TakeDamage(power, "click");
    }
    public void HandleHold()
    {
        accumulatedHoldTime += Time.deltaTime;
        StatsManager.Ins.Add(StatType.HoldedTime, Time.deltaTime);
        if (accumulatedHoldTime >= timeHoldReset)
        {
            float power = StatsManager.Ins.Get(StatType.HoldPower) * timeHoldReset;
            TakeDamage(power, "hold", timeHoldReset);
            accumulatedHoldTime = 0f;
        }
    }

    public void HandleIdle()
    {
        accumulatedHoldTime += Time.deltaTime;
        if (accumulatedHoldTime >= timeIdleReset)
        {
            float power = StatsManager.Ins.Get(StatType.IdlePower) * timeIdleReset;
            TakeDamage(power, "idle", timeIdleReset);
            accumulatedHoldTime = 0f;
        }
    }

    void TakeDamage(float power, string source, float timeReset = 1)
    {
        CurrentHealth.Value = Mathf.Max(0, CurrentHealth.Value - power);

        StatsManager.Ins.Add(StatType.Clicks, 1 * timeReset);
        StatsManager.Ins.Add(StatType.TotalDamageDealed, power);

        AnalyticsManager.Ins?.TrackBlockClick(blockName, GetLocationString(), power, source);

        Toaster.Show($"-{power:F1}", null, 0.2f, onClickPos);

        long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        clickStream.OnNext(time);

        animCtrl?.PlayClick();
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
        animCtrl?.PlaySpawn(() =>
        {
            animCtrl.TryPlayIdle();
        });
    }


    void OnDisappear()
    {
        isDyingEffect = true;
        breakFinalized = false;
        float timeToBreak = Mathf.Max(0f, Time.unscaledTime - blockSpawnTime);
        AnalyticsManager.Ins?.TrackBlockBreak(blockName, GetLocationString(), timeToBreak);

        StatsManager.Ins.Add(StatType.TotalBlockBreaked, 1);

        animCtrl?.PlayDeath(() => FinalizeBreak());
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
        crackMeshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(CrackIndexID, crackIndex);
        crackMeshRenderer.SetPropertyBlock(propertyBlock);
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

        float tileSizeX = 1f / atlasColumns;
        float tileSizeY = 1f / atlasRows;

        Vector3[] verts = new Vector3[24];
        Vector2[] uvs = new Vector2[24];
        int[] tris = new int[36];

        // Cube face vertices
        Vector3[][] cubeFaces =
        {
            // Front
            new Vector3[] { new Vector3(-0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f,  0.5f),
                            new Vector3(-0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f,  0.5f)},
            // Back
            new Vector3[] { new Vector3( 0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f, -0.5f),
                            new Vector3( 0.5f,  0.5f, -0.5f), new Vector3(-0.5f,  0.5f, -0.5f)},
            // Top
            new Vector3[] { new Vector3(-0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f,  0.5f),
                            new Vector3(-0.5f,  0.5f, -0.5f), new Vector3( 0.5f,  0.5f, -0.5f)},
            // Bottom
            new Vector3[] { new Vector3(-0.5f, -0.5f, -0.5f), new Vector3( 0.5f, -0.5f, -0.5f),
                            new Vector3(-0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f,  0.5f)},
            // Left
            new Vector3[] { new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(-0.5f, -0.5f,  0.5f),
                            new Vector3(-0.5f,  0.5f, -0.5f), new Vector3(-0.5f,  0.5f,  0.5f)},
            // Right
            new Vector3[] { new Vector3( 0.5f, -0.5f,  0.5f), new Vector3( 0.5f, -0.5f, -0.5f),
                            new Vector3( 0.5f,  0.5f,  0.5f), new Vector3( 0.5f,  0.5f, -0.5f)},
        };

        for (int i = 0; i < 6; i++)
        {
            int vi = i * 4;

            // This is for handle position automaticly by index
            Vector2Int tile = faceTiles[i] + SetMapByName(blockName);

            if (flipY) tile.y = atlasRows - 1 - tile.y;

            Vector2 uvOffset = new Vector2(tile.x * tileSizeX, tile.y * tileSizeY);

            verts[vi + 0] = cubeFaces[i][0];
            verts[vi + 1] = cubeFaces[i][1];
            verts[vi + 2] = cubeFaces[i][2];
            verts[vi + 3] = cubeFaces[i][3];

            uvs[vi + 0] = uvOffset + new Vector2(0, 0) * new Vector2(tileSizeX, tileSizeY);
            uvs[vi + 1] = uvOffset + new Vector2(1, 0) * new Vector2(tileSizeX, tileSizeY);
            uvs[vi + 2] = uvOffset + new Vector2(0, 1) * new Vector2(tileSizeX, tileSizeY);
            uvs[vi + 3] = uvOffset + new Vector2(1, 1) * new Vector2(tileSizeX, tileSizeY);

            tris[i * 6 + 0] = vi + 0;
            tris[i * 6 + 1] = vi + 1;
            tris[i * 6 + 2] = vi + 2;
            tris[i * 6 + 3] = vi + 2;
            tris[i * 6 + 4] = vi + 1;
            tris[i * 6 + 5] = vi + 3;
        }

        Mesh mesh = new Mesh
        {
            name = "GeneratedCube",
            vertices = verts,
            uv = uvs,
            triangles = tris
        };
        mesh.RecalculateNormals();

        var mf = GetComponent<MeshFilter>();
        if (mf == null) mf = gameObject.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        if (cubeRenderer == null) cubeRenderer = gameObject.AddComponent<MeshRenderer>();
        cubeRenderer.sharedMaterial = cubeMaterial;

        if (cubeMaterial != null && textureAtlas != null)
        {
            cubeMaterial.mainTexture = textureAtlas;
        }
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

    #endregion
    #region HELPER --------------------------------------------------------------------------------------------
    string GetLocationString()
    {
        return DataSaver.Ins != null && DataSaver.Ins.currentLocation.HasValue
            ? DataSaver.Ins.currentLocation.Value.ToString()
            : "unknown";
    }
    #endregion
}




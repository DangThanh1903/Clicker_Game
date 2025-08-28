using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UniRx;
using System;
using System.Collections.Generic;
using DG.Tweening;
using Lean.Pool;

public class ClickableObject : MonoBehaviour, IDamagable
{
    [Header("Information")]
    private Vector3 spinAxis;

    [ReadOnly, SerializeField]
    private float spinSpeed;
    [ReadOnly, SerializeField]
    private string blockName;
    public float MaxHealth { get; private set; }
    public ReactiveProperty<float> CurrentHealth { get; private set;} = new ReactiveProperty<float>();
    public float BlockWeight;
    private static readonly int CrackIndexID = Shader.PropertyToID("_CrackIndex");
    private MeshRenderer cubeRenderer;
    private float accumulatedHoldTime = 0f;
    private readonly float timeHoldReset = 0.1f;
    private readonly float timeIdleReset = 1f;

    private bool isMouseHeld = false;

    [Header("Settings")]
    [SerializeField] private float spinBoostPerClick = 100f;
    [SerializeField, Range(0f, 1f)] private float decayPercentPerSecond = 0.95f;
    [SerializeField] private float stopThreshold = 0.1f;
    [SerializeField] private int crackLevels = 9;
    [SerializeField] private int numberOfFragment = 5;

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
    [SerializeField] GameObject fragmentPrefab;

    [Header("Explode anim")]
    float shrinkScale = 2f;
    float shrinkTime = 0.2f;
    float delayBeforeExpand = 0.1f;
    float expandScale = 4f;
    float expandTime = 0.2f;

    private bool isSpinning = false;

    // 📦 Internal click buffer and stream
    private readonly Subject<long> clickStream = new Subject<long>();
    private readonly List<long> clickBuffer = new List<long>();
    void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        cubeRenderer = GetComponent<MeshRenderer>();
    }

    void Start()
    {
        ListenerSetup();
    }

    void Update()
    {
        HandleSpinDecay();
        HandleClickDetection();
    }
    #region SETUP ---------------------------------------------------------------------------------------------
    void ListenerSetup()
    {
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
            .AddTo(this);

        // Push click timestamp into buffer
        clickStream.Subscribe(time =>
        {
            clickBuffer.Add(time);
        }).AddTo(this);

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
            .AddTo(this);
    }

    public void SetClickableBlock(string name)
    {
        blockName = name;
        DataSaver.Ins.currentBlock = name;
        MaxHealth = blockUVDatabase.GetHealth(name);
        CurrentHealth.Value = blockUVDatabase.GetHealth(name);
        BlockWeight = blockUVDatabase.GetWeight(name);
        if (BlockWeight <= BlockManager.Ins.rareWeightCap)
            DataSaver.Ins.SaveDataFn();
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
            specialWeatherName).blockName
        );
    }
    #endregion
    #region CLICK_LOGIC -------------------------------------------------------------------------------------
    // Click logic
    public void HandleClickDetection()
    {
        PlayerController.Instance.OnUpdate(this);

        if (!UIManager.Ins.IsMenuPanel()) return;
        
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform)
            {
                PlayerController.Instance.OnClick(this);
            }
        }

        if (Input.GetMouseButton(0)) 
        {
            if (!isMouseHeld)
            {
                isMouseHeld = true;  
            }

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform)
            {
                PlayerController.Instance.OnHold(this);
            }
        }
        else if (Input.GetMouseButtonUp(0)) 
        {
            if (isMouseHeld)
            {
                isMouseHeld = false; 
            }
        }
    }

    public void HandleClick()
    {
        spinAxis = UnityEngine.Random.onUnitSphere;
        spinSpeed += spinBoostPerClick;
        isSpinning = true;

        float power = StatsManager.Ins.Get(StatType.NormalPower);

        StatsManager.Ins.Add(StatType.Clicks, power);

        CurrentHealth.Value = Mathf.Max(0, CurrentHealth.Value - power);

        long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        clickStream.OnNext(time);
    }
    public void HandleHold()
    {
        accumulatedHoldTime += Time.deltaTime;
        if (accumulatedHoldTime >= timeHoldReset)
        {
            float power = StatsManager.Ins.Get(StatType.HoldPower) * timeHoldReset;
            CurrentHealth.Value = Mathf.Max(0, CurrentHealth.Value - power);
            accumulatedHoldTime = 0f;
            StatsManager.Ins.Add(StatType.Clicks, power);
            long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            clickStream.OnNext(time);
        }
    }

    public void HandleIdle()
    {
        accumulatedHoldTime += Time.deltaTime;
        if (accumulatedHoldTime >= timeIdleReset)
        {
            float power = StatsManager.Ins.Get(StatType.IdlePower) * timeIdleReset;
            CurrentHealth.Value = Mathf.Max(0, CurrentHealth.Value - power);
            accumulatedHoldTime = 0f;
            StatsManager.Ins.Add(StatType.Clicks, power);
            long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            clickStream.OnNext(time);
        }
    }

    private void HandleSpinDecay()
    {
        if (!isSpinning) return;

        transform.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.World);
        spinSpeed *= Mathf.Pow(decayPercentPerSecond, Time.deltaTime);

        if (spinSpeed < stopThreshold)
        {
            spinSpeed = 0f;
            isSpinning = false;
        }
    }

    #endregion

    #region CUBE_ANIM -------------------------------------------------------------------------------------
    void OnEnable()
    {
        OnAppear();
    }
    void OnAppear()
    {
        float duration = 2f;
        transform.localScale = Vector3.zero;
        cubeRenderer.enabled = true;
        transform.DOScale(Vector3.one * 2.5f, duration).SetEase(Ease.OutBack);
    }

    void OnDisappear()
    {
        PlayExplodeEffect();
        HandleItemDrop();
    }

    void HandleItemDrop()
    {
        var items = blockUVDatabase.GetDroppedItemsByName(blockName);
        string dropName = "";
        int countTemp = 0;
        if (items.Count == 0)
        {
            Debug.Log("There is no item drop");
            GameDebugHandler.LogStaticAfter(blockName + " drops nothing!", 1f);
            return;
        }
        foreach (var (item, amount) in items)
        {
            if (item == null)
            {
                Debug.LogWarning($"⚠️ Null item in drop list for block: {blockName}");
                continue;
            }
            InventoryController.Instance.AddItemToInventory(new InventoryItem(item, amount));
            dropName += (countTemp == 0) ? amount + " " + item.itemName : ", " + amount + " " + item.itemName;
            countTemp++;
        }
        GameDebugHandler.LogStaticAfter(blockName + " drops " + dropName + "!", 1f);
    }

    public void PlayExplodeEffect()
    {
        Sequence seq = DOTween.Sequence();

        seq.Append(transform.DOScale(shrinkScale, shrinkTime))           // Shrink
           .AppendInterval(delayBeforeExpand)                            // Pause
           .Append(transform.DOScale(expandScale, expandTime))           // Expand           
           .OnComplete(() =>
           {
               UpdateCrackVisual(MaxHealth);
               for (int i = 0; i < numberOfFragment; i++)
               {
                   cubeRenderer.enabled = false;
                   var go = LeanPool.Spawn(fragmentPrefab, transform.position, Quaternion.identity, transform);
                   var frag = go.GetComponent<BlockFragment>();
                   if (frag)
                   {
                       Vector2Int baseTile = SetMapByName(blockName);
                       frag.SetupTile(textureAtlas, atlasColumns, atlasRows, baseTile, flipY);
                   }

               }

               PlayBreakedSound();
           });
    }
    void PlayHittingSound()
    {
        if (!SoundEffectController.Ins.PlaySFX(blockName + "Breaking"))
            SoundEffectController.Ins.PlaySFX("Hit");
    }
    void PlayBreakedSound()
    {
        float value = UnityEngine.Random.Range(0f, 1f);
        if (value <= 0.98)
            SoundEffectController.Ins.PlaySFX("Break");
        else
            SoundEffectController.Ins.PlaySFX("Fart");
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
    public float GetDestroyBlockAnimTime()
    {
        return shrinkTime + delayBeforeExpand + expandTime;
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
}

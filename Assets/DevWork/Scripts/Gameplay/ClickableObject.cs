using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UniRx;
using System;
using System.Collections.Generic;
using DG.Tweening;
using Lean.Pool;

public class ClickableObject : MonoBehaviour
{
    [Header("Information")]
    private Vector3 spinAxis;

    [ReadOnly, SerializeField]
    private float spinSpeed;
    [ReadOnly, SerializeField]
    private string blockName;
    [ReadOnly, SerializeField]
    private float maxHealth;
    [ReadOnly, SerializeField]
    private ReactiveProperty<float> currentHealth = new ReactiveProperty<float>();
    public IReadOnlyReactiveProperty<float> CurrentHealth => currentHealth;
    private static readonly int CrackIndexID = Shader.PropertyToID("_CrackIndex");
    private MeshRenderer cubeRenderer;

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
    float shrinkScale = 1f;
    float shrinkTime = 0.3f;
    float delayBeforeExpand = 0.15f;
    float expandScale = 4.5f;
    float expandTime = 0.4f;

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
    // SetUp Logic
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
        currentHealth
            .DistinctUntilChanged()
            .Subscribe(newHealth =>
            {
                UpdateCrackVisual(newHealth);

                if (newHealth <= 0f)
                    OnDisappear();
            })
            .AddTo(this);
    }

    public void SetClickableBlock(string name)
    {
        blockName = name;
        DataSaver.Ins.currentBlock = name;
        maxHealth = blockUVDatabase.GetHealth(name);
        currentHealth.Value = blockUVDatabase.GetHealth(name);
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

    // Click logic
    private void HandleClickDetection()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        if (!UIManager.Ins.IsMenuPanel()) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit) && hit.transform == transform)
        {
            OnClicked();
        }
    }

    private void OnClicked()
    {
        spinAxis = UnityEngine.Random.onUnitSphere;
        spinSpeed += spinBoostPerClick;
        isSpinning = true;

        float power = 1 + StatsManager.Ins.Get(StatType.PickaxePower);

        // Increase total click count
        StatsManager.Ins.Add(StatType.Clicks, power);

        // - block HP
        currentHealth.Value = Mathf.Max(0, currentHealth.Value - power);

        // Track CPS click
        long time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        clickStream.OnNext(time);
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

    // Crack animation
    void UpdateCrackVisual(float currentHP)
    {
        if (crackMeshRenderer == null) return;

        // Compute crack index
        float healthPercent = currentHP / maxHealth;
        int crackIndex = Mathf.FloorToInt((1f - healthPercent) * crackLevels);
        crackIndex = Mathf.Clamp(crackIndex, 0, crackLevels - 1);

        // Apply to shader
        crackMeshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(CrackIndexID, crackIndex);
        crackMeshRenderer.SetPropertyBlock(propertyBlock);
    }

    // Cube anim

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
        if (items.Count == 0)
        {
            Debug.Log("There is no item drop");
        }
        foreach (var (item, amount) in items)
        {
            if (item == null)
            {
                Debug.LogWarning($"⚠️ Null item in drop list for block: {blockName}");
                continue;
            }
            InventoryController.Instance.AddItemToInventory(new InventoryItem(item, amount));
        }
    }

    public void PlayExplodeEffect()
    {
        Sequence seq = DOTween.Sequence();

        seq.Append(transform.DOScale(shrinkScale, shrinkTime))           // Shrink
           .AppendInterval(delayBeforeExpand)                            // Pause
           .Append(transform.DOScale(expandScale, expandTime))           // Expand           
           .OnComplete(() =>
           {
               UpdateCrackVisual(maxHealth);
               for (int i = 0; i < numberOfFragment; i++)
               {
                   cubeRenderer.enabled = false;
                   LeanPool.Spawn(fragmentPrefab, transform);
               }
           });
    }
    public float GetDestroyBlockAnimTime()
    {
        return shrinkTime + delayBeforeExpand + expandTime;
    }

    // Texture changing func
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

        Mesh mesh = new Mesh();
        mesh.name = "GeneratedCube";
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
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
}

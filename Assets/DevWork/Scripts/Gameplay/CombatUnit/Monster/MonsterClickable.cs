using UnityEngine;
using UniRx;
using System;
using Lean.Pool;
using GooglePlayGames.BasicApi;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using DG.Tweening;

public class MonsterClickable : MonoBehaviour, IDamagable
{
    private MonsterDef def;
     public float MaxHealth { get; private set; }
    public ReactiveProperty<float> CurrentHealth { get; private set; } = new ReactiveProperty<float>();

    private MonsterSpawner owner;
    private IMonsterMovement[] movementComponents;
    private Vector3 baseLocalPos;
    private IDisposable lifeTimer;
    private IDisposable healthSub;
    private float spawnTime;
    private string monsterId;

    private bool isMouseHeld;
    private bool isPressedOnThis;
    private float accumulatedHoldTime = 0f;
    private readonly float timeHoldReset = 0.1f;
    private readonly float timeIdleReset = 1f;
    private Vector2 onClickPos;
    [Header("Visual (child)")]
    [SerializeField] private Transform visual;
    [SerializeField] private float hitPunchScale = 0.12f;
    [SerializeField] private float hitPunchDuration = 0.12f;
    [SerializeField] private int hitPunchVibrato = 8;
    [SerializeField] private float hitPunchElasticity = 0.6f;
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.3f, 0.3f, 1f);
    [SerializeField] private float hitFlashDuration = 0.12f;
    [SerializeField] private string hitSfxKey = "Hit";
    private Vector3 visualBaseScale = Vector3.one;
    private Tween hitTween;
    private Tween hitColorTween;
    private SpriteRenderer[] spriteRenderers;
    private Color[] baseSpriteColors;
    private RendererColorInfo[] rendererInfos;
    private MaterialPropertyBlock mpb;

    private struct RendererColorInfo
    {
        public Renderer renderer;
        public int colorId;
        public Color baseColor;
    }

    private bool resolved;

    // gọi từ spawner
    public void Init(MonsterDef d, MonsterSpawner spawner)
    {
        def = d;
        owner = spawner;
        resolved = false;
        CacheVisual();
        CacheMovement();
        baseLocalPos = transform.localPosition;
        spawnTime = Time.unscaledTime;
        monsterId = ResolveMonsterId(def);

        MaxHealth = Mathf.Max(1f, def.MaxHP);
        CurrentHealth.Value = MaxHealth;

        // listen HP
        healthSub?.Dispose();
        healthSub = CurrentHealth
            .DistinctUntilChanged()
            .Subscribe(hp =>
            {
                if (hp <= 0f)
                    OnKilled();
            });

        // lifetime
        lifeTimer?.Dispose();
        lifeTimer = Observable.Timer(TimeSpan.FromSeconds(def.lifetime))
            .Subscribe(_ => OnMiss());
    }

    void Update()
    {
        UpdateMovement();
        HandleClickDetection();
    }

    // ===== IDamagable =====

    public void HandleClickDetection()
    {
        if (resolved) return;
        if (!UIManager.Ins.IsBlockCanClick()) return;
        if (PopupController.Instance != null && PopupController.Instance.IsAnyPopupOpen()) return;

        // giống Boss/Block
        PlayerController.Instance.OnUpdate(this);
        
        // Mouse Down
        if (Input.GetMouseButtonDown(0))
        {
            var cam = Camera.main;
            if (cam == null) return;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit) && (hit.transform == transform || hit.transform.IsChildOf(transform)))
            {
                isPressedOnThis = true;
                onClickPos = GetUIPosition(hit.point);
                PlayerController.Instance.OnClick(this);
            }
            else
            {
                isPressedOnThis = false;
            }
        }

        // Mouse Held
        if (Input.GetMouseButton(0))
        {
            if (!isMouseHeld && isPressedOnThis) isMouseHeld = true;

            var cam = Camera.main;
            if (cam == null) return;
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (isPressedOnThis && Physics.Raycast(ray, out RaycastHit hit) && (hit.transform == transform || hit.transform.IsChildOf(transform)))
            {
                onClickPos = GetUIPosition(hit.point);
                PlayerController.Instance.OnHold(this);
            }
        }
        else if (Input.GetMouseButtonUp(0))
        {
            if (isMouseHeld)
            {
                isMouseHeld = false;
                StatsManager.Ins.Set(StatType.HoldedTime, 0);
            }
            isPressedOnThis = false;
        }
    }

    public void HandleClick()
    {
        float power = StatsManager.Ins.Get(StatType.NormalPower);
        TakeDamage(power);
    }

    public void HandleHold()
    {
        accumulatedHoldTime += Time.deltaTime;
        StatsManager.Ins.Add(StatType.HoldedTime, Time.deltaTime);

        if (accumulatedHoldTime >= timeHoldReset)
        {
            float power = StatsManager.Ins.Get(StatType.HoldPower) * timeHoldReset;
            TakeDamage(power);
            accumulatedHoldTime = 0f;
        }
    }

    public void HandleIdle()
    {
        accumulatedHoldTime += Time.deltaTime;
        if (accumulatedHoldTime >= timeIdleReset)
        {
            float power = StatsManager.Ins.Get(StatType.IdlePower) * timeIdleReset;
            TakeDamage(power);
            accumulatedHoldTime = 0f;
        }
    }

    // ===== Combat =====

    void TakeDamage(float power)
    {
        if (resolved) return;

        CurrentHealth.Value = Mathf.Max(0f, CurrentHealth.Value - power);

        // Optional: stats tracking
        StatsManager.Ins.Add(StatType.TotalDamageDealed, power);
        StatsManager.Ins.Add(StatType.Clicks, 1);

        // Optional: hit feedback (toast / anim)
        Toaster.Show($"-{power:F1}", null, 0.2f, onClickPos);
        PlayHitFeedback();
    }

    void OnKilled()
    {
        if (resolved) return;
        resolved = true;

        string rewardId = def != null && def.buffReward != null ? def.buffReward.name : "none";
        AnalyticsManager.Ins?.TrackMonsterKill(monsterId, Time.unscaledTime - spawnTime, rewardId);

        // reward buff
        if (def != null && def.buffReward != null)
            StatsManager.Ins.ApplyConsumableBuff(def.buffReward);

        HandleItemDrop();

        // sfx
        if (def != null && def.successSfx != null)
            SoundEffectController.Ins?.PlaySFX(def.successSfx);

        ResolveAndDespawn();
    }

    void OnMiss()
    {
        if (resolved) return;
        resolved = true;
        AnalyticsManager.Ins?.TrackMonsterMiss(monsterId, Time.unscaledTime - spawnTime);
        ResolveAndDespawn();
    }

    void HandleItemDrop()
    {
        if (def == null || def.drops == null || def.drops.Count == 0) return;

        if (InventoryController.Instance == null)
        {
            Debug.LogWarning("[MonsterDrop] InventoryController.Instance is null, cannot add drop.");
            return;
        }

        float luck = StatsManager.Ins != null ? StatsManager.Ins.Get(StatType.Lucky) : 0f;
        var drops = def.GetDroppedItems(luck);
        if (drops == null || drops.Count == 0) return;

        foreach (var result in drops)
        {
            if (result.item == null || result.item.Type == ItemType.None) continue;
            InventoryController.Instance.TryAddItemToInventory(new InventoryItem(result.item, result.amount));
            QuestSignals.CollectItem(result.item.itemName, result.amount);
            var pos = Toaster.GetRandomAnchoredPosition();
            bool rainbow = result.item.rarity == Rarity.Exclusive;
            Toaster.Show($"x{result.amount}", result.item.icon, 1.6f, pos, rainbow);
        }
    }

    void ResolveAndDespawn()
    {
        lifeTimer?.Dispose();
        lifeTimer = null;
        healthSub?.Dispose();
        healthSub = null;
        owner?.NotifyResolved(this);
        LeanPool.Despawn(gameObject);
    }

    void OnDisable()
    {
        lifeTimer?.Dispose();
        lifeTimer = null;
        healthSub?.Dispose();
        healthSub = null;
        resolved = false;
        isMouseHeld = false;
        accumulatedHoldTime = 0f;
        spawnTime = 0f;
        monsterId = null;
        if (hitTween != null)
        {
            hitTween.Kill(false);
            hitTween = null;
        }
        if (hitColorTween != null)
        {
            hitColorTween.Kill(false);
            hitColorTween = null;
        }
        if (visual != null)
            visual.localScale = visualBaseScale;
        ResetVisualColor();
    }

    void CacheMovement()
    {
        movementComponents = GetComponents<IMonsterMovement>();
    }

    void CacheVisual()
    {
        if (visual == null)
        {
            var named = transform.Find("Visual");
            if (named != null)
                visual = named;
            else if (transform.childCount > 0)
                visual = transform.GetChild(0);
        }

        if (visual != null)
            visualBaseScale = visual.localScale;

        CacheVisualRenderers();
    }

    void PlayHitFeedback()
    {
        if (visual == null) return;
        if (hitTween != null) hitTween.Kill(false);
        visual.localScale = visualBaseScale;
        hitTween = visual.DOPunchScale(
            new Vector3(hitPunchScale, hitPunchScale, 0f),
            hitPunchDuration,
            hitPunchVibrato,
            hitPunchElasticity
        );

        PlayHitColorFlash();
        if (!string.IsNullOrEmpty(hitSfxKey))
            SoundEffectController.Ins?.PlaySFX(hitSfxKey);
    }

    void CacheVisualRenderers()
    {
        spriteRenderers = null;
        baseSpriteColors = null;
        rendererInfos = null;
        mpb = null;

        if (visual == null) return;

        spriteRenderers = visual.GetComponentsInChildren<SpriteRenderer>(true);
        if (spriteRenderers != null && spriteRenderers.Length > 0)
        {
            baseSpriteColors = new Color[spriteRenderers.Length];
            for (int i = 0; i < spriteRenderers.Length; i++)
                baseSpriteColors[i] = spriteRenderers[i].color;
        }

        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        var list = new System.Collections.Generic.List<RendererColorInfo>();
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null || r is SpriteRenderer) continue;
            var mat = r.sharedMaterial;
            if (mat == null) continue;

            int colorId = -1;
            if (mat.HasProperty("_BaseColor")) colorId = Shader.PropertyToID("_BaseColor");
            else if (mat.HasProperty("_Color")) colorId = Shader.PropertyToID("_Color");
            if (colorId == -1) continue;

            list.Add(new RendererColorInfo
            {
                renderer = r,
                colorId = colorId,
                baseColor = mat.GetColor(colorId)
            });
        }

        if (list.Count > 0)
        {
            rendererInfos = list.ToArray();
            mpb = new MaterialPropertyBlock();
        }
    }

    void PlayHitColorFlash()
    {
        if (!HasColorTargets()) return;
        if (hitColorTween != null) hitColorTween.Kill(false);

        float half = Mathf.Max(0.01f, hitFlashDuration * 0.5f);
        hitColorTween = DOTween.To(() => 0f, ApplyHitColor, 1f, half)
            .SetEase(Ease.OutQuad)
            .SetLoops(2, LoopType.Yoyo);
    }

    bool HasColorTargets()
    {
        return (spriteRenderers != null && spriteRenderers.Length > 0)
            || (rendererInfos != null && rendererInfos.Length > 0);
    }

    void ApplyHitColor(float t)
    {
        if (spriteRenderers != null && baseSpriteColors != null)
        {
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                var sr = spriteRenderers[i];
                if (sr == null) continue;
                sr.color = Color.Lerp(baseSpriteColors[i], hitFlashColor, t);
            }
        }

        if (rendererInfos != null && rendererInfos.Length > 0 && mpb != null)
        {
            for (int i = 0; i < rendererInfos.Length; i++)
            {
                var info = rendererInfos[i];
                if (info.renderer == null) continue;
                mpb.Clear();
                mpb.SetColor(info.colorId, Color.Lerp(info.baseColor, hitFlashColor, t));
                info.renderer.SetPropertyBlock(mpb);
            }
        }
    }

    void ResetVisualColor()
    {
        ApplyHitColor(0f);
    }

    void UpdateMovement()
    {
        if (resolved) return;
        if (movementComponents == null || movementComponents.Length == 0) return;

        float dt = Time.deltaTime;
        Vector3 offset = Vector3.zero;
        bool hasActive = false;

        for (int i = 0; i < movementComponents.Length; i++)
        {
            var movement = movementComponents[i];
            if (movement is Behaviour behaviour && !behaviour.enabled) continue;
            offset += movement.MoveUpdate(dt);
            hasActive = true;
        }

        if (hasActive)
            transform.localPosition = baseLocalPos + offset;
    }

    string ResolveMonsterId(MonsterDef d)
    {
        if (d == null) return gameObject.name;
        return string.IsNullOrEmpty(d.id) ? d.name : d.id;
    }

    private Vector2 GetUIPosition(Vector3 worldPos)
    {
        if (Toaster.Ins == null || Toaster.Ins.canvas == null)
            return Vector2.zero;
        var cam = Camera.main;
        if (cam == null)
            return Vector2.zero;

        Vector2 screenPos = cam.WorldToScreenPoint(worldPos);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            Toaster.Ins.canvas.transform as RectTransform,
            screenPos,
            Toaster.Ins.canvas.worldCamera,
            out Vector2 localPoint
        );
        return localPoint;
    }
}

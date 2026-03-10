using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using Lean.Pool;

[CreateAssetMenu(fileName="NormalDeathAnim", menuName="Block/Anim/Death/Normal")]
public class NormalDeathAnim : BlockAnimationAsset
{
    [Header("Fragments")]
    public GameObject[] fragmentPrefabs;
    [Min(0f)] public float spawnRadius = 0.08f;
    [Min(0)] public int prewarmPerPrefab = 6;

    [Header("Game Feel")]
    [Min(0.05f)] public float baseImpulseMultiplier = 1.8f;
    [Min(0f)] public float extraImpulsePerHit = 0.08f;
    [Min(1f)] public float maxImpulseMultiplier = 2.2f;
    [Min(0.05f)] public float hitWindowSeconds = 1f;

    static readonly HashSet<int> prewarmedPrefabIds = new HashSet<int>();
    static readonly HashSet<int> warnedMissingFragmentScriptPrefabIds = new HashSet<int>();

    public override bool IsLooping => false;
    public override float EstimatedDuration => 0f;

    public override Tween PlayTween(GameObject target)
    {
        Stop(target);
        var t = target.transform;
        var clickable = target.GetComponent<ClickableObject>();
        Vector2Int tile = Vector2Int.zero;
        bool hasTile = clickable != null && clickable.TryGetRandomFaceTile(out tile);
        var atlas = hasTile ? ResolveAtlasTexture(clickable) : null;
        if (hasTile && atlas == null)
            Debug.LogError("[NormalDeathAnim] Atlas texture is missing on ClickableObject. Fragments can't map block UV.", target);

        if (fragmentPrefabs != null && fragmentPrefabs.Length > 0)
        {
            PrewarmPoolsIfNeeded();
            int recentHits = clickable != null ? clickable.GetRecentHitCount(hitWindowSeconds) : 0;
            int stackCount = Mathf.Max(0, recentHits - 1);
            float impulseMultiplier = ResolveImpulseMultiplier(recentHits);

            int total = fragmentPrefabs.Length;
            float startAngle = Random.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < fragmentPrefabs.Length; i++)
            {
                SpawnOneFragment(fragmentPrefabs[i], t.position, hasTile, tile, clickable, atlas, i, total, startAngle, impulseMultiplier, stackCount);
            }
        }

        return null;
    }

    void SpawnOneFragment(
        GameObject prefab,
        Vector3 center,
        bool hasTile,
        Vector2Int tile,
        ClickableObject clickable,
        Texture atlas,
        int index,
        int total,
        float startAngle,
        float impulseMultiplier,
        int stackCount
    )
    {
        if (prefab == null) return;

        Vector3 radialDir = BuildRadialDirection(index, total, startAngle);
        Vector3 spawnPos = center + radialDir * spawnRadius;
        var fragGo = LeanPool.Spawn(prefab, spawnPos, Random.rotation);
        if (fragGo == null) return;

        var fragment = fragGo.GetComponent<BlockFragment>();
        if (fragment == null)
        {
            int key = prefab.GetInstanceID();
            if (!warnedMissingFragmentScriptPrefabIds.Contains(key))
            {
                warnedMissingFragmentScriptPrefabIds.Add(key);
                Debug.LogError($"[NormalDeathAnim] Prefab '{prefab.name}' is missing BlockFragment. Add BlockFragment on prefab.", prefab);
            }
            LeanPool.Despawn(fragGo);
            return;
        }

        fragment.LaunchDirected(radialDir, impulseMultiplier, stackCount);

        if (!hasTile || clickable == null || atlas == null) return;

        fragment.SetupTile(
            atlas,
            clickable.AtlasColumns,
            clickable.AtlasRows,
            tile,
            clickable.AtlasFlipY
        );
    }

    float ResolveImpulseMultiplier(int recentHits)
    {
        int bonusHits = Mathf.Max(0, recentHits - 1);
        float value = baseImpulseMultiplier + bonusHits * extraImpulsePerHit;
        return Mathf.Clamp(value, 0.05f, Mathf.Max(1f, maxImpulseMultiplier));
    }

    static Vector3 BuildRadialDirection(int index, int total, float startAngle)
    {
        if (total <= 0)
            return Vector3.forward;

        float angle = startAngle + (Mathf.PI * 2f * index / total);
        Vector3 baseDir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

        Vector2 jitter = Random.insideUnitCircle * 0.18f;
        Vector3 dir = baseDir + new Vector3(jitter.x, 0f, jitter.y);
        if (dir.sqrMagnitude < 0.0001f)
            dir = baseDir;

        return dir.normalized;
    }

    void PrewarmPoolsIfNeeded()
    {
        if (fragmentPrefabs == null || fragmentPrefabs.Length == 0 || prewarmPerPrefab <= 0)
            return;

        for (int i = 0; i < fragmentPrefabs.Length; i++)
        {
            var prefab = fragmentPrefabs[i];
            if (prefab == null) continue;

            int key = prefab.GetInstanceID();
            if (prewarmedPrefabIds.Contains(key))
                continue;

            LeanGameObjectPool pool = null;
            if (!LeanGameObjectPool.TryFindPoolByPrefab(prefab, ref pool))
            {
                var bootstrap = LeanPool.Spawn(prefab);
                if (bootstrap != null)
                    LeanPool.Despawn(bootstrap);

                LeanGameObjectPool.TryFindPoolByPrefab(prefab, ref pool);
                if (pool == null)
                {
                    Debug.LogError($"[NormalDeathAnim] Failed to bootstrap pool for fragment prefab '{prefab.name}'.", prefab);
                    continue;
                }
            }

            if (pool.Preload < prewarmPerPrefab)
                pool.Preload = prewarmPerPrefab;

            pool.PreloadAll();
            prewarmedPrefabIds.Add(key);
        }
    }

    static Texture ResolveAtlasTexture(ClickableObject clickable)
    {
        return clickable != null ? clickable.AtlasTexture : null;
    }
}

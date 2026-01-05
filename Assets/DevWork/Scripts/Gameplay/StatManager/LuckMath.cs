using UnityEngine;

public static class LuckMath
{
    // Diminishing returns chance boost (stable, never > 1)
    public static float BoostChance(float baseChance01, float luck, float k = 120f)
    {
        baseChance01 = Mathf.Clamp01(baseChance01);
        if (baseChance01 <= 0f) return 0f;
        if (baseChance01 >= 1f) return 1f;

        float exponent = 1f + (luck / k);
        return Mathf.Clamp01(1f - Mathf.Pow(1f - baseChance01, exponent));
    }

    // Make low weights (rare) relatively more likely when luck increases.
    // We do this by scaling each weight with a multiplier based on "rarity score".
    public static float BoostWeightForRarity(float baseWeight, float rarityScore01, float luck, float k = 200f, float maxMult = 3f)
    {
        baseWeight = Mathf.Max(0f, baseWeight);
        float t = Mathf.Clamp01(rarityScore01);      // 0=common, 1=rare
        float mult = 1f + (luck / k) * t;            // only rare gets boosted
        mult = Mathf.Min(mult, maxMult);
        return baseWeight * mult;
    }
}

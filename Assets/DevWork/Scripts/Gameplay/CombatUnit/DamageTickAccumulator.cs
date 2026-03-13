public static class DamageTickAccumulator
{
    public static bool TryConsumeTick(ref float accumulator, float deltaTime, float tickSeconds)
    {
        accumulator += deltaTime;
        if (accumulator < tickSeconds)
            return false;

        // Preserve existing behavior: consume one tick and clear remainder.
        accumulator = 0f;
        return true;
    }
}

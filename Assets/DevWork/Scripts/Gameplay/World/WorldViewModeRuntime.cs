using System;

public enum WorldViewMode
{
    Combat = 0,
    Transition = 1,
    SideView = 2
}

public static class WorldViewModeRuntime
{
    public static event Action<WorldViewMode> ModeChanged;

    private static WorldViewMode currentMode = WorldViewMode.Combat;
    public static WorldViewMode CurrentMode => currentMode;

    public static void SetMode(WorldViewMode mode)
    {
        if (currentMode == mode)
            return;

        currentMode = mode;
        ModeChanged?.Invoke(currentMode);
    }
}

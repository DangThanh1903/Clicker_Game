public sealed class PlayerProfileRepository
{
    private const string FileName = "local_save.json";
    private readonly SaveCoordinator saveCoordinator;

    public PlayerProfileRepository(SaveCoordinator saveCoordinator = null)
    {
        this.saveCoordinator = saveCoordinator ?? SaveCoordinator.Ins;
    }

    public bool Exists()
    {
        return saveCoordinator.Exists(FileName);
    }

    public string GetPath()
    {
        return saveCoordinator.GetPath(FileName);
    }

    public bool TryLoad(out LocalSaveData data)
    {
        return saveCoordinator.TryLoadJson(FileName, out data, "LocalSave");
    }

    public bool Save(LocalSaveData data)
    {
        return saveCoordinator.TrySaveJson(FileName, data ?? new LocalSaveData(), "LocalSave");
    }

    public bool Delete()
    {
        return saveCoordinator.Delete(FileName, "LocalSave");
    }
}

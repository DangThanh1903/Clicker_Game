using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LocationLoader : MonoBehaviour
{
    public BlockSpawnLocation currentLocation;
    [SerializeField] private Button[] LocationButton;
    [SerializeField] private TMP_Text[] LocationText;

    void Start()
    {
        InitializeLocationButton();
    } 
    void InitializeLocationButton()
    {
        for (int i = 1; i < LocationButton.Length; i++)
        {
            int cachedIndex = i;

            LocationButton[i - 1].onClick.AddListener(() =>
            {
                UIManager.Ins.MoveToMain();
                if (currentLocation == (BlockSpawnLocation)cachedIndex)
                {
                    GameDebugHandler.LogStatic($"You have already in {((BlockSpawnLocation)cachedIndex).ToString()}!");
                    return;
                }
                SetLocation(cachedIndex);
                GameDebugHandler.LogStatic($"Moving to {((BlockSpawnLocation)cachedIndex).ToString()}!");
            });

            LocationText[i - 1].text = ((BlockSpawnLocation)cachedIndex).ToString();
        }
    }

    public void SetLocation(int index)
    {
        BlockSpawnLocation blockSpawnLocation = (BlockSpawnLocation)index;
        UIManager.Ins.SetLocationBackground(index - 1);
        currentLocation = blockSpawnLocation;
        DataSaver.Ins.currentLocation = blockSpawnLocation;
        if (DataSaver.Ins.PeakLocation < blockSpawnLocation || DataSaver.Ins.PeakLocation == null)
        {
            DataSaver.Ins.PeakLocation = blockSpawnLocation;
        }
    }
}

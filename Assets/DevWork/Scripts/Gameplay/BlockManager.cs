using System;
using UniRx;
using UnityEngine;

public class BlockManager : MonoBehaviour
{
    [SerializeField] private ClickableObject currentBlock;
    [SerializeField] private LocationLoader locationLoader;
    void Awake()
    {
        currentBlock.SetClickableBlock(
            DataSaver.Ins.currentBlock ?? "Stone");
        locationLoader.SetLocation(
            DataSaver.Ins.currentLocation == null ?
            BlockSpawnLocation.Any :
            (BlockSpawnLocation)DataSaver.Ins.currentLocation);
    }
    void Start()
    {
        currentBlock.CurrentHealth
            .Where(health => health <= 0f)
            .Delay(TimeSpan.FromSeconds(currentBlock.GetDestroyBlockAnimTime()))
            .Subscribe(_ =>
            {
                Debug.Log("GameplayManager: Block has broken!");
                OnBlockBroken();
            })
            .AddTo(this);
    }

    private void OnBlockBroken()
    {
        NormalWeatherName normalName =
            (WeatherManager.Instance.CurrentNormalWeather.Value as NormalWeatherData)?.weatherName
            ?? NormalWeatherName.Any;

        SpecialWeatherName specialName = 
            (WeatherManager.Instance.CurrentSpecialWeather.Value as SpecialWeatherData)?.weatherName 
            ?? SpecialWeatherName.Any;

        currentBlock.SetClickableBlockByCondition(
            locationLoader.currentLocation,
            TimeSystem.Instance.CurrentTimeState,
            normalName,
            specialName
        );

    }
}

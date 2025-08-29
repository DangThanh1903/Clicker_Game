using System.Collections.Generic;
using System.Linq;
using UniRx;
using UnityEngine;

public class TrashCan : MonoBehaviour
{
    [SerializeField] private InventorySection trashCanData;
    private CompositeDisposable disposables = new CompositeDisposable();
    void Awake()
    {
        InventorySlotFactory.CreateSlots(trashCanData);

        trashCanData.inventoryData.OnPlayerSetItem
            .Where(change => change.index == 0)
            .Subscribe(_ =>
            {
                trashCanData.inventoryData.RemoveItemAt(0);
            })
            .AddTo(disposables);
    }
}


using UnityEngine;
using UnityEngine.UI;

public class Test : MonoBehaviour
{
    [SerializeField] Button button;

    void Start()
    {
        button.onClick.AddListener(DataSaver.Ins.SaveDataFn);
    }
}

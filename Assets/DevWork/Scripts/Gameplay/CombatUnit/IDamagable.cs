using System.Collections;
using System.Collections.Generic;
using UniRx;
using UnityEngine;

public interface IDamagable
{
    public void HandleClickDetection();
    public void HandleClick();
    public void HandleHold();
    public void HandleIdle();
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NormalState : ClickerState
{
    public override void OnEnter(PlayerController controller)
    {
        Debug.Log("Entered Normal state.");
    }

    public override void OnExit(PlayerController controller)
    {
        Debug.Log("Exited Normal state.");
    }

    public override void OnClick(IDamagable clickableObject)
    {
        clickableObject.HandleClick();
    }
}

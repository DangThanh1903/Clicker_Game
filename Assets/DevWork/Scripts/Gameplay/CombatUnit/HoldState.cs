using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HoldState : ClickerState
{
    public override void OnEnter(PlayerController controller)
    {
        Debug.Log("Entered Hold state.");
    }

    public override void OnExit(PlayerController controller)
    {
        Debug.Log("Exited Hold state.");
    }

    public override void OnHold(PlayerController controller, IDamagable clickableObject)
    {
        clickableObject.HandleHold();
        controller.UseMana();
    }

    public override void OnUpdate(PlayerController controller, IDamagable clickableObject)
    {
        // Regen is handled centrally in PlayerController.Update so it does not depend on target callbacks.
    }
}

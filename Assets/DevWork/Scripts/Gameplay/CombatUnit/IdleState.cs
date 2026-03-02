using UnityEngine;

public class IdleState : ClickerState
{
    public override void OnEnter(PlayerController controller)
    {
        Debug.Log("Entered Idle state.");
    }

    public override void OnExit(PlayerController controller)
    {
        Debug.Log("Exited Idle state.");
    }

    public override void OnClick(IDamagable clickableObject)
    {
        clickableObject.HandleClick();
    }

    public override void OnUpdate(PlayerController controller, IDamagable clickableObject)
    {
        controller.ProcessIdleAttack(clickableObject);
    }
}

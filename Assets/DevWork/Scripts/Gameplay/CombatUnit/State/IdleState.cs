public class IdleState : ClickerState
{
    public override void OnClick(IDamageReceiver clickableObject)
    {
        clickableObject.ApplyDamageInput(DamageInputKind.Click);
    }

    public override void OnUpdate(PlayerController controller, IDamageReceiver clickableObject)
    {
        controller.ProcessIdleAttack(clickableObject);
    }
}

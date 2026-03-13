public class IdleState : ClickerState
{
    public override void OnClick(IDamagable clickableObject)
    {
        clickableObject.ApplyDamageInput(DamageInputKind.Click);
    }

    public override void OnUpdate(PlayerController controller, IDamagable clickableObject)
    {
        controller.ProcessIdleAttack(clickableObject);
    }
}

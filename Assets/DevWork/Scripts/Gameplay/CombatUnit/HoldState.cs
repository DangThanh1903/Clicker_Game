public class HoldState : ClickerState
{
    public override void OnHold(PlayerController controller, IDamagable clickableObject)
    {
        clickableObject.ApplyDamageInput(DamageInputKind.Hold);
        controller.UseMana();
    }
}

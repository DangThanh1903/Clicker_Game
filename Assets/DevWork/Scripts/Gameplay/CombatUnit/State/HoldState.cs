public class HoldState : ClickerState
{
    public override void OnHold(PlayerController controller, IDamageReceiver clickableObject)
    {
        clickableObject.ApplyDamageInput(DamageInputKind.Hold);
        controller.UseMana();
    }
}

public class NormalState : ClickerState
{
    public override void OnClick(IDamageReceiver clickableObject)
    {
        clickableObject.ApplyDamageInput(DamageInputKind.Click);
    }
}

public class NormalState : ClickerState
{
    public override void OnClick(IDamagable clickableObject)
    {
        clickableObject.ApplyDamageInput(DamageInputKind.Click);
    }
}

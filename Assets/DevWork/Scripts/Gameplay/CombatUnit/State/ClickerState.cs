public abstract class ClickerState
{
    public virtual void OnEnter(PlayerController controller) { }

    public virtual void OnExit(PlayerController controller) { }

    public virtual void OnClick(IDamageReceiver clickableObject) { }

    public virtual void OnHold(PlayerController controller, IDamageReceiver clickableObject) { }

    public virtual void OnUpdate(PlayerController controller, IDamageReceiver clickableObject) { }
}

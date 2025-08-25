using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class ClickerState
{
    public virtual void OnEnter(PlayerController controller) { }

    public virtual void OnExit(PlayerController controller) { }

    public virtual void OnClick(IDamagable clickableObject) { }

    public virtual void OnHold(PlayerController controller, IDamagable clickableObject) { }

    public virtual void OnUpdate(PlayerController controller, IDamagable clickableObject) { }
}

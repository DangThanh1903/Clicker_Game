using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Buff System/Normal BuffSO")]
public class NormalBuffSO : BuffSO
{
    public override bool IsPermanent => duration <= 0;
}

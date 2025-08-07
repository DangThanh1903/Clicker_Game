using System.Collections;
using System.Collections.Generic;
using UnityEngine;
public interface IStatProvider
{
    IEnumerable<StatModifier> GetStatModifiers();
}


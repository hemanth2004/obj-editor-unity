using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "primitive", menuName = "")]
public class PrimitiveData : ScriptableObject
{
    [TextArea(15, 20)]
    public string primitiveText;
}

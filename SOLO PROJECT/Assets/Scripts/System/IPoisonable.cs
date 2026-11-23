using System.Collections;
using UnityEngine;

public interface IPoisonable
{
    IEnumerator PoisonDamage(int level);
}

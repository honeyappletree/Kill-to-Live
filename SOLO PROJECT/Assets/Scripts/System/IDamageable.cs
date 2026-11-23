using UnityEngine;
using System.Collections;

public interface IDamageable
{
    void TakeDamage(float damage);
    IEnumerator PoisonDamage(int level);
    void ApplyKnockback(Vector2 dir, float force, float duration);
}

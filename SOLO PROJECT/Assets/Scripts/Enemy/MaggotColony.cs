using UnityEngine;

/// <summary>
/// 구더기 군체 — 자연 소멸 없이 영구 유지되며, 사망 시 분열체(splitPrefab) 여러 마리를
/// 사망 지점 주변에 스폰한다. 분열체는 일반 Enemy(비-MaggotColony)로 구성해야
/// 재귀 분열이 발생하지 않는다.
/// </summary>
public class MaggotColony : Enemy
{
    [Header("분열 설정")]
    [SerializeField] private GameObject splitPrefab;
    [SerializeField] private int splitCount = 3;
    [SerializeField] private float splitScatterRadius = 0.6f;

    protected override void Die()
    {
        SpawnSplits();
        base.Die();
    }

    void SpawnSplits()
    {
        if (splitPrefab == null) return;

        for (int i = 0; i < splitCount; i++)
        {
            Vector2 offset = Random.insideUnitCircle * splitScatterRadius;
            Instantiate(splitPrefab, (Vector2)transform.position + offset, Quaternion.identity);
        }
    }
}

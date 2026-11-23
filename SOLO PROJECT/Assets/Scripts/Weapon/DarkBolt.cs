using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// MVP 무기 1 : Dark Bolt
/// - 플레이어 주위를 공전하며 닿는 적에게 주기적으로 피해를 줌
/// - 실제 충돌 판정은 OrbHitbox(동일 파일)가 담당
/// - 같은 적에게 fireInterval(0.5초)마다 한 번만 피해를 줌
///
/// 레벨업 수치
///   Lv.1 : 오브 1개, 데미지 20
///   Lv.2 : 오브 2개, 데미지 30
///   Lv.3 : 오브 3개, 데미지 40
/// </summary>
public class DarkBolt : WeaponBase
{
    [Header("투사체 설정")]
    [SerializeField] private GameObject projectilePrefab;

    [Header("레벨별 수치")]
    public static float[] damageLevels   = { 20f, 30f, 40f }; //레벨업 수치별 데미지
    [SerializeField] private float[] attackSpeed = { 1f, 0.9f, 0.8f };
    [SerializeField] private int[] projectileSpeed = { 10, 10, 12 };

    float currentProjectileSpeed; //현재 적용 중인 투사체 속도
    [SerializeField] private float explosionRadius = 2f;
    public override string WeaponName => "Dark Bolt";

    [Header("조준 범위 설정")]
    [Tooltip("뷰포트 경계(0~1)에서 안쪽으로 들어온 마진. 0.125 = 화면 가장자리 1/8 지점부터 조준 후보로 인정")]
    [Range(0f, 0.5f)]
    public float viewportMargin = 0.125f;

    AudioSource audioSource;
    public AudioClip clip;

    // ── 라이프사이클 ──────────────────────────────────────────

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        ApplyStats();

        if (projectilePrefab == null)
        {
            //Debug.LogError("projectilePrefab 비어있음");
        }
        WeaponBase weapon = GetComponent<WeaponBase>();
    }

    // ── WeaponBase 구현 ───────────────────────────────────────

    protected override void Fire()
    {
        Transform nearest = FindNearestEnemyInViewport();
        if (nearest == null) return;

        Vector2 dir = ((Vector2)nearest.position - (Vector2)transform.position).normalized;
        GameObject projectilePrefab = Resources.Load<GameObject>("Dark Bolt");

        GameObject obj = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        Projectile projectile = obj.GetComponent<Projectile>();
        projectile.Init(dir, baseDamage, currentProjectileSpeed);
        audioSource.PlayOneShot(clip);
        projectile.SetExplosion(weaponLevel >= 3, explosionRadius, weaponLevel);
    }

    protected override void OnLevelUp()
    {
        ApplyStats();
    }

    void ApplyStats()
    {
        int index = Mathf.Clamp(weaponLevel - 1, 0, damageLevels.Length - 1);
        baseDamage = damageLevels[index];
        fireInterval = attackSpeed[index];
        currentProjectileSpeed = projectileSpeed[index];
    }

    /// <summary>
    /// Dark Bolt 전용 조준 후보 판정 — 화면 뷰포트 경계에서 viewportMargin만큼 안쪽으로
    /// 들어온 사각형 범위(x, y 각각 [viewportMargin, 1-viewportMargin]) 안에 있는 적만 후보로 인정.
    /// 화면 중심 기준 원형 판정이 아니라 x/y축에 각각 독립적으로 적용되는 사각형 판정이라
    /// 상/하/좌/우 어느 방향에서 들어와도 동일한 규칙이 적용된다.
    /// 후보가 하나도 없으면 null을 반환하며, Fire()의 null 체크가 그대로 발사 대기로 처리한다.
    /// Poison Cloud/Collapse Edge는 이 메서드를 쓰지 않고 WeaponBase.FindNearestEnemy()를 그대로 사용한다.
    /// </summary>
    Transform FindNearestEnemyInViewport()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform nearest = null;
        float minDist = float.MaxValue;

        foreach (GameObject obj in enemies)
        {
            if (!obj.TryGetComponent(out IDamageable _)) continue;

            Vector3 vp = Camera.main.WorldToViewportPoint(obj.transform.position);
            if (vp.z <= 0f) continue;

            if (vp.x < viewportMargin || vp.x > 1f - viewportMargin) continue;
            if (vp.y < viewportMargin || vp.y > 1f - viewportMargin) continue;

            float d = Vector2.Distance(transform.position, obj.transform.position);
            if (d < minDist) { minDist = d; nearest = obj.transform; }
        }
        return nearest;
    }

//#if UNITY_EDITOR
//    // 임시 디버그 Gizmo — Scene 뷰에서 유효 조준 경계 사각형을 시각적으로 확인하기 위함.
//    // 확인 후 최종 커밋 전 제거할 것.
//    void OnDrawGizmos()
//    {
//        if (Camera.main == null) return;

//        Camera cam = Camera.main;
//        Vector3 bl = cam.ViewportToWorldPoint(new Vector3(viewportMargin, viewportMargin, cam.nearClipPlane + 1f));
//        Vector3 br = cam.ViewportToWorldPoint(new Vector3(1f - viewportMargin, viewportMargin, cam.nearClipPlane + 1f));
//        Vector3 tl = cam.ViewportToWorldPoint(new Vector3(viewportMargin, 1f - viewportMargin, cam.nearClipPlane + 1f));
//        Vector3 tr = cam.ViewportToWorldPoint(new Vector3(1f - viewportMargin, 1f - viewportMargin, cam.nearClipPlane + 1f));

//        Gizmos.color = Color.cyan;
//        Gizmos.DrawLine(bl, br);
//        Gizmos.DrawLine(br, tr);
//        Gizmos.DrawLine(tr, tl);
//        Gizmos.DrawLine(tl, bl);
//    }
//#endif
}
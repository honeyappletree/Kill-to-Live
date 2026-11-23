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
        Enemy nearest = FindNearestEnemy();
        if (nearest == null) return;

        Vector2 dir = ((Vector2)nearest.transform.position - (Vector2)transform.position).normalized;
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
}
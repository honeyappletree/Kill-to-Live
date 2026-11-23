using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// MVP 무기 2 : Poison Cloud
/// - 가장 가까운 적 방향으로 직선 발사, 최대 N적을 관통
/// - 기존 AutoAttack + Projectile 구조와 동일한 패턴으로 구현
/// - 발사체(PiercingProjectile)는 동일 파일에 정의
///
/// 레벨업 수치
///   Lv.1 : 데미지 8, 관통 3적
///   Lv.2 : 데미지 12, 관통 4적
///   Lv.3 : 데미지 18, 관통 5적
/// </summary>
public class PoisonCloud : WeaponBase
{
    [Header("무기 프리팹 설정")]
    [SerializeField] private GameObject gasPrefab; //무기 프리팹 변수

    [Header("레벨별 수치")]
    public static float[] gasTickDmg = { 8f, 12f, 18f }; //레벨별 가스 틱 데미지
    float[] gasRemainTime = { 2f, 2f, 1.8f }; //가스 잔류 시간
    float[] gasAttackInterval = { 2.5f, 2.2f, 2f }; //가스 공격 간격
    public static int[] poisonTickDmg = { 3, 4, 6 }; //독 상태 틱 데미지
    public static int[] poisonTickTime = { 3, 4, 5 }; //독 틱 지속시간

    float gasTickInterval = 0.5f; //가스 틱 간격
    float tickTimer = 0;
    float poisonTickTimer = 0;
    float poisonTickInterval = 1f; //독 틱 간격
    float gasStartTime = 0;
    [SerializeField] private float explosionRadius = 2f; //범위 반경
    [SerializeField] private GameObject player;

    bool outside = false;
    public override string WeaponName => "Poison Cloud";
    Coroutine tickCoroutine;
    Coroutine poisonCoroutine;

    AudioSource audioSource;
    public AudioClip clip;
    Collider2D[] colliders;

    // ── 라이프사이클 ──────────────────────────────────────────

    void Awake()
    {
        colliders = GetComponentsInChildren<Collider2D>();
        ApplyStats();
        if(transform.parent != null)
        {
            foreach (var c in colliders) c.enabled = false;
        }
    }

    private void Start()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private void Update()
    {
        tickTimer += Time.deltaTime;
        if(tickTimer >= gasAttackInterval[weaponLevel - 1])
        {
            tickTimer = 0;
            Debug.Log("gasAttackInterval: " + gasAttackInterval[weaponLevel - 1]);
            Fire();
        }
    }

    // ── WeaponBase 구현 ───────────────────────────────────────

    protected override void Fire()
    {
        GameObject gasPrefab = Instantiate(Resources.Load<GameObject>("PoisonCloud"), transform.position, Quaternion.identity);
        //audioSource.PlayOneShot(clip);
        var cloud = gasPrefab.GetComponent<PoisonCloud>();
        cloud.weaponLevel = weaponLevel;
        cloud.StartCoroutine(cloud.EnableColliderAfter(0.2f));
        Destroy(gasPrefab, gasRemainTime[weaponLevel - 1]);
        Debug.Log("gasRemainTIme: " + gasRemainTime[weaponLevel - 1]);
    }

    protected override void OnLevelUp()
    {
        ApplyStats();
    }

    void ApplyStats()
    {
        int index = Mathf.Clamp(weaponLevel - 1, 0, gasTickDmg.Length - 1);
        baseDamage = gasTickDmg[index];
        fireInterval = gasAttackInterval[index];
    }

    public void SetLevel(int level)
    {
        weaponLevel = level;
    }

    List<IDamageable> enemiesInRange = new List<IDamageable>();

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.gameObject.CompareTag("Enemy")) return;

        IDamageable target = collision.GetComponentInParent<IDamageable>();
        if (target == null || enemiesInRange.Contains(target)) return;

        enemiesInRange.Add(target);
        StartCoroutine(TickDamage(target, weaponLevel));
        StartCoroutine(target.PoisonDamage(weaponLevel));
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.gameObject.CompareTag("Enemy")) return;

        IDamageable target = collision.GetComponentInParent<IDamageable>();
        if (target != null) enemiesInRange.Remove(target);
    }

    public IEnumerator EnableColliderAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        foreach (var c in colliders) if (c != null) c.enabled = true;
    }

    IEnumerator TickDamage(IDamageable target, int level)
    {
        while (enemiesInRange.Contains(target))   // 범위를 벗어나면 종료
        {
            yield return new WaitForSeconds(0.5f);

            // 파괴된 오브젝트 체크 (인터페이스는 UnityEngine.Object로 캐스팅)
            if ((target as Object) == null) yield break;
            if (!enemiesInRange.Contains(target)) yield break;

            target.TakeDamage(gasTickDmg[level - 1]);
        }
    }
}


using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MVP 무기 3 : Collapse Edge
/// - 발사 시점의 가장 가까운 적을 타겟으로 설정하고 추적
/// - 미사일 발사체(MissileProjectile)는 동일 파일에 정의
///
/// 레벨업 수치
///   Lv.1 : 데미지 30, 속도 6
///   Lv.2 : 데미지 42, 속도 7
///   Lv.3 : 데미지 80, 속도 8.5
/// </summary>
public class CollapseEdge : WeaponBase
{
    [Header("이펙트")]
    [SerializeField] private GameObject effectPrefab;

    [Header("레벨별 수치")]
    [SerializeField] private float[] damageLevels = { 30f, 42f, 80f };
    [SerializeField] private float[] attackSpeed = { 1.2f, 1f, 0.8f };

    [SerializeField] private float[] attackAngle = { 90f, 120f, 150f };
    [SerializeField] private float[] attackRange = { 3f, 3.5f, 4f };

    [SerializeField] private float offsetDistance = 3.3f;

    //[SerializeField] private float knockbackForce = 3f;
    //[SerializeField] private LayerMask enemyLayer;
    [Header("넉백")]
    [SerializeField] private float knockbackForce = 8f;
    [SerializeField] private float knockbackDuration = 0.25f;

    [Header("판정 보정")]
    [SerializeField] private float hitMargin = 0.5f; // 후보 수집용 여유 (실제 판정과 무관)

    private float currentAngle;
    private float currentRange;

    private PlayerController player;
    int facingDirection = 1; //오른쪽: 1, 왼쪽: -1

    private SpriteRenderer sr;
    private Collider2D col;
    Animator anim;

    public override string WeaponName => "Collapse Edge";

    void Awake()
    {
        ApplyStats();
        sr = GetComponent<SpriteRenderer>();
        col = GetComponent<Collider2D>();
        anim = GetComponent<Animator>();

        //sr.enabled = false;  // 처음엔 안보이게
        //col.enabled = false; // 판정도 꺼두기
    }

    void Start()
    {
        player = FindObjectOfType<PlayerController>();
    }

    protected override void Update()
    {
        if(player == null)
        {
            return;
        }
        
        Vector2 dir = player.LastMoveDirection;

        if(dir.x > 0.1f)
        {
            facingDirection = 1;
        }
        else if(dir.x < -0.1f)
        {
            facingDirection = -1;
        }

        transform.localPosition = new Vector3(offsetDistance * facingDirection, 0f, 0f);

        if(facingDirection == 1)
        {
            transform.localRotation = Quaternion.Euler(0, 0, -90);
        }
        else
        {
            transform.localRotation = Quaternion.Euler(0, 0, 90);
        }
        base.Update();
    }

    protected override void Fire()
    {
        Debug.Log("Fire 호출됨");
        sr.enabled = true;  // 보이게
        col.enabled = true; // 판정 켜기
        anim.Play("CollapseEdge_Attack", 0, 0f);
        Vector2 attackDir = new Vector2(facingDirection, 0f);
        AttackSector(attackDir);
        StartCoroutine(HideAfterAnimation());
    }

    protected override void OnLevelUp()
    {
        ApplyStats();
    }

    void ApplyStats()
    {
        int index = Mathf.Clamp(
            weaponLevel - 1,
            0,
            damageLevels.Length - 1);

        baseDamage = damageLevels[index];
        fireInterval = attackSpeed[index];

        currentAngle = attackAngle[index];
        currentRange = attackRange[index];
    }

    void AttackSector(Vector2 attackDir)
    {
        Vector2 origin = player.transform.position;
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, currentRange + hitMargin);

        var damaged = new HashSet<IDamageable>();   // 한 적에게 중복 타격 방지

        foreach (Collider2D hit in hits)
        {
            IDamageable target = hit.GetComponentInParent<IDamageable>();
            if (target == null || !damaged.Add(target)) continue;

            Vector2 toTarget = (Vector2)hit.transform.position - origin;
            if (toTarget.magnitude > currentRange) continue;

            float angle = Vector2.Angle(attackDir, toTarget);
            if (angle > currentAngle * 0.5f) continue;

            target.TakeDamage(baseDamage);
            target.ApplyKnockback(toTarget.normalized, knockbackForce, knockbackDuration);
        }
    }

    void SpawnEffect(Vector2 attackDir)
    {
        float angle =
        Mathf.Atan2(
            attackDir.y,
            attackDir.x)
        * Mathf.Rad2Deg;

        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    IEnumerator HideAfterAnimation()
    {
        Debug.Log("코루틴 시작");
        yield return new WaitForSeconds(2f);
        Debug.Log("2초 지남");
        sr.enabled = false;  // 안보이게
        col.enabled = false; // 판정 끄기
    }
}


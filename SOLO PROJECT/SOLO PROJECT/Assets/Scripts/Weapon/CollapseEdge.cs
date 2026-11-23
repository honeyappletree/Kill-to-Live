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

    [SerializeField] private float knockbackForce = 20f;
    //[SerializeField] private LayerMask enemyLayer;

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
        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                transform.position,
                currentRange);
        Debug.Log("감지된 오브젝트 수: " + hits.Length);


        foreach (Collider2D hit in hits)
        {
            Debug.Log(hit.name);
            Vector2 targetDir =
                ((Vector2)hit.transform.position -
                (Vector2)transform.position).normalized;

            float angle =
                Vector2.Angle(
                    attackDir,
                    targetDir);

            if (angle <= currentAngle * 0.5f)
            {
                Enemy enemy =
                    hit.GetComponent<Enemy>();
                Debug.Log(hit.name + " angle: " + angle + " / 허용각도: " + currentAngle * 0.5f);

                if (enemy != null)
                {
                    enemy.TakeDamage(baseDamage);
                    Debug.Log(enemy.name + " " + baseDamage + " 데미지 입힘");
                    Rigidbody2D rb = enemy.GetComponent<Rigidbody2D>();

                    if (rb != null)
                    {
                        enemy.ApplyKnockback(targetDir, knockbackForce);
                        Debug.Log(enemy.name + " 넉백 적용");
                    }
                    else
                    {
                        Debug.Log(enemy.name + " rb가 null");
                    }
                }
            }
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


using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem.Processors;


public class DashOnSpawnOverride : MonoBehaviour, IDamageable
{
    [Header("보스 패턴1(병사 소환)")]
    [SerializeField] float chargeSpeed = 7f; //돌진 속도
    [SerializeField] float chaseSpeed = 2.5f; //감속 후 속도
    [SerializeField] float maxChargeDistance = 8f;

    [Header("전투 스탯")]
    [SerializeField] float maxHP = 50f;
    [SerializeField] float contactDamagePerSec = 6f; //접촉 공격력
    [SerializeField] float killHealValue = 4f; //처치 회복량

    [Header("사망 연출 (점멸 후 소멸)")]
    public float flickerDuration = 1.0f;
    public float flickerInterval = 0.12f;

    [Header("드롭 설정")]
    public GameObject xpOrbPrefab;
    public GameObject blackManaPrefab;
    public float dropHealAmount = 4f;

    private SoldierState state;
    public enum SoldierState { Charge, Chase, Dead }

    private Rigidbody2D rigid;
    private Vector2 chargeDir;
    private SpriteRenderer _sr;
    float traveled;
    float currentHP;
    Transform player;
    bool isDead;
    bool isKnockedBack;

    private DamageFlash damageFlash; //DamageFlash 스크립트 정보를 불러오기 위함

    void Awake()
    {
        rigid = GetComponent<Rigidbody2D>();
        currentHP = maxHP;
        damageFlash = GetComponent<DamageFlash>();
        _sr = GetComponentInChildren<SpriteRenderer>();
    }

    void Start()
    {
        if (PlayerStats.Instance != null)
            player = PlayerStats.Instance.transform;
    }

    public void LaunchDash(Vector2 targetPos)
    {
        chargeDir = (targetPos - (Vector2)transform.position).normalized;
        traveled = 0f;
        state = SoldierState.Charge;
        StartCoroutine(ChargeRoutine());
    }

    IEnumerator ChargeRoutine()
    {
        while(state == SoldierState.Charge)
        {
            Vector2 step = chargeDir * chargeSpeed * Time.deltaTime;
            rigid.MovePosition(rigid.position + step);
            traveled += step.magnitude;

            if(traveled >= maxChargeDistance)
            {
                EnterChase();
                yield break;
            }
            yield return null;
        }
    }

    void EnterChase()
    {
        state = SoldierState.Chase;
    }

    void FixedUpdate()
    {
        if (state != SoldierState.Chase || isKnockedBack) return;
        Vector2 dir = ((Vector2)player.position - rigid.position).normalized;
        rigid.linearVelocity = dir * chaseSpeed;

        // 이동 방향에 따라 스프라이트 좌우 반전
        if (_sr != null)
        {
            if (dir.x < 0f) _sr.flipX = true;
            else if (dir.x > 0f) _sr.flipX = false;
        }
    }

    public void TakeDamage(float damage)
    {
        currentHP -= damage;
        Debug.Log("solider's currentHP: " + currentHP);
        damageFlash.CallDamageFlash();
        if (currentHP <= 0f)
        {
            isDead = true;
            Die();
        }
    }

    void Die()
    {
        state = SoldierState.Dead;
        //PlayerStats.Instance.AddTimeReward(killHealValue);
        Destroy(gameObject);
        if (blackManaPrefab != null)
        {
            var orb = Object.Instantiate(blackManaPrefab, transform.position, Quaternion.identity);
            orb.GetComponent<BlackManaOrb>()?.Init(dropHealAmount);
        }
    }

    public IEnumerator PoisonDamage(int level)
    {
        if (_sr == null) yield break;
        Color originalColor = _sr.color;
        _sr.color = new Color(0.9012212f, 0.4962264f, 1, 1);

        float duration = PoisonCloud.poisonTickTime[level - 1];
        float elapsed = 0f;

        while (elapsed < duration)
        {
            yield return new WaitForSeconds(1f);
            if (this == null || isDead) yield break;
            elapsed += 1f;
            TakeDamage(PoisonCloud.poisonTickDmg[level - 1]);
        }
        if (_sr != null) _sr.color = originalColor;
    }

    public void ApplyKnockback(Vector2 dir, float force, float duration)
    {
        if (isDead || state != SoldierState.Chase) return;
        StartCoroutine(KnockbackRoutine(dir, force, duration));
    }

    IEnumerator KnockbackRoutine(Vector2 dir, float force, float duration)
    {
        isKnockedBack = true;
        rigid.linearVelocity = dir * force;
        yield return new WaitForSeconds(duration);
        isKnockedBack = false;
    }

    void OnTriggerStay2D(Collider2D col)
    {
        if (state == SoldierState.Dead) return;
        if (col.CompareTag("Player"))
            PlayerStats.Instance?.TakeDamage(contactDamagePerSec * Time.fixedDeltaTime);
    }
}

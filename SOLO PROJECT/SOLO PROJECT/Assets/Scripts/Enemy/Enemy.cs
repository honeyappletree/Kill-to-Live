using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem.Processors;

/// <summary>
/// 언데드 적 AI
/// - 플레이어를 추적하여 접촉 데미지를 줌
/// - 처치 시 플레이어 생명유지시간 회복 (초과분은 ExpOverflowConverter가 경험치 변환)
///
/// GDD v2: HP와 생명유지시간이 통합 운용됨.
/// 몬스터의 내구력도 maxLifeTime/currentLifeTime 단일 수치로 관리한다.
///
/// MonsterDataSO가 연결된 경우 해당 수치를 우선 적용한다.
/// 연결되지 않은 경우 Inspector 직접 입력값을 그대로 사용한다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class Enemy : MonoBehaviour
{
    [Header("몬스터 데이터 SO (연결 시 아래 수치 자동 덮어씀)")]
    [SerializeField] private MonsterDataSO monsterData;

    [Header("전투 스탯 (GDD v2: lifeTime = 내구력)")]
    public float maxLifeTime          = 50f;
    public float moveSpeed            = 1f;
    public float contactDamagePerSec  = 20f;

    [Header("처치 회복량 (초과분은 ExpOverflowConverter → 경험치 자동 변환)")]
    public float timeReward = 5f;

    [Header("드롭 설정")]
    public GameObject xpOrbPrefab;
    public GameObject blackManaPrefab;
    public float dropHealAmount = 3f;

    private float           currentLifeTime;
    private Rigidbody2D     rb;
    private Transform       player;
    private bool            initialized = false;
    private Animator        _anim;
    private SpriteRenderer  _sr;
    bool isDead;
    bool isCharging = false;
    Vector2 chargeDirection;
    bool isWaitingRespawn = false;
    private bool isKnockback = false;

    private DamageFlash damageFlash; //DamageFlash 스크립트 정보를 불러오기 위함

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale   = 0f;
        rb.freezeRotation = true;
        rb.bodyType       = RigidbodyType2D.Dynamic;

        GetComponent<BoxCollider2D>().isTrigger = true;

        _anim = GetComponentInChildren<Animator>();
        _sr   = GetComponentInChildren<SpriteRenderer>();

        if (monsterData != null)
            ApplyMonsterData(monsterData);

        currentLifeTime = maxLifeTime;
    }

    void Start()
    {
        if (!initialized)
            currentLifeTime = maxLifeTime;

        if (PlayerStats.Instance != null)
            player = PlayerStats.Instance.transform;

        damageFlash = GetComponent<DamageFlash>();
    }

    /// <summary>스포너에서 웨이브 강도에 따라 스탯 초기화</summary>
    public void Init(int wave)
    {
        float waveMultiplier = 1f + wave * 0.15f;
        maxLifeTime     *= waveMultiplier;
        currentLifeTime  = maxLifeTime;
        moveSpeed       += wave * 0.04f;
        initialized      = true;
    }

    public void SetChargeDirection(Vector2 direction)
    {
        isCharging = true;
        chargeDirection = direction;
    }

    void FixedUpdate()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.Playing)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isDead == true)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (isKnockback) return;

        if (isCharging)
        {
            rb.linearVelocity = chargeDirection * moveSpeed;

            if(_sr != null)
            {
                if(chargeDirection.x < 0f)
                {
                    _sr.flipX = true;
                }
                else if(chargeDirection.x > 0f)
                {
                    _sr.flipX = false;
                }
            }

            if(!IsOnScreen() && !isWaitingRespawn)
            {
                isWaitingRespawn = true;
                //StartCoroutine(RespawnOpposite());
            }
            return;
        }

        if (player == null)
        {
            if (PlayerStats.Instance != null)
                player = PlayerStats.Instance.transform;
            return;
        }

        Vector2 dir = ((Vector2)player.position - rb.position).normalized;
        rb.linearVelocity = dir * moveSpeed;

        // 이동 방향에 따라 스프라이트 좌우 반전
        if (_sr != null)
        {
            if (dir.x < 0f)       _sr.flipX = true;
            else if (dir.x > 0f)  _sr.flipX = false;
        }
    }

    public void ApplyKnockback(Vector2 direction, float force)
    {
        StartCoroutine(KnockbackRoutine(direction, force));
    }

    IEnumerator KnockbackRoutine(Vector2 direction, float force)
    {
        isKnockback = true;
        rb.linearVelocity = direction * force;
        yield return new WaitForSeconds(0.2f);
        isKnockback = false;
        rb.linearVelocity = Vector2.zero;
    }

    /// <summary>발사체에 의한 피해 처리 (몬스터 생명유지시간 감소)</summary>
    public void TakeDamage(float damage)
    {
        currentLifeTime -= damage;
        Debug.Log("damageFlash: " + damageFlash);
        damageFlash.CallDamageFlash();
        if (currentLifeTime <= 0f)
        {
            isDead = true;
            Die();
        }
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        if (col.CompareTag("Player"))
        {
            PlayerStats.Instance?.TakeDamage(10);
            Debug.Log("player의 hp" + PlayerStats.Instance.currentLifeTime);
        }
    }

    void Die()
    {
        PlayerStats.Instance?.AddKill();
        PlayerStats.Instance?.AddTimeReward(timeReward);

        if(PlayerStats.Instance.currentLifeTime < PlayerStats.Instance.maxLifeTimeCap)
        {
            if (blackManaPrefab != null)
            {
                var orb = Object.Instantiate(blackManaPrefab, transform.position, Quaternion.identity);
                orb.GetComponent<BlackManaOrb>()?.Init(dropHealAmount);
                Debug.Log("black mana를 얻고 회복되었습니다.");
            }
        }
        if (PlayerStats.Instance.currentLifeTime >= PlayerStats.Instance.maxLifeTimeCap)
        {
            if (xpOrbPrefab != null)
                Object.Instantiate(xpOrbPrefab, transform.position, Quaternion.identity);
        }

        if (_anim != null && isDead == true)
        {
            _anim.SetTrigger("doDead");
            rb.linearVelocity = Vector2.zero;
        }

        StartCoroutine(DestroyAfterDeath(0.4f));
    }

    IEnumerator DestroyAfterDeath(float delay)
    {
        yield return new WaitForSeconds(delay);
        Destroy(gameObject);
    }

    bool IsOnScreen()
    {
        Vector3 vp = Camera.main.WorldToViewportPoint(transform.position);
        return vp.x > -0.2f && vp.x < 1.2f && vp.y > -0.2f && vp.y < 1.2f;
    }

    void ApplyMonsterData(MonsterDataSO data)
    {
        maxLifeTime         = data.MaxHP;
        moveSpeed           = data.MoveSpeed;
        contactDamagePerSec = data.AttackDamage;
        timeReward          = data.TimeReward;
    }

    public IEnumerator PoisonDamage(int level)
    {
        Color originalColor = _sr.color; // 원래 색 저장
        _sr.color = new Color(0.9012212f, 0.4962264f, 1, 1); // 보라색으로 변경(독 상태를 시각적으로 표현하기 위함)

        float duration = PoisonCloud.poisonTickTime[level - 1];
        float elapsed = 0f;

        while(elapsed < duration)
        {
            yield return new WaitForSeconds(1f);
            if(this == null || gameObject == null)
            {
                yield break;
            }
            elapsed += 1f;
            TakeDamage(PoisonCloud.poisonTickDmg[level - 1]);
        }
        if (_sr != null)
        {
            _sr.color = originalColor;
        }
    }

#if UNITY_EDITOR
    //void OnDrawGizmosSelected()
    //{
    //    Gizmos.color = Color.red;
    //    Gizmos.DrawWireSphere(transform.position, GetComponent<BoxCollider2D>()?.radius ?? 0.5f);
    //}
#endif
}

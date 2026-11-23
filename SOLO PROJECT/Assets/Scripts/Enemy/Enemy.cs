using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem.Processors;
using UnityEngine.UIElements;

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
public class Enemy : MonoBehaviour, IDamageable
{
    [Header("몬스터 데이터 SO (연결 시 아래 수치 자동 덮어씀)")]
    [SerializeField] private MonsterDataSO monsterData;

    [Header("전투 스탯 (GDD v2: lifeTime = 내구력)")]
    public float maxLifeTime = 50f;
    public float moveSpeed = 1f;
    public float contactDamagePerSec = 20f;

    [Header("처치 회복량 (초과분은 ExpOverflowConverter → 경험치 자동 변환)")]
    public float timeReward = 5f;

    [Header("드롭 설정")]
    public GameObject xpOrbPrefab;
    public GameObject blackManaPrefab;
    public float dropHealAmount = 3f;

    [Header("자동 디스폰 (0 = 비활성, 예: 구더기 분열체는 10초)")]
    public float autoDespawnTime = 0f;

    [Header("사망 연출 (점멸 후 소멸)")]
    public float flickerDuration = 1.0f;
    public float flickerInterval = 0.12f;

    /// <summary>true면 Enemy의 기본 추적 이동을 멈추고 외부 컨트롤러(웨이브 이벤트 등)가 rb.MovePosition으로 이동을 전담</summary>
    [HideInInspector] public bool externallyControlled = false;

    private float currentLifeTime;
    private Rigidbody2D rb;
    private Transform player;
    private bool initialized = false;
    private Animator _anim;
    private SpriteRenderer _sr;
    protected bool isDead;
    /// <summary>외부(웨이브 이벤트 등)에서 사망 여부를 확인해 MovePosition 등 이동 처리를 건너뛰기 위한 공개 접근자</summary>
    public bool IsDead => isDead;
    bool isCharging = false;
    Vector2 chargeDirection;
    bool isWaitingRespawn = false;

    // ── 넉백 ──
    private Vector2 knockbackVelocity;
    private float knockbackTimer;
    private float knockbackDuration;

    // ── 몬스터끼리 물리적으로 밀어내는 솔리드 바디 ─────────────────
    // 기존 트리거 콜라이더(플레이어 접촉 데미지 판정용)는 그대로 두고,
    // 별도 자식 오브젝트에 논트리거 콜라이더를 추가해 "MonsterBody" 레이어끼리만
    // 충돌하도록 한다(includeLayers로 제한 — 전역 Physics2D 충돌 매트릭스는 무변경).
    private CircleCollider2D solidBodyCollider;
    private int fixedFrameCount = 0;
    private const int SolidBodyActivationDelayFrames = 2; // 스폰 직후 겹침 폭발(지터) 방지

    // 무기 투사체(Projectile/DarkBolt/PoisonCloud/CollapseEdge 등)가 OnTriggerEnter2D·
    // OverlapCircleAll로 피격 판정에 쓰는 히트 콜라이더 — solidBodyCollider(몬스터간 물리용)와는
    // 별개 컴포넌트이므로 Die()에서 따로 꺼줘야 사망 연출 중 추가 피격으로 Die()가 중복 호출되지 않는다.
    private BoxCollider2D hitCollider;

    private DamageFlash damageFlash; //DamageFlash 스크립트 정보를 불러오기 위함

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.bodyType = RigidbodyType2D.Dynamic;

        hitCollider = GetComponent<BoxCollider2D>();
        hitCollider.isTrigger = true;
        hitCollider.enabled = true;

        _anim = GetComponentInChildren<Animator>();
        _sr = GetComponentInChildren<SpriteRenderer>();
        damageFlash = GetComponent<DamageFlash>();

        if (monsterData != null)
            ApplyMonsterData(monsterData);

        currentLifeTime = maxLifeTime;

        SetupSolidBody();
    }

    /// <summary>몬스터끼리만 물리적으로 밀어내는 논트리거 콜라이더를 자식 오브젝트로 추가</summary>
    void SetupSolidBody()
    {
        int monsterBodyLayer = LayerMask.NameToLayer("MonsterBody");
        if (monsterBodyLayer < 0)
        {
            Debug.LogWarning("[Enemy] \"MonsterBody\" 레이어가 없습니다 — 몬스터 간 물리 충돌이 비활성화됩니다.");
            return;
        }

        GameObject solidBodyObj = new GameObject("SolidBody");
        solidBodyObj.transform.SetParent(transform, false);
        solidBodyObj.layer = monsterBodyLayer;

        solidBodyCollider = solidBodyObj.AddComponent<CircleCollider2D>();
        solidBodyCollider.isTrigger = false;

        // MonsterBody 레이어끼리만 충돌하도록 제한 (플레이어/투사체 등 다른 모든 레이어와는 상호작용 없음)
        int monsterBodyMask = 1 << monsterBodyLayer;
        solidBodyCollider.includeLayers = monsterBodyMask;
        solidBodyCollider.excludeLayers = ~monsterBodyMask;

        // 기존 트리거 콜라이더 크기를 참고해 대략적인 반경 산정
        var triggerCol = GetComponent<BoxCollider2D>();
        float approxRadius = triggerCol != null ? Mathf.Min(triggerCol.size.x, triggerCol.size.y) * 0.5f : 0.5f;
        solidBodyCollider.radius = approxRadius;

        solidBodyCollider.enabled = false; // 지연 활성화 — FixedUpdate에서 몇 프레임 뒤 켬
    }

    void Start()
    {
        if (!initialized)
            currentLifeTime = maxLifeTime;

        if (PlayerStats.Instance != null)
            player = PlayerStats.Instance.transform;

        if (autoDespawnTime > 0f)
            Invoke(nameof(AutoDespawn), autoDespawnTime);
    }

    void OnEnable()
    {
        if(GameManager.Instance != null)
        {
            GameManager.Instance.OnBossPhaseStart += HandleBossPhase;
        }
    }

    void OnDisable()
    {
        if(GameManager.Instance != null)
        {
            GameManager.Instance.OnBossPhaseStart -= HandleBossPhase;
        }
    }

    void HandleBossPhase()
    {
        if(isDead)
        {
            return;
        }
        isDead = true;
        Die();
    }

    /// <summary>생성 후 일정 시간이 지나면 처치 못해도 자동으로 사라짐 (구더기 분열체 등)</summary>
    void AutoDespawn()
    {
        if (isDead) return;
        Destroy(gameObject);
    }

    /// <summary>스포너에서 웨이브 강도에 따라 스탯 초기화</summary>
    public void Init(int wave)
    {
        float waveMultiplier = 1f + wave * 0.15f;
        maxLifeTime *= waveMultiplier;
        currentLifeTime = maxLifeTime;
        moveSpeed += wave * 0.04f;
        initialized = true;
    }

    public void SetChargeDirection(Vector2 direction)
    {
        isCharging = true;
        chargeDirection = direction;
    }

    void FixedUpdate()
    {
        // 스폰 직후 겹쳐 있는 상태로 솔리드 콜라이더가 즉시 켜지면 물리 솔버가 한 프레임에
        // 몰아서 밀어내며 튕겨나가는(지터) 현상이 생기므로, 일정 프레임 뒤에 활성화한다.
        if (solidBodyCollider != null && !solidBodyCollider.enabled)
        {
            fixedFrameCount++;
            if (fixedFrameCount >= SolidBodyActivationDelayFrames)
                solidBodyCollider.enabled = true;
        }

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

        if (externallyControlled)
        {
            rb.linearVelocity = Vector2.zero; // 이동은 외부 컨트롤러의 rb.MovePosition이 전담
            return;
        }

        if (knockbackTimer > 0f)
        {
            float t = knockbackTimer / knockbackDuration;
            rb.linearVelocity = knockbackVelocity * t;
            knockbackTimer -= Time.fixedDeltaTime;
            return;
        }

        if (isCharging)
        {
            rb.linearVelocity = chargeDirection * moveSpeed;

            if (_sr != null)
            {
                if (chargeDirection.x < 0f)
                {
                    _sr.flipX = true;
                }
                else if (chargeDirection.x > 0f)
                {
                    _sr.flipX = false;
                }
            }

            if (!IsOnScreen() && !isWaitingRespawn)
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
            if (dir.x < 0f) _sr.flipX = true;
            else if (dir.x > 0f) _sr.flipX = false;
        }
    }

    /// <summary>발사체에 의한 피해 처리 (몬스터 생명유지시간 감소)</summary>
    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentLifeTime -= damage;
        damageFlash.CallDamageFlash();
        if (currentLifeTime <= 0f)
        {
            isDead = true;
            Die();
        }
    }

    public void ApplyKnockback(Vector2 dir, float force, float duration)
    {
        if (isDead) return;
        knockbackVelocity = dir.normalized * force;
        knockbackDuration = duration;
        knockbackTimer = duration;
    }

    void OnTriggerStay2D(Collider2D col)
    {
        if (isDead) return;
        if (col.CompareTag("Player"))
        {
            PlayerStats.Instance?.TakeDamage(contactDamagePerSec);
        }
    }

    protected virtual void Die()
    {
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Kinematic; // 다른 몬스터의 SolidBody 충돌로 더 이상 밀리지 않도록
        if (solidBodyCollider != null) solidBodyCollider.enabled = false; // 죽은 개체는 몬스터간 충돌 자체에서 제외
        if (hitCollider != null) hitCollider.enabled = false; // 사망 연출(점멸) 중 무기 판정에 다시 걸려 Die()가 중복 호출되는 것을 방지

        PlayerStats.Instance?.AddKill();
        PlayerStats.Instance?.AddTimeReward(timeReward);

        // 처치 자체는 항상 검은 마나만 드롭한다 — 경험치는 이 오브를 실제로 주웠을 때
        // (HealLifeTime의 초과분 변환을 통해서만) 발생해야 하므로, 여기서 xpOrb를
        // 직접 드롭하는 분기는 두지 않는다.
        if (blackManaPrefab != null)
        {
            var orb = Object.Instantiate(blackManaPrefab, transform.position, Quaternion.identity);
            orb.GetComponent<BlackManaOrb>()?.Init(dropHealAmount);
        }

        if (_anim != null && isDead == true)
        {
            _anim.SetTrigger("doDead");
        }

        StartCoroutine(DeathFlicker());
    }

    /// <summary>점멸 후 소멸 — flickerInterval 간격으로 SpriteRenderer를 껐다 켜며 flickerDuration 동안 유지한 뒤 파괴.
    /// 파티클 버스트 등 사망 연출을 추가할 경우 이 코루틴 종료 시점(파괴 직전)에 맞추면 flickerDuration 변경에 자동으로 맞물린다.</summary>
    IEnumerator DeathFlicker()
    {
        if (_sr != null)
        {
            float elapsed = 0f;
            while (elapsed < flickerDuration)
            {
                _sr.enabled = !_sr.enabled;
                yield return new WaitForSeconds(flickerInterval);
                elapsed += flickerInterval;
            }
            _sr.enabled = true;
        }
        else
        {
            yield return new WaitForSeconds(flickerDuration);
        }

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

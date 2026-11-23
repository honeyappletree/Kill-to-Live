using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class BossController : MonoBehaviour, IDamageable
{
    public enum BossState
    {
        Spawn, // 등장 연출 — 이 동안은 패턴 발동/이동 없음
        Idle,
        P1_Telegraph, P1_Active,
        P2_Telegraph, P2_Active, P2_Recovery,
        P3_Active,
        Dead
    }

    [Header("등장 연출")]
    [SerializeField] private float spawnIntroDuration = 1.5f; // 미정 — 연출 길이 확정 후 수정

    [Header("몬스터 데이터 SO")]
    [SerializeField] private MonsterDataSO monsterData;

    [Header("기본 스탯 (SO 미연결 시 직접 입력)")]
    [SerializeField] private float maxHP = 1000f;
    [SerializeField] private float moveSpeed = 1.0f;
    [SerializeField] private float contactDamagePerSec = 25f;
    [SerializeField] private float timeReward = 30f;

    [Header("패턴 1 — 수렴 돌진 (병사 소환)")]
    [SerializeField] private GameObject soldierPrefab;
    [SerializeField] private int summonCount = 4;     // 세로 일렬 소환 수
    [SerializeField] private float summonSpacing = 0.6f;  // 병사 간 세로 간격
    [SerializeField] private float summonForwardOffset = 1.5f;  // 보스 앞쪽으로 띄우는 거리
    [SerializeField] private float summonInterval = 11f;   // 10~12s(병사 소환 주기)
    [SerializeField] private float p1TelegraphDuration = 0.65f; // 0.5~0.8s (마젠타 글로우)

    [Header("패턴 2 — 처형 스윕 (스켈레톤, 다음 단계에서 채울 예정)")]
    [SerializeField] private float meleeRange = 2.5f;
    [SerializeField] private float p2CooldownBase = 4.5f;  // 4~5s
    [SerializeField] private float p2TelegraphDuration = 0.9f;  // 0.8~1s
    [SerializeField] private float p2Damage = 11f;   // -10~-12
    [SerializeField] private float p2RecoveryDuration = 1.0f;  // 0.8~1.2s 집중딜 찬스

    [Header("패턴 3 — 마나 장벽 (스켈레톤, 다음 단계에서 채울 예정)")]
    [SerializeField] private GameObject shieldPrefab;
    [SerializeField] private float p3Interval = 15f;
    [SerializeField] private float p3Duration = 6f;

    private BossState state = BossState.Spawn;
    private Coroutine currentRoutine;

    private float currentHP;
    private Rigidbody2D rb;
    private Transform player;
    private bool isDead = false;

    private float p1Timer;
    private float p2Cooldown;
    private float p3Timer;

    [SerializeField] private GameObject clearPanel;
    [SerializeField] private Button mainButton;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.bodyType = RigidbodyType2D.Dynamic;
        GetComponent<CircleCollider2D>().isTrigger = true;

        if (monsterData != null)
            ApplyMonsterData(monsterData);

        currentHP = maxHP;
        //clearPanel.SetActive(false);
    }

    void Start()
    {
        if (PlayerStats.Instance != null)
            player = PlayerStats.Instance.transform;

        //currentRoutine = StartCoroutine(SpawnIntroRoutine());
    }

    // 보스는 넉백 면역 (돌진/패턴 연출이 깨지지 않도록)
    public void ApplyKnockback(Vector2 dir, float force, float duration) { }

    // 독 지속 데미지
    public IEnumerator PoisonDamage(int level)
    {
        float duration = PoisonCloud.poisonTickTime[level - 1];
        float elapsed = 0f;

        while (elapsed < duration)
        {
            yield return new WaitForSeconds(1f);
            if (this == null || isDead) yield break;
            elapsed += 1f;
            TakeDamage(PoisonCloud.poisonTickDmg[level - 1]);
        }
    }

    IEnumerator SpawnIntroRoutine()
    {
        // TODO: 소환 연출 재생 (이펙트/애니메이션)
        yield return new WaitForSeconds(spawnIntroDuration);

        // 연출 종료 시점부터 각 패턴 타이머 시작 — 등장하자마자 텔레그래프가 뜨는 걸 방지
        p1Timer = summonInterval;
        p2Cooldown = p2CooldownBase;
        p3Timer = p3Interval;

        ChangeState(BossState.Idle);
    }

    public void BeginCombat()
    {
        p1Timer = summonInterval;
        p2Cooldown = p2CooldownBase;
        p3Timer = p3Interval;
        ChangeState(BossState.Idle);
    }

    void Update()
    {
        if (isDead) return;
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.Boss)
            return;

        // 최우선: 상태 무관하게 사망 체크
        if (currentHP <= 0f && state != BossState.Dead)
        {
            ChangeState(BossState.Dead);
            return;
        }

        // 전이 판단은 Idle에서만 — 다른 상태는 자기 코루틴이 끝나야 Idle로 복귀
        if (state == BossState.Idle)
        {
            p1Timer -= Time.deltaTime;
            p2Cooldown -= Time.deltaTime;
            p3Timer -= Time.deltaTime;

            // 우선순위: P2(근접 처벌) > P1(회복 펌프) > P3
            if (p2Cooldown <= 0f && IsPlayerInMeleeRange())
            {
                ChangeState(BossState.P2_Telegraph);
            }
            else if (p1Timer <= 0f)
            {
                ChangeState(BossState.P1_Telegraph);
            }
            else if (p3Timer <= 0f)
            {
                ChangeState(BossState.P3_Active);
            }
        }
    }

    void FixedUpdate()
    {
        if (isDead) return;
        if (state != BossState.Idle) return; // 다른 상태의 이동은 각 코루틴이 직접 제어

        if (player == null)
        {
            if (PlayerStats.Instance != null)
                player = PlayerStats.Instance.transform;
            return;
        }

        Vector2 dir = ((Vector2)player.position - rb.position).normalized;
        rb.linearVelocity = dir * moveSpeed;
    }

    // ── 상태 전이 ────────────────────────────────────────────────

    void ChangeState(BossState next)
    {
        if (currentRoutine != null) StopCoroutine(currentRoutine);
        state = next;

        switch (next)
        {
            case BossState.P1_Telegraph: currentRoutine = StartCoroutine(P1_TelegraphRoutine()); break;
            case BossState.P1_Active: currentRoutine = StartCoroutine(P1_ActiveRoutine()); break;
            case BossState.P2_Telegraph: currentRoutine = StartCoroutine(P2_TelegraphRoutine()); break;
            case BossState.P2_Active: currentRoutine = StartCoroutine(P2_ActiveRoutine()); break;
            case BossState.P2_Recovery: currentRoutine = StartCoroutine(P2_RecoveryRoutine()); break;
            case BossState.P3_Active: currentRoutine = StartCoroutine(P3_ActiveRoutine()); break;
            case BossState.Dead: currentRoutine = StartCoroutine(DeathRoutine()); break;
        }
    }

    // ── 패턴 1: 수렴 돌진 (병사 소환) ─────────────────

    IEnumerator P1_TelegraphRoutine()
    {
        rb.linearVelocity = Vector2.zero;

        // TODO: 마젠타 글로우 연출 트리거
        yield return new WaitForSeconds(p1TelegraphDuration);

        if (isDead) yield break;
        ChangeState(BossState.P1_Active);
    }

    IEnumerator P1_ActiveRoutine()
    {
        // 조준은 스폰 시 1회 고정, 이후 재조준 없음 (호밍 아님) — 기획서: "스폰 순간 각 병사가
        // 플레이어의 현재 위치로 방향을 1회 고정"
        Vector2 aimPos = player != null ? (Vector2)player.position : rb.position;

        for (int i = 0; i < summonCount; i++)
        {
            // 세로 일렬 배치 축: 조준 방향에 수직인 축을 기준으로 나열
            Vector2 forwardDir = aimPos - rb.position;
            forwardDir = forwardDir.sqrMagnitude > 0.0001f ? forwardDir.normalized : (Vector2)transform.up;
            Vector2 perpendicular = new Vector2(-forwardDir.y, forwardDir.x);

            float offset = (i - (summonCount - 1) / 2f) * summonSpacing;
            Vector2 spawnPos = rb.position + forwardDir * summonForwardOffset + perpendicular * offset;

            if (soldierPrefab == null) continue;

            GameObject go = Instantiate(soldierPrefab, spawnPos, Quaternion.identity);

            // 각 병사는 spawnPos → aimPos(동일한 한 점) 방향으로 직진 돌진.
            // 시작 지점이 서로 다르기 때문에 4명 모두 같은 목표 지점으로 모이는
            // "부채꼴 수렴" 형태가 자연스럽게 만들어짐.
            if (go.TryGetComponent(out DashOnSpawnOverride dash))
            {
                dash.LaunchDash(aimPos);
            }
            else
            {
                Debug.LogWarning($"{soldierPrefab.name}에 DashOnSpawnOverride가 없어 수렴 돌진이 적용되지 않습니다.", go);
            }
        }

        p1Timer = summonInterval; // 다음 주기까지 타이머 리셋
        yield return null;
        ChangeState(BossState.Idle);
    }

    // ── 패턴 2: 처형 스윕 — 골격만, 다음 단계에서 채움 ────────────

    IEnumerator P2_TelegraphRoutine()
    {
        rb.linearVelocity = Vector2.zero;
        // TODO: 검 치켜드는 모션 + 바닥 부채꼴(90~120도) 범위 표시, 텔레그래프 시작 시 방향 1회 고정
        yield return new WaitForSeconds(p2TelegraphDuration);

        if (isDead) yield break;
        ChangeState(BossState.P2_Active);
    }

    IEnumerator P2_ActiveRoutine()
    {
        // TODO: 위→아래 시간차 스윕 판정, 부채꼴 범위 내 플레이어에게 p2Damage + 넉백
        yield return null;
        ChangeState(BossState.P2_Recovery);
    }

    IEnumerator P2_RecoveryRoutine()
    {
        // 이 구간이 플레이어 집중딜 찬스 — 보스는 아무것도 못 함
        yield return new WaitForSeconds(p2RecoveryDuration);

        p2Cooldown = p2CooldownBase;
        ChangeState(BossState.Idle);
    }

    // ── 패턴 3: 마나 장벽 — 골격만, 다음 단계에서 채움 ────────────

    IEnumerator P3_ActiveRoutine()
    {
        // TODO: 실드 HP/파괴 판정/미파괴 시 자연 소멸 처리는 shieldPrefab 쪽 스크립트에 위임
        GameObject shield = null;
        if (shieldPrefab != null && player != null)
        {
            Vector2 dirToPlayer = ((Vector2)player.position - rb.position).normalized;
            shield = Instantiate(shieldPrefab, rb.position + dirToPlayer * 1.5f, Quaternion.identity);
        }

        yield return new WaitForSeconds(p3Duration);

        if (shield != null) Destroy(shield);
        p3Timer = p3Interval;
        ChangeState(BossState.Idle);
    }

    // ── 사망 ─────────────────────────────────────────────────────

    IEnumerator DeathRoutine()
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static;

        PlayerStats.Instance?.AddKill();
        PlayerStats.Instance?.AddTimeReward(timeReward);
        // GameManager.Instance?.TriggerLevelUp();   // 레벨업 팝업과 겹치므로 제거

        // 사망 모션을 볼 시간 (필요 없으면 줄이거나 삭제)
        yield return new WaitForSeconds(1.5f);

        if (ClearUI.Instance != null)
            ClearUI.Instance.Show();
        else
            Debug.LogWarning("씬에 ClearUI가 없습니다.");

        Destroy(gameObject);
    }

    // ── 피해 처리 ────────────────────────────────────────────────

    public void TakeDamage(float damage)
    {
        if (isDead) return;
        currentHP -= damage;
        if (currentHP <= 0f && state != BossState.Dead)
            ChangeState(BossState.Dead);
    }
    
    void GoMain()
    {
        SceneManager.LoadScene("MainMenu");
    }

    void OnTriggerStay2D(Collider2D col)
    {
        if (isDead) return;
        if (col.CompareTag("Player"))
            PlayerStats.Instance?.TakeDamage(contactDamagePerSec * Time.fixedDeltaTime);
    }

    bool IsPlayerInMeleeRange()
    {
        if (player == null) return false;
        return Vector2.Distance(transform.position, player.position) <= meleeRange;
    }

    // ── 내부 유틸 ─────────────────────────────────────────────────

    void ApplyMonsterData(MonsterDataSO data)
    {
        maxHP = data.MaxHP;
        moveSpeed = data.MoveSpeed;
        contactDamagePerSec = data.AttackDamage;
        timeReward = data.TimeReward;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.2f, 0.6f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, meleeRange);
    }
#endif
}
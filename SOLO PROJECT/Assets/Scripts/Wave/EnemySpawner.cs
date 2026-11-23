using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 웨이브 기반 적 스폰 시스템
/// - 일정 시간마다 대규모 웨이브 발생
/// - 웨이브 사이에도 지속적으로 소규모 스폰
/// - 웨이브 진행에 따라 스폰량과 적 스탯 증가
/// - 게임 시작 10분(bossSpawnTime) 후 보스 소환 및 일반 스폰 중단
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("적 프리팹")]
    public GameObject[] enemyPrefab; //일반 몬스터 프리팹 배열
    // 더 이상 어떤 스폰 로직도 참조하지 않음 — 직선 돌격/배경 지속 스폰 모두 GetOffscreenSpawnOrigin()으로
    // 교체됨(원점 고정 좌표라 플레이어가 멀어지면 화면과 무관한 곳에서 스폰되는 버그의 원인이었음).
    // 씬의 마커 4개와 이 필드 자체는 삭제하지 않고 남겨둠 — 삭제 여부는 보고 후 판단 필요.
    public Transform[] enemyPattern1;

    [Header("보스 설정")]
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private float bossSpawnTime = 360f; // 6분 후 보스 소환

    [Header("웨이브 설정")]
    public float waveInterval = 20f;      // 웨이브 발생 간격 (초)
    public int baseWaveCount = 8;         // 첫 웨이브 스폰 수
    public int waveCountIncrement = 4;    // 웨이브당 추가 스폰 수

    [Header("지속 스폰 설정")]
    public float continuousInterval = 3f; // 지속 스폰 간격 (초)
    public int baseContinuousCount = 2;   // 기본 지속 스폰 수

    [Header("스폰 범위")]
    public float spawnRadius = 13f;       // 플레이어 기준 스폰 반경
    public float spawnRadiusVariance = 2f;

    [Header("3분 구간 지속 스폰 비율 (썩은좀비/해골하운드/기어다니는자)")]
    public float wave3_zombieRatio  = 0.45f;
    public float wave3_houndRatio   = 0.25f;
    public float wave3_crawlerRatio = 0.30f;

    [Header("4분 구간 지속 스폰 비율 — 구더기 군체 비율 기획 미확정, 임시 15% (썩은좀비/기어다니는자/구더기군체)")]
    public float wave4_zombieRatio  = 0.40f;
    public float wave4_crawlerRatio = 0.40f;
    public float wave4_maggotRatio  = 0.15f;

    [Header("5분 구간 지속 스폰 비율 — 세부 비율 기획 미확정, 임시 균등 (썩은좀비/해골하운드/기어다니는자/구더기군체)")]
    public float wave5_zombieRatio  = 0.25f;
    public float wave5_houndRatio   = 0.25f;
    public float wave5_crawlerRatio = 0.25f;
    public float wave5_maggotRatio  = 0.25f;

    [Header("웨이브 이벤트: 직선 돌격 (1분) — 화면 이탈 후 이 시간이 지나면 디스폰")]
    public float linearChargeDespawnTime = 7f; // 원형 감옥의 despawnTime(30초)과는 별개 값 — 화면 이탈 후에도 잠시 화면 밖에 머무를 시간을 좀 더 확보(기존 3f는 너무 빨리 사라진다는 피드백)
    [Tooltip("동심원 배치 — 중심 1마리 + 안쪽 링(linearChargeInnerRadius) + 바깥쪽 링(linearChargeSpawnRadius)으로 20마리를 채운다. " +
             "기존엔 반경 하나짜리 원주 위에만 배치해 중심부가 비어있었음. " +
             "콜라이더 지름(약 0.32) 기준: 중심-안쪽링(0.3) 간격 0.3, 안쪽링 내부 간격 0.3, 바깥쪽링 내부 간격 0.383 — " +
             "전부 지름과 비슷하거나 살짝 겹치는 수준이라 폭발적 지터 없이 국소적으로만 부대낌")]
    public float linearChargeSpawnRadius = 0.8f;  // 바깥쪽 링 반경
    public float linearChargeInnerRadius = 0.3f;  // 안쪽 링 반경
    public int linearChargeInnerRingCount = 6;    // 안쪽 링에 배치할 마리 수 (나머지는 바깥쪽 링, 중심 1마리는 별도)
    public float linearChargeExitSafetyTimeout = 15f; // 화면 이탈 판정이 끝내 안 되는 예외 상황 안전장치 (최대 생존시간)

    [Header("웨이브 이벤트: 원형 감옥 소프트락 방지 안전장치 (물리 밀집 저항으로 인한 방치성 정체만 차단)")]
    [Tooltip("이 반경 안쪽까지 조여지면 '충분히 수축 완료'로 간주 — 원형 감옥 자체엔 명시적 목표 반경 개념이 없어 새로 도입한 값. " +
             "Crawler 콜라이더 2.5배 확대(2026-07-06)에 맞춰 기존 1.5 x 2.5 = 3.75로 비례 재계산 " +
             "(콜라이더 지름이 커진 만큼 '몸으로 밀집할 수 있는 최소 반경'도 같이 커지는 게 물리적으로 맞음)")]
    public float circlePrisonTargetRadius = 3.75f;
    public float circlePrisonSnapCheckTime = 25f;      // 이 시점(수축 타이머) 이후부터 소프트락 여부를 검사
    public float circlePrisonRecentKillWindow = 5f;    // 최근 몇 초간의 처치 수를 볼지
    public int circlePrisonRecentKillThreshold = 2;    // 이 처치 수 이하이면 "방치"로 판정

    [Header("웨이브 이벤트: 회전 나선 (4분/4분30초) — 이동 속도 등 기획 미확정, 조정 가능")]
    public float spiralRadius = 4f;         // 진형 초기 반지름
    public float spiralMinRadius = 1.5f;    // 수축 최소 반지름
    public float spiralShrinkSpeed = 0.3f;  // 초당 반지름 감소량
    public float spiralRotationSpeed = 60f; // 초당 회전 각도(도)
    public float spiralDespawnTime = 20f;   // 생성 후 전체 디스폰 시간

    private int currentWave = 0; //현재 웨이브를 카운트하는 변수
    private float waveTimer;
    private float continuousTimer; //지속 스폰 타이머
    private bool bossSpawned = false;
    bool enemySpawned = false;
    bool waveEventActive = false; //웨이브 이벤트 진행 중 여부
    bool spiralEventActive = false; //회전 나선 진행 중 여부
    bool circleEventActive = false; //원형 감옥 패턴 진행 중 여부
    bool circlePrisonSpawned = false; //원형 감옥 패턴이 실행되었는지 확인하는 변수
    bool linearChargeSpawned = false; //직선 돌격 이벤트가 이미 시작되었는지 확인하는 변수 (30초간 6배치, 최초 1회만 시작)
    bool spiral4MinSpawned = false;   //회전 나선(4분)이 실행되었는지 확인하는 변수
    bool spiral430Spawned = false;    //회전 나선(4분30초)이 실행되었는지 확인하는 변수

    void Start()
    {
        if (enemyPrefab == null)
        {
            Debug.LogWarning("[EnemySpawner] enemyPrefab이 설정되지 않았습니다!");
            return;
        }

        // 게임 시작 시 첫 웨이브 즉시 스폰
        SpawnWave();
    }

    void Update()
    {
        if (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.Playing)
            return;

        // 보스 소환 조건: 6분 경과 & 아직 미소환
        if (!bossSpawned && GameManager.Instance.survivalTimer >= bossSpawnTime)
        {
            SpawnBoss();
            return; // 보스 소환 후 일반 스폰 중단 (enabled = false 처리됨)
        }

        if (enemyPrefab == null) return;

        // 웨이브 타이머
        waveTimer += Time.deltaTime;
        if (waveTimer >= waveInterval)
        {
            waveTimer = 0f;
            SpawnWave();
        }

        TriggerWaveEvent();

        if(!waveEventActive)
        {
            // 지속 스폰 타이머
            continuousTimer += Time.deltaTime;
            if (continuousTimer >= continuousInterval)
            {
                continuousTimer = 0f;
                int count = baseContinuousCount + currentWave;
                SpawnEnemies(count, currentWave);
            }
        }
    }

    void SpawnBoss()
    {
        bossSpawned = true;

        if (bossPrefab == null)
        {
            Debug.LogWarning("[EnemySpawner] bossPrefab이 설정되지 않았습니다!");
            return;
        }

        if (PlayerStats.Instance == null) return;
        Vector2 playerPos = PlayerStats.Instance.transform.position;
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector2 spawnPos = playerPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * spawnRadius;

        Instantiate(bossPrefab, spawnPos, Quaternion.identity);
        //UIManager.Instance?.ShowWaveNotification(0); // 0 = 보스 등장 알림 (ShowWaveNotification 확장 전까지 임시)
        Debug.Log("[EnemySpawner] 불멸의 사령관 소환 — 일반 스폰 중단");

        // 일반 웨이브 스폰 중단
        this.enabled = false;
    }

    void SpawnWave()
    {
        currentWave++;
    }

    void SpawnEnemies(int count, int wave)
    {
        if(GameManager.Instance.survivalTimer >= 0 && GameManager.Instance.survivalTimer < 60) //0분 ~ 1분
        {
            continuousInterval = Random.Range(2.5f, 3f);
            Debug.Log("continousInterval: " + continuousInterval);
            
            for(int i = 0; i < Random.Range(1, 3); i++)
            {
                GameObject enemy = Instantiate(enemyPrefab[0], GetOffscreenSpawnOrigin(), Quaternion.identity);
            }
        }
        else if (GameManager.Instance.survivalTimer >= 60 && GameManager.Instance.survivalTimer < 120) //1분 ~ 2분
        {
            continuousInterval = Random.Range(2f, 2.5f);
            Debug.Log("continousInterval: " + continuousInterval);

            for(int i = 0; i < Random.Range(2, 4); i++)
            {
                int random = Random.Range(0, 10);
                if (random < 7) //70% 확률
                {
                    GameObject enemy = Instantiate(enemyPrefab[0], GetOffscreenSpawnOrigin(), Quaternion.identity);
                }
                else //30% 확률
                {
                    GameObject enemy = Instantiate(enemyPrefab[1], GetOffscreenSpawnOrigin(), Quaternion.identity);
                }
            }
        }
        else if (GameManager.Instance.survivalTimer >= 120 && GameManager.Instance.survivalTimer < 180) //2분 ~ 3분
        {
            continuousInterval = Random.Range(1.5f, 2f);
            Debug.Log("continousInterval: " + continuousInterval);

            for(int i = 0; i < Random.Range(2, 4); i++)
            {
                int random = Random.Range(0, 10);
                if (random < 6) //60% 확률
                {
                    GameObject enemy = Instantiate(enemyPrefab[0], GetOffscreenSpawnOrigin(), Quaternion.identity);
                }
                else //40% 확률
                {
                    GameObject enemy = Instantiate(enemyPrefab[2], GetOffscreenSpawnOrigin(), Quaternion.identity);
                }
            }
        }
        else if (GameManager.Instance.survivalTimer >= 180 && GameManager.Instance.survivalTimer < 240) //3분 ~ 4분
        {
            continuousInterval = Random.Range(1f, 1.5f);

            int spawnCount = Random.Range(3, 5); // 3~4마리
            for (int i = 0; i < spawnCount; i++)
            {
                // 0: 썩은좀비, 1: 해골하운드, 2: 기어다니는자
                int prefabIndex = PickWeightedIndex(wave3_zombieRatio, wave3_houndRatio, wave3_crawlerRatio);
                Instantiate(enemyPrefab[prefabIndex], GetOffscreenSpawnOrigin(), Quaternion.identity);
            }
        }
        else if (GameManager.Instance.survivalTimer >= 240 && GameManager.Instance.survivalTimer < 300) //4분 ~ 5분
        {
            continuousInterval = Random.Range(1.5f, 2f);

            int spawnCount = Random.Range(3, 6); // 3~5마리
            for (int i = 0; i < spawnCount; i++)
            {
                // 0: 썩은좀비, 1: 기어다니는자, 2: 구더기군체
                int picked = PickWeightedIndex(wave4_zombieRatio, wave4_crawlerRatio, wave4_maggotRatio);
                int prefabIndex = picked == 0 ? 0 : (picked == 1 ? 2 : 3);
                Instantiate(enemyPrefab[prefabIndex], GetOffscreenSpawnOrigin(), Quaternion.identity);
            }
        }
        else if (GameManager.Instance.survivalTimer >= 300 && GameManager.Instance.survivalTimer < 360) //5분 ~ 6분
        {
            continuousInterval = 1f;

            int spawnCount = Random.Range(4, 7); // 4~6마리
            for (int i = 0; i < spawnCount; i++)
            {
                // 0: 썩은좀비, 1: 해골하운드, 2: 기어다니는자, 3: 구더기군체 (enemyPrefab 배열 순서와 동일)
                int prefabIndex = PickWeightedIndex(wave5_zombieRatio, wave5_houndRatio, wave5_crawlerRatio, wave5_maggotRatio);
                Instantiate(enemyPrefab[prefabIndex], GetOffscreenSpawnOrigin(), Quaternion.identity);
            }
        }
    }

    /// <summary>가중치 배열을 기반으로 인덱스를 무작위 선택 (합이 1이 아니어도 비율대로 정규화되어 동작)</summary>
    int PickWeightedIndex(params float[] weights)
    {
        float total = 0f;
        for (int i = 0; i < weights.Length; i++) total += weights[i];

        float roll = Random.Range(0f, total);
        float cumulative = 0f;
        for (int i = 0; i < weights.Length; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative) return i;
        }
        return weights.Length - 1;
    }

    void TriggerWaveEvent() //웨이브 이벤트 패턴
    {
        if (GameManager.Instance.survivalTimer >= 60 && GameManager.Instance.survivalTimer < 120 && !linearChargeSpawned)
        {
            StartCoroutine(RunLinearChargeEvent()); //1분: 직선 돌격 (30초간 5초 간격 6배치)
            linearChargeSpawned = true;
        }
        else if (GameManager.Instance.survivalTimer >= 120 && GameManager.Instance.survivalTimer < 180 && !circlePrisonSpawned)
        {
            SpawnCirclePrison(); //2분: 원형 감옥
            circlePrisonSpawned = true;
        }
        else if (GameManager.Instance.survivalTimer >= 240 && GameManager.Instance.survivalTimer < 270 && !spiral4MinSpawned)
        {
            StartCoroutine(SpawnRotatingSpiral()); //4분: 회전 나선
            spiral4MinSpawned = true;
        }
        else if (GameManager.Instance.survivalTimer >= 270 && GameManager.Instance.survivalTimer < 300 && !spiral430Spawned)
        {
            StartCoroutine(SpawnRotatingSpiral()); //4분30초: 회전 나선 (동일 로직 2회차)
            spiral430Spawned = true;
        }
    }

    /// <summary>
    /// 직선 돌격 이벤트 총괄 — 5초 간격으로 20마리 배치를 6회(0/5/10/15/20/25초) 스폰한 뒤,
    /// 스폰된 전체 개체(최대 120마리)가 전부 디스폰될 때까지 기다렸다가 종료한다.
    /// 원형 감옥·회전 나선과 달리 waveEventActive를 세팅하지 않는다 — 직선 돌격 중에도 배경
    /// 지속 스폰이 계속 나오도록 하는 것이 의도된 동작이라 지속 스폰 일시정지 대상에서 제외한다.
    /// </summary>
    IEnumerator RunLinearChargeEvent()
    {
        const int totalBatches = 6;
        const float batchInterval = 5f;
        // 배치 스케줄 종료 시점 기준 안전장치 — 개체별 DespawnAfterScreenExit는 최악의 경우에도
        // 자기 스폰 시점 기준 linearChargeExitSafetyTimeout(15s) + linearChargeDespawnTime(7s) 안에는
        // 정리되므로, 마지막 배치 기준 60초면 정상 케이스에서는 절대 발동하지 않는 넉넉한 여유가 있다.
        const float allClearSafetyTimeout = 60f;

        float startTime = GameManager.Instance != null ? GameManager.Instance.survivalTimer : -1f;
        Debug.Log($"[LinearChargeEvent] 시작 — survivalTimer={startTime:F2}s");

        var allSpawned = new List<GameObject>();

        for (int batch = 0; batch < totalBatches; batch++)
        {
            allSpawned.AddRange(SpawnLinearChargeBatch(batch + 1, totalBatches));
            if (batch < totalBatches - 1)
                yield return new WaitForSeconds(batchInterval);
        }

        float scheduleEndTime = GameManager.Instance != null ? GameManager.Instance.survivalTimer : -1f;
        Debug.Log($"[LinearChargeEvent] 배치 스폰 완료 — survivalTimer={scheduleEndTime:F2}s, 총 {allSpawned.Count}마리 디스폰 대기 중");

        // 스폰된 전체 개체가 전부 디스폰(화면 이탈 후 처리 완료)될 때까지 대기 — 개별 정리는
        // 각자의 DespawnAfterScreenExit 코루틴이 처리하므로 여기서는 전멸 여부만 매 프레임 확인한다.
        float waitElapsed = 0f;
        while (waitElapsed < allClearSafetyTimeout)
        {
            bool anyAlive = false;
            foreach (var e in allSpawned)
            {
                if (e != null) { anyAlive = true; break; }
            }
            if (!anyAlive) break;

            yield return null;
            waitElapsed += Time.deltaTime;
        }

        // 안전장치 — 방향 오류 등으로 화면 안에 무한정 머무는 개체가 있으면 강제 디스폰
        int forcedCount = 0;
        foreach (var e in allSpawned)
        {
            if (e != null)
            {
                Destroy(e);
                forcedCount++;
            }
        }
        if (forcedCount > 0)
            Debug.LogWarning($"[LinearChargeEvent] 안전장치 발동 — 배치 스폰 완료 후 {allClearSafetyTimeout}초 내 전원 이탈 실패, {forcedCount}마리 강제 디스폰");

        float endTime = GameManager.Instance != null ? GameManager.Instance.survivalTimer : -1f;
        Debug.Log($"[LinearChargeEvent] 완전 종료(전원 디스폰 확인) — survivalTimer={endTime:F2}s (경과 {endTime - startTime:F2}s)");
    }

    /// <summary>
    /// Camera.main의 현재 뷰포트 경계 기준으로 화면 4방향(좌/우/상/하) 중 하나를 랜덤으로 골라
    /// 그 바깥쪽(뷰포트 밖 edgeMargin만큼)의 월드 좌표를 반환한다. 호출 시점의 카메라(=플레이어 추적)
    /// 위치를 그대로 반영하므로, 배치마다 새로 호출하면 그 시점 기준으로 자동 재계산된다.
    /// Dark Bolt의 WorldToViewportPoint 방식과 짝을 이루는 역변환(ViewportToWorldPoint)이다.
    /// </summary>
    Vector3 GetOffscreenSpawnOrigin()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            // Camera.main은 "MainCamera" 태그로 매 호출 재검색한다 — 씬엔 태그가 붙은 카메라가
            // 하나뿐이고 항상 활성 상태임을 확인했지만, 그래도 null이 나오는 경우를 대비해
            // 태그와 무관하게 씬에 존재하는 카메라를 한 번 더 찾아본다.
            cam = Object.FindAnyObjectByType<Camera>();
            Debug.LogWarning($"[EnemySpawner] Camera.main이 null이라 GetOffscreenSpawnOrigin이 원점 근처 폴백 경로를 탈 뻔했습니다. FindAnyObjectByType 재탐색 결과: {(cam != null ? cam.name : "역시 못 찾음 — Vector3.zero로 폴백")}");
        }
        if (cam == null) return Vector3.zero; // 카메라가 정말 없는 극단적 예외 상황 폴백

        float z = -cam.transform.position.z; // 카메라에서 플레이 평면(Z=0)까지의 거리

        const float edgeMargin = 0.15f; // 뷰포트 경계(0~1) 밖으로 이만큼 더 나가 완전히 화면 밖에서 스폰

        int edge = Random.Range(0, 4); // 0=좌, 1=우, 2=상, 3=하
        float vx, vy;
        switch (edge)
        {
            case 0: vx = -edgeMargin;     vy = Random.Range(0f, 1f); break;
            case 1: vx = 1f + edgeMargin; vy = Random.Range(0f, 1f); break;
            case 2: vx = Random.Range(0f, 1f); vy = 1f + edgeMargin; break;
            default: vx = Random.Range(0f, 1f); vy = -edgeMargin;    break;
        }

        return cam.ViewportToWorldPoint(new Vector3(vx, vy, z));
    }

    List<GameObject> SpawnLinearChargeBatch(int batchNumber, int totalBatches) //직선 돌격 패턴 — 배치 1회분 (20마리)
    {
        const int count = 20;

        if (PlayerStats.Instance == null) return new List<GameObject>();

        // 스폰 위치(화면 모서리 4개 중 랜덤) — enemyPattern1(월드 원점 근처 고정 좌표)을 쓰던
        // 기존 방식은 플레이어가 원점에서 멀어지면 화면과 무관한 곳에서 스폰되는 버그가 있었음.
        // 매 배치(5초마다)마다 그 시점의 Camera.main 뷰포트를 다시 기준으로 삼아 화면 밖에서 스폰한다.
        Vector3 spawnOrigin = GetOffscreenSpawnOrigin();

        // 방향 벡터도 배치마다 그 시점의 플레이어 위치 기준으로 새로 계산.
        // (배치 내 20마리는 이 값을 동일하게 공유해 대형을 유지 — 배치 간에는 서로 달라도 됨)
        Vector2 playerPos = PlayerStats.Instance.transform.position;
        Vector2 sharedMoveDirection = (playerPos - (Vector2)spawnOrigin).normalized;

        List<GameObject> chargeGroup = new List<GameObject>(); // 디버그(간격 로그)용 + RunLinearChargeEvent의 전원 디스폰 추적용으로 반환

        // 동심원(원반) 배치 — 중심 1마리 + 안쪽 링 + 바깥쪽 링으로 20마리를 채운다.
        // (기존엔 반경 하나짜리 원주 위에만 늘어놔 중심부가 항상 비어있었음)
        int innerCount = linearChargeInnerRingCount;
        int outerCount = count - 1 - innerCount;

        List<Vector2> offsets = new List<Vector2>();
        offsets.Add(Vector2.zero); // 중심 1마리

        for (int i = 0; i < innerCount; i++)
        {
            float rad = (360f / innerCount) * i * Mathf.Deg2Rad;
            offsets.Add(new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * linearChargeInnerRadius);
        }

        for (int i = 0; i < outerCount; i++)
        {
            float rad = (360f / outerCount) * i * Mathf.Deg2Rad;
            offsets.Add(new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * linearChargeSpawnRadius);
        }

        foreach (var offset in offsets)
        {
            Vector2 spawnPos = (Vector2)spawnOrigin + offset;

            GameObject enemy = Instantiate(enemyPrefab[1], spawnPos, Quaternion.identity);
            chargeGroup.Add(enemy);

            var ai = enemy.GetComponent<Enemy>();
            if (ai != null)
            {
                ai.SetChargeDirection(sharedMoveDirection);
                // 화면 이탈 후 일정 시간 뒤 디스폰 — 스폰 시점 기준 타이머가 아니라
                // 매 프레임 뷰포트 이탈 여부를 확인하는 방식으로 전환 (작업2)
                // EnemySpawner가 아닌 enemy 자신에게 코루틴을 호스팅 — enemy가 파괴되면
                // Unity가 이 코루틴도 자동으로 멈춰서, EnemySpawner에 남겨진 채로 죽은
                // 오브젝트를 계속 참조하는 크래시 원인 자체를 구조적으로 제거한다.
                ai.StartCoroutine(DespawnAfterScreenExit(enemy));
            }
            else
            {
                Debug.LogWarning("AI 컴포넌트를 찾지 못했습니다!");
            }
        }
        return chargeGroup;
    }

   

    float MeasureFormationRadius(List<GameObject> group)
    {
        Vector2 centroid = Vector2.zero;
        int aliveCount = 0;
        foreach (var g in group)
        {
            if (g == null) continue;
            centroid += (Vector2)g.transform.position;
            aliveCount++;
        }
        if (aliveCount == 0) return 0f;
        centroid /= aliveCount;

        float sumDist = 0f;
        foreach (var g in group)
        {
            if (g == null) continue;
            sumDist += Vector2.Distance(centroid, g.transform.position);
        }
        return sumDist / aliveCount;
    }

    /// <summary>
    /// 작업2 — despawn 기준점을 스폰 시점이 아니라 "화면 뷰포트 이탈 시점"으로 변경.
    /// 매 프레임 뷰포트 위치를 확인하다가 [0,1] 범위를 벗어나는 순간부터 linearChargeDespawnTime(7초)을
    /// 센다. 화면에 끝내 들어오지 못하거나 이탈 판정이 안 되는 예외 상황을 대비해
    /// linearChargeExitSafetyTimeout(기본 15초) 안전장치를 둔다.
    /// </summary>
    IEnumerator DespawnAfterScreenExit(GameObject enemy)
    {
        float elapsed = 0f;
        bool hasExited = false;

        while (enemy != null && elapsed < linearChargeExitSafetyTimeout)
        {
            yield return null;

            // yield로 한 프레임 넘어간 사이 처치 등으로 파괴됐을 수 있으므로,
            // enemy 관련 필드에 접근하기 전 반드시 재확인한다.
            if (enemy == null) yield break;

            elapsed += Time.deltaTime;

            if (Camera.main == null) continue;

            Vector3 vp = Camera.main.WorldToViewportPoint(enemy.transform.position);
            bool isOutsideViewport = vp.x < 0f || vp.x > 1f || vp.y < 0f || vp.y > 1f || vp.z < 0f;

            if (isOutsideViewport)
            {
                hasExited = true;
                break;
            }
        }

        if (enemy == null) yield break; // 이미 처치 등 다른 이유로 파괴됨

        if (hasExited)
        {
            yield return new WaitForSeconds(linearChargeDespawnTime);
        }
        else
        {
            Debug.LogWarning("[LinearCharge] 화면 이탈이 감지되지 않아 안전장치로 디스폰합니다.");
        }

        if (enemy != null) Destroy(enemy);
    }

    IEnumerator LogSpacingSnapshot(List<GameObject> group, string label)
    {
        yield return new WaitForSeconds(0.1f);

        float min = float.MaxValue, max = float.MinValue, sum = 0f;
        int count = 0;
        for (int i = 0; i < group.Count - 1; i++)
        {
            if (group[i] == null || group[i + 1] == null) continue;
            float d = Vector2.Distance(group[i].transform.position, group[i + 1].transform.position);
            min = Mathf.Min(min, d);
            max = Mathf.Max(max, d);
            sum += d;
            count++;
        }

        if (count == 0)
        {
            Debug.Log($"[ChargeSpacing][{label}] 비교 가능한 개체 없음");
            yield break;
        }
        Debug.Log($"[ChargeSpacing][{label}] 인접 간격 min={min:F3} max={max:F3} avg={sum / count:F3} (편차가 커지면 대형이 벌어지는 것)");
    }

    void SpawnCirclePrison() //원형 감옥 패턴
    {
        int count = 50;
        float radius = 8f;
        float despawnTime = 30f;
        const float chargeSpeed = 0.3f;

        if (PlayerStats.Instance == null) return;
        Vector2 playerPos = PlayerStats.Instance.transform.position;

        List<GameObject> prisonGroup = new List<GameObject>(); // 소프트락 감시용
        // 회전 나선과 동일하게 Kinematic + MovePosition으로 직접 구동 — Dynamic 바디의 속도(velocity)에
        // 맡기면 밀집 시 물리 충돌 저항으로 수축 속도가 초반/후반에 다르게 느껴지던 문제가 있었다.
        var members = new List<(Rigidbody2D rb, Vector2 direction, Enemy ai)>();

        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i * Mathf.Deg2Rad;
            Vector2 spawnPos = playerPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            GameObject enemy = Instantiate(enemyPrefab[2], spawnPos, Quaternion.identity);
            prisonGroup.Add(enemy);

            var ai = enemy.GetComponent<Enemy>();
            if (ai != null)
            {
                Vector2 chargeDir = (playerPos - spawnPos).normalized;
                ai.externallyControlled = true; // 이동은 DriveCirclePrisonCharge 코루틴이 전담

                var rb = enemy.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.bodyType = RigidbodyType2D.Kinematic; // 다른 Dynamic 몬스터를 밀어내는 특성은 유지하되, 자기 자신은 저항 없이 등속 이동
                    members.Add((rb, chargeDir, ai));
                }

                Destroy(enemy, despawnTime);
            }
        }

        StartCoroutine(MonitorCirclePrisonSoftlock(prisonGroup, despawnTime));
        StartCoroutine(DriveCirclePrisonCharge(members, chargeSpeed, despawnTime));

        circleEventActive = true;
        StartCoroutine(EndWaveEvent(despawnTime));
    }

    /// <summary>
    /// 원형 감옥 수축 이동 전담 — 회전 나선과 같은 방식으로 매 FixedUpdate마다 각자의 고정
    /// 방향(chargeDir)으로 일정 속도(chargeSpeed)만큼 MovePosition 이동시킨다. 서로 부대끼며
    /// 밀어내는 물리 반응(다른 Dynamic 몬스터를 밀어내는 것)은 Kinematic 특성상 그대로 유지되지만,
    /// 이동 주체 자신의 진행 속도는 그 저항에 영향받지 않아 수축 속도감이 처음부터 끝까지 일정하다.
    /// </summary>
    IEnumerator DriveCirclePrisonCharge(List<(Rigidbody2D rb, Vector2 direction, Enemy ai)> members, float speed, float duration)
    {
        float elapsed = 0f;
        int monsterBodyMask = 1 << LayerMask.NameToLayer("MonsterBody");
        float separationRadius = 1.0f;   // 크롤러 콜라이더 지름 기준으로 조정
        float separationStrength = 0.5f; // 수축 속도 대비 밀어내는 힘 비율

        while (elapsed < duration)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;

            foreach (var member in members)
            {
                if (member.rb == null) continue;
                if (member.ai != null && member.ai.IsDead) continue;

                // ── 수축 이동 벡터 ──
                Vector2 moveVec = member.direction * speed * Time.fixedDeltaTime;

                // ── separation 벡터: 주변 MonsterBody와 겹치면 반대 방향으로 밀어내기 ──
                Vector2 separation = Vector2.zero;
                var overlaps = Physics2D.OverlapCircleAll(member.rb.position, separationRadius, monsterBodyMask);
                foreach (var col in overlaps)
                {
                    if (col.attachedRigidbody == member.rb) continue; // 자기 자신 제외
                    Vector2 diff = member.rb.position - (Vector2)col.transform.position;
                    float dist = diff.magnitude;
                    if (dist < 0.001f) diff = Random.insideUnitCircle; // 완전히 겹친 경우 대비
                    else diff /= dist;
                    separation += diff * (separationRadius - dist); // 가까울수록 강하게
                }

                member.rb.MovePosition(member.rb.position + moveVec + separation * separationStrength * Time.fixedDeltaTime);
            }
        }
    }

    /// <summary>
    /// 원형 감옥 소프트락 방지 — 물리적으로 서로 밀집 저항하는 것 자체는 의도된 연출이라 그대로 두되,
    /// "아무것도 처치하지 않고 방치"한 상태에서 물리 저항 때문에 패턴이 영원히 안 끝나는 것만 막는다.
    /// 최근 circlePrisonRecentKillWindow초간 처치 수가 circlePrisonRecentKillThreshold 이하이고,
    /// circlePrisonSnapCheckTime(기본 25초)이 지났는데도 평균 반경이 목표 반경의 110%를 넘으면
    /// 남은 개체를 목표 반경 위치로 1회 강제 보정(snap)한다. 일점사로 뚫는 중이면(최근 처치가 충분하면)
    /// 물리 저항에 의한 지연을 그대로 허용하고 아무 것도 하지 않는다.
    /// </summary>
    IEnumerator MonitorCirclePrisonSoftlock(List<GameObject> group, float totalDuration)
    {
        List<float> killTimestamps = new List<float>();
        int previousAliveCount = group.Count;
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;

            int currentAliveCount = 0;
            foreach (var g in group) if (g != null) currentAliveCount++;

            int diedThisSample = previousAliveCount - currentAliveCount;
            for (int k = 0; k < diedThisSample; k++) killTimestamps.Add(elapsed);
            previousAliveCount = currentAliveCount;

            killTimestamps.RemoveAll(t => elapsed - t > circlePrisonRecentKillWindow);

            if (elapsed < circlePrisonSnapCheckTime || PlayerStats.Instance == null) continue;
            if (currentAliveCount == 0) yield break; // 전멸 — 안전장치 필요 없음

            Vector2 playerPos = PlayerStats.Instance.transform.position;
            float sumDist = 0f;
            foreach (var g in group)
            {
                if (g == null) continue;
                sumDist += Vector2.Distance(playerPos, g.transform.position);
            }
            float currentRadius = sumDist / currentAliveCount;

            bool isNeglected  = killTimestamps.Count <= circlePrisonRecentKillThreshold;
            bool stillTooWide = currentRadius > circlePrisonTargetRadius * 1.1f;

            if (isNeglected && stillTooWide)
            {
                foreach (var g in group)
                {
                    if (g == null) continue;

                    var gAi = g.GetComponent<Enemy>();
                    if (gAi != null && gAi.IsDead) continue; // 점멸 중인 개체는 강제 스냅 대상에서 제외

                    Vector2 dirFromPlayer = (Vector2)g.transform.position - playerPos;
                    dirFromPlayer = dirFromPlayer.sqrMagnitude > 0.0001f ? dirFromPlayer.normalized : Random.insideUnitCircle.normalized;

                    Vector2 targetPos = playerPos + dirFromPlayer * circlePrisonTargetRadius;

                    var rb = g.GetComponent<Rigidbody2D>();
                    if (rb != null) rb.position = targetPos;
                    else g.transform.position = targetPos;
                }

                yield break; // 1회성 보정 — 반복 스냅으로 인한 부자연스러운 자석 효과 방지
            }
        }
    }

    /// <summary>
    /// 웨이브 이벤트: 회전 나선 — 해골 하운드가 플레이어를 중심으로 원형 진형을 이루고,
    /// 진형 전체가 회전하는 동시에 반지름이 줄어드는 이중 벡터 이동 패턴.
    ///
    /// 탈출구(비어있는 슬롯) 설계 의도: 8개의 등간격 슬롯 중 일부러 6~7개만 몬스터로 채워
    /// 나머지 1~2개를 항상 빈 슬롯(탈출구)으로 남긴다. 빈 슬롯의 각도도 다른 몬스터와 동일하게
    /// currentAngleOffset만큼 매 프레임 회전하므로, 탈출구는 "월드 고정 좌표"가 아니라
    /// "진형과 함께 도는 상대 위치"가 된다 — 플레이어가 탈출구의 회전 경로를 예측/추적해야 함.
    /// (occupiedSlots를 8로 고정하면 빈 슬롯이 0개가 되어 탈출구가 사라지므로 6~7로 제한한다.)
    /// </summary>
    IEnumerator SpawnRotatingSpiral()
    {
        if (PlayerStats.Instance == null) yield break;

        const int totalSlots = 8;
        int occupiedSlots = Random.Range(6, 8); // 6~7개 슬롯만 사용 → 최소 1슬롯은 항상 탈출구로 비워둠

        int[] slotOrder = { 0, 1, 2, 3, 4, 5, 6, 7 };
        for (int i = slotOrder.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (slotOrder[i], slotOrder[j]) = (slotOrder[j], slotOrder[i]);
        }

        float rotationDir = Random.value < 0.5f ? 1f : -1f; // 좌/우 랜덤 회전 방향
        float currentRadius = spiralRadius;
        float currentAngleOffset = 0f;

        Vector2 center = PlayerStats.Instance.transform.position;

        var members = new System.Collections.Generic.List<(Rigidbody2D rb, float baseAngle, Enemy ai)>();

        for (int i = 0; i < occupiedSlots; i++)
        {
            float baseAngle = (360f / totalSlots) * slotOrder[i];
            //Vector2 playerPos = PlayerStats.Instance.transform.position;
            float rad = baseAngle * Mathf.Deg2Rad;
            Vector2 spawnPos = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * currentRadius;

            GameObject hound = Instantiate(enemyPrefab[1], spawnPos, Quaternion.identity); // 해골 하운드

            var ai = hound.GetComponent<Enemy>();
            if (ai != null)
                ai.externallyControlled = true; // 회전+수축 이동을 이 코루틴이 전담

            var rb = hound.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                members.Add((rb, baseAngle, ai));
            }

            Destroy(hound, spiralDespawnTime);
        }

        spiralEventActive = true;

        // 임시 디버그 — MovePosition으로 요청한 목표 위치와 실제 반영된 위치의 오차(물리 충돌로
        // 인한 드리프트)를 측정. 확인 후 제거할 것.
        var lastTarget = new Dictionary<Rigidbody2D, Vector2>();
        float driftLogTimer = 0f;

        float elapsed = 0f;
        while (elapsed < spiralDespawnTime)
        {
            yield return new WaitForFixedUpdate();
            elapsed += Time.fixedDeltaTime;

            currentAngleOffset += spiralRotationSpeed * rotationDir * Time.fixedDeltaTime; // 회전
            currentRadius = Mathf.Max(spiralMinRadius, currentRadius - spiralShrinkSpeed * Time.fixedDeltaTime); // 수축

            float maxDriftThisStep = 0f;

            foreach (var member in members)
            {
                if (member.rb == null) continue; // 이미 처치된 개체는 건너뜀
                if (member.ai != null && member.ai.IsDead) continue; // 점멸 중인 개체는 MovePosition으로 끌고 다니지 않음

                if (lastTarget.TryGetValue(member.rb, out var prevTarget))
                {
                    float drift = Vector2.Distance(member.rb.position, prevTarget);
                    if (drift > maxDriftThisStep) maxDriftThisStep = drift;
                }

                float rad = (member.baseAngle + currentAngleOffset) * Mathf.Deg2Rad;
                Vector2 targetPos = center + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * currentRadius;
                member.rb.MovePosition(targetPos);
                lastTarget[member.rb] = targetPos;
            }

            driftLogTimer += Time.fixedDeltaTime;
            if (driftLogTimer >= 1f)
            {
                driftLogTimer = 0f;
            }
        }

        spiralEventActive = false;
    }

    IEnumerator EndWaveEvent(float duration) //웨이브 이벤트 몬스터 디스폰 코루틴
    {
        yield return new WaitForSeconds(duration);
        waveEventActive = false;
    }


#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (PlayerStats.Instance == null) return;
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.3f);
        Gizmos.DrawWireSphere(PlayerStats.Instance.transform.position, spawnRadius);
    }
#endif
}

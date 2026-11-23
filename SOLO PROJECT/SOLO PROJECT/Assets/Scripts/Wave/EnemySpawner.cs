using System.Collections;
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
    public Transform[] enemyPattern1; //직선 돌격 패턴 & 지속 스폰 이벤트의 스폰 위치 좌표

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

    private int currentWave = 0; //현재 웨이브를 카운트하는 변수
    private float waveTimer;
    private float continuousTimer; //지속 스폰 타이머
    private bool bossSpawned = false;
    bool enemySpawned = false;
    bool waveEventActive = false; //웨이브 이벤트 진행 중 여부
    bool circlePrisonSpawned = false; //원형 감옥 패턴이 실행되었는지 확인하는 변수
    private int[] linearEnemies = { 2, 3, 5, 5, 3, 2 };

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
            TriggerWaveEvent(); //웨이브 이벤트 패턴
        }
        
        //if(!waveEventActive)
        //{
            // 지속 스폰 타이머
            continuousTimer += Time.deltaTime;
            if (continuousTimer >= continuousInterval)
            {
                continuousTimer = 0f;
                int count = baseContinuousCount + currentWave;
                SpawnEnemies(count, currentWave);
            }
        //}

       // OutsideScreen();
    }

    //void OutsideScreen()
    //{
    //    Vector3 screenPos = Camera.main.WorldToViewportPoint(transform.position);
    //    if (screenPos.x < 0 || screenPos.x > 1 ||
    //        screenPos.y < 0 || screenPos.y > 1)
    //    {
    //        Debug.Log("화면 밖으로 벗어났으므로 적이 사라짐");
    //        Destroy(gameObject);
    //    }
    //}

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
        TriggerWaveEvent();
    }

    void SpawnEnemies(int count, int wave)
    {
        if(GameManager.Instance.survivalTimer >= 0 && GameManager.Instance.survivalTimer < 60) //0분 ~ 1분
        {
            continuousInterval = Random.Range(2.5f, 3f);
            Debug.Log("continousInterval: " + continuousInterval);
            
            for(int i = 0; i < Random.Range(1, 3); i++)
            {
                int pos = Random.Range(0, enemyPattern1.Length);
                GameObject enemy = Instantiate(enemyPrefab[0], enemyPattern1[pos].position, Quaternion.identity);
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
                    int pos = Random.Range(0, enemyPattern1.Length);
                    GameObject enemy = Instantiate(enemyPrefab[0], enemyPattern1[pos].position, Quaternion.identity);
                }
                else //30% 확률
                {
                    int pos = Random.Range(0, enemyPattern1.Length);
                    GameObject enemy = Instantiate(enemyPrefab[1], enemyPattern1[pos].position, Quaternion.identity);
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
                    int pos = Random.Range(0, enemyPattern1.Length);
                    GameObject enemy = Instantiate(enemyPrefab[0], enemyPattern1[pos].position, Quaternion.identity);
                }
                else //40% 확률
                {
                    int pos = Random.Range(0, enemyPattern1.Length);
                    GameObject enemy = Instantiate(enemyPrefab[2], enemyPattern1[pos].position, Quaternion.identity);
                }
            }
        }
    }

    void TriggerWaveEvent() //웨이브 이벤트 패턴
    {
        if (GameManager.Instance.survivalTimer >= 60 && GameManager.Instance.survivalTimer < 120)
        {
            SpawnLinearCharge(); //1분: 직선 돌격
        }
        else if (GameManager.Instance.survivalTimer >= 120 && GameManager.Instance.survivalTimer < 180 && !circlePrisonSpawned)
        {
            SpawnCirclePrison(); //2분: 원형 감옥
            circlePrisonSpawned = true;
        }
    }

    void SpawnLinearCharge() //직선 돌격 패턴
    {
        // 중복 호출 방지
        if (waveEventActive) return;

        int count = 20;
        float spawnRadius = 1.5f; // 뭉침 반경 (작을수록 더 뭉침)

        Debug.Log(count + "명의 적이 뭉쳐서 나타났습니다!");

        if (PlayerStats.Instance == null) return;

        int random = Random.Range(0, enemyPattern1.Length);
        Vector3 spawnOrigin = enemyPattern1[random].position;
        Vector2 playerPos = PlayerStats.Instance.transform.position;

        for (int i = 0; i < count; i++)
        {
            // 원형 균등 배치 대신 랜덤 오프셋으로 뭉침 효과
            Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
            Vector2 spawnPos = (Vector2)spawnOrigin + randomOffset;

            GameObject enemy = Instantiate(enemyPrefab[1], spawnPos, Quaternion.identity);
            var ai = enemy.GetComponent<Enemy>();

            if (ai != null)
            {
                Vector2 moveDirection = (playerPos - spawnPos).normalized;
                ai.SetChargeDirection(moveDirection);
            }
            else
            {
                Debug.LogWarning("AI 컴포넌트를 찾지 못했습니다!");
            }
        }

        enemySpawned = true;
        waveEventActive = true;
        StartCoroutine(EndWaveEvent(30f));
    }

    void SpawnCirclePrison() //원형 감옥 패턴
    {
        int count = 100;
        float radius = 8f;
        float shrinkSpeed = 0.08f; //수축 속도
        float despawnTime = 30f;

        if (PlayerStats.Instance == null) return;
        Vector2 playerPos = PlayerStats.Instance.transform.position;

        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i * Mathf.Deg2Rad;
            Vector2 spawnPos = playerPos + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            GameObject enemy = Instantiate(enemyPrefab[2], spawnPos, Quaternion.identity);
            var ai = enemy.GetComponent<Enemy>();
            if (ai != null)
            {
                ai.SetChargeDirection((playerPos - spawnPos).normalized);
                ai.moveSpeed = 0.3f;
                Destroy(enemy, despawnTime);
            }
        }

        waveEventActive = true;
        StartCoroutine(EndWaveEvent(despawnTime));
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

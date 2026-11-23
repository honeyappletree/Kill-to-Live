using UnityEngine;

/// <summary>
/// 경험치 오브
/// - 플레이어가 가까이 오면 자동으로 끌려가서 수집됨
/// </summary>
public class XPOrb : MonoBehaviour
{
    [Header("경험치")]
    public float xpAmount = 15f;

    [Header("자석 효과")]
    public float attractRadius = 0.5f;   // 이 거리 이내로 들어오면 끌림 시작
    public float attractSpeed = 5f;      // 끌려가는 속도
    public float collectRadius = 0.1f;  // 이 거리 이내면 수집

    private Transform player;
    private bool attracted = false;
    bool isCollected = false;

    AudioSource audioSource;
    public AudioClip clip;

    void Start()
    {
        if (PlayerStats.Instance != null)
            player = PlayerStats.Instance.transform;
        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (player == null)
        {
            if (PlayerStats.Instance != null)
                player = PlayerStats.Instance.transform;
            return;
        }

        float dist = Vector2.Distance(transform.position, player.position);

        if (dist <= attractRadius)
            attracted = true;

        if (attracted)
        {
            // 수집 범위 체크를 이동보다 먼저 하지 않도록 순서 주의
            transform.position = Vector2.MoveTowards(
                transform.position,
                player.position,
                attractSpeed * Time.deltaTime
            );

            //이동 후 거리를 다시 계산
            dist = Vector2.Distance(transform.position, player.position);

            if (dist <= collectRadius)
            {
                // 중복 실행 방지
                if (!isCollected)
                {
                    isCollected = true;
                    audioSource.PlayOneShot(clip);
                    PlayerStats.Instance?.AddXP(xpAmount);
                    Destroy(gameObject);
                }
            }
        }
    }
}

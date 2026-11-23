using UnityEngine;

/// <summary>
/// 블랙 마나 오브
/// - 적 처치 시 드롭, 플레이어가 가까이 오면 자동으로 끌려가서 수집됨
/// - 수집 시 플레이어 생명유지시간 회복 (HealLifeTime)
/// </summary>
public class BlackManaOrb : MonoBehaviour
{
    [Header("설정 (BlackManaData SO 또는 직접 입력)")]
    public BlackManaData data;

    private float _healAmount   = 3f;
    private float _attractRadius = 0.5f;
    private float _moveSpeed    = 4f;
    private float _pickupRadius = 0.1f;

    private Transform _player;
    private bool _attracted = false;
    bool _isCollected = false;

    AudioSource audioSource;
    public AudioClip clip;

    /// <summary>스포너/Enemy에서 heal량 직접 지정할 때 호출</summary>
    public void Init(float heal)
    {
        _healAmount = heal;
    }

    void Start()
    {
        if (data != null)
        {
            _healAmount    = data.healAmount;
            _attractRadius = data.attractRadius;
            _moveSpeed     = data.moveSpeed;
            _pickupRadius  = data.pickupRadius;
        }

        if (PlayerStats.Instance != null)
            _player = PlayerStats.Instance.transform;
        audioSource = GetComponent<AudioSource>();
    }

    void Update()
    {
        if (_player == null)
        {
            if (PlayerStats.Instance != null)
                _player = PlayerStats.Instance.transform;
            return;
        }

        // 이미 수집됐으면 즉시 리턴
        if (_isCollected) return;

        float dist = Vector2.Distance(transform.position, _player.position);

        if (dist <= _attractRadius)
            _attracted = true;

        if (_attracted)
        {
            transform.position = Vector2.MoveTowards(
                transform.position,
                _player.position,
                _moveSpeed * Time.deltaTime
            );

            // 이동 후 거리 재계산
            dist = Vector2.Distance(transform.position, _player.position);

            if (dist <= _pickupRadius)
            {
                _isCollected = true; // 즉시 플래그 설정 → 중복 실행 차단
                audioSource.PlayOneShot(clip);
                PlayerStats.Instance?.HealLifeTime(_healAmount);
                Destroy(gameObject);
            }
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0f, 0.8f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, _attractRadius);
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, _pickupRadius);
    }
#endif
}

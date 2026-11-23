using System.Runtime.CompilerServices;
using UnityEngine;

/// <summary>
/// 플레이어의 자동 공격 발사체
/// - 일직선으로 이동하며 적에 닿으면 데미지를 주고 소멸
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class Projectile : MonoBehaviour
{
    private Rigidbody2D rb;
    private float damage;
    private const float Lifetime = 6f; // 발사 후 자동 소멸 시간 (초)

    [Header("폭발 설정")]
    [SerializeField] private GameObject explosionObject; //레벨 3 hit 애니메이션
    Animator anim;

    [SerializeField]
    private float destroyDelay = 0.5f;

    [SerializeField]
    private SpriteRenderer spriteRenderer;

    private bool canExplode;

    private float explosionRadius;

    private int level;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;

        // 발사체는 Kinematic으로 설정 (물리 힘을 받지 않음)
        rb.bodyType = RigidbodyType2D.Kinematic;

        GetComponent<CircleCollider2D>().isTrigger = true;
        anim = GetComponent<Animator>();
    }

    private void Start()
    {
        if(explosionObject != null)
        {
            explosionObject.SetActive(false);
        }
    }

    private void Update()
    {
        OutsideScreen();
    }

    /// <summary>생성 직후 방향과 데미지를 초기화</summary>
    public void Init(Vector2 direction, float damage, float speed)
    {
        this.damage = damage;
        rb.linearVelocity = direction * speed;

        // 발사 방향으로 스프라이트 회전
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        Destroy(gameObject, Lifetime);
    }

    public void SetExplosion(bool canExplode, float explosionRadius, int level)
    {
        this.canExplode = canExplode;
        this.explosionRadius = explosionRadius;
        this.level = level;
    }

    void OnTriggerEnter2D(Collider2D col)
    {
        Enemy enemy = col.GetComponent<Enemy>();
        if (enemy != null)
        {
            if(canExplode)
            {
                Explode();
            }
            else
            {
                enemy.TakeDamage(damage);
                PlayHitAnimation();
            }
        }
    }

    void PlayHitAnimation() //DarkBolt 무기 레벨 1 ~ 2일 때 재생 애니메이션
    {
        if (explosionObject != null)
        {
            explosionObject.SetActive(true);
            explosionObject.transform.SetParent(null);
            anim.SetInteger("level", level);
            anim.SetTrigger("isHit");
            Destroy(explosionObject, destroyDelay);
        }

        if (spriteRenderer != null) spriteRenderer.enabled = false;

        Collider2D collision = GetComponent<Collider2D>();
        if (collision != null) collision.enabled = false;

        rb.linearVelocity = Vector2.zero;
        Destroy(gameObject, destroyDelay);
    }

    void Explode() //DarkBolt 무기 레벨 3일 때 재생 애니메이션
    {
        Debug.Log("Explode 호출됨!");
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);

        foreach(Collider2D hit in hits)
        {
            Enemy enemy = hit.GetComponent<Enemy>();

            if(enemy != null)
            {
                enemy.TakeDamage(damage);
            }
        }

        if(explosionObject != null)
        {
            Debug.Log("explosionObject 활성화!");
            explosionObject.SetActive(true);
            explosionObject.transform.SetParent(null);
            anim.SetInteger("level", level);
            anim.SetTrigger("isHit");
            Destroy(explosionObject, destroyDelay);
        }
        else
        {
            Debug.Log("explosionObject가 null임!");
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        Collider2D collision = GetComponent<Collider2D>();
        if(collision != null)
        {
            collision.enabled = false;
        }
        rb.linearVelocity = Vector2.zero;
        Destroy(gameObject,destroyDelay);
    }

    void OutsideScreen() //총알 오브젝트가 화면 밖으로 벗어났는지 확인하는 함수
    {
        Vector3 screenPos = Camera.main.WorldToViewportPoint(transform.position);

        if (screenPos.x < 0 || screenPos.x > 1 ||
            screenPos.y < 0 || screenPos.y > 1)
        {
            Debug.Log("화면 밖으로 벗어났으므로 총알이 사라짐");
            Destroy(gameObject);
        }
    }

}

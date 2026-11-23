using UnityEngine;

public class DarkBoltProjectile : MonoBehaviour
{
    [SerializeField] private GameObject explosionObject;
    [SerializeField] float explosionRadius = 2f;
    [SerializeField] float destroyDelay = 0.5f;

    [SerializeField] private SpriteRenderer spriteRenderer;

    Collider2D collider;

    Vector2 direction;
    float damage;
    float speed;

    bool canExplode;

    private void Start()
    {
        collider = GetComponent<Collider2D>();
        if (explosionObject != null)
        {
            explosionObject.SetActive(false);
        }
    }

    public void Init(Vector2 dir, float damage, float speed, bool canExplode)
    {
        direction = dir;
        this.damage = damage;
        this.speed = speed;
        this.canExplode = canExplode;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Enemy enemy = collision.GetComponent<Enemy>();

        if (enemy == null)
        {
            return;
        }

        if (canExplode)
        {
            Explode();
        }
        else
        {
            enemy.TakeDamage(damage);
            Destroy(gameObject);
        }
    }

    void Explode()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);

        foreach (Collider2D hit in hits)
        {
            Enemy enemy = hit.GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.TakeDamage(damage);
            }
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = false;
        }

        if (collider != null)
        {
            collider.enabled = false;
        }

        if (explosionObject != null)
        {
            explosionObject.SetActive(true);
        }

        Destroy(gameObject, destroyDelay);
    }

    private void OnBecameInvisible()
    {
        Destroy(gameObject);
    }
}

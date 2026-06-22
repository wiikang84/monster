using UnityEngine;

// 발사체: 목표 적을 추적, 명중 시 데미지
public class Projectile : MonoBehaviour
{
    Enemy target;
    float speed;
    int damage;
    float life = 3f;

    public void Init(Enemy t, float s, int d)
    {
        target = t; speed = s; damage = d;
    }

    void Update()
    {
        life -= Time.deltaTime;
        if (target == null || life <= 0f) { Destroy(gameObject); return; }

        transform.position = Vector3.MoveTowards(transform.position, target.transform.position, speed * Time.deltaTime);
        if ((transform.position - target.transform.position).sqrMagnitude < 0.16f)
        {
            target.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}

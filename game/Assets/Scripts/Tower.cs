using UnityEngine;

// 타워: 사거리 내 가장 가까운 적을 일정 간격으로 자동 공격(발사체)
public class Tower : MonoBehaviour
{
    public float range = 5f;
    public float fireRate = 1f;   // 초당 1발
    public int damage = 25;
    public float projectileSpeed = 14f;

    float timer = 0f;

    void Update()
    {
        timer -= Time.deltaTime;
        if (timer > 0) return;

        var gm = GameManager.Instance;
        if (gm == null) return;

        Enemy target = null;
        float best = range * range;
        foreach (var e in gm.Enemies)
        {
            if (e == null) continue;
            float d = (e.transform.position - transform.position).sqrMagnitude;
            if (d <= best) { best = d; target = e; }
        }

        if (target != null)
        {
            timer = fireRate;
            Shoot(target);
        }
    }

    void Shoot(Enemy target)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>());
        var root = GameManager.Instance.SpawnedRoot;
        if (root != null) go.transform.SetParent(root);
        go.transform.position = transform.position + Vector3.up * 0.7f;
        go.transform.localScale = Vector3.one * 0.3f;
        GameManager.SetColor(go, Color.white);

        var p = go.AddComponent<Projectile>();
        p.Init(target, projectileSpeed, damage);
    }
}

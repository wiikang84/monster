using UnityEngine;

// 타워: 사거리 내 가장 가까운 적을 향해 무기를 돌리고, 일정 간격으로 탄(모델) 발사
// (클래스명 Tower → TowerUnit 개명: 루트 네임스페이스 Tower 와의 충돌 회피. 2026-06-23 M0)
public class TowerUnit : MonoBehaviour
{
    public Transform weapon;          // 회전시킬 무기 모델
    public float range = 7f;
    public float fireRate = 0.9f;
    public float projectileSpeed = 16f;
    public int damage = 25;

    float timer;

    void Update()
    {
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

        if (target != null && weapon != null)
        {
            Vector3 look = target.transform.position; look.y = weapon.position.y;
            Vector3 dir = look - weapon.position;
            if (dir.sqrMagnitude > 0.001f)
                weapon.rotation = Quaternion.Slerp(weapon.rotation, Quaternion.LookRotation(dir), 12f * Time.deltaTime);
        }

        timer -= Time.deltaTime;
        if (timer > 0) return;
        if (target != null) { timer = fireRate; Shoot(target); }
    }

    void Shoot(Enemy target)
    {
        Vector3 from = (weapon != null ? weapon.position : transform.position) + Vector3.up * 0.2f;
        var go = GameManager.Instance.SpawnModel("weapon-ammo-bullet", from);
        go.transform.localScale *= 1.2f;
        var p = go.AddComponent<Projectile>();
        p.Init(target, projectileSpeed, damage);
    }
}

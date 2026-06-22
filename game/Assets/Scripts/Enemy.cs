using UnityEngine;

// 적: 웨이포인트를 따라 이동, 기지 도달 시 라이프 감소, 처치 시 골드
public class Enemy : MonoBehaviour
{
    GameManager gm;
    float speed;
    public int Hp;
    int wp = 1;          // 다음에 향할 웨이포인트 인덱스 (0에서 출발)
    bool done = false;   // 중복 콜백 방지

    public void Init(GameManager g, int hp, float spd)
    {
        gm = g; Hp = hp; speed = spd;
    }

    void Update()
    {
        if (gm == null || done) return;
        var pts = gm.Waypoints;
        if (wp >= pts.Count) { Reach(); return; }

        Vector3 target = pts[wp];
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        if ((transform.position - target).sqrMagnitude < 0.01f)
        {
            wp++;
            if (wp >= pts.Count) Reach();
        }
    }

    public void TakeDamage(int dmg)
    {
        if (done) return;
        Hp -= dmg;
        if (Hp <= 0)
        {
            done = true;
            gm.OnEnemyKilled(this);
            Destroy(gameObject);
        }
    }

    void Reach()
    {
        if (done) return;
        done = true;
        gm.OnEnemyReachedBase(this);
        Destroy(gameObject);
    }
}

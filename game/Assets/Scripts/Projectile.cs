using UnityEngine;
using Tower.Data;

// 발사체: 목표 적을 추적, 명중 시 데미지(단일/광역/슬로우).
public class Projectile : MonoBehaviour
{
    GameManager gm;
    Enemy target;
    float speed;
    int damage;
    bool crit;
    DamageType type;
    float splashRadius, splashFalloff, slowFactor, slowDuration;
    string bonusTag; float bonusMult = 1f;
    float life = 3f;

    public void Init(GameManager g, Enemy t, float s, int d, bool isCrit, DamageType dt,
                     float splashR, float falloff, float slowF, float slowDur,
                     string bonusTagArg = null, float bonusMultArg = 1f)
    {
        gm = g; target = t; speed = s; damage = d; crit = isCrit; type = dt;
        splashRadius = splashR; splashFalloff = falloff; slowFactor = slowF; slowDuration = slowDur;
        bonusTag = bonusTagArg; bonusMult = bonusMultArg;
    }

    // 적별 최종 데미지(낙폭 + 비행 보너스/대공 태그). 최소 1.
    int Dmg(Enemy e, float falloff)
    {
        float m = (bonusTag != null && e.Has(bonusTag)) ? bonusMult : 1f;
        return Mathf.Max(1, Mathf.RoundToInt(damage * falloff * m));
    }

    void Update()
    {
        life -= Time.deltaTime;
        if (target == null || life <= 0f) { Destroy(gameObject); return; }

        transform.position = Vector3.MoveTowards(transform.position, target.transform.position, speed * Time.deltaTime);
        if ((transform.position - target.transform.position).sqrMagnitude < 0.16f)
        {
            Hit();
            Destroy(gameObject);
        }
    }

    void Hit()
    {
        Vector3 at = transform.position;
        if (type == DamageType.Splash && splashRadius > 0.01f)
        {
            Tower.Audio.Sfx.I?.Play("explosionCrunch_000", 0.7f, 0.05f);
            float r2 = splashRadius * splashRadius;
            var list = gm.Enemies;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                var e = list[i];
                if (e == null) continue;
                float d2 = (e.transform.position - at).sqrMagnitude;
                if (d2 > r2) continue;
                float f = Mathf.Lerp(1f, splashFalloff, Mathf.Sqrt(d2) / splashRadius);
                if (e.IsFlying) f *= 0.5f;                 // 비행은 지상 폭발 절반만
                e.TakeDamage(Dmg(e, f), crit);
            }
        }
        else
        {
            Tower.Audio.Sfx.I?.Play("impactMetal_light_000", 0.55f, 0.04f);
            if (target != null) target.TakeDamage(Dmg(target, 1f), crit);
        }

        if (type == DamageType.Slow && target != null && slowFactor > 0f)
            target.ApplySlow(slowFactor, slowDuration);
    }
}

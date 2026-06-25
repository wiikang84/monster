using System.Collections.Generic;
using UnityEngine;

// 적: 웨이포인트를 따라 이동, 기지 도달 시 라이프 감소, 처치 시 골드.
// (M3) EnemyDef 기반 — hp/속도/보상/장갑/슬로우/틴트/스케일. 피격 플래시 + 처치 연출.
public class Enemy : MonoBehaviour
{
    GameManager gm;
    float baseSpeed;          // 월드유닛/초 (cell 반영 완료)
    public int Hp, MaxHp, Reward, LivesCost, Resist;
    public string DefId;
    public List<string> Tags;          // fast/swarm/armored/flying
    public bool IsFlying;
    float flyHeight;
    public bool Has(string t) => Tags != null && Tags.Contains(t);

    int wp = 1;
    bool done = false;

    // 슬로우
    float slowMul = 1f, slowUntil = 0f;

    // 피격 플래시(머티리얼 인스턴스)
    Material mat;
    Color baseColor;
    float flashUntil = 0f;

    public float Speed => baseSpeed * slowMul;
    public bool IsSlowed => Time.time < slowUntil;

    public void Init(GameManager g, Tower.Data.EnemyDef def, float cell)
    {
        gm = g;
        Hp = MaxHp = Mathf.Max(1, def.hp);
        baseSpeed = def.speed * cell;
        Reward = def.reward; LivesCost = def.livesCost; Resist = def.resist;
        DefId = def.id;
        Tags = def.tags;
        IsFlying = def.Has("flying");
        flyHeight = IsFlying ? cell * 0.9f : 0f;   // 비행 적은 경로 위로 띄움

        if (def.scale > 0f && Mathf.Abs(def.scale - 1f) > 0.01f)
            transform.localScale *= def.scale;

        // 자기 전용 머티리얼 인스턴스(틴트 + 피격 플래시용)
        var rends = GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            mat = new Material(rends[0].sharedMaterial);
            if (!string.IsNullOrEmpty(def.tint) && ColorUtility.TryParseHtmlString(def.tint, out var c))
                mat.color = c;
            baseColor = mat.color;
            foreach (var r in rends) r.sharedMaterial = mat;
        }
    }

    void Update()
    {
        if (gm == null || done) return;

        // 슬로우 만료
        if (Time.time >= slowUntil) slowMul = 1f;

        // 피격 플래시 복구
        if (mat != null && flashUntil > 0f && Time.time >= flashUntil)
        {
            mat.color = baseColor;
            mat.SetColor("_EmissionColor", Color.black);
            flashUntil = 0f;
        }

        var pts = gm.Waypoints;
        if (pts == null || pts.Count == 0) return;
        if (wp >= pts.Count) { Reach(); return; }

        Vector3 target = pts[wp];
        target.y += flyHeight;                       // 비행: 공중 경로
        transform.position = Vector3.MoveTowards(transform.position, target, Speed * Time.deltaTime);
        if ((transform.position - target).sqrMagnitude < 0.01f)
        {
            wp++;
            if (wp >= pts.Count) Reach();
        }
    }

    /// <summary>경로 진행도(클수록 기지에 가까움) — First 타게팅용.</summary>
    public float Progress
    {
        get
        {
            var pts = gm != null ? gm.Waypoints : null;
            if (pts == null || pts.Count == 0) return wp;
            int i = Mathf.Min(wp, pts.Count - 1);
            return wp * 1000f - Vector3.Distance(transform.position, pts[i]);
        }
    }

    public void ApplySlow(float factor, float duration)
    {
        float m = Mathf.Clamp01(1f - factor);
        if (m < slowMul || Time.time >= slowUntil) slowMul = m;
        slowUntil = Mathf.Max(slowUntil, Time.time + duration);
    }

    public void TakeDamage(int dmg, bool crit = false)
    {
        if (done) return;
        int applied = Mathf.Max(1, dmg - Resist);   // 장갑: 고정 감산, 최소 1
        Hp -= applied;

        // 데미지 숫자 + 흰색 피격 플래시
        gm.ShowDamage(transform.position, applied, crit);
        if (mat != null)
        {
            mat.color = Color.white;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.white * 0.6f);
            flashUntil = Time.time + 0.1f;
        }

        if (Hp <= 0) Die();
    }

    void Die()
    {
        if (done) return;
        done = true;
        gm.OnEnemyKilled(this);
        StartCoroutine(DeathShrink());
    }

    System.Collections.IEnumerator DeathShrink()
    {
        Vector3 s0 = transform.localScale;
        float t = 0f;
        while (t < 0.12f)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(s0, Vector3.zero, t / 0.12f);
            yield return null;
        }
        Destroy(gameObject);
    }

    void Reach()
    {
        if (done) return;
        done = true;
        gm.OnEnemyReachedBase(this);
        Destroy(gameObject);
    }
}

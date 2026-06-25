using UnityEngine;
using Tower.Data;

// 타워: TowerDef 기반으로 스탯을 들고, 사거리 내 적을 타게팅 정책대로 조준·발사.
// (M3) 티어 업그레이드 / 판매 환불 / 광역·슬로우·치명타 지원.
public class TowerUnit : MonoBehaviour
{
    public TowerDef Def;
    public int Tier = 1;
    public int InvestedGold;
    public TargetingMode Targeting = TargetingMode.Nearest;

    // 현재 스탯(업그레이드 시 누적)
    public int Damage;
    public float Range, FireRate, ProjectileSpeed;
    public DamageType DType;
    public float SplashRadius, SplashFalloff, SlowFactor, SlowDuration;
    public float CritChance;
    public string BonusTag; public float BonusMult = 1f;   // 태그 보너스(대공 등)

    public Transform weapon;
    public TowerSlot Slot;     // 설치된 슬롯(판매 시 해제용)
    string projectileModel;
    float timer;

    public void SetDef(TowerDef def)
    {
        Def = def;
        Tier = 1;
        InvestedGold = def.cost;
        Damage = def.damage;
        Range = def.range;
        FireRate = Mathf.Max(0.05f, def.fireRate);
        ProjectileSpeed = def.projectileSpeed;
        DType = def.DamageTypeEnum;
        SplashRadius = def.splashRadius;
        SplashFalloff = def.splashFalloff;
        SlowFactor = def.slowFactor;
        SlowDuration = def.slowDuration;
        Targeting = def.TargetingEnum;
        projectileModel = def.projectileModel;
        CritChance = 0f;
        BonusTag = string.IsNullOrEmpty(def.bonusTag) ? null : def.bonusTag;
        BonusMult = def.bonusMult <= 0f ? 1f : def.bonusMult;
    }

    public bool CanUpgrade => Def != null && Def.upgrades != null && (Tier - 1) < Def.upgrades.Count;
    public UpgradeDef NextUpgrade => CanUpgrade ? Def.upgrades[Tier - 1] : null;
    public int RefundGold => Mathf.FloorToInt(InvestedGold * 0.7f);

    public void Upgrade()
    {
        var u = NextUpgrade;
        if (u == null) return;
        Tier++;
        Damage += u.damage;
        Range += u.range;
        FireRate += u.fireRate;
        ProjectileSpeed += u.projectileSpeed;
        SplashRadius += u.splashRadius;
        SlowFactor += u.slowFactor;
        SlowDuration += u.slowDuration;
        InvestedGold += u.cost;
        if (u.special == "crit15") CritChance = 0.15f;
        // burnDoT / freeze15 는 후속 구현(훅만 둠)
    }

    void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        Enemy target = SelectTarget(gm);

        if (target != null && weapon != null)
        {
            Vector3 look = target.transform.position; look.y = weapon.position.y;
            Vector3 dir = look - weapon.position;
            if (dir.sqrMagnitude > 0.001f)
                weapon.rotation = Quaternion.Slerp(weapon.rotation, Quaternion.LookRotation(dir), 12f * Time.deltaTime);
        }

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        if (target != null) { timer = 1f / FireRate; Shoot(gm, target); }
    }

    Enemy SelectTarget(GameManager gm)
    {
        var list = gm.Enemies;
        Enemy best = null;
        float r2 = Range * Range;

        switch (Targeting)
        {
            case TargetingMode.First:
            {
                float bp = float.NegativeInfinity;
                foreach (var e in list)
                {
                    if (e == null) continue;
                    if ((e.transform.position - transform.position).sqrMagnitude > r2) continue;
                    if (e.Progress > bp) { bp = e.Progress; best = e; }
                }
                break;
            }
            case TargetingMode.MaxHp:
            case TargetingMode.MinHp:
            {
                int bh = Targeting == TargetingMode.MaxHp ? int.MinValue : int.MaxValue;
                foreach (var e in list)
                {
                    if (e == null) continue;
                    if ((e.transform.position - transform.position).sqrMagnitude > r2) continue;
                    bool better = Targeting == TargetingMode.MaxHp ? e.Hp > bh : e.Hp < bh;
                    if (better) { bh = e.Hp; best = e; }
                }
                break;
            }
            default: // Nearest
            {
                float bd = r2;
                foreach (var e in list)
                {
                    if (e == null) continue;
                    float d = (e.transform.position - transform.position).sqrMagnitude;
                    if (d <= bd) { bd = d; best = e; }
                }
                break;
            }
        }
        return best;
    }

    void Shoot(GameManager gm, Enemy target)
    {
        Vector3 from = (weapon != null ? weapon.position : transform.position) + Vector3.up * 0.2f;
        var go = gm.SpawnModel(projectileModel, from);
        go.transform.localScale *= 1.2f;
        bool crit = CritChance > 0f && Random.value < CritChance;
        int dmg = crit ? Damage * 2 : Damage;
        var p = go.AddComponent<Projectile>();
        p.Init(gm, target, ProjectileSpeed, dmg, crit, DType, SplashRadius, SplashFalloff, SlowFactor, SlowDuration, BonusTag, BonusMult);
        if (Def != null) Tower.Audio.Sfx.I?.Play(Def.shootSound, 0.5f, 0.06f);
    }
}

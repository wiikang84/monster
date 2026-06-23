namespace Tower.Data
{
    /// <summary>타워 공격의 데미지 처리 방식.</summary>
    public enum DamageType
    {
        Single,   // 단일 대상
        Splash,   // 착탄 반경 광역
        Slow,     // 둔화
        DoT       // 지속 피해
    }

    /// <summary>타워의 타깃 선택 정책.</summary>
    public enum TargetingMode
    {
        Nearest,  // 가장 가까운 적
        First,    // 기지에 가장 가까운(앞선) 적
        MaxHp,    // 최대 체력
        MinHp     // 최소 체력
    }
}

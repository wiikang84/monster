namespace Tower.Core
{
    /// <summary>전투 한 판의 종료 결과.</summary>
    public enum BattleResult
    {
        None,
        Cleared,   // 모든 웨이브 방어 성공
        Defeated   // 라이프 0
    }
}

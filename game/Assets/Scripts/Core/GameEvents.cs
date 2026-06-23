using System;

namespace Tower.Core
{
    /// <summary>
    /// UI·오디오 등 표시 계층이 게임 로직을 직접 호출하지 않고 구독으로만 갱신되게 하는 이벤트 허브.
    /// 발행은 internal Raise* 로 캡슐화(게임플레이 어셈블리만 호출), 구독은 누구나.
    /// (마이그레이션 M0 골격 — 게임플레이 시스템이 후속 마일스톤에서 Raise* 를 호출하도록 연결한다.)
    ///
    /// 참고: Enemy/TowerSlot 등 게임 오브젝트를 인자로 받는 이벤트(EnemyKilled, SlotSelected 등)는
    ///       해당 클래스가 Tower.Gameplay 네임스페이스로 이동하는 M3/M4에서 추가한다.
    /// </summary>
    public static class GameEvents
    {
        // ── 경제/진행 상태 (값 타입만 전달 → Core가 게임 클래스에 결합되지 않음) ──
        public static event Action<int> GoldChanged;        // 새 골드 총량
        public static event Action<int> LivesChanged;       // 새 라이프
        public static event Action<int, int> WaveChanged;   // (현재 웨이브 1-base, 총 웨이브)
        public static event Action<BattleResult> BattleEnded;

        // ── 발행기 (게임플레이 어셈블리 내부에서만 호출) ──
        public static void RaiseGoldChanged(int gold) => GoldChanged?.Invoke(gold);
        public static void RaiseLivesChanged(int lives) => LivesChanged?.Invoke(lives);
        public static void RaiseWaveChanged(int current, int total) => WaveChanged?.Invoke(current, total);
        public static void RaiseBattleEnded(BattleResult result) => BattleEnded?.Invoke(result);

        /// <summary>판 재시작/씬 정리 시 구독자 누수 방지를 위해 전체 해제.</summary>
        public static void ClearAll()
        {
            GoldChanged = null;
            LivesChanged = null;
            WaveChanged = null;
            BattleEnded = null;
        }
    }
}

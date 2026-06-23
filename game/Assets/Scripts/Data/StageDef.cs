using System;
using System.Collections.Generic;

namespace Tower.Data
{
    /// <summary>스테이지 1개의 정의(데이터). JSON(Content/stage_NN.json)에서 JsonUtility로 역직렬화된다.</summary>
    [Serializable]
    public sealed class StageDef
    {
        public string id;
        public string displayName;

        // 맵: ASCII 그리드. 위(북)부터 적으며 로더/맵서비스가 행 순서를 뒤집어 (c,r)로 매핑.
        //   . 빈 바닥(설치 불가)  # 길  o 설치 슬롯  S 스폰  G 기지
        public string[] grid;

        public int startGold = 120;
        public int startLives = 10;

        public List<string> allowedTowers = new List<string>();
        public List<WaveDef> waves = new List<WaveDef>();

        public bool autoNextWave = false;
        public int[] starThresholds = { 60, 90 }; // 별 2★/3★ 컷(남은 라이프 비율 %)
        public string tutorialId;                 // 있으면 첫 진입 시 튜토리얼 표시
    }
}

using System;
using System.Collections.Generic;

namespace Tower.Data
{
    /// <summary>적 1종의 정의(데이터). JSON(Content/enemies.json)에서 JsonUtility로 역직렬화된다.</summary>
    [Serializable]
    public sealed class EnemyDef
    {
        public string id;
        public string displayName;
        public string model;          // Resources/Models 키

        public int hp;
        public float speed;           // 셀 단위 배수(런타임에 cell 크기를 곱함)
        public int reward;            // 처치 시 골드
        public int livesCost = 1;     // 기지 도달 시 라이프 감소량

        public List<string> resist = new List<string>(); // 면역/저항 태그 (예: "slow")
        public bool isBoss;
    }
}

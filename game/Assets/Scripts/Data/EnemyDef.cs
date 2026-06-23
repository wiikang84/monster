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

        public float scale = 1f;      // 모델 스케일
        public string tint;           // 색 틴트(#RRGGBB)
        public int resist;            // 받는 피해 고정 감산(장갑), 최소 1 보장
        public List<string> tags = new List<string>(); // fast/swarm/armored/flying...
        public bool isBoss;

        public bool Has(string tag) => tags != null && tags.Contains(tag);
    }
}

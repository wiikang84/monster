using System;
using System.Collections.Generic;

namespace Tower.Data
{
    /// <summary>웨이브 1개의 정의(스테이지에 종속).</summary>
    [Serializable]
    public sealed class WaveDef
    {
        public float restBefore;      // 웨이브 시작 전 대기(초)
        public List<SpawnGroup> groups = new List<SpawnGroup>();
    }

    /// <summary>한 웨이브 내 동일 적 묶음의 스폰 규칙.</summary>
    [Serializable]
    public sealed class SpawnGroup
    {
        public string enemyId;
        public int count;
        public float interval;        // 스폰 간격(초)
        public float startDelay;      // 그룹 시작 지연(초)
        public int pathIndex;         // 다중 경로 시 어느 경로(0=기본)
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Tower.Data
{
    /// <summary>로드된 모든 콘텐츠 정의(id→Def)의 런타임 보관소.</summary>
    public sealed class ContentDB
    {
        readonly Dictionary<string, TowerDef> towers = new Dictionary<string, TowerDef>();
        readonly Dictionary<string, EnemyDef> enemies = new Dictionary<string, EnemyDef>();
        readonly Dictionary<string, StageDef> stages = new Dictionary<string, StageDef>();

        public IReadOnlyList<TowerDef> Towers { get; }
        public IReadOnlyList<EnemyDef> Enemies { get; }
        public IReadOnlyList<StageDef> Stages { get; }

        public ContentDB(List<TowerDef> towerList, List<EnemyDef> enemyList, List<StageDef> stageList)
        {
            towerList ??= new List<TowerDef>();
            enemyList ??= new List<EnemyDef>();
            stageList ??= new List<StageDef>();

            foreach (var t in towerList) if (t != null && t.id != null) towers[t.id] = t;
            foreach (var e in enemyList) if (e != null && e.id != null) enemies[e.id] = e;
            foreach (var s in stageList) if (s != null && s.id != null) stages[s.id] = s;

            Towers = towerList;
            Enemies = enemyList;
            Stages = stageList;
        }

        public TowerDef Tower(string id)
        {
            if (id != null && towers.TryGetValue(id, out var d)) return d;
            Debug.LogWarning($"[ContentDB] 알 수 없는 타워 id: {id}");
            return null;
        }

        public EnemyDef Enemy(string id)
        {
            if (id != null && enemies.TryGetValue(id, out var d)) return d;
            Debug.LogWarning($"[ContentDB] 알 수 없는 적 id: {id}");
            return null;
        }

        public StageDef Stage(string id)
        {
            if (id != null && stages.TryGetValue(id, out var d)) return d;
            Debug.LogWarning($"[ContentDB] 알 수 없는 스테이지 id: {id}");
            return null;
        }
    }
}

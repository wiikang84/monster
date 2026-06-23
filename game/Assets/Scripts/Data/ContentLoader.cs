using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tower.Data
{
    /// <summary>
    /// Resources/Content/*.json 을 Unity 내장 JsonUtility 로 역직렬화해 ContentDB 를 만든다.
    /// (Newtonsoft 미사용 — WebGL 빌드 용량 ~21MB 절감. JsonUtility는 최상위 배열을 못 읽으므로 래퍼로 감쌈.)
    /// 에셋 참조는 문자열 키(모델/사운드)로 두어 JSON 친화 유지(현 Resources.Load 방식과 동일).
    /// </summary>
    public static class ContentLoader
    {
        [Serializable] class TowerList { public List<TowerDef> items; }
        [Serializable] class EnemyList { public List<EnemyDef> items; }
        [Serializable] class StageIdList { public List<string> ids; }

        public static ContentDB LoadAll()
        {
            var towers = LoadWrapped<TowerList>("Content/towers")?.items ?? new List<TowerDef>();
            var enemies = LoadWrapped<EnemyList>("Content/enemies")?.items ?? new List<EnemyDef>();

            var stageIds = LoadWrapped<StageIdList>("Content/stages")?.ids ?? new List<string>();
            var stages = new List<StageDef>();
            foreach (var id in stageIds)
            {
                var stage = LoadWrapped<StageDef>("Content/" + id);
                if (stage != null) stages.Add(stage);
            }

            return new ContentDB(towers, enemies, stages);
        }

        static T LoadWrapped<T>(string resourcePath) where T : class
        {
            var ta = Resources.Load<TextAsset>(resourcePath);
            if (ta == null)
            {
                Debug.LogError($"[ContentLoader] 리소스 없음: Resources/{resourcePath}.json");
                return null;
            }
            try
            {
                return JsonUtility.FromJson<T>(ta.text);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ContentLoader] 파싱 실패 ({resourcePath}): {e.Message}");
                return null;
            }
        }
    }
}

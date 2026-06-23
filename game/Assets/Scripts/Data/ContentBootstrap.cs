using Tower.Core;
using UnityEngine;

namespace Tower.Data
{
    /// <summary>
    /// (M1) 콘텐츠 JSON 로드 검증 + ServiceLocator 등록.
    /// 아직 게임플레이는 기존 GameManager 하드코딩으로 동작하며, 여기서는 로드 성공만 확인한다.
    /// 후속 마일스톤(M2~)에서 GameManager가 이 ContentDB 를 사용하도록 전환한다.
    /// </summary>
    public static class ContentBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            var db = ContentLoader.LoadAll();
            ServiceLocator.Register(db);
            Debug.Log($"[Content] 로드 완료 — towers={db.Towers.Count}, enemies={db.Enemies.Count}, stages={db.Stages.Count}");
            foreach (var s in db.Stages)
                Debug.Log($"[Content] 스테이지 '{s.id}' ({s.displayName}) grid={s.grid?.Length}행, waves={s.waves.Count}, allowedTowers=[{string.Join(",", s.allowedTowers)}]");
        }
    }
}

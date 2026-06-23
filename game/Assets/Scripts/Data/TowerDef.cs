using System;
using System.Collections.Generic;

namespace Tower.Data
{
    /// <summary>타워 1종의 정의(데이터). JSON(Content/towers.json)에서 JsonUtility로 역직렬화된다.</summary>
    [Serializable]
    public sealed class TowerDef
    {
        public string id;
        public string displayName;

        // 외형 (Resources/Models 키)
        public string baseModel;
        public string weaponModel;
        public string projectileModel;

        // 1레벨 스탯
        public int cost;
        public float range;
        public float fireRate;        // 발사 간격(초)
        public int damage;
        public float projectileSpeed;

        // 공격 타입 옵션 (JSON엔 문자열로 표기: "Single"/"Splash"/"Slow"/"DoT")
        public string damageType = "Single";
        public float splashRadius;
        public float slowFactor;      // 0~1 (Slow 타입에서 속도 배수 감소량)
        public float dotDps;

        // 타깃 선택 정책 (JSON 문자열: "Nearest"/"First"/"MaxHp"/"MinHp")
        public string targeting = "Nearest";
        public string shootSound;

        public List<UpgradeDef> upgrades = new List<UpgradeDef>();

        // ── 코드에서 쓰는 enum 변환(문자열 → enum). JsonUtility는 문자열 enum을 직접 못 읽어 보조. ──
        public DamageType DamageTypeEnum => Parse(damageType, DamageType.Single);
        public TargetingMode TargetingEnum => Parse(targeting, TargetingMode.Nearest);

        static T Parse<T>(string s, T fallback) where T : struct
            => Enum.TryParse<T>(s, true, out var v) ? v : fallback;
    }

    /// <summary>타워 업그레이드 1단계의 증분값.</summary>
    [Serializable]
    public sealed class UpgradeDef
    {
        public int cost;
        public int damage;            // 증가량
        public float range;           // 증가량
        public float fireRate;        // 감소량(빨라짐) — 음수로 표기 가능
        public float projectileSpeed; // 증가량
    }
}

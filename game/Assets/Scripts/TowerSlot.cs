using UnityEngine;

// 타워 설치 가능 슬롯 (클릭 대상). 점유 여부 + 설치된 타워 참조.
public class TowerSlot : MonoBehaviour
{
    public bool Occupied = false;
    public TowerUnit Tower;   // 설치된 타워(판매 시 해제용)
}

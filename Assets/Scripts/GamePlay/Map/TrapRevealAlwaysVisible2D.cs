using UnityEngine;

namespace Dungeon.Map
{
    /// <summary>
    /// 이 오브젝트의 SpriteRenderer를 함정 발견 전 숨김 대상에서 제외합니다.
    /// 공격 스크립트가 직접 표시 상태를 제어하는 레이저·경고선 등에 사용합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrapRevealAlwaysVisible2D : MonoBehaviour
    {
    }
}

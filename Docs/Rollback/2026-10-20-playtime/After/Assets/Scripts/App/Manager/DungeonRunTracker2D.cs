using UnityEngine;
using Dungeon.Core;

namespace Dungeon.Manager
{
    /// <summary>기존 씬/외부 호출 호환 어댑터. 실제 측정과 저장은 공용 StatusSaveFile이 담당합니다.</summary>
    public class DungeonRunTracker2D : MonoBehaviour
    {
        public static DungeonRunTracker2D Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // GameManager의 다른 컴포넌트는 유지하고 중복 어댑터만 제거합니다.
                Destroy(this);
                return;
            }
            Instance = this;
            if (transform.parent != null) transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        // 이름은 기존 연결과 호환하되 파일 쓰기 없이 다음 상태 저장에 합칩니다.
        public void FlushTimeToSave() => StatusSaveFile.Instance?.CapturePlayTime();

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}

using UnityEngine;

namespace Dungeon.System
{
    public class FrameLimiter : MonoBehaviour
    {
        public static FrameLimiter Instance { get; private set; }

        [Header("Frame Settings")]
        [SerializeField] private int targetFrameRate = 120;
        [SerializeField] private int vSyncCount = 1;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            if (transform.parent != null)
                transform.SetParent(null);

            DontDestroyOnLoad(gameObject);

            QualitySettings.vSyncCount = vSyncCount;
            Application.targetFrameRate = targetFrameRate;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
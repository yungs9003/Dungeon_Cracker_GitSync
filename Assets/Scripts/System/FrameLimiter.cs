using UnityEngine;

namespace Dungeon.System
{
public class FrameLimiter : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(gameObject); // 씬 전환해도 유지
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 60;
    }
}
}
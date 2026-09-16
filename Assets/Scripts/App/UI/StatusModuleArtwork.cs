using TMPro;
using UnityEngine;

namespace Dungeon.UI
{
    /// <summary>
    /// Resources에서 로드되는 Status 모듈 페이지의 공용 시각 에셋입니다.
    /// 실제 PNG는 Assets/Sprites/UI/Moudle에 두고 이 카탈로그만 Resources에 둡니다.
    /// </summary>
    public sealed class StatusModuleArtwork : ScriptableObject
    {
        [SerializeField] private Sprite deviceFrame;
        [SerializeField] private Sprite patternPanel;
        [SerializeField] private Sprite scanLines;
        [SerializeField] private Sprite slotFrame;
        [SerializeField] private TMP_FontAsset moduleTextFont;
        [SerializeField] private TMP_FontAsset footerFont;

        public Sprite DeviceFrame => deviceFrame;
        public Sprite PatternPanel => patternPanel;
        public Sprite ScanLines => scanLines;
        public Sprite SlotFrame => slotFrame;
        public TMP_FontAsset ModuleTextFont => moduleTextFont;
        public TMP_FontAsset FooterFont => footerFont;
    }
}

using System;
using UnityEngine;

namespace Dungeon.UI.Tutorial
{
    [CreateAssetMenu(menuName = "Dungeon/UI/Tutorial Slides", fileName = "TutorialSlides")]
    public sealed class TutorialSlides : ScriptableObject
    {
        [Tooltip("표시할 PNG Sprite를 순서대로 넣습니다. 같은 이미지를 여러 번 넣을 수 있습니다.")]
        public Sprite[] slides = Array.Empty<Sprite>();
    }
}

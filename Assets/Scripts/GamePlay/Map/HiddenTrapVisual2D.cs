using System.Collections.Generic;
using UnityEngine;
using Dungeon.Player;

namespace Dungeon.Map
{
    /// <summary>
    /// 숨겨진 함정 타일의 표시 상태를 관리합니다.
    ///
    /// 핵심 동작:
    /// - 숨김 상태: 메인 SpriteRenderer는 hidden/disguise sprite로 바꾸고,
    ///              함정의 화살/가시/마스크 같은 자식 SpriteRenderer는 꺼둡니다.
    /// - 발견 상태: 메인 SpriteRenderer를 실제 sprite로 되돌리고,
    ///              숨겨두었던 자식 SpriteRenderer를 다시 켭니다.
    ///
    /// 기존 버전은 메인 SpriteRenderer 하나만 sprite 교체를 했기 때문에,
    /// Bow/Spike 프리팹의 자식 SpriteRenderer가 계속 보여서
    /// clearRadius 밖에서도 화살/가시가 보이는 문제가 생길 수 있었습니다.
    /// </summary>
    public class HiddenTrapVisual2D : MonoBehaviour
    {
        [Header("Main Tile Renderer")]
        [Tooltip("실제 타일의 대표 SpriteRenderer입니다. 비워두면 AssetVariantSprite2D.Renderer 또는 첫 번째 SpriteRenderer를 자동으로 사용합니다.")]
        [SerializeField] private SpriteRenderer targetRenderer;

        [Header("Reveal Position")]
        [Tooltip("켜져 있으면 큰 감지 콜라이더가 아니라 타일 오브젝트 중심(transform.position) 기준으로 reveal 판정합니다.")]
        [SerializeField] private bool useTileCenterForReveal = true;

        [Tooltip("useTileCenterForReveal이 꺼져 있을 때 사용할 콜라이더입니다. 비워두면 자동으로 찾습니다.")]
        [SerializeField] private Collider2D revealCollider;

        [Header("Extra Trap Visuals")]
        [Tooltip("켜져 있으면 targetRenderer를 제외한 자식 SpriteRenderer를 숨김 상태에서 자동으로 꺼둡니다. 화살/가시/함정 장식이 미리 보이는 문제를 막습니다.")]
        [SerializeField] private bool hideChildSpriteRenderersUntilRevealed = true;

        [Tooltip("자동 수집 외에 숨김 상태에서 꺼둘 SpriteRenderer를 직접 지정할 수 있습니다.")]
        [SerializeField] private SpriteRenderer[] extraSpriteRenderersToHideUntilRevealed;

        [Tooltip("숨김 상태에서 통째로 꺼둘 오브젝트가 있다면 지정합니다. 예: 함정 마스크, 발사구 장식, 이펙트 루트.")]
        [SerializeField] private GameObject[] extraVisualsToHideUntilRevealed;

        [Header("Reveal Mode")]
        [Tooltip("켜져 있으면 한 번 발견된 뒤에는 조명 밖으로 나가도 실제 모습이 유지됩니다.")]
        [SerializeField] private bool stayRevealedAfterSeen = true;

        [Tooltip("이 이름을 가진 overlay 렌더러는 자동 숨김 대상에서 제외합니다. 이전 overlay 방식과의 호환용입니다.")]
        [SerializeField] private string hiddenOverlayName = "__HiddenVisualOverlay";

        private Sprite actualSprite;
        private Sprite disguiseSprite;

        private bool configured;
        private bool discovered;
        private bool lastInsideForwardZone;

        private readonly List<SpriteRenderer> renderersToHide = new();
        private readonly List<bool> rendererInitialEnabled = new();

        /// <summary>
        /// 구버전 JsonBlockBuilder2D 호환용 Configure입니다.
        /// </summary>
        public void Configure(Sprite disguise, bool stayRevealed)
        {
            Configure(disguise, 0f, 0f, stayRevealed);
        }

        /// <summary>
        /// 최신 JsonBlockBuilder2D 호환용 Configure입니다.
        /// actualZRotation/hiddenZRotation은 overlay 방식에서 쓰던 값이므로,
        /// 현재 direct sprite swap 방식에서는 받기만 하고 사용하지 않습니다.
        /// </summary>
        public void Configure(Sprite disguise, float actualZRotation, float hiddenZRotation, bool stayRevealed)
        {
            ResolveRenderer();
            ResolveRevealCollider();

            if (targetRenderer == null)
            {
                Debug.LogWarning($"{name}: HiddenTrapVisual2D에 사용할 SpriteRenderer가 없습니다.");
                return;
            }

            actualSprite = targetRenderer.sprite;
            disguiseSprite = disguise;
            stayRevealedAfterSeen = stayRevealed;

            discovered = false;
            lastInsideForwardZone = false;
            configured = true;

            CollectExtraRenderersToHide();
            ApplyForwardZoneState(false);
        }

        public void SetStayRevealedAfterSeen(bool value)
        {
            stayRevealedAfterSeen = value;
            ApplyForwardZoneState(lastInsideForwardZone);
        }

        /// <summary>
        /// ForwardZoneVisionMask2D가 직접 center 판정을 하지 않는 경우에도,
        /// 기본적으로 타일 중심 기준으로 reveal되도록 합니다.
        /// 큰 공격/감지용 Trigger Collider 때문에 멀리서 reveal되는 것을 방지합니다.
        /// </summary>
        public bool IsInsideForwardZone(ForwardZoneCollector collector)
        {
            if (collector == null)
                return false;

            if (useTileCenterForReveal)
                return collector.IsWorldPointInsideZone(transform.position);

            ResolveRevealCollider();

            if (revealCollider != null)
                return collector.IsColliderInsideZone(revealCollider);

            return collector.IsWorldPointInsideZone(transform.position);
        }

        public void ApplyForwardZoneState(bool isInside)
        {
            lastInsideForwardZone = isInside;

            if (isInside)
                discovered = true;

            bool showActual = isInside || (stayRevealedAfterSeen && discovered);
            ApplyVisual(showActual);
        }

        public void ForceReveal()
        {
            discovered = true;
            ApplyVisual(true);
        }

        public void ForceHideAgain()
        {
            discovered = false;
            ApplyForwardZoneState(false);
        }

        public void SetActualSprite(Sprite sprite)
        {
            if (sprite == null)
                return;

            actualSprite = sprite;
            ApplyForwardZoneState(lastInsideForwardZone);
        }

        /// <summary>
        /// 최신 overlay 방식 JsonBlockBuilder2D와의 컴파일 호환용입니다.
        /// 현재 방식에서는 hidden sprite만 교체하고 회전값은 사용하지 않습니다.
        /// </summary>
        public void SetHiddenSpriteAndRotation(Sprite sprite, float actualZRotation, float hiddenZRotation)
        {
            disguiseSprite = sprite;
            ApplyForwardZoneState(lastInsideForwardZone);
        }

        private void ApplyVisual(bool showActual)
        {
            ResolveRenderer();

            if (targetRenderer != null)
            {
                if (showActual && actualSprite != null)
                    targetRenderer.sprite = actualSprite;
                else if (!showActual && disguiseSprite != null)
                    targetRenderer.sprite = disguiseSprite;
            }

            ApplyExtraRendererVisibility(showActual);
            ApplyExtraObjectVisibility(showActual);
        }

        private void ResolveRenderer()
        {
            if (targetRenderer != null)
                return;

            var variant = GetComponent<AssetVariantSprite2D>();
            if (variant != null && variant.Renderer != null)
            {
                targetRenderer = variant.Renderer;
                return;
            }

            var rootRenderer = GetComponent<SpriteRenderer>();
            if (rootRenderer != null)
            {
                targetRenderer = rootRenderer;
                return;
            }

            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in renderers)
            {
                if (sr == null) continue;
                if (sr.name == hiddenOverlayName) continue;

                targetRenderer = sr;
                return;
            }
        }

        private void ResolveRevealCollider()
        {
            if (revealCollider != null)
                return;

            revealCollider = GetComponent<Collider2D>();

            if (revealCollider == null)
                revealCollider = GetComponentInChildren<Collider2D>(true);
        }

        /// <summary>
        /// 숨김 상태에서 꺼둘 자식 SpriteRenderer들을 수집합니다.
        /// targetRenderer는 hidden sprite를 보여줘야 하므로 제외합니다.
        /// </summary>
        private void CollectExtraRenderersToHide()
        {
            renderersToHide.Clear();
            rendererInitialEnabled.Clear();

            if (hideChildSpriteRenderersUntilRevealed)
            {
                var renderers = GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var sr in renderers)
                {
                    AddRendererToHideList(sr);
                }
            }

            if (extraSpriteRenderersToHideUntilRevealed != null)
            {
                foreach (var sr in extraSpriteRenderersToHideUntilRevealed)
                {
                    AddRendererToHideList(sr);
                }
            }
        }

        private void AddRendererToHideList(SpriteRenderer sr)
        {
            if (sr == null) return;
            if (sr == targetRenderer) return;
            if (sr.name == hiddenOverlayName) return;
            if (renderersToHide.Contains(sr)) return;

            renderersToHide.Add(sr);
            rendererInitialEnabled.Add(sr.enabled);
        }

        private void ApplyExtraRendererVisibility(bool showActual)
        {
            if (!configured)
                return;

            for (int i = renderersToHide.Count - 1; i >= 0; i--)
            {
                SpriteRenderer sr = renderersToHide[i];

                if (sr == null)
                {
                    renderersToHide.RemoveAt(i);
                    rendererInitialEnabled.RemoveAt(i);
                    continue;
                }

                bool initialEnabled = i < rendererInitialEnabled.Count && rendererInitialEnabled[i];
                sr.enabled = showActual && initialEnabled;
            }
        }

        private void ApplyExtraObjectVisibility(bool showActual)
        {
            if (extraVisualsToHideUntilRevealed == null)
                return;

            foreach (var obj in extraVisualsToHideUntilRevealed)
            {
                if (obj != null)
                    obj.SetActive(showActual);
            }
        }
    }
}

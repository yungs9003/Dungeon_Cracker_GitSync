using System.Collections.Generic;
using UnityEngine;
using Dungeon.Player;

namespace Dungeon.Map
{
    /// <summary>
    /// 발견 전/후 함정 타일의 표시 상태를 관리합니다.
    ///
    /// 핵심 동작:
    /// - Variant 모드: 발견 전/후에 AssetVariantSprite2D의 Element 0/1을 전환합니다.
    /// - Visibility 모드: 발견 전에는 지정된 SpriteRenderer의 렌더링만 차단하고 발견 후 해제합니다.
    /// - SpriteRenderer.enabled는 건드리지 않아 함정의 레이저/전격 애니메이션 상태를 보존합니다.
    /// </summary>
    public class HiddenTrapVisual2D : MonoBehaviour
    {
        private enum RevealVisualMode
        {
            AssetVariant,
            RendererVisibility
        }

        [Header("Main Tile Renderer")]
        [Tooltip("함정 프리팹의 Element 0/1을 가진 AssetVariantSprite2D입니다.")]
        [SerializeField] private AssetVariantSprite2D assetVariant;

        [Tooltip("실제 타일의 대표 SpriteRenderer입니다. 비워두면 AssetVariantSprite2D.Renderer 또는 첫 번째 SpriteRenderer를 자동으로 사용합니다.")]
        [SerializeField] private SpriteRenderer targetRenderer;

        [SerializeField] private int hiddenAssetNo = 0;
        [SerializeField] private int revealedAssetNo = 1;

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

        private bool configured;
        private bool discovered;
        private bool lastInsideForwardZone;
        private bool hasAppliedVisualState;
        private bool lastAppliedShowActual;
        private RevealVisualMode visualMode;

        private readonly List<SpriteRenderer> renderersToHide = new();

        public bool ConfigureFromAssetVariants(
            AssetVariantSprite2D variant,
            bool stayRevealed,
            int hiddenElement = 0,
            int revealedElement = 1)
        {
            assetVariant = variant != null ? variant : GetComponent<AssetVariantSprite2D>();
            ResolveRenderer();
            ResolveRevealCollider();

            if (assetVariant == null ||
                !assetVariant.HasVariant(hiddenElement) ||
                !assetVariant.HasVariant(revealedElement))
            {
                Debug.LogWarning($"{name}: 발견 전/후에 사용할 AssetVariantSprite2D Element {hiddenElement}/{revealedElement}가 없습니다.");
                configured = false;
                return false;
            }

            hiddenAssetNo = hiddenElement;
            revealedAssetNo = revealedElement;
            stayRevealedAfterSeen = stayRevealed;
            visualMode = RevealVisualMode.AssetVariant;

            discovered = false;
            lastInsideForwardZone = false;
            hasAppliedVisualState = false;
            configured = true;

            CollectExtraRenderersToHide();
            ApplyForwardZoneState(false);
            return true;
        }

        /// <summary>
        /// 별도의 Element 교체 없이 지정된 렌더러의 렌더링을 발견 전에는 차단하고,
        /// 발견 후에는 차단을 해제합니다. enabled 값은 변경하지 않습니다.
        /// 루트의 공격 스크립트와 Collider는 건드리지 않습니다.
        /// </summary>
        public bool ConfigureVisibilityOnly(
            SpriteRenderer[] renderers,
            bool stayRevealed)
        {
            assetVariant = null;
            targetRenderer = null;
            renderersToHide.Clear();

            if (renderers != null)
            {
                foreach (SpriteRenderer sr in renderers)
                    AddRendererToHideList(sr);
            }

            if (renderersToHide.Count == 0)
            {
                Debug.LogWarning($"{name}: 발견 전 숨길 자식 SpriteRenderer가 없습니다.");
                configured = false;
                return false;
            }

            stayRevealedAfterSeen = stayRevealed;
            visualMode = RevealVisualMode.RendererVisibility;

            discovered = false;
            lastInsideForwardZone = false;
            hasAppliedVisualState = false;
            configured = true;

            ResolveRevealCollider();
            ApplyForwardZoneState(false);
            return true;
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
            ApplyVisualState(showActual);
        }

        public void ForceReveal()
        {
            discovered = true;
            ApplyVisualState(true);
        }

        public void ForceHideAgain()
        {
            discovered = false;
            ApplyForwardZoneState(false);
        }

        private void ApplyVisual(bool showActual)
        {
            ResolveRenderer();

            if (visualMode == RevealVisualMode.AssetVariant &&
                assetVariant != null &&
                configured)
            {
                assetVariant.SetAssetNo(showActual ? revealedAssetNo : hiddenAssetNo);
            }

            ApplyExtraRendererVisibility(showActual);
            ApplyExtraObjectVisibility(showActual);
        }

        /// <summary>
        /// 숨김 상태는 매 프레임 강제해 아직 발견되지 않은 동적 효과가 켜지는 것을 막습니다.
        /// 발견 상태는 전환 시 한 번만 복원해 이후 함정 스크립트가 렌더러를 제어하도록 둡니다.
        /// </summary>
        private void ApplyVisualState(bool showActual)
        {
            if (!configured)
                return;

            bool shouldApply =
                !hasAppliedVisualState ||
                lastAppliedShowActual != showActual ||
                !showActual;

            if (!shouldApply)
                return;

            ApplyVisual(showActual);
            hasAppliedVisualState = true;
            lastAppliedShowActual = showActual;
        }

        private void ResolveRenderer()
        {
            if (targetRenderer != null)
                return;

            if (assetVariant == null)
                assetVariant = GetComponent<AssetVariantSprite2D>();

            if (assetVariant != null && assetVariant.Renderer != null)
            {
                targetRenderer = assetVariant.Renderer;
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
        /// targetRenderer는 Element 0/1을 직접 보여줘야 하므로 제외합니다.
        /// </summary>
        private void CollectExtraRenderersToHide()
        {
            renderersToHide.Clear();

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
            if (sr.GetComponent<TrapRevealAlwaysVisible2D>() != null) return;
            if (renderersToHide.Contains(sr)) return;

            renderersToHide.Add(sr);
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
                    continue;
                }

                sr.forceRenderingOff = !showActual;
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

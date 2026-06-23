using UnityEngine;
using Dungeon.Player;

namespace Dungeon.Map
{
    /// <summary>
    /// 실제 함정/파괴가능 타일 오브젝트의 방향/콜라이더/스크립트는 그대로 유지하고,
    /// 숨김 상태에서는 별도의 SpriteRenderer를 같은 타일 위치에 얹어서 가짜 타일처럼 보이게 합니다.
    ///
    /// 중요 수정점:
    /// - 실제 SpriteRenderer가 루트 오브젝트에 있을 때, overlay의 localPosition을 루트의 localPosition으로 복사하지 않습니다.
    /// - overlay는 항상 실제 렌더러와 같은 위치에만 놓입니다.
    /// - 이전 버전에서 잘못 생성된 중복 __HiddenVisualOverlay를 정리합니다.
    /// </summary>
    public class HiddenTrapVisual2D : MonoBehaviour
    {
        [Header("Actual Renderer")]
        [SerializeField] private SpriteRenderer actualRenderer;

        [Header("Hidden Overlay Renderer")]
        [SerializeField] private SpriteRenderer hiddenOverlayRenderer;
        [SerializeField] private string hiddenOverlayName = "__HiddenVisualOverlay";
        [SerializeField] private int hiddenOverlaySortingOrderOffset = 1;

        [Header("Reveal Collider")]
        [Tooltip("비워두면 transform.position 기준으로 판정합니다. 가능하면 타일 본체 Collider2D를 넣는 것을 권장합니다.")]
        [SerializeField] private Collider2D revealCollider;

        [Header("Optional Extra Visuals")]
        [Tooltip("숨김 상태에서 같이 꺼둘 자식 오브젝트가 있다면 넣으세요. 예: 함정 마스크, 장식 Sprite 등")]
        [SerializeField] private GameObject[] extraVisualsToHideUntilRevealed;

        [Header("Reveal Mode")]
        [Tooltip("켜져 있으면 한 번 발견된 뒤에는 ForwardZone 밖으로 나가도 실제 모습이 유지됩니다.")]
        [SerializeField] private bool stayRevealedAfterSeen = true;

        [Tooltip("숨김 상태에서 실제 함정 SpriteRenderer를 꺼두고, hidden overlay만 보이게 합니다.")]
        [SerializeField] private bool disableActualRendererWhileHidden = true;

        private bool discovered;
        private bool lastInsideForwardZone;
        private bool actualRendererInitialEnabled = true;
        private Sprite hiddenSprite;
        private float configuredHiddenWorldZRotation;

        /// <summary>
        /// 구버전 호출 호환용입니다.
        /// </summary>
        public void Configure(Sprite disguise, bool stayRevealed)
        {
            Configure(disguise, 0f, 0f, stayRevealed);
        }

        /// <summary>
        /// actualZRotation은 호환용으로 받지만, overlay 위치 계산에는 사용하지 않습니다.
        /// hiddenZRotation은 숨김용 스프라이트가 월드 기준으로 보여야 하는 Z 회전값입니다.
        /// </summary>
        public void Configure(Sprite disguise, float actualZRotation, float hiddenZRotation, bool stayRevealed)
        {
            ResolveActualRenderer();
            ResolveRevealCollider();

            if (actualRenderer == null)
            {
                Debug.LogWarning($"{name}: HiddenTrapVisual2D에 사용할 실제 SpriteRenderer가 없습니다.");
                return;
            }

            actualRendererInitialEnabled = actualRenderer.enabled;
            hiddenSprite = disguise;
            configuredHiddenWorldZRotation = hiddenZRotation;
            stayRevealedAfterSeen = stayRevealed;

            EnsureHiddenOverlayRenderer();
            ApplyHiddenOverlaySpriteAndTransform(hiddenZRotation);

            discovered = false;
            lastInsideForwardZone = false;

            ApplyForwardZoneState(false);
        }

        public void SetStayRevealedAfterSeen(bool value)
        {
            stayRevealedAfterSeen = value;
            ApplyForwardZoneState(lastInsideForwardZone);
        }

        public bool IsInsideForwardZone(ForwardZoneCollector collector)
        {
            if (collector == null)
                return false;

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
            ResolveActualRenderer();
            if (actualRenderer != null && sprite != null)
                actualRenderer.sprite = sprite;
        }

        public void SetHiddenSpriteAndRotation(Sprite sprite, float actualZRotation, float hiddenZRotation)
        {
            hiddenSprite = sprite;
            configuredHiddenWorldZRotation = hiddenZRotation;
            EnsureHiddenOverlayRenderer();
            ApplyHiddenOverlaySpriteAndTransform(hiddenZRotation);
            ApplyForwardZoneState(lastInsideForwardZone);
        }

        private void ApplyVisual(bool showActual)
        {
            ResolveActualRenderer();
            EnsureHiddenOverlayRenderer();

            if (actualRenderer != null)
            {
                if (showActual)
                    actualRenderer.enabled = actualRendererInitialEnabled;
                else if (disableActualRendererWhileHidden)
                    actualRenderer.enabled = false;
                else
                    actualRenderer.enabled = actualRendererInitialEnabled;
            }

            if (hiddenOverlayRenderer != null)
            {
                // 혹시 런타임 중 부모/위치가 틀어진 경우에도 매번 올바른 위치로 되돌립니다.
                ApplyHiddenOverlaySpriteAndTransform(configuredHiddenWorldZRotation);
                hiddenOverlayRenderer.enabled = !showActual && hiddenSprite != null;
            }

            if (extraVisualsToHideUntilRevealed != null)
            {
                foreach (var obj in extraVisualsToHideUntilRevealed)
                {
                    if (obj != null)
                        obj.SetActive(showActual);
                }
            }
        }

        private void ResolveActualRenderer()
        {
            if (actualRenderer != null && actualRenderer.name != hiddenOverlayName)
                return;

            actualRenderer = null;

            var variant = GetComponent<AssetVariantSprite2D>();
            if (variant != null && variant.Renderer != null && variant.Renderer.name != hiddenOverlayName)
            {
                actualRenderer = variant.Renderer;
                return;
            }

            var rootRenderer = GetComponent<SpriteRenderer>();
            if (rootRenderer != null && rootRenderer.name != hiddenOverlayName)
            {
                actualRenderer = rootRenderer;
                return;
            }

            var renderers = GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var sr in renderers)
            {
                if (sr == null) continue;
                if (sr.name == hiddenOverlayName) continue;

                actualRenderer = sr;
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

        private void EnsureHiddenOverlayRenderer()
        {
            ResolveActualRenderer();

            Transform desiredParent = GetDesiredOverlayParent();
            CleanupWrongOrDuplicateOverlays(desiredParent);

            Transform found = desiredParent.Find(hiddenOverlayName);
            GameObject overlayObj;

            if (found != null)
            {
                overlayObj = found.gameObject;
            }
            else
            {
                overlayObj = new GameObject(hiddenOverlayName);
                overlayObj.transform.SetParent(desiredParent, false);
            }

            hiddenOverlayRenderer = overlayObj.GetComponent<SpriteRenderer>();
            if (hiddenOverlayRenderer == null)
                hiddenOverlayRenderer = overlayObj.AddComponent<SpriteRenderer>();

            hiddenOverlayRenderer.enabled = false;
        }

        private Transform GetDesiredOverlayParent()
        {
            // 실제 SpriteRenderer가 루트 오브젝트라면 overlay는 반드시 루트의 자식이어야 합니다.
            // 이전 버전처럼 루트의 localPosition을 overlay localPosition으로 복사하면
            // 특정 좌표, 특히 0000 근처에 다른 타일 overlay가 겹치는 문제가 생길 수 있습니다.
            if (actualRenderer == null || actualRenderer.transform == transform)
                return transform;

            // 실제 SpriteRenderer가 자식 오브젝트에 붙어 있는 프리팹이면 같은 부모 아래에 overlay를 만듭니다.
            if (actualRenderer.transform.parent != null)
                return actualRenderer.transform.parent;

            return transform;
        }

        private void CleanupWrongOrDuplicateOverlays(Transform desiredParent)
        {
            if (desiredParent == null)
                return;

            var transforms = GetComponentsInChildren<Transform>(true);
            foreach (var t in transforms)
            {
                if (t == null) continue;
                if (t == transform) continue;
                if (t.name != hiddenOverlayName) continue;

                bool isDesiredDirectChild = t.parent == desiredParent;
                bool isCurrentRenderer = hiddenOverlayRenderer != null && t == hiddenOverlayRenderer.transform;

                if (isDesiredDirectChild && !isCurrentRenderer)
                    continue;

                if (isCurrentRenderer && isDesiredDirectChild)
                    continue;

                if (Application.isPlaying)
                    Destroy(t.gameObject);
                else
                    DestroyImmediate(t.gameObject);
            }
        }

        private void ApplyHiddenOverlaySpriteAndTransform(float hiddenWorldZRotation)
        {
            if (hiddenOverlayRenderer == null)
                return;

            Transform desiredParent = GetDesiredOverlayParent();
            if (hiddenOverlayRenderer.transform.parent != desiredParent)
                hiddenOverlayRenderer.transform.SetParent(desiredParent, false);

            hiddenOverlayRenderer.sprite = hiddenSprite;

            if (actualRenderer != null && actualRenderer.transform != transform)
            {
                // 실제 SpriteRenderer가 자식이면 그 자식 SpriteRenderer의 상대 위치/스케일을 따라갑니다.
                hiddenOverlayRenderer.transform.localPosition = actualRenderer.transform.localPosition;
                hiddenOverlayRenderer.transform.localScale = actualRenderer.transform.localScale;
            }
            else
            {
                // 실제 SpriteRenderer가 루트에 있으면 overlay는 루트 타일 중심에 정확히 붙어야 합니다.
                hiddenOverlayRenderer.transform.localPosition = Vector3.zero;
                hiddenOverlayRenderer.transform.localScale = Vector3.one;
            }

            float parentWorldZ = hiddenOverlayRenderer.transform.parent != null
                ? hiddenOverlayRenderer.transform.parent.eulerAngles.z
                : 0f;
            float localZ = Mathf.DeltaAngle(parentWorldZ, hiddenWorldZRotation);
            hiddenOverlayRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, localZ);

            if (actualRenderer != null)
            {
                hiddenOverlayRenderer.sortingLayerID = actualRenderer.sortingLayerID;
                hiddenOverlayRenderer.sortingOrder = actualRenderer.sortingOrder + hiddenOverlaySortingOrderOffset;
                hiddenOverlayRenderer.flipX = actualRenderer.flipX;
                hiddenOverlayRenderer.flipY = actualRenderer.flipY;
                hiddenOverlayRenderer.color = Color.white;
                hiddenOverlayRenderer.material = actualRenderer.sharedMaterial;
            }
        }
    }
}

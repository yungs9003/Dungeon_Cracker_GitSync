using UnityEngine;

public class AssetVariantSprite2D : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] variants;

    public SpriteRenderer Renderer
    {
        get
        {
            EnsureRenderer();
            return spriteRenderer;
        }
    }

    public int CurrentAssetNo { get; private set; } = -1;
    public int VariantCount => variants != null ? variants.Length : 0;

    private void Awake()
    {
        EnsureRenderer();
    }

    private void EnsureRenderer()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void SetAssetNo(int assetNo)
    {
        EnsureRenderer();

        if (spriteRenderer == null)
        {
            Debug.LogWarning($"{name}: SpriteRenderer가 없습니다.");
            return;
        }

        Sprite sprite = GetSpriteForAssetNo(assetNo, true);
        if (sprite == null)
        {
            Debug.LogWarning($"{name}: 적용할 Sprite가 없습니다.");
            return;
        }

        CurrentAssetNo = assetNo;
        spriteRenderer.sprite = sprite;
    }

    public Sprite GetSpriteForAssetNo(int assetNo, bool fallbackToZero = true)
    {
        EnsureRenderer();

        if (variants == null || variants.Length == 0)
            return spriteRenderer != null ? spriteRenderer.sprite : null;

        if (assetNo < 0 || assetNo >= variants.Length)
        {
            if (!fallbackToZero)
                return null;

            Debug.LogWarning($"{name}: assetNo {assetNo}는 범위를 벗어났습니다. 0번으로 대체합니다.");
            assetNo = 0;
        }

        return variants[assetNo];
    }

    public bool HasVariant(int assetNo)
    {
        return variants != null &&
               assetNo >= 0 &&
               assetNo < variants.Length &&
               variants[assetNo] != null;
    }
}

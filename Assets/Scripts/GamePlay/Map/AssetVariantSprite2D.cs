using UnityEngine;

public class AssetVariantSprite2D : MonoBehaviour
{
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] variants;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void SetAssetNo(int assetNo)
    {
        if (spriteRenderer == null)
        {
            Debug.LogWarning($"{name}: SpriteRenderer가 없습니다.");
            return;
        }

        if (variants == null || variants.Length == 0)
        {
            Debug.LogWarning($"{name}: variants 배열이 비어 있습니다.");
            return;
        }

        if (assetNo < 0 || assetNo >= variants.Length)
        {
            Debug.LogWarning($"{name}: assetNo {assetNo}는 범위를 벗어났습니다. 0번으로 대체합니다.");
            assetNo = 0;
        }

        spriteRenderer.sprite = variants[assetNo];
    }
}
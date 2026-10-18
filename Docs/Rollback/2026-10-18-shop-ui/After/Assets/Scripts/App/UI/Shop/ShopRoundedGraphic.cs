using UnityEngine;
using UnityEngine.UI;

namespace Dungeon.UI.Shop
{
    /// <summary>시안의 원형 상품 받침과 둥근 결제 버튼을 UI 도형으로 그립니다.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class ShopRoundedGraphic : MaskableGraphic
    {
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * .5f;
            mesh.AddVert(rect.center, color, Vector2.one * .5f);
            int count = 0;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 center = new(corner < 2 ? rect.xMax - radius : rect.xMin + radius,
                    corner == 0 || corner == 3 ? rect.yMax - radius : rect.yMin + radius);
                for (int step = 0; step <= 12; step++)
                {
                    float angle = (90f - corner * 90f - step * 7.5f) * Mathf.Deg2Rad;
                    mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, color, Vector2.one * .5f);
                    count++;
                }
            }
            for (int i = 1; i <= count; i++) mesh.AddTriangle(0, i, i == count ? 1 : i + 1);
        }
    }
}

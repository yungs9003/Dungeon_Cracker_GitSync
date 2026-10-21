using UnityEngine;

namespace Dungeon.Map
{
    public partial class JsonBlockBuilder2D
    {
        [Header("Map Area / Exterior")]
        public bool constrainToTileArea = true;
        [Tooltip("비워두면 Resources/Data/MapExteriorSettings의 테마별 색상/이미지를 사용합니다.")]
        public MapExteriorSettings2D exteriorSettings;
        public Camera exteriorCamera;
        public MapArea2D MapArea { get; private set; }

        private void BeginMapEnvironment()
        {
            if (MapArea == null) MapArea = GetComponent<MapArea2D>();
            if (constrainToTileArea)
            {
                if (MapArea == null) MapArea = gameObject.AddComponent<MapArea2D>();
                MapArea.enabled = true; MapArea.Initialize(cellSize, player);
            }
            else if (MapArea != null) MapArea.enabled = false;
        }

        private void CompleteMapEnvironment()
        {
            if (MapArea != null && MapArea.enabled) MapArea.BuildBoundary();
            var visual = GetComponent<MapExteriorVisual2D>();
            if (visual == null) visual = gameObject.AddComponent<MapExteriorVisual2D>();
            visual.Configure(exteriorSettings != null ? exteriorSettings : Resources.Load<MapExteriorSettings2D>("Data/MapExteriorSettings"),
                CurrentSpriteTheme, exteriorCamera != null ? exteriorCamera : Camera.main, baseZ);
        }
    }
}

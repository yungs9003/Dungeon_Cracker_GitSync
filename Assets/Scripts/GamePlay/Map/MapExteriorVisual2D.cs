using UnityEngine;

namespace Dungeon.Map
{
    [DisallowMultipleComponent]
    public sealed class MapExteriorVisual2D : MonoBehaviour
    {
        private Camera targetCamera;
        private Camera configuredCamera;
        private Color previousColor;
        private CameraClearFlags previousFlags;
        private MapExteriorSettings2D.Background background;
        private SpriteRenderer image;
        private float worldZ;

        public void Configure(MapExteriorSettings2D settings, FieldEffectManager.FieldEffectType theme, Camera camera, float baseZ)
        {
            configuredCamera = camera;
            if (targetCamera != camera) { RestoreCamera(); targetCamera = camera;
                if (targetCamera != null) { previousColor = targetCamera.backgroundColor; previousFlags = targetCamera.clearFlags; } }
            background = settings != null ? settings.Get(theme) : null;
            worldZ = baseZ + 10f;
            ApplyColor();
            UpdateImage();
        }

        private void ApplyColor()
        {
            if (targetCamera == null || background == null) return;
            var color = background.color; color.a = 1f;
            targetCamera.backgroundColor = color;
            targetCamera.clearFlags = CameraClearFlags.SolidColor;
        }

        private void LateUpdate() => UpdateImage();
        private void UpdateImage()
        {
            bool visible = targetCamera != null && targetCamera.orthographic && background != null && background.sprite != null;
            if (!visible) { if (image != null) image.enabled = false; return; }
            if (image == null)
            {
                var go = new GameObject("MapExteriorImage"); go.transform.SetParent(transform, false);
                image = go.AddComponent<SpriteRenderer>(); image.sortingLayerID = SortingLayer.layers[0].id; image.sortingOrder = -32760;
            }
            image.enabled = true; image.sprite = background.sprite;
            float height = targetCamera.orthographicSize * 2f, width = height * targetCamera.aspect;
            Vector3 cameraPosition = targetCamera.transform.position;
            Vector2 native = image.sprite.bounds.size;
            if (background.tiled)
            {
                float tileSize = Mathf.Max(0.01f, background.tileWorldSize);
                float scale = tileSize / Mathf.Max(0.001f, native.x);
                image.drawMode = SpriteDrawMode.Tiled;
                image.transform.localScale = Vector3.one * scale;
                image.size = new Vector2(Mathf.Ceil(width / tileSize + 2f) * tileSize / scale, Mathf.Ceil(height / tileSize + 2f) * tileSize / scale);
                image.transform.position = new Vector3(Mathf.Floor(cameraPosition.x / tileSize) * tileSize, Mathf.Floor(cameraPosition.y / tileSize) * tileSize, worldZ);
            }
            else
            {
                image.drawMode = SpriteDrawMode.Simple;
                image.transform.localScale = new Vector3((width + 0.1f) / native.x, (height + 0.1f) / native.y, 1f);
                image.transform.position = new Vector3(cameraPosition.x, cameraPosition.y, worldZ);
            }
        }
        private void RestoreCamera()
        {
            if (targetCamera == null) return;
            targetCamera.backgroundColor = previousColor; targetCamera.clearFlags = previousFlags;
            targetCamera = null;
        }
        private void OnDisable() { RestoreCamera(); if (image != null) image.enabled = false; }
        private void OnEnable()
        {
            if (targetCamera != null || configuredCamera == null) return;
            targetCamera = configuredCamera;
            previousColor = targetCamera.backgroundColor; previousFlags = targetCamera.clearFlags;
            ApplyColor(); UpdateImage();
        }
    }
}

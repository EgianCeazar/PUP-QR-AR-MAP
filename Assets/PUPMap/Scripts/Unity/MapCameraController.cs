// Top-down camera: drag to pan, pinch (or mouse wheel) to zoom, tap to pick a place.
// It also glides to show the whole route, leaving room for the top and bottom panels.
using UnityEngine;
using UnityEngine.EventSystems;

namespace PUPMap
{
    [RequireComponent(typeof(Camera))]
    public class MapCameraController : MonoBehaviour
    {
        public MapView2D view;
        public float minZoom = 15f;
        public float maxZoom = 400f;

        [Header("Screen space covered by UI panels (0-1 of screen height)")]
        public float topPanel = 0.27f;
        public float bottomPanel = 0.30f;

        Camera cam;
        Vector3 lastMouse, downMouse;
        float downTime;
        bool dragging, pinching;

        // smooth glide target
        bool gliding;
        Vector3 glidePos;
        float glideSize;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            if (view != null)
            {
                view.Drawn += FitToMap;
                if (view.MapBounds.size != Vector3.zero) FitToMap();
            }
        }

        public void FitToMap()
        {
            if (view == null) return;
            maxZoom = Mathf.Max(maxZoom, view.MapBounds.extents.y * 1.4f);
            FocusOn(view.MapBounds, instant: true, padding: 1f);
        }

        /// <summary>Move/zoom so the box fits in the visible area between the panels.</summary>
        public void FocusOn(Bounds b, bool instant = false, float padding = 1.25f)
        {
            float visible = Mathf.Clamp(1f - topPanel - bottomPanel, 0.2f, 1f);
            float sizeForHeight = b.extents.y * padding / visible;
            float sizeForWidth = b.extents.x * padding / Mathf.Max(cam.aspect, 0.01f);
            float size = Mathf.Clamp(Mathf.Max(sizeForHeight, sizeForWidth), minZoom, maxZoom);
            // Center the box in the gap between the panels, not the middle of the screen.
            float visibleCenter = bottomPanel + visible / 2f;          // 0 = bottom of screen, 1 = top
            float y = b.center.y - (visibleCenter - 0.5f) * 2f * size;
            var pos = new Vector3(b.center.x, y, -100f);
            if (instant) { transform.position = pos; cam.orthographicSize = size; gliding = false; }
            else { glidePos = pos; glideSize = size; gliding = true; }
        }

        void Update()
        {
            if (gliding)
            {
                float t = 1f - Mathf.Exp(-8f * Time.deltaTime);
                transform.position = Vector3.Lerp(transform.position, glidePos, t);
                cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, glideSize, t);
                if (Vector3.Distance(transform.position, glidePos) < 0.05f && Mathf.Abs(cam.orthographicSize - glideSize) < 0.05f) gliding = false;
            }

            if (Input.touchCount == 2)
            {
                var t0 = Input.GetTouch(0); var t1 = Input.GetTouch(1);
                float prev = ((t0.position - t0.deltaPosition) - (t1.position - t1.deltaPosition)).magnitude;
                float now = (t0.position - t1.position).magnitude;
                if (prev > 0) ZoomBy(prev / Mathf.Max(now, 1f));
                dragging = false; pinching = true; gliding = false;
                return;
            }
            if (Input.touchCount == 0 && !Input.GetMouseButton(0)) pinching = false;

            if (Mathf.Abs(Input.mouseScrollDelta.y) > 0.01f && !IsOverUI())
            { ZoomBy(Mathf.Pow(0.9f, Input.mouseScrollDelta.y)); gliding = false; }

            if (Input.GetMouseButtonDown(0) && !IsOverUI())
            {
                dragging = true;
                lastMouse = downMouse = Input.mousePosition;
                downTime = Time.unscaledTime;
            }
            if (dragging && Input.GetMouseButton(0) && !pinching)
            {
                Vector3 delta = cam.ScreenToWorldPoint(lastMouse) - cam.ScreenToWorldPoint(Input.mousePosition);
                if (delta.sqrMagnitude > 0) gliding = false;
                transform.position += new Vector3(delta.x, delta.y, 0);
                lastMouse = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(0) && dragging)
            {
                dragging = false;
                bool isTap = (Input.mousePosition - downMouse).magnitude < Screen.height * 0.015f
                             && Time.unscaledTime - downTime < 0.4f && !pinching;
                if (isTap && view != null) view.HandleTap(cam.ScreenToWorldPoint(Input.mousePosition));
            }
            KeepOnMap();
        }

        void ZoomBy(float factor)
        {
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize * factor, minZoom, maxZoom);
        }

        /// <summary>Don't let the camera wander far off the campus.</summary>
        void KeepOnMap()
        {
            if (view == null || view.MapBounds.size == Vector3.zero || gliding) return;
            var b = view.MapBounds;
            var p = transform.position;
            p.x = Mathf.Clamp(p.x, b.min.x, b.max.x);
            p.y = Mathf.Clamp(p.y, b.min.y - cam.orthographicSize * 0.5f, b.max.y + cam.orthographicSize * 0.5f);
            transform.position = p;
        }

        static bool IsOverUI()
        {
            if (EventSystem.current == null) return false;
            if (Input.touchCount > 0) return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
            return EventSystem.current.IsPointerOverGameObject();
        }
    }
}

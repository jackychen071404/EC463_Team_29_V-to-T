using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public class FreeDragUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform rt;
    private Canvas canvas;
    private RectTransform canvasRt;
    private CanvasGroup cg;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
        cg = GetComponent<CanvasGroup>();

        canvas = GetComponentInParent<Canvas>();
        canvasRt = canvas.GetComponent<RectTransform>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // Bring to front so it doesn't hide behind other UI
        rt.SetAsLastSibling();

        // While dragging, don't block raycasts (optional; helps with buttons if needed)
        cg.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        // Move based on pointer delta, corrected for canvas scaling
        rt.anchoredPosition += eventData.delta / canvas.scaleFactor;

        ClampToCanvas();
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        cg.blocksRaycasts = true;

        // Final clamp to avoid ending slightly off-screen
        ClampToCanvas();
    }

    private void ClampToCanvas()
    {
        // Canvas rect (in its own local space)
        Rect c = canvasRt.rect;

        // This element size
        Vector2 size = rt.rect.size;

        // Compute half-extents considering current scale
        float halfW = (size.x * rt.localScale.x) * 0.5f;
        float halfH = (size.y * rt.localScale.y) * 0.5f;

        // Clamp anchoredPosition so the whole item stays inside the canvas rect
        Vector2 p = rt.anchoredPosition;

        p.x = Mathf.Clamp(p.x, c.xMin + halfW, c.xMax - halfW);
        p.y = Mathf.Clamp(p.y, c.yMin + halfH, c.yMax - halfH);

        rt.anchoredPosition = p;
    }
}
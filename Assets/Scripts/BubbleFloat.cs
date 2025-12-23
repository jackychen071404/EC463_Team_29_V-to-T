using UnityEngine;
using System.Collections.Generic;

public class BubbleFloat : MonoBehaviour
{
    private static List<BubbleFloat> allBubbles = new List<BubbleFloat>();

    private RectTransform rect;
    private RectTransform canvasRect;
    private Vector2 dir;
    private float speed;

    // controls bubble interaction
    [Header("Bubble Settings")]
    public float bubbleRadius = 90f;     // half of bubble diameter 
    public float repelForce = 500f;      // strength of separation force
    public float padding = 100f;         // keeps bubbles away from edges

    private float xBoundary;
    private float yBoundary;

    void Start()
    {
        rect = GetComponent<RectTransform>();
        canvasRect = GetComponentInParent<Canvas>().GetComponent<RectTransform>();

        // random direction & speed
        dir = Random.insideUnitCircle.normalized;
        speed = Random.Range(40f, 80f);

        // calculate boundaries based on canvas size
        xBoundary = (canvasRect.rect.width / 2f) - padding;
        yBoundary = (canvasRect.rect.height / 2f) - padding;

        allBubbles.Add(this);
    }

    void OnDestroy()
    {
        allBubbles.Remove(this);
    }

    void Update()
    {
        if (rect == null) return;

        // movement
        rect.anchoredPosition += dir * speed * Time.deltaTime;
        Vector2 pos = rect.anchoredPosition;

        // bounce away from edges 
        if (Mathf.Abs(pos.x) > xBoundary)
        {
            dir.x = -dir.x;
            pos.x = Mathf.Sign(pos.x) * xBoundary;
        }
        if (Mathf.Abs(pos.y) > yBoundary)
        {
            dir.y = -dir.y;
            pos.y = Mathf.Sign(pos.y) * yBoundary;
        }

        rect.anchoredPosition = pos;

        // repel nearby bubbles
        foreach (var other in allBubbles)
        {
            if (other == this || other.rect == null) continue;

            Vector2 diff = rect.anchoredPosition - other.rect.anchoredPosition;
            float dist = diff.magnitude;

            // only repel if within radius
            if (dist < bubbleRadius * 2f && dist > 0.001f)
            {
                float strength = (1f - (dist / (bubbleRadius * 2f))) * repelForce * Time.deltaTime;
                Vector2 push = diff.normalized * strength;

                // balance push between both bubbles
                rect.anchoredPosition += push / 2f;
                other.rect.anchoredPosition -= push / 2f;
            }
        }

        // scale bob for realism
        float s = 1f + Mathf.Sin(Time.time * 2f) * 0.03f;
        rect.localScale = new Vector3(s, s, 1);
    }
}

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public class TspRouteLine : Graphic
{
    [SerializeField] private float lineWidth = 6f;

    private readonly List<Vector2> points = new();
    private readonly List<Color> segmentColors = new();

    protected override void Awake()
    {
        base.Awake();

        color = Color.red;
        raycastTarget = false;
    }

    public void SetPoints(List<Vector2> newPoints)
    {
        points.Clear();
        points.AddRange(newPoints);

        segmentColors.Clear();

        SetVerticesDirty();
    }

    public void SetColoredSegments(
        List<Vector2> newPoints,
        List<Color> newSegmentColors)
    {
        points.Clear();
        points.AddRange(newPoints);

        segmentColors.Clear();
        segmentColors.AddRange(newSegmentColors);

        SetVerticesDirty();
    }

    public void SetLineColor(Color newColor)
    {
        color = newColor;
        segmentColors.Clear();

        SetVerticesDirty();
    }

    public void ClearLine()
    {
        points.Clear();
        segmentColors.Clear();

        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (points.Count < 2)
            return;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Color segmentColor = color;

            if (i < segmentColors.Count)
                segmentColor = segmentColors[i];

            // The controller marks shared edges with its existing orange color.
            // Render that marker as parallel red/green stripes instead.
            if (IsSharedRouteColor(segmentColor))
                AddSharedSegment(vh, points[i], points[i + 1]);
            else
            {
                // Per-segment colors are used by Compare Both. A unique edge
                // has the same width as one stripe of a shared edge.
                float width = i < segmentColors.Count ? lineWidth * 0.5f : lineWidth;
                AddSegment(vh, points[i], points[i + 1], segmentColor, width);
            }
        }
    }

    private static bool IsSharedRouteColor(Color segmentColor)
    {
        return Mathf.Approximately(segmentColor.r, 1f) &&
            Mathf.Approximately(segmentColor.g, 0.65f) &&
            Mathf.Approximately(segmentColor.b, 0f);
    }

    private void AddSharedSegment(VertexHelper vh, Vector2 start, Vector2 end)
    {
        // Both route graphics may draw this edge in opposite directions.
        // A consistent endpoint order keeps their red and green halves aligned.
        if (start.x > end.x || (start.x == end.x && start.y > end.y))
        {
            Vector2 swap = start;
            start = end;
            end = swap;
        }

        Vector2 direction = (end - start).normalized;
        Vector2 offset = new Vector2(-direction.y, direction.x) * (lineWidth / 2f);

        AddQuad(vh, start - offset, start, end, end - offset, Color.red);
        AddQuad(vh, start, start + offset, end + offset, end, Color.green);
    }

    private void AddSegment(
        VertexHelper vh,
        Vector2 start,
        Vector2 end,
        Color segmentColor,
        float width)
    {
        Vector2 direction = (end - start).normalized;

        Vector2 offset =
            new Vector2(-direction.y, direction.x) *
            (width / 2f);

        AddQuad(vh, start - offset, start + offset,
            end + offset, end - offset, segmentColor);
    }

    private static void AddQuad(VertexHelper vh, Vector2 first, Vector2 second,
        Vector2 third, Vector2 fourth, Color segmentColor)
    {
        int index = vh.currentVertCount;

        UIVertex vertex = UIVertex.simpleVert;
        vertex.color = segmentColor;

        vertex.position = first;
        vh.AddVert(vertex);

        vertex.position = second;
        vh.AddVert(vertex);

        vertex.position = third;
        vh.AddVert(vertex);

        vertex.position = fourth;
        vh.AddVert(vertex);

        vh.AddTriangle(index, index + 1, index + 2);
        vh.AddTriangle(index, index + 2, index + 3);
    }
}
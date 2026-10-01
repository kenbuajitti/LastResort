using UnityEngine;
using UnityEngine.UI;

// Geometry avoids missing speaker emoji in TMP font atlases.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class IQSpeakerGraphic : MaskableGraphic
{
    public bool IsOn;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        // Thin outlined speaker, like the supplied system-volume icons.
        Vector2[] outline = {
            new Vector2(-12,-4), new Vector2(-7,-4), new Vector2(-1,-9),
            new Vector2(-1,9), new Vector2(-7,4), new Vector2(-12,4)
        };
        for (int i = 0; i < outline.Length; i++)
            Line(vh, outline[i], outline[(i+1) % outline.Length]);
        if (IsOn)
        {
            Arc(vh, 5); Arc(vh, 9); Arc(vh, 13);
        }
        else
        {
            Line(vh, new Vector2(5,-4), new Vector2(13,4));
            Line(vh, new Vector2(5,4), new Vector2(13,-4));
        }
    }
    void Arc(VertexHelper vh, float radius)
    {
        const int segments = 12;
        for (int i = 0; i < segments; i++)
        {
            float a = Mathf.Lerp(-.82f, .82f, i / (float)segments);
            float b = Mathf.Lerp(-.82f, .82f, (i+1) / (float)segments);
            Line(vh, new Vector2(Mathf.Cos(a)*radius, Mathf.Sin(a)*radius),
                new Vector2(Mathf.Cos(b)*radius, Mathf.Sin(b)*radius));
        }
    }
    void Line(VertexHelper vh, Vector2 a, Vector2 b)
    {
        var d = (b-a).normalized; var n = new Vector2(-d.y,d.x) * .65f;
        Quad(vh, a-n,b-n,b+n,a+n);
    }
    void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        int i = vh.currentVertCount;
        vh.AddVert(a,color,Vector2.zero); vh.AddVert(b,color,Vector2.zero);
        vh.AddVert(c,color,Vector2.zero); vh.AddVert(d,color,Vector2.zero);
        vh.AddTriangle(i,i+1,i+2); vh.AddTriangle(i,i+2,i+3);
    }
}

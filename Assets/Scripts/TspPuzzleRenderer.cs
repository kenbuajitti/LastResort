using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TspPuzzleRenderer : MonoBehaviour
{
    private bool selectionEnabled;

    [SerializeField] private TspRouteLine routeLine;
    [SerializeField] private TspPuzzleLoader puzzleLoader;
    [SerializeField] private RectTransform puzzleArea;
    [SerializeField] private Button nodeButtonPrefab;

    [SerializeField] private float horizontalPadding = 70f;
    [SerializeField] private float verticalPadding = 55f;
    [SerializeField] private float nodeLabelFontSize = 24f;
    [SerializeField] private float touchRadius = 70f;

    public event Action<int> NodeSelected;
    public event Action LayoutChanged;
    private TspPuzzleData displayedPuzzle;
    private Vector2 lastBoardSize;

    // UI is created inside the existing board; no scene or prefab rewiring needed.
    private RectTransform boardHeader;
    private TMP_Text boardLegend;
    private TMP_Text boardError;
    private string completionError = "";
    private float currentRouteDistance;
    private float optimalRouteDistance;

    private void UpdateRouteDistances()
    {
        if (boardLegend == null) return;
        boardLegend.text =
            $"<space=0.5em><color=#FF0000><b>—</b></color> Your route: {currentRouteDistance:F2}\n" +
            $"<space=0.5em><color=#00FF00><b>—</b></color> Optimal route: {optimalRouteDistance:F2}";
    }

    // Scale only the two bar glyphs, leaving the labels and numbers unchanged.
    // The leading space reserves room for the extra bar length.
    private void ResizeRouteLegendBars(TMP_TextInfo textInfo)
    {
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo character = textInfo.characterInfo[i];
            if (!character.isVisible || character.character != '—') continue;

            Vector3[] vertices = textInfo.meshInfo[character.materialReferenceIndex].vertices;
            int first = character.vertexIndex;
            float right = vertices[first + 2].x;
            float centerY = (vertices[first].y + vertices[first + 1].y) * .5f;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 vertex = vertices[first + corner];
                vertex.x = right + (vertex.x - right) * 1.5f;
                vertex.y = centerY + (vertex.y - centerY) * 2f;
                vertices[first + corner] = vertex;
            }
        }
    }

    private float NodeDistance(int first, int second)
    {
        TspNodeData a = displayedPuzzle.nodes[first];
        TspNodeData b = displayedPuzzle.nodes[second];
        return Vector2.Distance(new Vector2(a.x, a.y), new Vector2(b.x, b.y));
    }

    private void ResetRouteDistances()
    {
        currentRouteDistance = 0f;
        optimalRouteDistance = 0f;
        List<int> path = displayedPuzzle.optimalPath;
        if (path != null && path.Count > 1)
        {
            for (int i = 1; i < path.Count; i++)
                optimalRouteDistance += NodeDistance(path[i - 1], path[i]);
            // Accept JSON optimal paths with or without a repeated start node.
            if (path[path.Count - 1] != path[0])
                optimalRouteDistance += NodeDistance(path[path.Count - 1], path[0]);
        }
        UpdateRouteDistances();
    }
    private float HeaderHeight => puzzleArea.rect.height * .12f;

    public void SetCompletionError(float? errorPercentage)
    {
        completionError = errorPercentage.HasValue
            ? $"Error: {Mathf.Max(0f, errorPercentage.Value):F2}%" : "";
        if (boardError != null) boardError.text = completionError;
    }

    private void CreateBoardPresentation()
    {
        Image boardImage = puzzleArea.GetComponent<Image>();
        if (boardImage != null)
        {
            boardImage.sprite = null;
            boardImage.type = Image.Type.Simple;
            boardImage.color = new Color(.97f, .97f, .99f, 1f);
        }

        Texture2D texture = Resources.Load<Texture2D>("RouteIQ/BoardBackground");
        if (texture != null)
        {
            var backgroundObject = new GameObject("RouteIQBoardBackground", typeof(RectTransform), typeof(RawImage));
            var background = backgroundObject.GetComponent<RawImage>();
            background.rectTransform.SetParent(puzzleArea, false);
            background.rectTransform.anchorMin = Vector2.zero;
            background.rectTransform.anchorMax = Vector2.one;
            background.rectTransform.offsetMin = background.rectTransform.offsetMax = Vector2.zero;
            background.texture = texture;
            background.color = new Color(1f, 1f, 1f, .75f);
            background.raycastTarget = false;
            background.transform.SetAsFirstSibling();
        }
        else Debug.LogWarning("RouteIQ background missing: place BoardBackground.png in Assets/Resources/RouteIQ.");

        var headerObject = new GameObject("RouteIQBoardHeader", typeof(RectTransform), typeof(Image));
        boardHeader = headerObject.GetComponent<RectTransform>();
        boardHeader.SetParent(puzzleArea, false);
        boardHeader.anchorMin = new Vector2(0f, 1f);
        boardHeader.anchorMax = Vector2.one;
        boardHeader.pivot = new Vector2(.5f, 1f);
        boardHeader.anchoredPosition = Vector2.zero;
        var headerImage = headerObject.GetComponent<Image>();
        headerImage.color = new Color(1f, 1f, 1f, .8f);
        headerImage.raycastTarget = false;

        boardLegend = CreateBoardText("RouteLegend", new Vector2(.34f, 0f), Vector2.one);
        boardLegend.alignment = TextAlignmentOptions.MidlineRight;
        boardLegend.OnPreRenderText += ResizeRouteLegendBars;
        UpdateRouteDistances();
        boardError = CreateBoardText("CompletionError", Vector2.zero, new Vector2(.33f, 1f));
        boardError.alignment = TextAlignmentOptions.MidlineLeft;
        boardError.fontStyle = FontStyles.Bold;
        boardError.text = completionError;
        UpdateBoardPresentation();
    }

    private TMP_Text CreateBoardText(string name, Vector2 anchorMin, Vector2 anchorMax)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.rectTransform.SetParent(boardHeader, false);
        text.rectTransform.anchorMin = anchorMin;
        text.rectTransform.anchorMax = anchorMax;
        TMP_Text sourceLabel = nodeButtonPrefab.GetComponentInChildren<TMP_Text>(true);
        if (sourceLabel != null && sourceLabel.font != null) text.font = sourceLabel.font;
        text.color = new Color(.12f, .13f, .2f, 1f);
        text.richText = true;
        text.raycastTarget = false;
        text.enableAutoSizing = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private void UpdateBoardPresentation()
    {
        if (boardHeader == null) return;
        float width = puzzleArea.rect.width;
        float inset = width * .024f;
        boardHeader.sizeDelta = new Vector2(0f, HeaderHeight);
        foreach (TMP_Text text in new[] { boardLegend, boardError })
        {
            text.rectTransform.offsetMin = new Vector2(inset, 0f);
            text.rectTransform.offsetMax = new Vector2(-inset, 0f);
            text.fontSizeMax = width * .04f;
            text.fontSizeMin = width * .032f;
        }
    }

    private void LateUpdate()
    {
        if (puzzleArea != null && displayedPuzzle != null && lastBoardSize != puzzleArea.rect.size)
        {
            PositionNodes();
            LayoutChanged?.Invoke();
        }
    }

    private Vector2 PositionFor(TspNodeData node)
    {
        // One scale for both axes preserves the Euclidean distances used by scoring.
        float padding = Mathf.Min(Mathf.Max(horizontalPadding, verticalPadding),
            Mathf.Min(puzzleArea.rect.width, puzzleArea.rect.height) * .08f);
        // Reserve room above each route point for the teardrop marker.
        padding = Mathf.Max(padding, 54f);
        // Reserve a header above the nodes; retain ONE uniform scale so the
        // visible route geometry agrees with the route-length calculation.
        float span = Mathf.Max(1f, Mathf.Min(puzzleArea.rect.width,
            puzzleArea.rect.height - HeaderHeight) - 2f * padding);
        return new Vector2((node.x / 100f - .5f) * span,
            (node.y / 100f - .5f) * span - HeaderHeight * .5f);
    }

    private void PositionNodes()
    {
        lastBoardSize = puzzleArea.rect.size;
        UpdateBoardPresentation();
        for (int i = 0; i < nodeTransforms.Count; i++)
            nodeTransforms[i].anchoredPosition = PositionFor(displayedPuzzle.nodes[i]);
        ArrangeNodePins();
    }

    private readonly List<RectTransform> nodeTransforms = new();
    private readonly HashSet<int> selectedNodes = new();

    private IEnumerator Start()
    {
        while (puzzleLoader != null &&
               !puzzleLoader.IsReady)
        {
            yield return null;
        }

        if (puzzleLoader == null)
        {
            Debug.LogError(
                "Puzzle Loader has not been assigned."
            );
            yield break;
        }

        if (puzzleArea == null)
        {
            Debug.LogError(
                "Puzzle Area has not been assigned."
            );
            yield break;
        }

        if (nodeButtonPrefab == null)
        {
            Debug.LogError(
                "Node Button Prefab has not been assigned."
            );
            yield break;
        }

        CreateBoardPresentation();
        ConfigurePuzzleAreaInput();
        RefreshPuzzle();
    }

    private void ConfigurePuzzleAreaInput()
    {
        Graphic inputGraphic = puzzleArea.GetComponent<Graphic>();

        if (inputGraphic == null)
        {
            Image transparentImage =
                puzzleArea.gameObject.AddComponent<Image>();

            transparentImage.color = Color.clear;
            inputGraphic = transparentImage;
        }

        inputGraphic.raycastTarget = true;

        EventTrigger trigger =
            puzzleArea.GetComponent<EventTrigger>();

        if (trigger == null)
            trigger = puzzleArea.gameObject.AddComponent<EventTrigger>();

        if (trigger.triggers == null)
            trigger.triggers = new List<EventTrigger.Entry>();

        trigger.triggers.RemoveAll(
            entry => entry.eventID == EventTriggerType.PointerDown
        );

        EventTrigger.Entry pointerDownEntry =
            new EventTrigger.Entry
            {
                eventID = EventTriggerType.PointerDown
            };

        pointerDownEntry.callback.AddListener(
            HandlePuzzleAreaPointerDown
        );

        trigger.triggers.Add(pointerDownEntry);
    }

    private void HandlePuzzleAreaPointerDown(
        BaseEventData eventData)
    {
        if (!selectionEnabled ||
            nodeTransforms.Count == 0)
        {
            return;
        }

        PointerEventData pointerData =
            eventData as PointerEventData;

        if (pointerData == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                puzzleArea,
                pointerData.position,
                pointerData.pressEventCamera,
                out Vector2 tapPosition))
        {
            return;
        }

        int closestNodeIndex = -1;
        Canvas canvas = puzzleArea.GetComponentInParent<Canvas>();
        float radius = Mathf.Max(touchRadius, 22f / (canvas != null ? canvas.scaleFactor : 1f));
        float closestDistanceSquared = radius * radius;

        for (int i = 0; i < nodeTransforms.Count; i++)
        {
            if (!IsNodeAvailable(i))
                continue;

            float distanceSquared =
                (nodeTransforms[i].anchoredPosition - tapPosition)
                .sqrMagnitude;

            if (distanceSquared <= closestDistanceSquared)
            {
                closestDistanceSquared = distanceSquared;
                closestNodeIndex = i;
            }
        }

        if (closestNodeIndex >= 0)
            NodeSelected?.Invoke(closestNodeIndex);
    }

    private bool IsNodeAvailable(int nodeIndex)
    {
        if (!selectedNodes.Contains(nodeIndex))
            return true;

        // A becomes selectable again only after every node is used.
        return nodeIndex == 0 &&
               selectedNodes.Count == nodeTransforms.Count;
    }

    public void RefreshPuzzle()
    {
        // Start creates the board presentation once, even for an empty filter.
        if (boardHeader == null) return;
        selectionEnabled = false;
        SetCompletionError(null);
        selectedNodes.Clear();

        foreach (RectTransform nodeTransform in nodeTransforms)
        {
            if (nodeTransform != null)
                Destroy(nodeTransform.gameObject);
        }

        nodeTransforms.Clear();

        if (puzzleLoader.CurrentPuzzle == null)
        {
            displayedPuzzle = null;
            currentRouteDistance = optimalRouteDistance = 0f;
            UpdateRouteDistances();
            return;
        }

        DisplayPuzzle(puzzleLoader.CurrentPuzzle);
    }

    private void DisplayPuzzle(TspPuzzleData puzzle)
    {
        if (puzzle == null ||
            puzzle.nodes == null ||
            puzzle.nodes.Count == 0)
        {
            Debug.LogError(
                "The selected puzzle contains no nodes."
            );
            return;
        }

        Canvas.ForceUpdateCanvases();

        displayedPuzzle = puzzle;
        ResetRouteDistances();
        lastBoardSize = puzzleArea.rect.size;
        UpdateBoardPresentation();

        for (int i = 0; i < puzzle.nodes.Count; i++)
        {
            TspNodeData node = puzzle.nodes[i];

            Button nodeButton =
                Instantiate(nodeButtonPrefab, puzzleArea);

            nodeButton.name = $"Node_{i}";

            nodeButton.interactable = true;

            RectTransform nodeTransform = nodeButton.GetComponent<RectTransform>();
            nodeTransform.anchorMin = nodeTransform.anchorMax = new Vector2(.5f, .5f);
            nodeTransform.pivot = new Vector2(.5f, .5f);
            nodeTransform.sizeDelta = new Vector2(26f, 26f);
            nodeTransform.anchoredPosition = PositionFor(node);

            nodeTransforms.Add(nodeTransform);
            CreateNodePin(nodeButton, i);

            // The puzzle area handles taps so overlapping node hit areas
            // can be resolved by distance and availability.
            foreach (Graphic graphic in
                     nodeButton.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            TMP_Text label =
                nodeButton.transform
                    .Find("NodeLabel")
                    .GetComponent<TMP_Text>();

            label.text = ((char)('A' + i)).ToString();
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(.5f, .5f);
            label.rectTransform.pivot = new Vector2(.5f, .5f);
            label.rectTransform.localScale = Vector3.one;
            label.rectTransform.anchoredPosition = new Vector2(0f, 17f);
            label.rectTransform.sizeDelta = new Vector2(13f, 13f);
            label.margin = Vector4.zero;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = false;
            label.fontSize = Mathf.Clamp(nodeLabelFontSize * .75f, 14f, 19f) * .5f;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.transform.SetAsLastSibling();

        }

        ArrangeNodePins();

        Debug.Log(
            $"Displayed {puzzle.nodes.Count} puzzle nodes."
        );
    }

    // Candidate order keeps pins above their dots whenever there is room.
    private static Vector2 PinDirection(int orientation)
    {
        switch (orientation)
        {
            case 1: return Vector2.left;  // 90 degrees
            case 2: return Vector2.down;  // 180 degrees
            case 3: return Vector2.right; // 270 degrees
            default: return Vector2.up;
        }
    }

    private Rect PinBounds(int node, int orientation)
    {
        Vector2 center = nodeTransforms[node].anchoredPosition + PinDirection(orientation) * 16f;
        // Conservative bounds include a one-unit clearance around the outline.
        Vector2 size = orientation % 2 == 0 ? new Vector2(19f, 22f) : new Vector2(22f, 19f);
        return new Rect(center - size * .5f, size);
    }

    private float PinObstruction(int node, Rect bounds)
    {
        float penalty = 0f;
        Rect board = puzzleArea.rect;
        if (bounds.xMin < board.xMin || bounds.xMax > board.xMax ||
            bounds.yMin < board.yMin || bounds.yMax > board.yMax - HeaderHeight)
            penalty += 10000f;
        for (int other = 0; other < nodeTransforms.Count; other++)
        {
            if (other == node) continue;
            Vector2 dot = nodeTransforms[other].anchoredPosition;
            // Ten-unit dots plus two units of clearance on each side.
            if (bounds.Overlaps(new Rect(dot - Vector2.one * 7f, Vector2.one * 14f)))
                penalty += 1000f;
        }
        return penalty;
    }

    private void ArrangeNodePins()
    {
        int count = nodeTransforms.Count;
        if (count == 0) return;
        var order = new List<int>();
        for (int i = 0; i < count; i++) order.Add(i);
        // Place upper nodes first; prefer rotating the lower pin in a conflict.
        order.Sort((a, b) =>
        {
            int comparison = nodeTransforms[b].anchoredPosition.y.CompareTo(nodeTransforms[a].anchoredPosition.y);
            return comparison != 0 ? comparison : a.CompareTo(b);
        });
        var bounds = new Rect[count, 4];
        var obstruction = new float[count, 4];
        var chosen = new int[count];
        for (int i = 0; i < count; i++)
        {
            chosen[i] = -1;
            for (int direction = 0; direction < 4; direction++)
            {
                bounds[i, direction] = PinBounds(i, direction);
                obstruction[i, direction] = PinObstruction(i, bounds[i, direction]);
            }
        }
        // Bounded backtracking avoids a greedy placement trapping a later node.
        int budget = 50000;
        if (!TryArrangePins(0, order, bounds, obstruction, chosen, ref budget))
        {
            // Extremely dense puzzles may have no solution in four directions.
            // Prefer dot visibility, then the fewest marker overlaps.
            for (int position = 0; position < count; position++)
            {
                int node = order[position];
                float best = float.MaxValue;
                for (int direction = 0; direction < 4; direction++)
                {
                    float score = obstruction[node, direction];
                    for (int previous = 0; previous < position; previous++)
                    {
                        int other = order[previous];
                        if (bounds[node, direction].Overlaps(bounds[other, chosen[other]])) score += 1f;
                    }
                    if (score < best) { best = score; chosen[node] = direction; }
                }
            }
        }
        for (int i = 0; i < count; i++) ApplyPinOrientation(i, chosen[i]);
    }

    private bool TryArrangePins(int position, List<int> order, Rect[,] bounds,
        float[,] obstruction, int[] chosen, ref int budget)
    {
        if (position == order.Count) return true;
        if (--budget < 0) return false;
        int node = order[position];
        for (int direction = 0; direction < 4; direction++)
        {
            if (obstruction[node, direction] > 0f) continue;
            bool overlaps = false;
            for (int previous = 0; previous < position; previous++)
            {
                int other = order[previous];
                if (bounds[node, direction].Overlaps(bounds[other, chosen[other]]))
                { overlaps = true; break; }
            }
            if (overlaps) continue;
            chosen[node] = direction;
            if (TryArrangePins(position + 1, order, bounds, obstruction, chosen, ref budget)) return true;
        }
        chosen[node] = -1;
        return false;
    }

    private void ApplyPinOrientation(int node, int orientation)
    {
        Vector2 direction = PinDirection(orientation);
        foreach (string name in new[] { "PinOutline", "PinFill" })
        {
            RectTransform pin = nodeTransforms[node].Find(name) as RectTransform;
            if (pin == null) continue;
            pin.anchoredPosition = direction * 6f;
            pin.localRotation = Quaternion.Euler(0f, 0f, orientation * 90f);
        }
        RectTransform label = nodeTransforms[node].Find("NodeLabel") as RectTransform;
        if (label != null)
        {
            label.anchoredPosition = direction * 17f;
            label.localRotation = Quaternion.identity;
        }
    }

    private Sprite nodePinSprite;
    private Texture2D nodePinTexture;

    private void CreateNodePin(Button button, int index)
    {
        // Keep the root at the original route point. Only the artwork rises
        // above it, so GetNodePosition and nearest-node touch input stay aligned.
        Transform dot = button.transform.Find("NodeDot");
        if (dot != null)
        {
            dot.gameObject.SetActive(true);
            CenterNodeDot(button.GetComponent<RectTransform>(), index);
        }
        Image oldImage = button.GetComponent<Image>();
        if (oldImage != null) oldImage.enabled = false;
        button.transition = Selectable.Transition.None;
        if (nodePinSprite == null) CreatePinSprite();

        AddPinImage(button.transform, "PinOutline", new Vector2(17f, 20f),
            new Color(.83f, .86f, .91f));
        AddPinImage(button.transform, "PinFill", new Vector2(15f, 18f),
            index == 0 ? new Color(.85f, .035f, .06f) : new Color(.045f, .055f, .075f));
    }

    private void AddPinImage(Transform parent, string name, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var image = go.GetComponent<Image>();
        RectTransform rect = image.rectTransform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 6f);
        rect.sizeDelta = size;
        image.sprite = nodePinSprite;
        image.color = color;
        image.raycastTarget = false;
    }

    private static void CenterNodeDot(RectTransform nodeTransform, int nodeIndex)
    {
        Transform dotTransform = nodeTransform.Find("NodeDot");
        if (dotTransform == null) return;

        TMP_Text dot = dotTransform.GetComponent<TMP_Text>();
        if (dot == null) return;

        // The prefab stretches NodeDot with -50 sizeDelta. With a 26-unit
        // button this produces a negative rectangle and offsets the bullet.
        RectTransform rect = dot.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        rect.sizeDelta = new Vector2(64f, 64f);
        rect.anchoredPosition = Vector2.zero;
        dot.text = "\u25CF";
        dot.fontSize = 32f;
        dot.color = nodeIndex == 0 ? Color.red : Color.black;
        dot.margin = Vector4.zero;
        dot.alignment = TextAlignmentOptions.Center;
        dot.enableAutoSizing = false;
        dot.textWrappingMode = TextWrappingModes.NoWrap;
        dot.raycastTarget = false;

        // Text alignment centers the font's line metrics, not necessarily the
        // visible bullet. Align its actual glyph geometry with the node origin.
        dot.ForceMeshUpdate();
        for (int i = 0; i < dot.textInfo.characterCount; i++)
        {
            TMP_CharacterInfo glyph = dot.textInfo.characterInfo[i];
            if (!glyph.isVisible) continue;
            Vector3 glyphCenter = (glyph.bottomLeft + glyph.topRight) * .5f;
            Vector3 glyphSize = glyph.topRight - glyph.bottomLeft;
            float scale = 10f / Mathf.Max(.001f, Mathf.Max(glyphSize.x, glyphSize.y));
            rect.localScale = Vector3.one * scale;
            rect.anchoredPosition = new Vector2(-glyphCenter.x, -glyphCenter.y) * scale;
            break;
        }
    }

    private void CreatePinSprite()
    {
        // A smooth map-pin silhouette built in code; no imported sprite needed.
        const int width = 128, height = 160, arcSteps = 64;
        var outline = new List<Vector2> { new Vector2(.5f, 0f) };
        for (int i = 0; i <= arcSteps; i++)
        {
            float angle = Mathf.Lerp(-40f, 220f, i / (float)arcSteps) * Mathf.Deg2Rad;
            outline.Add(new Vector2(.5f + .48f * Mathf.Cos(angle),
                .62f + .37f * Mathf.Sin(angle)));
        }
        var pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            Vector2 point = new Vector2((x + .5f) / width, (y + .5f) / height);
            bool inside = false;
            float distance = float.MaxValue;
            for (int i = 0, j = outline.Count - 1; i < outline.Count; j = i++)
            {
                Vector2 a = outline[j], b = outline[i];
                if ((a.y > point.y) != (b.y > point.y) &&
                    point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
                Vector2 edge = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(point - a, edge) / edge.sqrMagnitude);
                distance = Mathf.Min(distance, Vector2.Distance(point, a + edge * t));
            }
            float alpha = Mathf.Clamp01(.5f + (inside ? distance : -distance) * width);
            pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
        }
        nodePinTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        nodePinTexture.name = "RouteIQ Teardrop";
        nodePinTexture.wrapMode = TextureWrapMode.Clamp;
        nodePinTexture.filterMode = FilterMode.Bilinear;
        nodePinTexture.SetPixels(pixels);
        nodePinTexture.Apply(false, true);
        nodePinSprite = Sprite.Create(nodePinTexture, new Rect(0, 0, width, height),
            new Vector2(.5f, 0f), 100f);
    }

    private void OnDestroy()
    {
        if (nodePinSprite != null) Destroy(nodePinSprite);
        if (nodePinTexture != null) Destroy(nodePinTexture);
    }

    public void SetSelectionEnabled(bool enabled)
    {
        selectionEnabled = enabled;
    }

    public void SetSelectedPath(IEnumerable<int> selectedPath)
    {
        selectedNodes.Clear();
        currentRouteDistance = 0f;

        if (selectedPath != null)
        {
            int previous = -1;
            foreach (int nodeIndex in selectedPath)
            {
                selectedNodes.Add(nodeIndex);
                // Count only the segments actually selected. The return leg
                // is included only when the player selects A to close the loop.
                if (displayedPuzzle != null && previous >= 0)
                    currentRouteDistance += NodeDistance(previous, nodeIndex);
                previous = nodeIndex;
            }
        }
        UpdateRouteDistances();
    }

    public Vector2 GetNodePosition(int nodeIndex)
    {
        if (nodeIndex < 0 ||
            nodeIndex >= nodeTransforms.Count)
        {
            return Vector2.zero;
        }

        return nodeTransforms[nodeIndex]
            .anchoredPosition;
    }
}

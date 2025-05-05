using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.IO;

public class ClosestEdibleDetector : MonoBehaviour
{
    public Camera mainCamera;
    [Range(0f, 0.5f)] public float viewportMargin = 0.05f;
    [Range(0f, 1f)] public float iouThreshold = 0.1f;
    public int imageWidth = 1920;
    public int imageHeight = 1080;
    public int numObjectsToDetect = 5;
    private int frameIndex = 1;

    private readonly List<string> edibleKeywords = new List<string>
    {
        "chips", "snack", "drink", "soda", "bento", "candy", "chocolate",
        "cupnoodles", "cereal", "cookie", "onigiri", "icecream", "sandwich", "can", "bottle"
    };

    private List<Rect> debugBoxes = new List<Rect>();
    private List<string> debugLabels = new List<string>();

    void Start()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        FindAndExportClosestEdibles();
    }

    void FindAndExportClosestEdibles()
    {
        debugBoxes.Clear();
        debugLabels.Clear();

        MeshRenderer[] allRenderers = GameObject.FindObjectsOfType<MeshRenderer>();
        List<(GameObject go, Bounds bounds, string label, float distance)> edibleObjects = new List<(GameObject, Bounds, string, float)>();

        foreach (MeshRenderer renderer in allRenderers)
        {
            GameObject go = renderer.gameObject;
            string nameLower = go.name.ToLower();
            string match = edibleKeywords.FirstOrDefault(k => nameLower.Contains(k));
            if (match == null) continue;

            Vector3 viewportPoint = mainCamera.WorldToViewportPoint(renderer.bounds.center);
            if (viewportPoint.z <= 0) continue;
            if (viewportPoint.x < viewportMargin || viewportPoint.x > 1 - viewportMargin) continue;
            if (viewportPoint.y < viewportMargin || viewportPoint.y > 1 - viewportMargin) continue;

            float distance = Vector3.Distance(mainCamera.transform.position, renderer.bounds.center);
            edibleObjects.Add((go, renderer.bounds, match, distance));
        }

        if (edibleObjects.Count == 0)
        {
            Debug.Log("⚠️ No visible edible objects in camera view.");
            return;
        }

        var sortedObjects = edibleObjects.OrderBy(e => e.distance).ToList();
        List<string> labelLines = new List<string>();
        List<Rect> savedBoxes = new List<Rect>();

        int accepted = 0;
        int index = 0;

        // First: non-overlapping labels
        while (accepted < numObjectsToDetect && index < sortedObjects.Count)
        {
            var obj = sortedObjects[index];
            index++;

            Rect box = GetViewportRect(obj.bounds);
            bool overlaps = savedBoxes.Any(existingBox => ComputeIoU(existingBox, box) > iouThreshold);
            if (overlaps) continue;

            AddLabel(obj, box, labelLines, savedBoxes);
            accepted++;
        }

        // Second: fill rest with overlapping boxes (avoid exact duplicates)
        int secondPassIndex = 0;
        while (accepted < numObjectsToDetect && secondPassIndex < sortedObjects.Count)
        {
            var obj = sortedObjects[secondPassIndex];
            secondPassIndex++;

            Rect box = GetViewportRect(obj.bounds);
            bool isDuplicate = savedBoxes.Any(existingBox => ComputeIoU(existingBox, box) >= 0.99f);
            if (isDuplicate) continue;

            AddLabel(obj, box, labelLines, savedBoxes);
            accepted++;
        }

        Debug.Log($"🔎 Total objects labeled this frame: {accepted}");

        if (labelLines.Count == 0)
        {
            Debug.Log("⚠️ No labelable objects found.");
            return;
        }

        // Save label file and image
        string datasetPath = Path.Combine(Application.dataPath, "Dataset");
        if (!Directory.Exists(datasetPath))
            Directory.CreateDirectory(datasetPath);

        string filename = $"frame_{frameIndex:D4}";
        string labelPath = Path.Combine(datasetPath, filename + ".txt");
        string imagePath = Path.Combine(datasetPath, filename + ".png");

        File.WriteAllLines(labelPath, labelLines);
        Debug.Log($"📄 Saved {labelLines.Count} labels to: {labelPath}");

        RenderTexture rt = new RenderTexture(imageWidth, imageHeight, 24);
        mainCamera.targetTexture = rt;
        Texture2D screenShot = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
        mainCamera.Render();
        RenderTexture.active = rt;
        screenShot.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
        mainCamera.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);
        byte[] bytes = screenShot.EncodeToPNG();
        File.WriteAllBytes(imagePath, bytes);
        Debug.Log($"🖼️ Saved image: {imagePath}");

        frameIndex++;
    }

    Rect GetViewportRect(Bounds b)
    {
        Vector3[] corners = new Vector3[8];
        corners[0] = mainCamera.WorldToViewportPoint(b.min);
        corners[1] = mainCamera.WorldToViewportPoint(new Vector3(b.max.x, b.min.y, b.min.z));
        corners[2] = mainCamera.WorldToViewportPoint(new Vector3(b.min.x, b.max.y, b.min.z));
        corners[3] = mainCamera.WorldToViewportPoint(new Vector3(b.min.x, b.min.y, b.max.z));
        corners[4] = mainCamera.WorldToViewportPoint(new Vector3(b.max.x, b.max.y, b.min.z));
        corners[5] = mainCamera.WorldToViewportPoint(new Vector3(b.max.x, b.min.y, b.max.z));
        corners[6] = mainCamera.WorldToViewportPoint(new Vector3(b.min.x, b.max.y, b.max.z));
        corners[7] = mainCamera.WorldToViewportPoint(b.max);

        float minX = corners.Min(v => v.x);
        float maxX = corners.Max(v => v.x);
        float minY = corners.Min(v => v.y);
        float maxY = corners.Max(v => v.y);

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    void AddLabel((GameObject go, Bounds bounds, string label, float distance) obj, Rect box, List<string> labelLines, List<Rect> savedBoxes)
    {
        float xCenter = box.center.x;
        float yCenter = 1f - box.center.y;
        float width = box.width;
        float height = box.height;

        int classId = edibleKeywords.IndexOf(obj.label);
        string labelLine = $"{classId} {xCenter:F6} {yCenter:F6} {width:F6} {height:F6}";
        labelLines.Add(labelLine);
        savedBoxes.Add(box);

        // Add for visual debugging
        float screenX = box.xMin * imageWidth;
        float screenY = (1f - box.yMax) * imageHeight;
        float screenW = box.width * imageWidth;
        float screenH = box.height * imageHeight;
        debugBoxes.Add(new Rect(screenX, screenY, screenW, screenH));
        debugLabels.Add(obj.label);

        Debug.Log($"✅ Added: {obj.go.name} ({obj.label})");
    }

    float ComputeIoU(Rect a, Rect b)
    {
        float xMin = Mathf.Max(a.xMin, b.xMin);
        float yMin = Mathf.Max(a.yMin, b.yMin);
        float xMax = Mathf.Min(a.xMax, b.xMax);
        float yMax = Mathf.Min(a.yMax, b.yMax);

        float intersection = Mathf.Max(0, xMax - xMin) * Mathf.Max(0, yMax - yMin);
        float union = a.width * a.height + b.width * b.height - intersection;

        return union > 0 ? intersection / union : 0;
    }

    void OnGUI()
    {
        GUIStyle boxStyle = new GUIStyle(GUI.skin.box);
        boxStyle.normal.textColor = Color.green;
        boxStyle.alignment = TextAnchor.UpperLeft;
        boxStyle.fontSize = 14;

        for (int i = 0; i < debugBoxes.Count; i++)
        {
            GUI.Box(debugBoxes[i], debugLabels[i], boxStyle);
        }
    }
}

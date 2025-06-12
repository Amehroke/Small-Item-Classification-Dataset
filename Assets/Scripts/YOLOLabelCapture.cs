using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.IO;

public class ClosestEdibleDetector : MonoBehaviour
{
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
        foreach (Camera cam in Camera.allCameras)
        {
            FindAndExportClosestEdibles(cam);
        }
    }

    void FindAndExportClosestEdibles(Camera cam)
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

            Vector3 viewportPoint = cam.WorldToViewportPoint(renderer.bounds.center);
            if (viewportPoint.z <= 0) continue;
            if (viewportPoint.x < viewportMargin || viewportPoint.x > 1 - viewportMargin) continue;
            if (viewportPoint.y < viewportMargin || viewportPoint.y > 1 - viewportMargin) continue;

            float distance = Vector3.Distance(cam.transform.position, renderer.bounds.center);
            edibleObjects.Add((go, renderer.bounds, match, distance));
        }

        if (edibleObjects.Count == 0)
        {
            Debug.Log($"⚠️ No visible edible objects in view of {cam.name}.");
            return;
        }

        var sortedObjects = edibleObjects.OrderBy(e => e.distance).ToList();
        List<string> labelLines = new List<string>();
        List<Rect> savedBoxes = new List<Rect>();

        int accepted = 0;
        int index = 0;

        while (accepted < numObjectsToDetect && index < sortedObjects.Count)
        {
            var obj = sortedObjects[index];
            index++;

            Rect box = GetViewportRect(cam, obj.bounds);
            bool overlaps = savedBoxes.Any(existingBox => ComputeIoU(existingBox, box) > iouThreshold);
            if (overlaps) continue;

            AddLabel(obj, box, labelLines, savedBoxes);
            accepted++;
        }

        int secondPassIndex = 0;
        while (accepted < numObjectsToDetect && secondPassIndex < sortedObjects.Count)
        {
            var obj = sortedObjects[secondPassIndex];
            secondPassIndex++;

            Rect box = GetViewportRect(cam, obj.bounds);
            bool isDuplicate = savedBoxes.Any(existingBox => ComputeIoU(existingBox, box) >= 0.99f);
            if (isDuplicate) continue;

            AddLabel(obj, box, labelLines, savedBoxes);
            accepted++;
        }

        Debug.Log($"🔎 [{cam.name}] Labeled objects: {accepted}");

        if (labelLines.Count == 0)
        {
            Debug.Log($"⚠️ No labelable objects for camera {cam.name}");
            return;
        }

        string datasetPath = Path.Combine(Application.dataPath, "Dataset");
        if (!Directory.Exists(datasetPath))
            Directory.CreateDirectory(datasetPath);

        string cameraName = cam.name.Replace(" ", "_");
        string filename = $"frame_{frameIndex:D4}_{cameraName}";
        string labelPath = Path.Combine(datasetPath, filename + ".txt");
        string imagePath = Path.Combine(datasetPath, filename + ".png");

        File.WriteAllLines(labelPath, labelLines);
        Debug.Log($"📄 [{cam.name}] Labels saved: {labelPath}");

        RenderTexture rt = new RenderTexture(imageWidth, imageHeight, 24);
        cam.targetTexture = rt;
        Texture2D screenShot = new Texture2D(imageWidth, imageHeight, TextureFormat.RGB24, false);
        cam.Render();
        RenderTexture.active = rt;
        screenShot.ReadPixels(new Rect(0, 0, imageWidth, imageHeight), 0, 0);
        cam.targetTexture = null;
        RenderTexture.active = null;
        Destroy(rt);
        byte[] bytes = screenShot.EncodeToPNG();
        File.WriteAllBytes(imagePath, bytes);
        Debug.Log($"🖼️ [{cam.name}] Image saved: {imagePath}");

        frameIndex++;
    }

    Rect GetViewportRect(Camera cam, Bounds b)
    {
        Vector3[] corners = new Vector3[8];
        corners[0] = cam.WorldToViewportPoint(b.min);
        corners[1] = cam.WorldToViewportPoint(new Vector3(b.max.x, b.min.y, b.min.z));
        corners[2] = cam.WorldToViewportPoint(new Vector3(b.min.x, b.max.y, b.min.z));
        corners[3] = cam.WorldToViewportPoint(new Vector3(b.min.x, b.min.y, b.max.z));
        corners[4] = cam.WorldToViewportPoint(new Vector3(b.max.x, b.max.y, b.min.z));
        corners[5] = cam.WorldToViewportPoint(new Vector3(b.max.x, b.min.y, b.max.z));
        corners[6] = cam.WorldToViewportPoint(new Vector3(b.min.x, b.max.y, b.max.z));
        corners[7] = cam.WorldToViewportPoint(b.max);

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

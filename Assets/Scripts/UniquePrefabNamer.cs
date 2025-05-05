using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class UniquePrefabNamer : MonoBehaviour
{
    [MenuItem("Tools/Rename Duplicate Prefabs Uniquely")]
    static void RenameDuplicatePrefabs()
    {
        GameObject[] allObjects = GameObject.FindObjectsOfType<GameObject>();
        Dictionary<string, int> nameCount = new Dictionary<string, int>();

        foreach (GameObject obj in allObjects)
        {
            if (PrefabUtility.GetPrefabAssetType(obj) == PrefabAssetType.NotAPrefab)
                continue; // skip non-prefab objects

            string baseName = obj.name;

            if (!nameCount.ContainsKey(baseName))
            {
                nameCount[baseName] = 1;
            }
            else
            {
                nameCount[baseName]++;
                string newName = baseName + "_" + nameCount[baseName];
                Undo.RecordObject(obj, "Rename Object");
                obj.name = newName;
            }
        }

        Debug.Log("Duplicate prefab names have been uniquely renamed.");
    }
}

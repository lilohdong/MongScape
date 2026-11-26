using UnityEditor;
using UnityEngine;

namespace Mongscape.EditorTools
{
    // One-off (v2 plan 4): removes the coin group from every chunk prefab.
    public static class MongscapeCoinRemoval
    {
        public static void Run()
        {
            int changed = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Content/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                var group = root.transform.Find("Collectibles");
                int missing = 0;
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) missing += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                if (group != null || missing > 0)
                {
                    if (group != null) Object.DestroyImmediate(group.gameObject);
                    PrefabUtility.SaveAsPrefabAsset(root, path); changed++;
                    Debug.Log("MONGSCAPE_COIN_REMOVAL " + path + " missing=" + missing);
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("MONGSCAPE_COIN_REMOVAL_DONE " + changed);
        }
    }
}

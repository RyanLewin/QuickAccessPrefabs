using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.Collections.Generic;

namespace QuickAccessPrefabs
{
    [ExecuteInEditMode]
    public class QuickAccessManager : ScriptableObject
    {
        private static List<GameObject> _lastPlacedObjects = new List<GameObject>();

        public static void AddObject(GameObject objToPlace)
        {
            var asset = (GameObject)PrefabUtility.InstantiatePrefab(objToPlace);
            asset.transform.position = SceneView.lastActiveSceneView.pivot;
            GameObject[] newSelection = new GameObject[1];
            newSelection[0] = asset;
            Selection.objects = newSelection;
            Undo.RegisterCreatedObjectUndo(asset, "QAP Creation");
        }

        public static List<GameObject> GetObjectsOfTypeInScene(Object prefab)
        {
            List<GameObject> result = new List<GameObject>();
            GameObject[] allObjects = (GameObject[])FindObjectsOfType(typeof(GameObject));
            foreach(GameObject GO in allObjects)
            {
                if (PrefabUtility.GetPrefabAssetType(GO) == PrefabAssetType.Regular)
                {
                    UnityEngine.Object GO_prefab = PrefabUtility.GetCorrespondingObjectFromSource(GO);
                    if (prefab == GO_prefab)
                        result.Add(GO);
                }
            }
            return result;
        }

        public static void SelectObjects(List<GameObject> objectsToSelect)
        {
            GameObject[] newSelection = new GameObject[objectsToSelect.Count];
            for(int i = 0; i < objectsToSelect.Count; i++)
            {
                newSelection[i] = objectsToSelect[i];
            }
            Selection.objects = newSelection;
        }

        public static void ReplaceObjects(GameObject objToPlace)
        {
            Debug.Log($"Replace {Selection.transforms.Length} assets with {objToPlace.name}.");

            Undo.IncrementCurrentGroup();
            Undo.SetCurrentGroupName("QAP Replace");
            int undoID = Undo.GetCurrentGroup();

            _lastPlacedObjects.Clear();
            foreach(var obj in Selection.transforms)
            {
                var createdAsset = (GameObject)PrefabUtility.InstantiatePrefab(objToPlace);
                createdAsset.transform.parent = obj.parent;
                if (QuickAccessWindow.matchOldPosition) createdAsset.transform.position = obj.position;
                if (QuickAccessWindow.matchOldRotation) createdAsset.transform.rotation = obj.rotation; 
                if (QuickAccessWindow.matchOldScale) createdAsset.transform.localScale = obj.localScale;
                createdAsset.transform.SetSiblingIndex(obj.GetSiblingIndex());
                _lastPlacedObjects.Add(createdAsset);

                Undo.RegisterCreatedObjectUndo(createdAsset, "");
                Undo.DestroyObjectImmediate(obj.gameObject);
            }
            Undo.CollapseUndoOperations(undoID);

            SelectObjects(_lastPlacedObjects);
        }

        public static void OpenScene(string sceneToOpen)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                var scenePath = AssetDatabase.LoadAssetAtPath(sceneToOpen, typeof(SceneAsset));
                EditorSceneManager.OpenScene(sceneToOpen);
                Debug.Log($"Open Scene: {scenePath}");
            }
        }
    }
}

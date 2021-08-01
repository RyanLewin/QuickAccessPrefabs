#if UNITY_EDITOR

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;

namespace QuickAccessPrefabs
{
    public class QuickAccessWindow : EditorWindow
    {
        [SerializeField] private SerializedObject _myQuickAccessTool;
        private Vector2 _scrollPosition = Vector2.zero;

        const string EditorPrefsKey = "QAPWindow";
        [SerializeField] private int _tab = 0;
        [Tooltip("Custom Path for the Scriptable Object, if left null will be put in default place: Assets/QuickAccessPrefabs/QuickAccessObject")]
        [SerializeField] private string _customScriptPath = "";
        [SerializeField] private int _capacity = 0;
        [SerializeField] private List<string> _listQuickAccessObjects = new List<string>();

        [SerializeField] private string _prefabToReplace;
        private Editor _prefabToReplaceEditor;
        [SerializeField] private string _prefabToReplaceWith;
        private Editor _prefabToReplaceWithEditor;

        [SerializeField] private bool _matchOldPosition = true;
        public static bool matchOldPosition;
        [SerializeField] private bool _matchOldRotation = true;
        public static bool matchOldRotation;
        [SerializeField] private bool _matchOldScale = true;
        public static bool matchOldScale;

        [Header("Scenes")]
        [SerializeField] private List<string> _listScenes = new List<string>(3) { "", "", "" };
        [SerializeField] private static List<string> _staticListScenes = new List<string>(3) { "", "", "" };

         // Add menu named "My Window" to the Window menu
        [MenuItem ("QAP/Open QAP Window %q")]
        static void Init () 
        {
            // Get existing open window or if none, make a new one:
            QuickAccessWindow window = (QuickAccessWindow)EditorWindow.GetWindow(typeof(QuickAccessWindow), true, "Quick Access Prefabs");
            window.Show();
        }

        [MenuItem("QAP/Open First Scene %&q")]
        private static void OpenFirstScene()
        {
            if (_staticListScenes[0] != "") QuickAccessManager.OpenScene(_staticListScenes[0]);
        }

        [MenuItem("QAP/Open Second Scene %&w")]
        private static void OpenSecondScene()
        {
            if (_staticListScenes[1] != "") QuickAccessManager.OpenScene(_staticListScenes[1]);
        }

        [MenuItem("QAP/Open Third Scene %&e")]
        private static void OpenThirdScene()
        {
            if (_staticListScenes[2] != "") QuickAccessManager.OpenScene(_staticListScenes[2]);
        }

        //Used to get the data that was in the Window's variables from last time
        private void OnEnable() {
            var data = EditorPrefs.GetString(EditorPrefsKey, JsonUtility.ToJson(this, false));
            JsonUtility.FromJsonOverwrite(data, this);
        }

        //Used to save the data in the variables marked with [SerializeField]
        private void OnDisable() {
            var data = JsonUtility.ToJson(this, false);
            EditorPrefs.SetString(EditorPrefsKey, data);
        }

        private void OnGUI() 
        {
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, false, true, GUILayout.Width(Screen.width), GUILayout.Height(Screen.height - 25));

            //Tabbed windows system
            _tab = GUILayout.Toolbar(_tab, new string[] {"QAP Menu", "Replacer", "Scene Selector"});
            switch(_tab)
            {
                case 0: //Window for the Quick Access Window
                    QuickPrefabs();
                    break;
                case 1: //Window for Replacing Prefabs In Scene
                    Replacer();
                    break;
                case 2: //Window for selecting the quick access scenes
                    SceneSelector();
                    break;
            }

            GUILayout.EndScrollView();
        }

        //Tab that adds objects to the Quick Access Menu
        private void QuickPrefabs()
        {
            GUILayout.Label("Customizables", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("Custom Path for the created script, if left null will be put in default place: \r\nAssets/QuickAccessPrefabs/QuickAccessObject.", EditorStyles.wordWrappedLabel);
            GUILayout.EndHorizontal();
            _customScriptPath = EditorGUILayout.TextField("Custom Path:", _customScriptPath);

            //Spacing
            GUILayout.Label("", EditorStyles.boldLabel);

            GUILayout.Label("Objects", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("Use the Add Capacity button to increment the capacity, or set the value manually.\r\n" +
                                "Fill the new slot(s) with any prefabs from your project you want Quick Access to.\r\n" +
                                "Use the 'X' button to remove a specific prefab from the list.\r\n" +
                                "Use the Apply button to apply changes you have made.", EditorStyles.wordWrappedLabel);
            GUILayout.EndHorizontal();
            _capacity = EditorGUILayout.IntField("Capacity", _capacity);
            _capacity = Mathf.Max(0, _capacity);
 
            List<Object> objects = GetAssets<Object>(_listQuickAccessObjects);
            //add any new fields
            for (int m = objects.Count; m < _capacity; m++)
                objects.Add(null);
 
            //remove extra fields - if capacity is reduced, removes from end of list
            for (int m = _capacity; m <  objects.Count; m++)
                objects.RemoveAt(objects.Count-1);

            for (int m = 0; m < objects.Count; m++) 
            {
                Rect r = EditorGUILayout.BeginHorizontal();
                //display the field
                objects[m] = (EditorGUILayout.ObjectField("Object #"+(m+1),objects[m], typeof(GameObject), false, GUILayout.Width(300))) as GameObject;
    
                // delete a field
                if (GUI.Button(new Rect(r.width - 20,r.y,r.height,20), "X"))
                {
                    objects.RemoveAt(m);
                    m--;
                    _capacity--;
                    continue;
                }
                EditorGUILayout.EndHorizontal();
            }
            _listQuickAccessObjects = AssetsToStrings(objects);
            
            //Spacing
            GUILayout.Label("", EditorStyles.boldLabel);

            float buttonWidth = 150;

            //Add button to increment capacity
            GUILayout.BeginHorizontal();
            GUILayout.Space(Screen.width / 2 - buttonWidth / 2);
            if (GUILayout.Button("Add Capacity", GUILayout.Width(buttonWidth)))
            {
                _capacity++;
            }
            GUILayout.EndHorizontal();
            
            //Add button that will update the script
            GUILayout.BeginHorizontal();
            GUILayout.Space(Screen.width / 2 - buttonWidth / 2);
            if (GUILayout.Button("Apply", GUILayout.Width(buttonWidth)))
            {
                CreateScript();
            }
            GUILayout.EndHorizontal();
        } 

        private void Replacer()
        {
            GUILayout.Label("Replacer", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("Set the first object to select all objects of prefab type in the scene, you can also click the " +
                            "Select All Objects of this Type to reselect them.\r\n" +
                            "Then set what the selected objects are replaced with and click the 'Replace Selected Objects' button", EditorStyles.wordWrappedLabel);
            GUILayout.EndHorizontal();

            //Set object to select and replace
            GUILayout.Label("Objects To Replace", EditorStyles.boldLabel);
            var prefabToReplace = AssetDatabase.LoadAssetAtPath(_prefabToReplace, typeof(GameObject));
            var newPrefabToReplace = (EditorGUILayout.ObjectField("Select this Object:", prefabToReplace, typeof(GameObject), false, GUILayout.Width(300))) as GameObject;
            
            //If the first object is changed then set to refresh the preview and update the selected objects 
            bool createNewEditorPreview = false; 
            if (newPrefabToReplace != prefabToReplace)
            {
                createNewEditorPreview = true;
                prefabToReplace = newPrefabToReplace;
                SelectObjects();
            }
            
            //Set the background colour of the object previews based on dark mode or not
            GUIStyle bgColour = new GUIStyle();
            if (EditorGUIUtility.isProSkin)
                bgColour.normal.background = Texture2D.grayTexture;
            else
                bgColour.normal.background = EditorGUIUtility.whiteTexture;

            if (prefabToReplace != null)
            {
                if (!_prefabToReplaceEditor || createNewEditorPreview)
                    _prefabToReplaceEditor = Editor.CreateEditor(prefabToReplace);
                _prefabToReplaceEditor.OnPreviewGUI(GUILayoutUtility.GetRect(128, 128), bgColour);
                // prefabToReplaceEditor.OnInteractivePreviewGUI(GUILayoutUtility.GetRect(128, 128), bgColour);
            }
            _prefabToReplace = AssetDatabase.GetAssetPath(prefabToReplace);

            if (GUILayout.Button("Select All Objects of this Type"))
            {
                SelectObjects();
            }
            GUILayout.Label($"There are {Selection.transforms.Length} objects to replace");
            GUILayout.Label("");

            _matchOldPosition = EditorGUILayout.Toggle("Match Old Position: ", _matchOldPosition);
            matchOldPosition = _matchOldPosition;
            _matchOldRotation = EditorGUILayout.Toggle("Match Old Rotation: ", _matchOldRotation);
            matchOldRotation = _matchOldRotation;
            _matchOldScale = EditorGUILayout.Toggle("Match Old Scale: ", _matchOldScale);
            matchOldScale = _matchOldScale;

            //Spacing
            GUILayout.Label("");

            GUILayout.Label("Replace With", EditorStyles.boldLabel);
            var prefabToReplaceWith = (GameObject)AssetDatabase.LoadAssetAtPath(_prefabToReplaceWith, typeof(GameObject));
            var newPrefabToReplaceWith = (EditorGUILayout.ObjectField("Object To Replace With:", prefabToReplaceWith, typeof(GameObject), false, GUILayout.Width(300))) as GameObject;
            Debug.Log(newPrefabToReplaceWith);
            if (newPrefabToReplaceWith != null)
            {
                if (!_prefabToReplaceWithEditor || newPrefabToReplaceWith != prefabToReplaceWith)
                {
                    prefabToReplaceWith = newPrefabToReplaceWith;
                    _prefabToReplaceWithEditor = Editor.CreateEditor(prefabToReplaceWith);
                }

                if (prefabToReplaceWith != null)
                    _prefabToReplaceWithEditor.OnPreviewGUI(GUILayoutUtility.GetRect(128, 128), bgColour);
            }
            else
            {
                prefabToReplaceWith = null;
            }
            _prefabToReplaceWith = AssetDatabase.GetAssetPath(prefabToReplaceWith);

            if (GUILayout.Button("Replace Selected Objects"))
            {
                QuickAccessManager.ReplaceObjects(prefabToReplaceWith);
            }
        }

        private void SceneSelector()
        {
            GUILayout.Label("Scene Selector", EditorStyles.boldLabel);
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("Set the scenes you want to access from the quick access menu. Uses the shortkeys " +
                            "CTRL + Alt and then Q, W or E to load scenes 1, 2 and 3 respectively.", EditorStyles.wordWrappedLabel);
            GUILayout.EndHorizontal();

            List<Object> sceneAssets = GetAssets<Object>(_listScenes);
            sceneAssets[0] = (EditorGUILayout.ObjectField("First Quick Scene:", sceneAssets[0], typeof(SceneAsset), false, GUILayout.Width(300))) as Object;
            sceneAssets[1] = (EditorGUILayout.ObjectField("Second Quick Scene:", sceneAssets[1], typeof(SceneAsset), false, GUILayout.Width(300))) as Object;
            sceneAssets[2] = (EditorGUILayout.ObjectField("Third Quick Scene:", sceneAssets[2], typeof(SceneAsset), false, GUILayout.Width(300))) as Object;
            _listScenes = AssetsToStrings(sceneAssets);
            _staticListScenes = _listScenes;
        }

        private void SelectObjects()
        {
            var prefabToReplace = AssetDatabase.LoadAssetAtPath(_prefabToReplace, typeof(Object));
            QuickAccessManager.SelectObjects(QuickAccessManager.GetObjectsOfTypeInScene(prefabToReplace));
        }

        private List<T> GetAssets<T>(List<string> paths)
        {
            List<Object> result = new List<Object>();
            foreach(var path in paths)
                result.Add(AssetDatabase.LoadAssetAtPath(path, typeof(T)));
            return result as List<T>;
        }

        private List<string> AssetsToStrings(List<Object> objects)
        {
            List<string> paths = new List<string>();
            foreach(var obj in objects)
                paths.Add(AssetDatabase.GetAssetPath(obj));
            return paths;
        }

        private void CreateScript()
        {
            //Top part of script, before the methods
            string newBaseScript = 
                "using UnityEngine;\r\n" +
                "using UnityEditor;\r\n\r\n" +
                "namespace QuickAccessPrefabs\r\n" +
                "{\r\n" +
                "    [ExecuteInEditMode]\r\n" +
                "    public class QuickAccessCustomObjects\r\n" +
                "    {\r\n" +
                "        private static GameObject _objToPlace;\r\n";

            List<string> newMethods = new List<string>(); 
            List<string> methodNames = new List<string>();
            
            List<Object> objects = GetAssets<Object>(_listQuickAccessObjects);
            foreach(var obj in objects)
            {
                if (obj == null) continue;

                string path = AssetDatabase.GetAssetPath(obj);

                //Remove whitespace and characters that break function names
                Regex rgx = new Regex("[^a-zA-Z0-9]");
                string name = rgx.Replace(obj.name, "");

                //Check that the method doesn't already exist
                if (methodNames.Contains(name)) 
                {
                    Debug.LogError($"Two prefabs in CustomObjects List of prefabs have the name {obj.name} - Currently not supported");
                    continue;
                }
                
                //Add method to list
                methodNames.Add(name);
                newMethods.Add( 
                   $"        [MenuItem(\"GameObject/Create Other/{obj.name}\")]\r\n" +
                   $"        static void CreateNew{name}()\r\n" +
                    "        {\r\n" +
                   $"            _objToPlace = (GameObject)AssetDatabase.LoadAssetAtPath(\"{path}\", typeof(GameObject));\r\n" +
                    "            QuickAccessManager.AddObject(_objToPlace);\r\n" +
                    "        }\r\n"
                );

                newMethods.Add( 
                   $"        [MenuItem(\"GameObject/Create Other/Replace Selected/{obj.name}\")]\r\n" +
                   $"        static void ReplaceWith{name}()\r\n" +
                    "        {\r\n" +
                   $"            _objToPlace = (GameObject)AssetDatabase.LoadAssetAtPath(\"{path}\", typeof(GameObject));\r\n" +
                    "            QuickAccessManager.ReplaceObjects(_objToPlace);\r\n" +
                    "        }\r\n"
                );
            }

            //Removes new Line from last line of methods
            if (newMethods.Count > 0)
            {
                var lastMethod = newMethods[newMethods.Count - 1];
                newMethods[newMethods.Count - 1] = lastMethod.Substring(0, lastMethod.LastIndexOf("\r\n"));
            }

            //End of file
            string endOfFile =    
                "    }\r\n" +
                "}\r\n";

            //Add all strings together and add to file
            List<string> toWrite = new List<string>() {newBaseScript};
            foreach(var newMethod in newMethods)
                toWrite.Add(newMethod);
            toWrite.Add(endOfFile);

            string scriptPath = _customScriptPath == "" ? "Assets/QuickAccessPrefabs/QuickAccessCustomObjects" : _customScriptPath;
            scriptPath += ".cs";
            File.WriteAllLines(scriptPath, toWrite);

            //Refreshes Script to show the new objects in the menu
            Debug.Log("Refreshing... Will Take a Second");
            AssetDatabase.ImportAsset(scriptPath);
        }
    }
}
#endif
# if UNITY_EDITOR


using System;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;


public class GlitchHopCustomWindow : OdinMenuEditorWindow
{

    [MenuItem("Glitch Hop/Data Editor")]
    private static void OpenWindow()
    {
       GetWindow<GlitchHopCustomWindow>().Show();
    }

    protected override void OnBeginDrawEditors()
    {
        if (GUILayout.Button("Refresh"))
        {
            ForceMenuTreeRebuild(); Repaint();
        }
    }
    protected override OdinMenuTree BuildMenuTree()
    {

        var tree = new OdinMenuTree();
        tree.Config.DrawSearchToolbar = true;
        tree.DefaultMenuStyle = CustomStyle;
        AddSciptableObj<WeaponData>(tree, root: "Weapon Data", folder: "Assets/Data", keySelector: w => w.ownerType.ToString() , null);
        AddSciptableObj<PlayerProjectileData>(tree, root: "ProjectileData", folder: "Assets/Data", null, "player");
        AddSciptableObj<PlayerMovementData>(tree, root: "MovementData", folder: "Assets/Data", null, "player");

        AddComponentsOfType(tree, "Character Prefab Comp", "Assets/Prefab/Character",
        typeof(Health), typeof(CustomCharacterMovement), typeof(EnemyController), typeof(Transformable), typeof(Targetable), typeof(Vision), typeof(Guardable), typeof(Patrollable));

        return tree;
    }

    private class PrefabInspectorPage
    {
        [HideLabel, AssetsOnly, InlineEditor(ObjectFieldMode = InlineEditorObjectFieldModes.CompletelyHidden, Expanded = true)]
        public GameObject Prefab;
        public PrefabInspectorPage(GameObject prefab) => Prefab= prefab;

    }


    private static void AddSciptableObj<T>(OdinMenuTree tree,string root,string folder,Func<T, string> keySelector, string keySelector2) where T : ScriptableObject
    {
        var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (!asset) continue;

            var key = keySelector != null ? keySelector(asset) : keySelector2;
            if (string.IsNullOrWhiteSpace(key)) key = "Uncategorized";
            

            var finalPath = $"{root}/{key}/{asset.name}";
            tree.Add(finalPath, asset);
        }
    }


    private class ComponentInspectorPage
    {
        [HideLabel, InlineEditor(ObjectFieldMode = InlineEditorObjectFieldModes.CompletelyHidden, Expanded = true)]
        public Component Component;
        public ComponentInspectorPage(Component component) 
        {
            Component = component; 
        } 
    }


    private void AddComponentsOfType(OdinMenuTree tree, string root, string folder, params Type[] types)
    {
        var guids = AssetDatabase.FindAssets("t:GameObject", new[] { folder });
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var assets = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!assets) continue;
            if (PrefabUtility.GetPrefabAssetType(assets) == PrefabAssetType.NotAPrefab) continue;


            foreach (var t in types)
            {
                if (t == null || !typeof(Component).IsAssignableFrom(t)) continue;

                var comps = assets.GetComponentsInChildren(t, includeInactive: true);
                if (comps.Length == 0) continue;
                for (int i = 0; i < comps.Length; i++)
                {
                    var comp = (Component)comps[i];
                    var node = comps.Length > 1 ? $"{t.Name} [{i}]" : t.Name;
                    //tree.Add($"{root}/{assets.name}/{node}", new ComponentInspectorPage(comp));
                    tree.Add($"{root}/{node}/{assets.name}", comp);

                }
            }
        }
    }


    private OdinMenuStyle CustomStyle
    {
        get 
        {
            OdinMenuStyle MenuStyle =  new OdinMenuStyle()
            {
                Height = 30,
                Offset = 16.00f,
                IndentAmount = 15.00f,
                IconSize = 16.00f,
                IconOffset = 0.00f,
                NotSelectedIconAlpha = 0.85f,
                IconPadding = 3.00f,
                TriangleSize = 20.00f,
                TrianglePadding = 5.00f,
                AlignTriangleLeft = true,
                Borders = true,
                BorderPadding = 13.00f,
                BorderAlpha = 0.50f,
                SelectedColorDarkSkin = new Color(0.243f, 0.373f, 0.588f, 1.000f),
                SelectedColorLightSkin = new Color(0.243f, 0.490f, 0.900f, 1.000f)
            };
            return  MenuStyle;
        }
    }



}
#endif
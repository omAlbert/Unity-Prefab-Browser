#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.IO;

public class PrefabBrowserWindow : EditorWindow
{
    private enum PrefabCategory { Geometry, Traps, CheckPoints, Enemies, Breakeables, Interactables, CameraTools, SceneManagers }
    private PrefabCategory selectedCategory = PrefabCategory.Geometry;
    private List<GameObject> prefabs = new List<GameObject>();
    private Dictionary<GameObject, PrefabCategory> prefabCategories = new Dictionary<GameObject, PrefabCategory>();
    private bool isEditingPack = false;
    private const string savePath = "Assets/prefabPackData.json";
    private ScrollView scrollView;
    private DropdownField categoryDropdown;
    private Button editPackButton;

    [MenuItem("Tools/Prefab Browser")]
    public static void ShowWindow()
    {
        GetWindow<PrefabBrowserWindow>("Prefab Browser");
    }

    private void OnEnable()
    {
        LoadPrefabs();
    }

    private void OnDisable()
    {
        SavePrefabs();
    }

    public void CreateGUI()
    {
        rootVisualElement.Clear();
        rootVisualElement.style.paddingLeft = 10;
        rootVisualElement.style.paddingRight = 10;
        rootVisualElement.style.paddingTop = 10;

        var titleLabel = new Label("Prefab Browser") { style = { unityFontStyleAndWeight = FontStyle.Bold } };
        rootVisualElement.Add(titleLabel);

        var categoryContainer = new VisualElement();
        categoryContainer.style.flexDirection = FlexDirection.Row;
        categoryContainer.style.justifyContent = Justify.SpaceBetween;

        categoryDropdown = new DropdownField("Category", System.Enum.GetNames(typeof(PrefabCategory)).ToList(), 0);
        categoryDropdown.RegisterValueChangedCallback(evt =>
        {
            selectedCategory = (PrefabCategory)System.Enum.Parse(typeof(PrefabCategory), evt.newValue);
            RefreshPrefabList();
        });
        categoryContainer.Add(categoryDropdown);

        editPackButton = new Button(() =>
        {
            isEditingPack = !isEditingPack;
            editPackButton.style.backgroundColor = isEditingPack ? Color.green : Color.gray;
            RefreshPrefabList();
        })
        { text = "Edit Pack" };
        categoryContainer.Add(editPackButton);

        rootVisualElement.Add(categoryContainer);

        scrollView = new ScrollView();
        rootVisualElement.Add(scrollView);

        RefreshPrefabList();
        RegisterDragAndDrop();
    }

    private void RefreshPrefabList()
    {
        scrollView.Clear();

        var grid = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                flexWrap = Wrap.Wrap,
                justifyContent = Justify.FlexStart,
                width = Length.Percent(100),
                minHeight = Length.Percent(100)
            }
        };

        foreach (var prefab in prefabs.ToList())
        {
            if (prefabCategories[prefab] != selectedCategory)
                continue;

            var prefabContainer = new VisualElement
            {
                style =
                {
                    width = 100,
                    height = 100,
                    backgroundColor = Color.gray,
                    marginRight = 30,
                    marginLeft = 30,
                    marginTop = 50,
                    display = DisplayStyle.Flex,
                    flexDirection = FlexDirection.Column,
                    justifyContent = Justify.Center,
                    alignItems = Align.Center
                }
            };

            var preview = new Image();
            
            prefabContainer.Add(preview);
           
            Texture2D texture = AssetPreview.GetAssetPreview(prefab);

            if (texture == null)
            {
                // Crear un marcador de lugar con un color sólido o una imagen predeterminada
                texture = CreateDefaultPlaceholder();
            }

            preview.image = texture;
            preview.style.width = 80;
            preview.style.height = 80;
            preview.style.alignSelf = Align.Center;

            EditorApplication.delayCall += () =>
            {
                Texture2D updatedTexture = AssetPreview.GetAssetPreview(prefab);
                if (updatedTexture != null)
                {
                    preview.image = updatedTexture;
                }
            };

            var nameLabel = new Label(prefab.name);
            nameLabel.style.position = Position.Absolute;
            nameLabel.style.top = 0;
            nameLabel.style.left = 0;
            nameLabel.style.right = 0;
            nameLabel.style.width = 100;
            nameLabel.style.height = 100;
            nameLabel.style.backgroundColor = new Color(0, 0, 0, 0.7f);
            nameLabel.style.color = Color.white;
            nameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            nameLabel.style.visibility = Visibility.Hidden;

            nameLabel.style.whiteSpace = WhiteSpace.Normal;  // Permitir salto de línea

            prefabContainer.Add(nameLabel);

            prefabContainer.RegisterCallback<PointerEnterEvent>(evt =>
            {
                nameLabel.style.visibility = Visibility.Visible;
            });

            prefabContainer.RegisterCallback<PointerLeaveEvent>(evt =>
            {
                nameLabel.style.visibility = Visibility.Hidden;
            });

            prefabContainer.RegisterCallback<PointerDownEvent>(evt =>
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new Object[] { prefab };
                DragAndDrop.StartDrag(prefab.name);
                evt.StopPropagation();
            });

            if (isEditingPack)
            {
                var deleteButton = new Button(() =>
                {
                    prefabs.Remove(prefab);
                    prefabCategories.Remove(prefab);
                    SavePrefabs();
                    RefreshPrefabList();
                })
                { text = "X" };
                deleteButton.style.alignSelf = Align.Center;
                prefabContainer.Add(deleteButton);
            }

            grid.Add(prefabContainer);
        }

        scrollView.Add(grid);
    }

    private void RegisterDragAndDrop()
    {
        rootVisualElement.RegisterCallback<DragUpdatedEvent>(evt =>
        {
            if (isEditingPack)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.StopPropagation();
            }
        });

        rootVisualElement.RegisterCallback<DragPerformEvent>(evt =>
        {
            if (isEditingPack)
            {
                foreach (var obj in DragAndDrop.objectReferences)
                {
                    if (obj is GameObject go && !prefabs.Contains(go))
                    {
                        prefabs.Add(go);
                        prefabCategories[go] = selectedCategory;
                        SavePrefabs();
                        RefreshPrefabList();
                    }
                }
                evt.StopPropagation();
            }
        });
    }

    private void SavePrefabs()
    {
        List<string> data = prefabs.Select(p => AssetDatabase.GetAssetPath(p) + "|" + prefabCategories[p]).ToList();
        File.WriteAllLines(savePath, data);
        AssetDatabase.Refresh();
    }

    private void LoadPrefabs()
    {
        if (!File.Exists(savePath)) return;

        string[] lines = File.ReadAllLines(savePath);
        prefabs.Clear();
        prefabCategories.Clear();

        foreach (string line in lines)
        {
            string[] parts = line.Split('|');
            if (parts.Length == 2)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(parts[0]);
                if (prefab != null)
                {
                    prefabs.Add(prefab);
                    if (System.Enum.TryParse(parts[1], out PrefabCategory category))
                    {
                        prefabCategories[prefab] = category;
                    }
                }
            }
        }
    }

    private Texture2D CreateDefaultPlaceholder()
    {
        Texture2D placeholder = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Sprites/Test/placeholder.png");
        return placeholder;
    }
}
#endif

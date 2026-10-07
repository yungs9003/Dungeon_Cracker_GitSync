using System.IO;
using System.Linq;
using System.Reflection;
using Dungeon.Core;
using Dungeon.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Dungeon.Tests
{
    public class ModuleSystemTests
    {
        [Test]
        public void ModuleScene_DescriptionDeserializesKoreanFontAndDbText()
        {
            var scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Module.unity");
            GameObject databaseObject = new GameObject("ModuleDescription_Database_Test");
            databaseObject.SetActive(false);

            try
            {
                Transform description = scene.GetRootGameObjects()
                    .Select(root => FindChildRecursive(root.transform, "DescriptionText"))
                    .FirstOrDefault(found => found != null);
                Assert.That(description, Is.Not.Null);
                Component text = description.GetComponents<Component>()
                    .FirstOrDefault(component => component != null &&
                        component.GetType().FullName == "TMPro.TextMeshProUGUI");
                Assert.That(text, Is.Not.Null);

                var serializedText = new SerializedObject(text);
                GameDatabase database = databaseObject.AddComponent<GameDatabase>();
                database.LoadDatabase();
                Assert.That(database.TryGetModule("000", out ModuleDefinition module), Is.True);
                Assert.That(serializedText.FindProperty("m_text").stringValue, Is.EqualTo(module.desc));
                Assert.That(
                    AssetDatabase.GetAssetPath(serializedText.FindProperty("m_fontAsset").objectReferenceValue),
                    Is.EqualTo("Assets/Fonts/Cafe24Danjunghae-v2.0 SDF.asset"));
                Assert.That(
                    AssetDatabase.GetAssetPath(serializedText.FindProperty("m_sharedMaterial").objectReferenceValue),
                    Is.EqualTo("Assets/Fonts/Cafe24Danjunghae-v2.0 SDF.asset"));
                Assert.That(serializedText.FindProperty("m_fontSize").floatValue, Is.EqualTo(42f));
                Assert.That(serializedText.FindProperty("m_enableAutoSizing").boolValue, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(databaseObject);
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void StatusModuleSlotBinding_IgnoresDestroyedImageReference()
        {
            GameObject root = new GameObject(
                "StatusModuleSlotBinding_Test",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button),
                typeof(Outline));
            GameObject iconObject = new GameObject(
                "ModuleIcon",
                typeof(RectTransform),
                typeof(Image));

            try
            {
                iconObject.transform.SetParent(root.transform, false);
                Button button = root.GetComponent<Button>();
                Image icon = iconObject.GetComponent<Image>();
                Outline outline = root.GetComponent<Outline>();

                System.Type bindingType = typeof(StatusModulePageUI).GetNestedType(
                    "StatusModuleSlotBinding",
                    BindingFlags.NonPublic);
                Assert.That(bindingType, Is.Not.Null);

                ConstructorInfo constructor = bindingType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    binder: null,
                    new[] { typeof(int), typeof(Button), typeof(Image), typeof(Outline) },
                    modifiers: null);
                Assert.That(constructor, Is.Not.Null);

                object binding = constructor.Invoke(new object[] { 1, button, icon, outline });
                MethodInfo setContent = bindingType.GetMethod(
                    "SetContent",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(setContent, Is.Not.Null);

                Object.DestroyImmediate(iconObject);

                Assert.DoesNotThrow(() =>
                    setContent.Invoke(binding, new object[] { null, null }));
                Assert.That((bool)setContent.Invoke(binding, new object[] { null, null }), Is.False);
            }
            finally
            {
                if (root != null)
                    Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GameDatabase_LoadsModules000Through013()
        {
            GameObject go = new GameObject("GameDatabase_ModuleTest");
            go.SetActive(false);

            try
            {
                GameDatabase database = go.AddComponent<GameDatabase>();
                database.LoadDatabase();

                Assert.That(database.GetAllModules().Count(), Is.EqualTo(14));

                for (int i = 0; i < 14; i++)
                {
                    string id = i.ToString("000");
                    Assert.That(database.TryGetModule(id, out ModuleDefinition module), Is.True);
                    Assert.That(module.id, Is.EqualTo(id));
                    Assert.That(module.name, Is.Not.Empty);
                    Assert.That(module.desc, Is.Not.Empty);
                    if (i < 5)
                        Assert.That(database.GetModuleResourceIcon(id), Is.Not.Null);
                    else
                        Assert.That(database.GetModuleResourceIcon(id), Is.Null);
                }
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ModuleInventory_FiltersMissingIconsAndKeepsSevenEmptyFrames()
        {
            GameObject pageObject = new GameObject("ModuleInventory_Test");
            GameObject databaseObject = new GameObject("ModuleInventory_Database_Test");
            pageObject.SetActive(false);
            databaseObject.SetActive(false);

            try
            {
                GameDatabase database = databaseObject.AddComponent<GameDatabase>();
                database.LoadDatabase();
                ModulePageUI page = pageObject.AddComponent<ModulePageUI>();
                GameObject area = new GameObject("ModuleInventoryArea", typeof(RectTransform));
                area.transform.SetParent(pageObject.transform, false);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/ModuleSlot.prefab");
                Assert.That(prefab, Is.Not.Null);
                for (int i = 0; i < 7; i++)
                {
                    GameObject slot = Object.Instantiate(prefab, area.transform);
                    slot.name = $"Slot{i + 1:00}";
                }

                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(ModulePageUI).GetField("database", flags).SetValue(page, database);
                typeof(ModulePageUI).GetField("inventoryArea", flags).SetValue(page, area.transform);
                typeof(ModulePageUI).GetMethod("CollectSlots", flags).Invoke(page, null);
                ModuleSaveRepository repository = (ModuleSaveRepository)typeof(ModulePageUI)
                    .GetField("saveRepository", flags).GetValue(page);
                StatusSaveData data = StatusSaveDefaults.Create();
                // 아이콘 없는 ID와 존재하지 않는 DB ID도 함께 보유한 경우를 검증합니다.
                for (int i = 13; i >= 0; i--)
                    data.inventory.module.Add(new System.Collections.Generic.Dictionary<string, int> { [i.ToString("000")] = 1 });
                data.inventory.module.Add(new System.Collections.Generic.Dictionary<string, int> { ["999"] = 1 });
                typeof(ModuleSaveRepository).GetField("data", flags).SetValue(repository, data);

                MethodInfo refresh = typeof(ModulePageUI).GetMethod("RefreshInventorySlots", flags);
                refresh.Invoke(page, null);
                ModuleSlotUI[] slots = area.GetComponentsInChildren<ModuleSlotUI>(true);
                Assert.That(slots.Length, Is.EqualTo(7));
                for (int i = 0; i < slots.Length; i++)
                {
                    Assert.That(slots[i].gameObject.activeSelf, Is.True);
                    Assert.That(slots[i].HasModule, Is.EqualTo(i < 5));
                    if (i < 5)
                    {
                        Assert.That(slots[i].ModuleId, Is.EqualTo(i.ToString("000")));
                        Assert.That(database.TryGetModule(slots[i].ModuleId, out ModuleDefinition definition), Is.True);
                        Assert.That(slots[i].ModuleName, Is.EqualTo(definition.name));
                        Assert.That(slots[i].Description, Is.EqualTo(definition.desc));
                        Assert.That(slots[i].ModuleIcon, Is.Not.Null);
                    }
                }

                data.inventory.module.Clear();
                refresh.Invoke(page, null);
                foreach (ModuleSlotUI slot in slots)
                {
                    Assert.That(slot.gameObject.activeSelf, Is.True);
                    Assert.That(slot.HasModule, Is.False);
                    Assert.That(slot.GetComponent<Button>().interactable, Is.False);
                }
            }
            finally
            {
                Object.DestroyImmediate(pageObject);
                Object.DestroyImmediate(databaseObject);
            }
        }

        [Test]
        public void DefaultSave_HasEmptyModuleInventoryAndSixSlots()
        {
            StatusSaveData data = StatusSaveDefaults.Create();

            Assert.That(data.save_version, Is.EqualTo(StatusSaveDefaults.CurrentSaveVersion));
            Assert.That(data.inventory.module, Is.Empty);
            Assert.That(data.equips.module, Has.Count.EqualTo(ModuleSaveRepository.SlotCount));

            for (int slot = 1; slot <= ModuleSaveRepository.SlotCount; slot++)
            {
                Assert.That(data.equips.module[slot - 1][$"slot{slot}"], Is.EqualTo(GameDatabase.NoneKey));
            }
        }

        [Test]
        public void Normalize_LegacySaveAddsModuleContainersAndSlots()
        {
            StatusSaveData data = StatusSaveDefaults.Create();
            data.inventory.module = null;
            data.equips.module = null;

            StatusSaveDefaults.Normalize(data);

            Assert.That(data.inventory.module, Is.Not.Null);
            Assert.That(data.equips.module, Has.Count.EqualTo(ModuleSaveRepository.SlotCount));
        }

        [Test]
        public void EditorTestSave_OwnsFourteenModulesAndEquipsSix()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "savefile.test.json");
            string json = File.ReadAllText(path);

            Assert.That(
                StatusSaveDefaults.TryDeserialize(json, out StatusSaveData data, out string error),
                Is.True,
                error
            );
            Assert.That(data.inventory.module, Has.Count.EqualTo(14));
            Assert.That(data.equips.module, Has.Count.EqualTo(ModuleSaveRepository.SlotCount));
        }

        [Test]
        public void StatusModuleArtwork_MatchesProvidedSvgAssetLayout()
        {
            const string artworkPath = "Assets/Resources/UI/StatusModuleArtwork.asset";
            const string prefabPath = "Assets/Prefabs/UI/SharedStatus.prefab";

            StatusModuleArtwork artwork =
                AssetDatabase.LoadAssetAtPath<StatusModuleArtwork>(artworkPath);
            Assert.That(artwork, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(artwork.DeviceFrame),
                Is.EqualTo("Assets/Sprites/UI/Moudle/IMG_4913 1.png")
            );
            Assert.That(
                AssetDatabase.GetAssetPath(artwork.PatternPanel),
                Is.EqualTo("Assets/Sprites/UI/Moudle/module_panel@3x 1.png")
            );
            Assert.That(
                AssetDatabase.GetAssetPath(artwork.ScanLines),
                Is.EqualTo("Assets/Sprites/UI/Moudle/Group 114.png")
            );
            var serializedArtwork = new SerializedObject(artwork);
            Object moduleTextFont = serializedArtwork
                .FindProperty("moduleTextFont")
                .objectReferenceValue;
            Assert.That(
                AssetDatabase.GetAssetPath(moduleTextFont),
                Is.EqualTo("Assets/Fonts/Cafe24Danjunghae-v2.0 SDF.asset")
            );

            string moduleFontYaml = File.ReadAllText(
                "Assets/Fonts/Cafe24Danjunghae-v2.0 SDF.asset"
            );
            StringAssert.Contains(
                "m_IsMultiAtlasTexturesEnabled: 1",
                moduleFontYaml
            );

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null);

            Transform detailGroup = FindChildRecursive(prefab.transform, "ModuleDetailGroup");
            Transform frame = FindChildRecursive(prefab.transform, "ModuleDeviceFrame");
            Transform panel = FindChildRecursive(detailGroup, "ModulePatternPanel");
            Transform scanLines = FindChildRecursive(detailGroup, "ModuleScanLines");
            Transform selectedIcon = FindChildRecursive(detailGroup, "SelectedModuleIcon");
            Transform nameText = FindChildRecursive(detailGroup, "ModuleNameText");
            Transform descriptionText = FindChildRecursive(detailGroup, "ModuleDescriptionText");
            Transform footer = FindChildRecursive(detailGroup, "ModuleViewerLabel");

            Assert.That(detailGroup, Is.Not.Null);
            Assert.That(frame, Is.Not.Null);
            Assert.That(panel, Is.Not.Null);
            Assert.That(scanLines, Is.Not.Null);
            Assert.That(selectedIcon, Is.Not.Null);
            Assert.That(nameText, Is.Not.Null);
            Assert.That(descriptionText, Is.Not.Null);
            Assert.That(footer, Is.Not.Null);
            Assert.That(frame.GetComponent<Image>().sprite, Is.EqualTo(artwork.DeviceFrame));
            Assert.That(panel.GetComponent<Image>().sprite, Is.EqualTo(artwork.PatternPanel));
            Assert.That(scanLines.GetComponent<Image>().sprite, Is.EqualTo(artwork.ScanLines));

            RectTransform frameRect = (RectTransform)frame;
            RectTransform panelRect = (RectTransform)panel;
            RectTransform scanRect = (RectTransform)scanLines;
            RectTransform iconRect = (RectTransform)selectedIcon;
            RectTransform nameRect = (RectTransform)nameText;
            RectTransform descriptionRect = (RectTransform)descriptionText;
            RectTransform footerRect = (RectTransform)footer;

            Assert.That(frameRect.anchoredPosition, Is.EqualTo(new Vector2(1.5f, -25f)));
            Assert.That(frameRect.sizeDelta, Is.EqualTo(new Vector2(960f, 579f)));
            Assert.That(panelRect.anchoredPosition, Is.EqualTo(new Vector2(39.6f, -82f)));
            Assert.That(panelRect.sizeDelta, Is.EqualTo(new Vector2(313f, 468f)));
            Assert.That(scanRect.anchoredPosition, Is.EqualTo(new Vector2(400.5f, -106f)));
            Assert.That(scanRect.sizeDelta, Is.EqualTo(new Vector2(507f, 381f)));
            Assert.That(iconRect.anchoredPosition, Is.EqualTo(new Vector2(196.86f, -316.36f)));
            Assert.That(iconRect.sizeDelta, Is.EqualTo(new Vector2(210.9f, 210.9f)));
            Assert.That(nameRect.anchoredPosition, Is.EqualTo(new Vector2(400.5f, -120f)));
            Assert.That(nameRect.sizeDelta, Is.EqualTo(new Vector2(507f, 82f)));
            Assert.That(descriptionRect.anchoredPosition, Is.EqualTo(new Vector2(439.74f, -235.63f)));
            Assert.That(descriptionRect.sizeDelta, Is.EqualTo(new Vector2(440f, 225f)));
            Assert.That(footerRect.anchoredPosition, Is.EqualTo(new Vector2(502.6f, -508f)));
            Assert.That(footerRect.sizeDelta, Is.EqualTo(new Vector2(323.1f, 34f)));
            Component footerText = footer.GetComponent("TextMeshProUGUI");
            Assert.That(footerText, Is.Not.Null);
            Assert.That(
                footerText.GetType().GetProperty("text")?.GetValue(footerText),
                Is.EqualTo("ADVANCED MODULE VIEWER")
            );

            Assert.That(frame.GetSiblingIndex(), Is.LessThan(detailGroup.GetSiblingIndex()));
            Assert.That(panel.GetSiblingIndex(), Is.LessThan(scanLines.GetSiblingIndex()));
            Assert.That(scanLines.GetSiblingIndex(), Is.LessThan(selectedIcon.GetSiblingIndex()));
            Assert.That(selectedIcon.GetSiblingIndex(), Is.LessThan(nameText.GetSiblingIndex()));
            Assert.That(nameText.GetSiblingIndex(), Is.LessThan(descriptionText.GetSiblingIndex()));
            Assert.That(descriptionText.GetSiblingIndex(), Is.LessThan(footer.GetSiblingIndex()));
        }

        [Test]
        public void ItemEquipmentPopup_MatchesSvgLayoutAndTagSprites()
        {
            const string spriteRoot = "Assets/Sprites/UI/Window/ItemEquipmentPopup/";
            string[] prefabPaths =
            {
                "Assets/Prefabs/UI/ItemEquipmentInfoPopup.prefab",
                "Assets/Prefabs/UI/SharedStatus.prefab"
            };

            foreach (string prefabPath in prefabPaths)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.That(prefab, Is.Not.Null, prefabPath);

                Transform popupRoot = FindChildRecursive(
                    prefab.transform,
                    "ItemEquipmentInfoPopup"
                );
                Assert.That(popupRoot, Is.Not.Null, prefabPath);

                Component controller = popupRoot.GetComponent("ItemEquipmentInfoPopupUI");
                Assert.That(controller, Is.Not.Null, prefabPath);
                SerializedObject serializedController = new SerializedObject(controller);

                Component categoryText = GetComponentReference(
                    serializedController,
                    "categoryText"
                );
                Component nameText = GetComponentReference(serializedController, "nameText");
                Component descriptionText = GetComponentReference(
                    serializedController,
                    "descriptionText"
                );
                Image itemIcon = GetImageReference(serializedController, "itemIcon");
                Image typeTag = GetImageReference(serializedController, "typeTagImage");
                Image rankTag = GetImageReference(serializedController, "rankTagImage");

                Assert.That(categoryText, Is.Not.Null, prefabPath);
                Assert.That(nameText, Is.Not.Null, prefabPath);
                Assert.That(descriptionText, Is.Not.Null, prefabPath);
                Assert.That(itemIcon, Is.Not.Null, prefabPath);
                Assert.That(typeTag, Is.Not.Null, prefabPath);
                Assert.That(rankTag, Is.Not.Null, prefabPath);

                RectTransform panelRect = nameText.transform.parent as RectTransform;
                Assert.That(panelRect, Is.Not.Null, prefabPath);
                Assert.That(panelRect.sizeDelta, Is.EqualTo(new Vector2(360f, 378f)));
                Assert.That(panelRect.anchoredPosition, Is.EqualTo(Vector2.zero));
                Image panelImage = panelRect.GetComponent<Image>();
                Assert.That(panelImage.color.a, Is.EqualTo(1f));
                Assert.That(
                    AssetDatabase.GetAssetPath(panelImage.sprite),
                    Is.EqualTo(spriteRoot + "ItemEquipmentPopup_Background.png")
                );

                AssertTopLeftRect(
                    categoryText.transform as RectTransform,
                    new Vector2(360f, 45f),
                    Vector2.zero
                );
                AssertTopLeftRect(
                    itemIcon.rectTransform,
                    new Vector2(78f, 78f),
                    new Vector2(45f, 78f)
                );
                AssertTopLeftRect(
                    nameText.transform as RectTransform,
                    new Vector2(195f, 58f),
                    new Vector2(142f, 53f)
                );
                AssertTopLeftRect(
                    typeTag.rectTransform,
                    new Vector2(63f, 27f),
                    new Vector2(159f, 108f)
                );
                AssertTopLeftRect(
                    rankTag.rectTransform,
                    new Vector2(84f, 27f),
                    new Vector2(159f, 144f)
                );
                AssertTopLeftRect(
                    descriptionText.transform as RectTransform,
                    new Vector2(294f, 165f),
                    new Vector2(33f, 195f)
                );

                SerializedObject serializedCategory = new SerializedObject(categoryText);
                Assert.That(
                    serializedCategory.FindProperty("m_text").stringValue,
                    Is.EqualTo("-EQUIPMENT-")
                );

                SerializedObject serializedName = new SerializedObject(nameText);
                SerializedObject serializedDescription = new SerializedObject(descriptionText);
                Assert.That(
                    AssetDatabase.GetAssetPath(
                        serializedName.FindProperty("m_fontAsset").objectReferenceValue
                    ),
                    Is.EqualTo("Assets/Fonts/Cafe24Dangdanghae-v2.0 SDF.asset")
                );
                Assert.That(
                    AssetDatabase.GetAssetPath(
                        serializedDescription.FindProperty("m_fontAsset").objectReferenceValue
                    ),
                    Is.EqualTo("Assets/Fonts/Cafe24Danjunghae-v2.0 SDF.asset")
                );

                AssertSpriteReference(
                    serializedController,
                    "consumableTag",
                    spriteRoot + "ItemType_Consumable.png"
                );
                AssertSpriteReference(
                    serializedController,
                    "materialTag",
                    spriteRoot + "ItemType_Material.png"
                );
                AssertSpriteReference(
                    serializedController,
                    "keyTag",
                    spriteRoot + "ItemType_Key.png"
                );
                AssertSpriteReference(
                    serializedController,
                    "headTag",
                    spriteRoot + "EquipmentSlot_Head.png"
                );
                AssertSpriteReference(
                    serializedController,
                    "bodyTag",
                    spriteRoot + "EquipmentSlot_Body.png"
                );
                AssertSpriteReference(
                    serializedController,
                    "legsTag",
                    spriteRoot + "EquipmentSlot_Legs.png"
                );

                SerializedProperty rankTags = serializedController.FindProperty("rankTags");
                Assert.That(rankTags.arraySize, Is.EqualTo(5), prefabPath);

                for (int rank = 0; rank < rankTags.arraySize; rank++)
                {
                    Object sprite = rankTags
                        .GetArrayElementAtIndex(rank)
                        .objectReferenceValue;
                    Assert.That(
                        AssetDatabase.GetAssetPath(sprite),
                        Is.EqualTo(spriteRoot + $"Rank_{rank}.png")
                    );
                }
            }
        }

        private static Component GetComponentReference(
            SerializedObject owner,
            string propertyName)
        {
            return owner.FindProperty(propertyName).objectReferenceValue as Component;
        }

        private static Image GetImageReference(
            SerializedObject owner,
            string propertyName)
        {
            return owner.FindProperty(propertyName).objectReferenceValue as Image;
        }

        private static void AssertSpriteReference(
            SerializedObject owner,
            string propertyName,
            string expectedPath)
        {
            Object sprite = owner.FindProperty(propertyName).objectReferenceValue;
            Assert.That(AssetDatabase.GetAssetPath(sprite), Is.EqualTo(expectedPath));
        }

        private static void AssertTopLeftRect(
            RectTransform rect,
            Vector2 expectedSize,
            Vector2 expectedTopLeft)
        {
            Assert.That(rect, Is.Not.Null);
            Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(rect.pivot, Is.EqualTo(new Vector2(0f, 1f)));
            Assert.That(rect.sizeDelta, Is.EqualTo(expectedSize));
            Assert.That(
                rect.anchoredPosition,
                Is.EqualTo(new Vector2(expectedTopLeft.x, -expectedTopLeft.y))
            );
        }

        private static Transform FindChildRecursive(Transform root, string targetName)
        {
            if (root == null)
                return null;

            if (root.name == targetName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindChildRecursive(root.GetChild(i), targetName);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

public static class MainMenuRedesignBuilder
{
    private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string BackgroundPath = "Assets/Art/MainMenuRedesign/MainMenu_Background.png";
    private const string BlankButtonPath = "Assets/Art/MainMenuRedesign/MainMenu_ButtonBlank.png";
    private const string TitlePlaquePath = "Assets/Art/MainMenuRedesign/MainMenu_TitlePlaque.png";
    private const string PrimaryFontPath = "Assets/Art/UI/Fonts/NotoSerifDisplay-Medium.ttf";
    private const string FontAssetPath = "Assets/Art/UI/Fonts/NotoSerifDisplay-Medium SDF.asset";
    private const string LegacyBackgroundPath = "Assets/Art/Final Images (DO NOT EDIT)/kitchen 2.png";
    private const string LegacyNewGamePath = "Assets/Art/MainMenuButtons/MainMenu_NewGame.png";
    private const string LegacyContinuePath = "Assets/Art/MainMenuButtons/MainMenu_Continue.png";
    private const string LegacySettingsPath = "Assets/Art/MainMenuButtons/MainMenu_Settings.png";
    private const string LegacyExitPath = "Assets/Art/MainMenuButtons/MainMenu_Exit.png";
    private const string LegacyFontPath = "Assets/Art/UI/Fonts/LiberationSerif-Bold.ttf";
    private const string CaptureDirectory = "/tmp/main-menu-captures";
    private const string RequiredFontCharacters =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 %";

    private static readonly BindingFlags PrivateInstance =
        BindingFlags.Instance | BindingFlags.NonPublic;

    private static readonly CaptureSize[] CaptureSizes =
    {
        new CaptureSize(1920, 1080),
        new CaptureSize(1366, 768),
        new CaptureSize(1280, 720),
        new CaptureSize(2560, 1080)
    };

    [MenuItem("Tools/Dreadforge/Main Menu/Rebuild Redesign")]
    public static void Rebuild()
    {
        try
        {
            ConfigureSpriteImporter(BackgroundPath, false);
            ConfigureSpriteImporter(BlankButtonPath, true);
            ConfigureSpriteImporter(TitlePlaquePath, true);

            TMP_FontAsset fontAsset = CreateOrReuseFontAsset();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Sprite background = RequireAsset<Sprite>(BackgroundPath);
            Sprite blankButton = RequireAsset<Sprite>(BlankButtonPath);
            Sprite titlePlaque = RequireAsset<Sprite>(TitlePlaquePath);
            Sprite legacyBackground = RequireAsset<Sprite>(LegacyBackgroundPath);
            Sprite legacyNewGame = RequireAsset<Sprite>(LegacyNewGamePath);
            Sprite legacyContinue = RequireAsset<Sprite>(LegacyContinuePath);
            Sprite legacySettings = RequireAsset<Sprite>(LegacySettingsPath);
            Sprite legacyExit = RequireAsset<Sprite>(LegacyExitPath);
            Font primaryFont = RequireAsset<Font>(PrimaryFontPath);
            Font legacyFont = RequireAsset<Font>(LegacyFontPath);

            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            MainMenuController controller = RequireSingleSceneComponent<MainMenuController>(scene);
            GameObject menuPanelObject = RequireSceneObject(scene, "Panel_StartMenu");
            GameObject backgroundObject = RequireSceneObject(scene, "Panel_Background");
            GameObject newGameObject = RequireSceneObject(scene, "Button_NewGame");
            GameObject continueObject = RequireSceneObject(scene, "Button_Continue");
            GameObject settingsObject = RequireSceneObject(scene, "Button_Settings");
            GameObject exitObject = RequireSceneObject(scene, "Button_Exit");

            RequireComponent<Button>(newGameObject);
            RequireComponent<Button>(continueObject);
            RequireComponent<Button>(settingsObject);
            RequireComponent<Button>(exitObject);

            ButtonHandlerSnapshot newGameHandler = ButtonHandlerSnapshot.Capture(
                newGameObject.GetComponent<Button>(),
                controller,
                nameof(MainMenuController.NewGame));
            ButtonHandlerSnapshot exitHandler = ButtonHandlerSnapshot.Capture(
                exitObject.GetComponent<Button>(),
                controller,
                nameof(MainMenuController.ExitGame));

            RectTransform menuPanel = RequireRectTransform(menuPanelObject);
            Image backgroundImage = RequireComponent<Image>(backgroundObject);
            Image plaqueImage = EnsurePlaque(scene, menuPanel);
            TextMeshProUGUI titleText = EnsureTmpText(scene, "Text_Title", plaqueImage.rectTransform);
            TextMeshProUGUI creditText = EnsureTmpText(
                scene,
                "Text_DeveloperCredit",
                plaqueImage.rectTransform);

            menuPanelObject.SetActive(true);
            backgroundObject.SetActive(true);
            plaqueImage.gameObject.SetActive(true);
            titleText.gameObject.SetActive(true);
            creditText.gameObject.SetActive(true);
            newGameObject.SetActive(true);
            continueObject.SetActive(true);
            settingsObject.SetActive(true);
            exitObject.SetActive(true);

            SerializedObject serializedController = new SerializedObject(controller);
            SetObjectReference(serializedController, "backgroundImage", backgroundImage);
            SetObjectReference(serializedController, "primaryMenuBackgroundSprite", background);
            SetObjectReference(serializedController, "legacyMenuBackgroundSprite", legacyBackground);
            SetObjectReference(serializedController, "sharedButtonFrameSprite", blankButton);
            SetObjectReference(serializedController, "legacyNewGameButtonSprite", legacyNewGame);
            SetObjectReference(serializedController, "legacyContinueButtonSprite", legacyContinue);
            SetObjectReference(serializedController, "legacySettingsButtonSprite", legacySettings);
            SetObjectReference(serializedController, "legacyExitButtonSprite", legacyExit);
            SetObjectReference(serializedController, "titlePlaqueImage", plaqueImage);
            SetObjectReference(serializedController, "primaryTitlePlaqueSprite", titlePlaque);
            SetObjectReference(serializedController, "legacyTitlePlaqueSprite", titlePlaque);
            SetObjectReference(serializedController, "titleText", titleText);
            SetObjectReference(serializedController, "developerCreditText", creditText);
            SetObjectReference(serializedController, "primaryTitleSourceFont", primaryFont);
            SetObjectReference(serializedController, "legacyTitleSourceFont", legacyFont);
            SetObjectReference(serializedController, "titleFontAsset", fontAsset);
            SetObjectReference(serializedController, "menuPanel", menuPanel);
            SetObjectReference(serializedController, "title", titleText.rectTransform);
            SetObjectReference(serializedController, "newGameButton", RequireRectTransform(newGameObject));
            SetObjectReference(serializedController, "continueButton", RequireRectTransform(continueObject));
            SetObjectReference(serializedController, "settingsButton", RequireRectTransform(settingsObject));
            SetObjectReference(serializedController, "exitButton", RequireRectTransform(exitObject));
            SetBoolean(serializedController, "configureCanvasScaling", true);
            SetBoolean(serializedController, "applyRightRailLayout", true);
            SetBoolean(serializedController, "applyLayoutEveryFrame", false);
            serializedController.ApplyModifiedPropertiesWithoutUndo();

            InvokeControllerMethod(controller, "CacheMenuReferences");
            InvokeControllerMethod(controller, "CacheVisualReferences");
            InvokeControllerMethod(controller, "ConfigureCanvasScalers");
            InvokeControllerMethod(controller, "ApplyMenuVisuals");
            InvokeControllerMethod(controller, "ApplyRightRailLayout");
            Canvas.ForceUpdateCanvases();

            newGameHandler.AssertUnchanged(newGameObject.GetComponent<Button>());
            exitHandler.AssertUnchanged(exitObject.GetComponent<Button>());
            ValidateAppliedScene(
                scene,
                controller,
                background,
                blankButton,
                titlePlaque,
                fontAsset);

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);

            if (!EditorSceneManager.SaveScene(scene, MainMenuScenePath))
            {
                throw new InvalidOperationException($"Unity failed to save {MainMenuScenePath}.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"Main menu redesign rebuilt and saved to {MainMenuScenePath}.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"Main menu redesign rebuild failed: {exception}");
            throw;
        }
    }

    [MenuItem("Tools/Dreadforge/Main Menu/Capture Redesign Screenshots")]
    public static void CaptureScreenshots()
    {
        byte[] savedSceneBytes = File.ReadAllBytes(MainMenuScenePath);
        GameObject cameraObject = null;
        RenderTexture previousActiveTexture = RenderTexture.active;

        try
        {
            Directory.CreateDirectory(CaptureDirectory);
            Scene scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            MainMenuController controller = RequireSingleSceneComponent<MainMenuController>(scene);
            Canvas canvas = controller.GetComponentInParent<Canvas>();

            if (canvas == null)
            {
                throw new InvalidOperationException("MainMenuController must be under the menu Canvas.");
            }

            InvokeControllerMethod(controller, "CacheMenuReferences");
            InvokeControllerMethod(controller, "CacheVisualReferences");
            InvokeControllerMethod(controller, "ConfigureCanvasScalers");
            InvokeControllerMethod(controller, "ApplyMenuVisuals");

            cameraObject = new GameObject("MainMenuCaptureCamera", typeof(Camera));
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            Camera captureCamera = cameraObject.GetComponent<Camera>();
            captureCamera.enabled = false;
            captureCamera.clearFlags = CameraClearFlags.SolidColor;
            captureCamera.backgroundColor = Color.black;
            captureCamera.cullingMask = ~0;
            captureCamera.orthographic = true;
            captureCamera.nearClipPlane = 0.01f;
            captureCamera.farClipPlane = 1000f;
            captureCamera.allowHDR = false;
            captureCamera.allowMSAA = false;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = captureCamera;
            canvas.planeDistance = 10f;

            foreach (CaptureSize size in CaptureSizes)
            {
                string outputPath = Path.Combine(
                    CaptureDirectory,
                    $"main-menu-{size.Width}x{size.Height}.png");
                CaptureFrame(scene, controller, canvas, captureCamera, size, outputPath);
            }

            SetPrivateBoolean(controller, "audioSettingsVisible", false);
            GameObject existingSettingsPanel = FindOptionalSceneObject(scene, "Panel_AudioSettings");

            if (existingSettingsPanel != null)
            {
                existingSettingsPanel.SetActive(false);
            }

            InvokeControllerMethod(controller, "ToggleAudioSettingsPanel");
            GameObject settingsPanel = RequireSceneObject(scene, "Panel_AudioSettings");

            if (!settingsPanel.activeInHierarchy)
            {
                throw new InvalidOperationException("The settings modal did not become active for capture.");
            }

            CaptureSize settingsSize = new CaptureSize(1920, 1080);
            CaptureFrame(
                scene,
                controller,
                canvas,
                captureCamera,
                settingsSize,
                Path.Combine(CaptureDirectory, "main-menu-settings-1920x1080.png"));

            Debug.Log($"Main menu captures written synchronously to {CaptureDirectory}.");
        }
        catch (Exception exception)
        {
            Debug.LogError($"Main menu capture failed: {exception}");
            throw;
        }
        finally
        {
            RenderTexture.active = previousActiveTexture;

            if (cameraObject != null)
            {
                UnityEngine.Object.DestroyImmediate(cameraObject);
            }

            EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        }

        if (!savedSceneBytes.SequenceEqual(File.ReadAllBytes(MainMenuScenePath)))
        {
            const string message = "CaptureScreenshots changed the saved main-menu scene.";
            Debug.LogError(message);
            throw new InvalidOperationException(message);
        }
    }

    private static void ConfigureSpriteImporter(string assetPath, bool hasAlpha)
    {
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (importer == null)
        {
            throw new InvalidOperationException($"Expected a texture importer for {assetPath}.");
        }

        bool changed = false;
        changed |= SetImporterValue(
            importer.textureType,
            TextureImporterType.Sprite,
            value => importer.textureType = value);
        changed |= SetImporterValue(
            importer.spriteImportMode,
            SpriteImportMode.Single,
            value => importer.spriteImportMode = value);
        changed |= SetImporterValue(
            importer.wrapMode,
            TextureWrapMode.Clamp,
            value => importer.wrapMode = value);
        changed |= SetImporterValue(
            importer.mipmapEnabled,
            false,
            value => importer.mipmapEnabled = value);
        changed |= SetImporterValue(
            importer.alphaIsTransparency,
            hasAlpha,
            value => importer.alphaIsTransparency = value);
        changed |= SetImporterValue(
            importer.alphaSource,
            hasAlpha ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.None,
            value => importer.alphaSource = value);
        changed |= SetImporterValue(
            importer.maxTextureSize,
            4096,
            value => importer.maxTextureSize = value);
        changed |= SetImporterValue(
            importer.textureCompression,
            TextureImporterCompression.CompressedHQ,
            value => importer.textureCompression = value);
        changed |= SetImporterValue(
            importer.compressionQuality,
            100,
            value => importer.compressionQuality = value);
        changed |= SetImporterValue(
            importer.npotScale,
            TextureImporterNPOTScale.None,
            value => importer.npotScale = value);
        changed |= SetImporterValue(
            importer.filterMode,
            FilterMode.Bilinear,
            value => importer.filterMode = value);
        TextureImporterPlatformSettings defaultSettings = importer.GetDefaultPlatformTextureSettings();

        if (defaultSettings.maxTextureSize != 4096 ||
            defaultSettings.textureCompression != TextureImporterCompression.CompressedHQ ||
            defaultSettings.compressionQuality != 100)
        {
            defaultSettings.maxTextureSize = 4096;
            defaultSettings.textureCompression = TextureImporterCompression.CompressedHQ;
            defaultSettings.compressionQuality = 100;
            importer.SetPlatformTextureSettings(defaultSettings);
            changed = true;
        }

        if (changed)
        {
            importer.SaveAndReimport();
        }

        TextureImporter verified = AssetImporter.GetAtPath(assetPath) as TextureImporter;

        if (verified == null ||
            verified.textureType != TextureImporterType.Sprite ||
            verified.spriteImportMode != SpriteImportMode.Single ||
            verified.wrapMode != TextureWrapMode.Clamp ||
            verified.mipmapEnabled ||
            verified.alphaIsTransparency != hasAlpha ||
            verified.maxTextureSize != 4096 ||
            verified.textureCompression != TextureImporterCompression.CompressedHQ ||
            verified.compressionQuality != 100)
        {
            throw new InvalidOperationException($"Texture importer normalization failed for {assetPath}.");
        }
    }

    private static TMP_FontAsset CreateOrReuseFontAsset()
    {
        AssetDatabase.ImportAsset(PrimaryFontPath, ImportAssetOptions.ForceSynchronousImport);
        Font sourceFont = RequireAsset<Font>(PrimaryFontPath);
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

        if (IsValidFontAsset(existing, sourceFont))
        {
            EnsureRequiredGlyphs(existing);
            return existing;
        }

        TMP_FontAsset generated = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            90,
            9,
            GlyphRenderMode.SDFAA,
            1024,
            1024,
            AtlasPopulationMode.Dynamic,
            true);

        if (generated == null)
        {
            throw new InvalidOperationException($"TMP could not create a font asset from {PrimaryFontPath}.");
        }

        generated.name = "NotoSerifDisplay-Medium SDF";
        SetFontClearDynamicDataOnBuild(generated, false);
        EnsureRequiredGlyphs(generated);
        Texture2D atlasTexture = generated.atlasTextures[0];
        Material material = generated.material;

        if (atlasTexture == null || material == null)
        {
            UnityEngine.Object.DestroyImmediate(generated);
            throw new InvalidOperationException("TMP created a font asset without an atlas or material.");
        }

        atlasTexture.name = "NotoSerifDisplay-Medium Atlas";
        material.name = "NotoSerifDisplay-Medium Atlas Material";

        if (existing == null)
        {
            AssetDatabase.CreateAsset(generated, FontAssetPath);
            AssetDatabase.AddObjectToAsset(atlasTexture, generated);
            AssetDatabase.AddObjectToAsset(material, generated);
            existing = generated;
        }
        else
        {
            foreach (UnityEngine.Object subAsset in AssetDatabase.LoadAllAssetsAtPath(FontAssetPath))
            {
                if (subAsset != null && subAsset != existing)
                {
                    UnityEngine.Object.DestroyImmediate(subAsset, true);
                }
            }

            EditorUtility.CopySerialized(generated, existing);
            existing.name = generated.name;
            AssetDatabase.AddObjectToAsset(atlasTexture, existing);
            AssetDatabase.AddObjectToAsset(material, existing);
            UnityEngine.Object.DestroyImmediate(generated);
        }

        EditorUtility.SetDirty(existing);
        EditorUtility.SetDirty(atlasTexture);
        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(FontAssetPath, ImportAssetOptions.ForceSynchronousImport);
        existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

        if (!IsValidFontAsset(existing, sourceFont))
        {
            throw new InvalidOperationException($"The serialized TMP font asset at {FontAssetPath} is invalid.");
        }

        return existing;
    }

    private static bool IsValidFontAsset(TMP_FontAsset fontAsset, Font sourceFont)
    {
        return fontAsset != null &&
               fontAsset.sourceFontFile == sourceFont &&
               fontAsset.atlasPopulationMode == AtlasPopulationMode.Dynamic &&
               fontAsset.atlasRenderMode == GlyphRenderMode.SDFAA &&
               fontAsset.atlasTextures != null &&
               fontAsset.atlasTextures.Length > 0 &&
               fontAsset.atlasTextures[0] != null &&
               fontAsset.material != null &&
               AssetDatabase.GetAssetPath(fontAsset.atlasTextures[0]) == FontAssetPath &&
               AssetDatabase.GetAssetPath(fontAsset.material) == FontAssetPath;
    }

    private static void EnsureRequiredGlyphs(TMP_FontAsset fontAsset)
    {
        SetFontClearDynamicDataOnBuild(fontAsset, false);

        if (!fontAsset.HasCharacters(RequiredFontCharacters) &&
            !fontAsset.TryAddCharacters(RequiredFontCharacters, out string missingCharacters))
        {
            throw new InvalidOperationException(
                $"The Noto Serif Display TMP asset is missing required characters: {missingCharacters}");
        }

        if (!fontAsset.HasCharacters(RequiredFontCharacters))
        {
            throw new InvalidOperationException(
                "The Noto Serif Display TMP asset did not retain its required character set.");
        }

        EditorUtility.SetDirty(fontAsset);

        if (fontAsset.atlasTextures != null)
        {
            foreach (Texture2D atlasTexture in fontAsset.atlasTextures)
            {
                if (atlasTexture != null)
                {
                    EditorUtility.SetDirty(atlasTexture);
                }
            }
        }
    }

    private static Image EnsurePlaque(Scene scene, RectTransform menuPanel)
    {
        GameObject plaqueObject = FindOptionalSceneObject(scene, "Image_TitlePlaque");

        if (plaqueObject == null)
        {
            plaqueObject = new GameObject(
                "Image_TitlePlaque",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            plaqueObject.layer = menuPanel.gameObject.layer;
        }

        RectTransform plaqueRect = RequireRectTransform(plaqueObject);

        if (plaqueRect.parent != menuPanel)
        {
            plaqueRect.SetParent(menuPanel, false);
        }

        return RequireComponent<Image>(plaqueObject);
    }

    private static TextMeshProUGUI EnsureTmpText(
        Scene scene,
        string objectName,
        RectTransform defaultParent)
    {
        GameObject textObject = FindOptionalSceneObject(scene, objectName);

        if (textObject == null)
        {
            textObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            textObject.layer = defaultParent.gameObject.layer;
            textObject.transform.SetParent(defaultParent, false);
        }

        RequireRectTransform(textObject);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();

        if (text == null)
        {
            text = textObject.AddComponent<TextMeshProUGUI>();
        }

        return text;
    }

    private static void ValidateAppliedScene(
        Scene scene,
        MainMenuController controller,
        Sprite background,
        Sprite blankButton,
        Sprite titlePlaque,
        TMP_FontAsset fontAsset)
    {
        Image backgroundImage = RequireComponent<Image>(RequireSceneObject(scene, "Panel_Background"));
        AspectRatioFitter backgroundFitter = backgroundImage.GetComponent<AspectRatioFitter>();
        Image plaqueImage = RequireComponent<Image>(RequireSceneObject(scene, "Image_TitlePlaque"));
        TextMeshProUGUI title = RequireComponent<TextMeshProUGUI>(RequireSceneObject(scene, "Text_Title"));
        TextMeshProUGUI credit = RequireComponent<TextMeshProUGUI>(
            RequireSceneObject(scene, "Text_DeveloperCredit"));
        GameObject continueObject = RequireSceneObject(scene, "Button_Continue");

        RequireReference(backgroundImage.sprite, background, "background sprite");
        RequireReference(plaqueImage.sprite, titlePlaque, "title plaque sprite");
        RequireReference(title.font, fontAsset, "title TMP font");
        RequireReference(credit.font, fontAsset, "developer credit TMP font");
        RequireValue(title.text == "Chantilly", "The title text must be exactly 'Chantilly'.");
        RequireValue(
            credit.text == "developed by Kadabra Games",
            "The developer credit text is incorrect.");
        RequireValue(title.gameObject.activeInHierarchy, "The title TMP label must be active.");
        RequireValue(credit.gameObject.activeInHierarchy, "The developer credit TMP label must be active.");
        RequireValue(!continueObject.activeSelf, "Button_Continue must be inactive.");
        RequireValue(
            backgroundFitter != null &&
            backgroundFitter.aspectMode == AspectRatioFitter.AspectMode.EnvelopeParent,
            "The background must aspect-fill through AspectRatioFitter.EnvelopeParent.");

        GameObject startObject = RequireSceneObject(scene, "Button_NewGame");
        GameObject settingsObject = RequireSceneObject(scene, "Button_Settings");
        GameObject exitObject = RequireSceneObject(scene, "Button_Exit");
        ValidateButton(startObject, "Start Game", blankButton, fontAsset);
        ValidateButton(settingsObject, "Settings", blankButton, fontAsset);
        ValidateButton(exitObject, "Exit", blankButton, fontAsset);

        Button startButton = startObject.GetComponent<Button>();
        Button settingsButton = settingsObject.GetComponent<Button>();
        Button exitButton = exitObject.GetComponent<Button>();
        RequireValue(
            startButton.navigation.mode == Navigation.Mode.Explicit &&
            startButton.navigation.selectOnDown == settingsButton,
            "Start Game must navigate down to Settings.");
        RequireValue(
            settingsButton.navigation.mode == Navigation.Mode.Explicit &&
            settingsButton.navigation.selectOnUp == startButton &&
            settingsButton.navigation.selectOnDown == exitButton,
            "Settings must navigate between Start Game and Exit.");
        RequireValue(
            exitButton.navigation.mode == Navigation.Mode.Explicit &&
            exitButton.navigation.selectOnUp == settingsButton,
            "Exit must navigate up to Settings.");
        RequireValue(
            settingsButton.onClick.GetPersistentEventCount() == 0,
            "Settings must keep its controller-owned runtime handler unserialized.");

        int activeButtonCount = FindSceneComponents<Button>(scene)
            .Count(button => button.gameObject.activeInHierarchy);
        RequireValue(activeButtonCount == 3, "The saved main menu must contain exactly three active Buttons.");

        SerializedObject serializedController = new SerializedObject(controller);
        RequireSerializedReference(serializedController, "primaryMenuBackgroundSprite", background);
        RequireSerializedReference(serializedController, "sharedButtonFrameSprite", blankButton);
        RequireSerializedReference(serializedController, "primaryTitlePlaqueSprite", titlePlaque);
        RequireSerializedReference(serializedController, "titleFontAsset", fontAsset);
        RequireSerializedReference(
            serializedController,
            "legacyMenuBackgroundSprite",
            RequireAsset<Sprite>(LegacyBackgroundPath));
        RequireSerializedReference(
            serializedController,
            "legacyNewGameButtonSprite",
            RequireAsset<Sprite>(LegacyNewGamePath));
        RequireSerializedReference(
            serializedController,
            "legacyContinueButtonSprite",
            RequireAsset<Sprite>(LegacyContinuePath));
        RequireSerializedReference(
            serializedController,
            "legacySettingsButtonSprite",
            RequireAsset<Sprite>(LegacySettingsPath));
        RequireSerializedReference(
            serializedController,
            "legacyExitButtonSprite",
            RequireAsset<Sprite>(LegacyExitPath));
        RequireSerializedReference(
            serializedController,
            "legacyTitleSourceFont",
            RequireAsset<Font>(LegacyFontPath));
    }

    private static void ValidateButton(
        GameObject buttonObject,
        string expectedText,
        Sprite expectedSprite,
        TMP_FontAsset expectedFont)
    {
        RequireValue(buttonObject.activeInHierarchy, $"{buttonObject.name} must be active.");
        Button button = RequireComponent<Button>(buttonObject);
        Image image = RequireComponent<Image>(buttonObject);
        TextMeshProUGUI label = buttonObject.GetComponentsInChildren<TextMeshProUGUI>(true)
            .SingleOrDefault(candidate => candidate.gameObject.name == "Text_Label");

        RequireReference(image.sprite, expectedSprite, $"{buttonObject.name} sprite");
        RequireValue(button.interactable, $"{buttonObject.name} must be interactable.");
        RequireValue(label != null, $"{buttonObject.name} must have one Text_Label TMP child.");
        RequireValue(label.gameObject.activeInHierarchy, $"{buttonObject.name} label must be active.");
        RequireValue(label.text == expectedText, $"{buttonObject.name} label must be '{expectedText}'.");
        RequireReference(label.font, expectedFont, $"{buttonObject.name} TMP font");
        RequireRightAnchored(RequireRectTransform(buttonObject));
    }

    private static void CaptureFrame(
        Scene scene,
        MainMenuController controller,
        Canvas canvas,
        Camera captureCamera,
        CaptureSize size,
        string outputPath)
    {
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        RenderTexture renderTexture = new RenderTexture(
            size.Width,
            size.Height,
            24,
            RenderTextureFormat.ARGB32,
            RenderTextureReadWrite.sRGB)
        {
            name = $"MainMenuCapture_{size.Width}x{size.Height}",
            antiAliasing = 1,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Texture2D screenshot = null;

        try
        {
            if (!renderTexture.Create())
            {
                throw new InvalidOperationException(
                    $"Could not create the {size.Width}x{size.Height} capture render texture.");
            }

            captureCamera.targetTexture = renderTexture;
            captureCamera.aspect = (float)size.Width / size.Height;
            canvas.worldCamera = captureCamera;
            Canvas.ForceUpdateCanvases();
            InvokeControllerMethod(controller, "ApplyRightRailLayout");

            foreach (TMP_Text text in FindSceneComponents<TMP_Text>(scene))
            {
                if (text.isActiveAndEnabled)
                {
                    text.ForceMeshUpdate(false, true);
                }
            }

            Canvas.ForceUpdateCanvases();
            captureCamera.Render();
            RenderTexture.active = renderTexture;
            screenshot = new Texture2D(size.Width, size.Height, TextureFormat.RGB24, false, false)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            screenshot.ReadPixels(new Rect(0f, 0f, size.Width, size.Height), 0, 0, false);
            screenshot.Apply(false, false);
            EnsureCaptureHasPixels(screenshot, outputPath);
            byte[] pngBytes = screenshot.EncodeToPNG();

            if (pngBytes == null || pngBytes.Length < 1024)
            {
                throw new InvalidOperationException($"PNG encoding produced no useful data for {outputPath}.");
            }

            File.WriteAllBytes(outputPath, pngBytes);

            if (!File.Exists(outputPath) || new FileInfo(outputPath).Length != pngBytes.Length)
            {
                throw new IOException($"Capture output was not written completely to {outputPath}.");
            }
        }
        finally
        {
            captureCamera.targetTexture = null;
            RenderTexture.active = null;

            if (screenshot != null)
            {
                UnityEngine.Object.DestroyImmediate(screenshot);
            }

            renderTexture.Release();
            UnityEngine.Object.DestroyImmediate(renderTexture);
        }
    }

    private static void EnsureCaptureHasPixels(Texture2D screenshot, string outputPath)
    {
        Color32[] pixels = screenshot.GetPixels32();

        if (pixels.Length == 0)
        {
            throw new InvalidOperationException($"Capture {outputPath} has no pixels.");
        }

        Color32 first = pixels[0];
        int differentPixels = 0;
        int nonBlackPixels = 0;

        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 pixel = pixels[i];

            if (Math.Abs(pixel.r - first.r) +
                Math.Abs(pixel.g - first.g) +
                Math.Abs(pixel.b - first.b) > 12)
            {
                differentPixels++;
            }

            if (pixel.r + pixel.g + pixel.b > 24)
            {
                nonBlackPixels++;
            }
        }

        int minimumUsefulPixels = Math.Max(256, pixels.Length / 1000);

        if (differentPixels < minimumUsefulPixels || nonBlackPixels < minimumUsefulPixels)
        {
            throw new InvalidOperationException(
                $"Capture {outputPath} is blank or nearly uniform " +
                $"({differentPixels} varied, {nonBlackPixels} non-black pixels).");
        }
    }

    private static void SetObjectReference(
        SerializedObject serializedObject,
        string propertyName,
        UnityEngine.Object value)
    {
        SerializedProperty property = RequireSerializedProperty(serializedObject, propertyName);
        property.objectReferenceValue = value;
    }

    private static void SetFontClearDynamicDataOnBuild(TMP_FontAsset fontAsset, bool value)
    {
        SerializedObject serializedFont = new SerializedObject(fontAsset);
        SerializedProperty property = serializedFont.FindProperty("m_ClearDynamicDataOnBuild");

        if (property == null)
        {
            throw new MissingFieldException(typeof(TMP_FontAsset).FullName, "m_ClearDynamicDataOnBuild");
        }

        property.boolValue = value;
        serializedFont.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBoolean(
        SerializedObject serializedObject,
        string propertyName,
        bool value)
    {
        SerializedProperty property = RequireSerializedProperty(serializedObject, propertyName);
        property.boolValue = value;
    }

    private static SerializedProperty RequireSerializedProperty(
        SerializedObject serializedObject,
        string propertyName)
    {
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            throw new MissingFieldException(serializedObject.targetObject.GetType().Name, propertyName);
        }

        return property;
    }

    private static void RequireSerializedReference(
        SerializedObject serializedObject,
        string propertyName,
        UnityEngine.Object expected)
    {
        SerializedProperty property = RequireSerializedProperty(serializedObject, propertyName);
        RequireReference(property.objectReferenceValue, expected, propertyName);
    }

    private static void InvokeControllerMethod(MainMenuController controller, string methodName)
    {
        MethodInfo method = typeof(MainMenuController).GetMethod(methodName, PrivateInstance);

        if (method == null)
        {
            throw new MissingMethodException(typeof(MainMenuController).FullName, methodName);
        }

        try
        {
            method.Invoke(controller, null);
        }
        catch (TargetInvocationException exception)
        {
            throw new InvalidOperationException(
                $"MainMenuController.{methodName} failed.",
                exception.InnerException ?? exception);
        }
    }

    private static void SetPrivateBoolean(
        MainMenuController controller,
        string fieldName,
        bool value)
    {
        FieldInfo field = typeof(MainMenuController).GetField(fieldName, PrivateInstance);

        if (field == null || field.FieldType != typeof(bool))
        {
            throw new MissingFieldException(typeof(MainMenuController).FullName, fieldName);
        }

        field.SetValue(controller, value);
    }

    private static GameObject RequireSceneObject(Scene scene, string objectName)
    {
        GameObject result = FindOptionalSceneObject(scene, objectName);

        if (result == null)
        {
            throw new InvalidOperationException(
                $"Expected exactly one scene object named '{objectName}' in {scene.path}.");
        }

        return result;
    }

    private static GameObject FindOptionalSceneObject(Scene scene, string objectName)
    {
        List<GameObject> matches = new List<GameObject>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (transform.gameObject.name == objectName)
                {
                    matches.Add(transform.gameObject);
                }
            }
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Found {matches.Count} scene objects named '{objectName}' in {scene.path}.");
        }

        return matches.SingleOrDefault();
    }

    private static T RequireSingleSceneComponent<T>(Scene scene) where T : Component
    {
        List<T> components = FindSceneComponents<T>(scene);

        if (components.Count != 1)
        {
            throw new InvalidOperationException(
                $"Expected exactly one {typeof(T).Name} in {scene.path}, found {components.Count}.");
        }

        return components[0];
    }

    private static List<T> FindSceneComponents<T>(Scene scene) where T : Component
    {
        List<T> components = new List<T>();

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            components.AddRange(root.GetComponentsInChildren<T>(true));
        }

        return components;
    }

    private static T RequireComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();

        if (component == null)
        {
            throw new InvalidOperationException(
                $"Scene object '{gameObject.name}' requires {typeof(T).Name}.");
        }

        return component;
    }

    private static RectTransform RequireRectTransform(GameObject gameObject)
    {
        return RequireComponent<RectTransform>(gameObject);
    }

    private static T RequireAsset<T>(string assetPath) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(assetPath);

        if (asset == null)
        {
            throw new FileNotFoundException(
                $"Required {typeof(T).Name} asset could not be loaded.",
                assetPath);
        }

        return asset;
    }

    private static void RequireRightAnchored(RectTransform rectTransform)
    {
        RequireValue(
            Mathf.Approximately(rectTransform.anchorMin.x, 1f) &&
            Mathf.Approximately(rectTransform.anchorMax.x, 1f),
            $"{rectTransform.gameObject.name} must be right-anchored.");
    }

    private static void RequireReference(
        UnityEngine.Object actual,
        UnityEngine.Object expected,
        string description)
    {
        RequireValue(actual == expected, $"Unexpected serialized reference for {description}.");
    }

    private static void RequireValue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static bool SetImporterValue<T>(T current, T expected, Action<T> assign)
    {
        if (EqualityComparer<T>.Default.Equals(current, expected))
        {
            return false;
        }

        assign(expected);
        return true;
    }

    private readonly struct CaptureSize
    {
        public CaptureSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public int Width { get; }
        public int Height { get; }
    }

    private readonly struct ButtonHandlerSnapshot
    {
        private ButtonHandlerSnapshot(
            UnityEngine.Object target,
            string methodName,
            UnityEventCallState callState)
        {
            Target = target;
            MethodName = methodName;
            CallState = callState;
        }

        private UnityEngine.Object Target { get; }
        private string MethodName { get; }
        private UnityEventCallState CallState { get; }

        public static ButtonHandlerSnapshot Capture(
            Button button,
            MainMenuController expectedController,
            string expectedMethod)
        {
            if (button.onClick.GetPersistentEventCount() != 1)
            {
                throw new InvalidOperationException(
                    $"{button.gameObject.name} must have exactly one persistent callback.");
            }

            ButtonHandlerSnapshot snapshot = new ButtonHandlerSnapshot(
                button.onClick.GetPersistentTarget(0),
                button.onClick.GetPersistentMethodName(0),
                button.onClick.GetPersistentListenerState(0));

            if (snapshot.Target != expectedController || snapshot.MethodName != expectedMethod)
            {
                throw new InvalidOperationException(
                    $"{button.gameObject.name} must call MainMenuController.{expectedMethod}.");
            }

            return snapshot;
        }

        public void AssertUnchanged(Button button)
        {
            if (button.onClick.GetPersistentEventCount() != 1 ||
                button.onClick.GetPersistentTarget(0) != Target ||
                button.onClick.GetPersistentMethodName(0) != MethodName ||
                button.onClick.GetPersistentListenerState(0) != CallState)
            {
                throw new InvalidOperationException(
                    $"{button.gameObject.name}'s persistent controller callback changed during rebuild.");
            }
        }
    }
}

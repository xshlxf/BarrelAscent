using System;
using System.IO;
using BarrelAscent.EditorTools.Art;
using BarrelAscent.Managers;
using BarrelAscent.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using static BarrelAscent.EditorTools.Phase2Builder;
using Object = UnityEngine.Object;

namespace BarrelAscent.EditorTools
{
    /// <summary>
    /// Fase 6: interfaz.
    ///  0. ImportTmpEssentials (ejecutar una vez, en su propia llamada): recursos esenciales de TextMeshPro.
    ///  1. Fuente pixel BarrelPixel y sprites de UI desde ArtSource/06_UI y el fondo de la zona 1.
    ///  2. Escena MainMenu: fondo, título, Jugar, Salir, controles.
    ///  3. GameLevel: HUD (puntos, vidas, zona, pausa), aviso de zona, paneles Pausa / Game Over / Victoria.
    /// Canvas de referencia 640×360 con 32 PPU: 1 píxel de sprite = 1 píxel de referencia (×3 a 1080p).
    /// Menú: Barrel Ascent/Setup/Build Phase 6 (UI). No reconstruye el nivel: solo la UI.
    /// </summary>
    public static class Phase6Builder
    {
        const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        const string UiArt = "ArtSource/06_UI";
        public const string UiSpriteDir = "Assets/UI/Sprites";

        static string UiSprite(string name) => $"{UiSpriteDir}/{name}.png";

        static readonly Color YellowC = new Color32(0xE6, 0xC6, 0x6B, 0xFF);
        static readonly Color LightGrayC = new Color32(0xD7, 0xD5, 0xC7, 0xFF);
        static readonly Color RedC = new Color32(0xB8, 0x3B, 0x36, 0xFF);
        static readonly Color OrangeC = new Color32(0xD9, 0x77, 0x32, 0xFF);
        static readonly Color ShadowC = new Color32(0x10, 0x18, 0x20, 0xFF);

        static TMP_FontAsset font;

        [MenuItem("Barrel Ascent/Setup/Import TMP Essentials")]
        public static void ImportTmpEssentials()
        {
            if (File.Exists(TmpSettingsPath)) { Debug.Log("[Phase6Builder] TMP Essentials already present."); return; }
            string ugui = Path.GetFullPath("Packages/com.unity.ugui");
            string package = Path.Combine(ugui, "Package Resources", "TMP Essential Resources.unitypackage");
            AssetDatabase.ImportPackage(package, false);
            AssetDatabase.Refresh();
            Debug.Log($"[Phase6Builder] Imported TMP Essentials: {File.Exists(TmpSettingsPath)}");
        }

        [MenuItem("Barrel Ascent/Setup/Build Phase 6 (UI)")]
        public static void Build()
        {
            if (!File.Exists(TmpSettingsPath))
                throw new InvalidOperationException("Ejecuta antes 'Import TMP Essentials'.");

            PixelFontBuilder.Build();
            ImportUiArt();
            AssetDatabase.SaveAssets();

            BuildMainMenu();
            BuildGameLevelUi();
            Debug.Log("[Phase6Builder] Phase 6 build complete.");
        }

        // ---------------- Arte ----------------

        static void ImportUiArt()
        {
            Directory.CreateDirectory(UiSpriteDir);
            // (archivo, nombre, factor de escala o altura objetivo, borde 9-slice)
            ExtractScaled("bigpannel", "panel_big", 1f / 8f, 12);
            ExtractScaled("smallpanel", "panel_small", 1f / 8f, 8);
            ExtractToHeight("heartLife", "icon_heart", 20); // corazón mecánico: con menos de 20 px pierde la forma
            ExtractToHeight("resumebutton", "button_resume", 32);
            ExtractToHeight("replaybutton", "button_replay", 32);
            ExtractToHeight("exitbutton", "button_exit", 32);
            ExtractToHeight("pausebutton", "button_pause", 24);

            SpriteExtractor.SavePng(
                SpriteExtractor.Resize("ArtSource/02_Environment/factory_background_zone_01.jpg", ProjectSetup.RefResolutionX, ProjectSetup.RefResolutionY),
                UiSprite("menu_background"));

            AssetDatabase.Refresh();
            foreach (var n in new[] { "panel_big", "panel_small", "icon_heart", "button_resume", "button_replay", "button_exit", "button_pause", "menu_background" })
                ConfigureSprite(UiSprite(n), SpriteAlignment.Center);
            SetBorder("panel_big", 12);
            SetBorder("panel_small", 8);
        }

        static void ExtractScaled(string file, string name, float scale, int border)
        {
            var sheet = SpriteExtractor.Load(Path.Combine(UiArt, file + ".png"), minGap: 10000);
            SpriteExtractor.SavePng(SpriteExtractor.Downscale(sheet, sheet.frames[0], scale), UiSprite(name));
        }

        static void ExtractToHeight(string file, string name, int height)
        {
            var sheet = SpriteExtractor.Load(Path.Combine(UiArt, file + ".png"), minGap: 10000);
            var f = sheet.frames[0];
            SpriteExtractor.SavePng(SpriteExtractor.Downscale(sheet, f, height / (float)f.height), UiSprite(name));
        }

        static void SetBorder(string name, int px)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(UiSprite(name));
            importer.spriteBorder = new Vector4(px, px, px, px);
            importer.SaveAndReimport();
        }

        static Sprite LoadSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(UiSprite(name));

        // ---------------- Menú principal ----------------

        static void BuildMainMenu()
        {
            var scene = EditorSceneManager.OpenScene(ProjectSetup.MainMenuScene, OpenSceneMode.Single);
            LoadUiAssets();
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "UI" || go.name == "EventSystem") Object.DestroyImmediate(go);

            CreateEventSystem();
            var canvas = CreateCanvas("UI");
            var menu = canvas.gameObject.AddComponent<MainMenu>();

            var bg = CreateImage(canvas.transform, "Background", LoadSprite("menu_background"), false);
            Stretch(bg.rectTransform);
            bg.raycastTarget = false;

            var band = CreateImage(canvas.transform, "TitleBand", null, false);
            band.color = new Color(ShadowC.r, ShadowC.g, ShadowC.b, 0.7f);
            Place(band.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 84f), new Vector2(640f, 72f));

            CreateText(canvas.transform, "Title", "BARREL ASCENT", 36, YellowC, new Vector2(0f, 92f), new Vector2(640f, 40f), shadow: true);
            CreateText(canvas.transform, "Subtitle", "ASCIENDE HASTA LA CIMA DE LA FÁBRICA", 9, LightGrayC, new Vector2(0f, 62f), new Vector2(640f, 12f));

            var panel = CreateImage(canvas.transform, "ButtonsPanel", LoadSprite("panel_big"), true);
            Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(200f, 96f));
            var play = CreateIconButton(panel.transform, "PlayButton", LoadSprite("button_resume"), "JUGAR", new Vector2(-44f, 8f));
            var exit = CreateIconButton(panel.transform, "ExitButton", LoadSprite("button_exit"), "SALIR", new Vector2(44f, 8f));
            UnityEventTools.AddPersistentListener(play.onClick, new UnityAction(menu.Play));
            UnityEventTools.AddPersistentListener(exit.onClick, new UnityAction(menu.Exit));

            var hintBand = CreateImage(canvas.transform, "HintBand", null, false);
            hintBand.color = new Color(ShadowC.r, ShadowC.g, ShadowC.b, 0.7f);
            Place(hintBand.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(640f, 20f));
            CreateText(canvas.transform, "Controls", "A/D MOVER · W/S ESCALERAS · ESPACIO SALTAR · ESC PAUSA", 9, LightGrayC,
                new Vector2(0f, 14f), new Vector2(640f, 12f), anchor: new Vector2(0.5f, 0f));

            var so = new SerializedObject(menu);
            so.FindProperty("firstSelected").objectReferenceValue = play;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ---------------- GameLevel ----------------

        static void BuildGameLevelUi()
        {
            var scene = EditorSceneManager.OpenScene(ProjectSetup.GameLevelScene, OpenSceneMode.Single);
            LoadUiAssets();
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "UI" || go.name == "EventSystem") Object.DestroyImmediate(go);

            var gm = Object.FindFirstObjectByType<GameManager>() ?? throw new InvalidOperationException("Falta GameManager (Fase 5).");
            var lm = Object.FindFirstObjectByType<LevelManager>();

            CreateEventSystem();
            var canvas = CreateCanvas("UI");
            var ui = canvas.gameObject.AddComponent<UIManager>();

            // HUD -------------------------------------------------------------
            var hud = CreateRect(canvas.transform, "HUD");
            Stretch(hud);

            var scorePanel = CreateImage(hud, "ScorePanel", LoadSprite("panel_small"), true);
            Place(scorePanel.rectTransform, new Vector2(0f, 1f), new Vector2(84f, -20f), new Vector2(152f, 28f));
            CreateText(scorePanel.transform, "Label", "PUNTOS", 9, LightGrayC, new Vector2(-34f, 0f), new Vector2(60f, 12f));
            var score = CreateText(scorePanel.transform, "Score", "000000", 18, YellowC, new Vector2(30f, 0f), new Vector2(80f, 16f));

            var zonePanel = CreateImage(hud, "ZonePanel", LoadSprite("panel_small"), true);
            Place(zonePanel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(84f, 28f));
            var zone = CreateText(zonePanel.transform, "Zone", "ZONA 1", 9, LightGrayC, Vector2.zero, new Vector2(80f, 12f));

            var livesPanel = CreateImage(hud, "LivesPanel", LoadSprite("panel_small"), true);
            Place(livesPanel.rectTransform, new Vector2(1f, 1f), new Vector2(-84f, -20f), new Vector2(92f, 28f));
            var hearts = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                hearts[i] = CreateImage(livesPanel.transform, $"Heart{i + 1}", LoadSprite("icon_heart"), false);
                Place(hearts[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-25f + i * 25f, 0f), hearts[i].rectTransform.sizeDelta);
            }

            var pauseButton = CreateIconButton(hud, "PauseButton", LoadSprite("button_pause"), null, Vector2.zero);
            Place((RectTransform)pauseButton.transform, new Vector2(1f, 1f), new Vector2(-20f, -20f), ((RectTransform)pauseButton.transform).sizeDelta);
            pauseButton.navigation = new Navigation { mode = Navigation.Mode.None };
            UnityEventTools.AddPersistentListener(pauseButton.onClick, new UnityAction(ui.OnPauseButton));

            var bannerImage = CreateImage(hud, "ZoneBanner", LoadSprite("panel_small"), true);
            Place(bannerImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -64f), new Vector2(280f, 28f));
            var bannerGroup = bannerImage.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.alpha = 0f;
            bannerGroup.interactable = false;
            bannerGroup.blocksRaycasts = false;
            var bannerText = CreateText(bannerImage.transform, "Text", "ZONA 1 · BASE INDUSTRIAL", 9, YellowC, Vector2.zero, new Vector2(270f, 12f));

            // Paneles ---------------------------------------------------------
            var pause = CreatePanel(canvas.transform, "PausePanel", "PAUSA", YellowC, 27);
            CreateText(pause.panel, "Hint", "ESC PARA CONTINUAR", 9, LightGrayC, new Vector2(0f, 18f), new Vector2(300f, 12f));
            var resume = CreateIconButton(pause.panel, "ResumeButton", LoadSprite("button_resume"), "REANUDAR", new Vector2(-80f, -36f));
            var restartP = CreateIconButton(pause.panel, "RestartButton", LoadSprite("button_replay"), "REINICIAR", new Vector2(0f, -36f));
            var menuP = CreateIconButton(pause.panel, "MenuButton", LoadSprite("button_exit"), "MENÚ", new Vector2(80f, -36f));
            UnityEventTools.AddPersistentListener(resume.onClick, new UnityAction(ui.OnResumeButton));
            UnityEventTools.AddPersistentListener(restartP.onClick, new UnityAction(ui.OnRestartButton));
            UnityEventTools.AddPersistentListener(menuP.onClick, new UnityAction(ui.OnMainMenuButton));

            var over = CreatePanel(canvas.transform, "GameOverPanel", "GAME OVER", RedC, 27);
            CreateText(over.panel, "Label", "PUNTUACIÓN", 9, LightGrayC, new Vector2(0f, 24f), new Vector2(300f, 12f));
            var overScore = CreateText(over.panel, "Score", "000000", 18, YellowC, new Vector2(0f, 8f), new Vector2(300f, 16f));
            CreateText(over.panel, "Hint", "PULSA R PARA REINICIAR", 9, LightGrayC, new Vector2(0f, -12f), new Vector2(300f, 12f));
            var restartO = CreateIconButton(over.panel, "RestartButton", LoadSprite("button_replay"), "REINICIAR", new Vector2(-44f, -50f));
            var menuO = CreateIconButton(over.panel, "MenuButton", LoadSprite("button_exit"), "MENÚ", new Vector2(44f, -50f));
            UnityEventTools.AddPersistentListener(restartO.onClick, new UnityAction(ui.OnRestartButton));
            UnityEventTools.AddPersistentListener(menuO.onClick, new UnityAction(ui.OnMainMenuButton));

            var win = CreatePanel(canvas.transform, "VictoryPanel", "¡CIMA ALCANZADA!", YellowC, 18);
            CreateText(win.panel, "Label", "PUNTUACIÓN FINAL", 9, LightGrayC, new Vector2(0f, 24f), new Vector2(300f, 12f));
            var winScore = CreateText(win.panel, "Score", "000000", 18, YellowC, new Vector2(0f, 8f), new Vector2(300f, 16f));
            CreateText(win.panel, "Hint", "PULSA R PARA JUGAR DE NUEVO", 9, LightGrayC, new Vector2(0f, -12f), new Vector2(300f, 12f));
            var restartW = CreateIconButton(win.panel, "RestartButton", LoadSprite("button_replay"), "REINICIAR", new Vector2(-44f, -50f));
            var menuW = CreateIconButton(win.panel, "MenuButton", LoadSprite("button_exit"), "MENÚ", new Vector2(44f, -50f));
            UnityEventTools.AddPersistentListener(restartW.onClick, new UnityAction(ui.OnRestartButton));
            UnityEventTools.AddPersistentListener(menuW.onClick, new UnityAction(ui.OnMainMenuButton));

            // Referencias -----------------------------------------------------
            var so = new SerializedObject(ui);
            so.FindProperty("gameManager").objectReferenceValue = gm;
            so.FindProperty("levelManager").objectReferenceValue = lm;
            so.FindProperty("scoreText").objectReferenceValue = score;
            var heartsProp = so.FindProperty("hearts");
            heartsProp.arraySize = hearts.Length;
            for (int i = 0; i < hearts.Length; i++) heartsProp.GetArrayElementAtIndex(i).objectReferenceValue = hearts[i];
            so.FindProperty("zoneText").objectReferenceValue = zone;
            so.FindProperty("pauseButton").objectReferenceValue = pauseButton;
            so.FindProperty("zoneBanner").objectReferenceValue = bannerGroup;
            so.FindProperty("zoneBannerText").objectReferenceValue = bannerText;
            so.FindProperty("pausePanel").objectReferenceValue = pause.root;
            so.FindProperty("pauseFirstSelected").objectReferenceValue = resume;
            so.FindProperty("gameOverPanel").objectReferenceValue = over.root;
            so.FindProperty("gameOverScoreText").objectReferenceValue = overScore;
            so.FindProperty("gameOverFirstSelected").objectReferenceValue = restartO;
            so.FindProperty("victoryPanel").objectReferenceValue = win.root;
            so.FindProperty("victoryScoreText").objectReferenceValue = winScore;
            so.FindProperty("victoryFirstSelected").objectReferenceValue = restartW;
            so.ApplyModifiedPropertiesWithoutUndo();

            pause.root.SetActive(false);
            over.root.SetActive(false);
            win.root.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        // ---------------- Utilidades de UI ----------------

        static void LoadUiAssets()
        {
            // Abrir una escena descarga los assets sin referencias: se recarga la fuente después.
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PixelFontBuilder.FontPath);
            if (font == null) throw new InvalidOperationException("Falta la fuente BarrelPixel.");
        }

        static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();

            // Acciones de UI por defecto del paquete Input System (asset persistente, válido en batchmode).
            const string path = "Packages/com.unity.inputsystem/InputSystem/Plugins/PlayerInput/DefaultInputActions.inputactions";
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.InputSystem.InputActionAsset>(path)
                        ?? throw new InvalidOperationException("No se encontró DefaultInputActions del Input System.");
            UnityEngine.InputSystem.InputActionReference Ref(string action)
            {
                foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                    if (o is UnityEngine.InputSystem.InputActionReference r && r.action != null &&
                        $"{r.action.actionMap.name}/{r.action.name}" == action)
                        return r;
                throw new InvalidOperationException($"Falta la acción {action}");
            }
            module.actionsAsset = asset;
            module.point = Ref("UI/Point");
            module.leftClick = Ref("UI/Click");
            module.rightClick = Ref("UI/RightClick");
            module.middleClick = Ref("UI/MiddleClick");
            module.scrollWheel = Ref("UI/ScrollWheel");
            module.move = Ref("UI/Navigate");
            module.submit = Ref("UI/Submit");
            module.cancel = Ref("UI/Cancel");
        }

        static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name) { layer = LayerMask.NameToLayer("UI") };
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ProjectSetup.RefResolutionX, ProjectSetup.RefResolutionY);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            scaler.referencePixelsPerUnit = ProjectSetup.PixelsPerUnit;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = LayerMask.NameToLayer("UI") };
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform rt, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
        }

        static Image CreateImage(Transform parent, string name, Sprite sprite, bool sliced)
        {
            var rt = CreateRect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.raycastTarget = false;
            // 1 píxel de sprite = 1 unidad de referencia. (SetNativeSize no sirve aquí: en el editor el Canvas
            // aún no tiene aplicado referencePixelsPerUnit = 32 del CanvasScaler y saldría ×3.)
            if (sprite != null && !sliced) img.rectTransform.sizeDelta = sprite.rect.size;
            return img;
        }

        static TextMeshProUGUI CreateText(Transform parent, string name, string text, int size, Color color,
                                          Vector2 position, Vector2 box, bool shadow = false, Vector2? anchor = null)
        {
            var a = anchor ?? new Vector2(0.5f, 0.5f);
            if (shadow)
            {
                var s = CreateText(parent, name + "Shadow", text, size, ShadowC, position + new Vector2(size / 9f, -size / 9f), box, false, a);
                s.raycastTarget = false;
            }
            var rt = CreateRect(parent, name);
            Place(rt, a, position, box);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = font;
            tmp.fontSharedMaterial = font.material;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.text = text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.raycastTarget = false;
            return tmp;
        }

        static Button CreateIconButton(Transform parent, string name, Sprite icon, string label, Vector2 position)
        {
            var img = CreateImage(parent, name, icon, false);
            img.raycastTarget = true;
            Place(img.rectTransform, new Vector2(0.5f, 0.5f), position, img.rectTransform.sizeDelta);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = YellowC;
            colors.selectedColor = YellowC;
            colors.pressedColor = OrangeC;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.4f);
            colors.fadeDuration = 0f;
            button.colors = colors;
            if (!string.IsNullOrEmpty(label))
            {
                float h = img.rectTransform.sizeDelta.y;
                CreateText(img.transform, "Label", label, 9, LightGrayC, new Vector2(0f, -h * 0.5f - 9f), new Vector2(90f, 12f));
            }
            return button;
        }

        static (GameObject root, Transform panel) CreatePanel(Transform canvas, string name, string title, Color titleColor, int titleSize)
        {
            var overlay = CreateImage(canvas, name, null, false);
            overlay.color = new Color(ShadowC.r, ShadowC.g, ShadowC.b, 0.8f);
            overlay.raycastTarget = true; // bloquea clics al juego
            Stretch(overlay.rectTransform);

            var panel = CreateImage(overlay.transform, "Panel", LoadSprite("panel_big"), true);
            Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 180f));
            CreateText(panel.transform, "Title", title, titleSize, titleColor, new Vector2(0f, 54f), new Vector2(310f, 30f), shadow: true);
            return (overlay.gameObject, panel.transform);
        }
    }
}

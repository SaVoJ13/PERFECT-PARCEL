using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>
    /// Q-05a: глибина і фон без нової графіки. У USS 6.3 немає drop-shadow, тому тінь, відблиск, градієнт і віньєтка -
    /// окремі елементи (QB-ALL-06/07/08). Текстури створюються кодом (1xN і 64x64 пікселі), ресурсів немає.
    /// Усі елементи не ловлять дотики (<see cref="PickingMode.Ignore"/>).
    /// </summary>
    public static class UiDecor
    {
        public const string ShadowName = "ks-shadow";
        public const string GlossName = "ks-gloss";
        public const string GradientName = "ks-gradient";
        public const string VignetteName = "ks-vignette";

        /// <summary>QB-ALL-06: зсув тіні-основи кнопки, px референсу.</summary>
        public const float ButtonShadowOffset = 8f;

        /// <summary>QB-ALL-06: відблиск займає верхні 30-40% висоти кнопки.</summary>
        public const float GlossHeightPercent = 32f;

        /// <summary>QB-ALL-06: світліший на 10-15%.</summary>
        public const float GlossAlpha = 0.12f;

        /// <summary>QB-ALL-06: тінь на 15% темніша за лицьову грань.</summary>
        public const float ShadowDarken = 0.15f;

        /// <summary>QB-ALL-07: непрозорість тіні панелі 25-35%.</summary>
        public const float PanelShadowAlpha = 0.30f;

        /// <summary>QB-ALL-07: зсув тіні панелі 6-10 px.</summary>
        public const float PanelShadowOffset = 8f;

        /// <summary>QB-ALL-08: краї темніші на 15-25%.</summary>
        public const float VignetteMaxAlpha = 0.22f;

        /// <summary>Градієнт фону: зверху світліший, знизу темніший (QB-ALL-07: різниця 6-10% яскравості).</summary>
        public const float GradientTopLightAlpha = 0.05f;
        public const float GradientBottomDarkAlpha = 0.12f;

        private const int GradientHeight = 64;
        private const int VignetteSize = 64;
        private const float VignetteInner = 0.45f;
        private const float ShadowRadius = 20f;

        private static Texture2D _gradient;
        private static Texture2D _vignette;

        /// <summary>Темніша на <see cref="ShadowDarken"/> версія кольору (непрозорість зберігається).</summary>
        public static Color Darken(Color color)
        {
            float k = 1f - ShadowDarken;
            return new Color(color.r * k, color.g * k, color.b * k, color.a);
        }

        /// <summary>Непрозорість віньєтки в точці (x, y у 0..1): 0 у центрі, <see cref="VignetteMaxAlpha"/> у кутах.</summary>
        public static float VignetteAlpha(float x, float y)
        {
            float dx = (x - 0.5f) * 2f;
            float dy = (y - 0.5f) * 2f;
            float d = Mathf.Clamp01(Mathf.Sqrt((dx * dx) + (dy * dy)) / 1.4142135f);
            float t = Mathf.Clamp01((d - VignetteInner) / (1f - VignetteInner));
            return VignetteMaxAlpha * (t * t * (3f - (2f * t)));
        }

        /// <summary>Колір градієнта на висоті t (0 - верх, 1 - низ): світла плівка зверху, темна знизу.</summary>
        public static Color GradientAt(float t)
        {
            t = Mathf.Clamp01(t);
            return Color.Lerp(
                new Color(1f, 1f, 1f, GradientTopLightAlpha),
                new Color(0f, 0f, 0f, GradientBottomDarkAlpha),
                t);
        }

        /// <summary>Градієнт і віньєтка на повний екран (під вмістом, якщо додати першими).</summary>
        public static void AddBackdrop(VisualElement parent, bool gradient = true)
        {
            if (parent == null)
            {
                return;
            }

            if (gradient)
            {
                parent.Insert(0, Fill(GradientName, GradientTexture()));
            }

            parent.Insert(gradient ? 1 : 0, Fill(VignetteName, VignetteTexture()));
        }

        /// <summary>Лише віньєтка (для екрана з власним фоном-ілюстрацією).</summary>
        public static void AddVignette(VisualElement parent)
        {
            AddBackdrop(parent, false);
        }

        /// <summary>Тінь-основа і відблиск кнопки (QB-ALL-06). Повторний виклик нічого не додає.</summary>
        public static void AddButtonDepth(VisualElement button, Color face)
        {
            if (button == null || button.Q(ShadowName) != null)
            {
                return;
            }

            var shadow = new VisualElement { name = ShadowName, pickingMode = PickingMode.Ignore };
            shadow.style.position = Position.Absolute;
            shadow.style.left = ShadowRadius / 2f;
            shadow.style.right = ShadowRadius / 2f;
            shadow.style.bottom = -ButtonShadowOffset;
            shadow.style.height = ButtonShadowOffset * 2f;
            shadow.style.backgroundColor = Darken(Darken(face));
            shadow.style.borderBottomLeftRadius = ShadowRadius;
            shadow.style.borderBottomRightRadius = ShadowRadius;
            button.Insert(0, shadow);

            var gloss = new VisualElement { name = GlossName, pickingMode = PickingMode.Ignore };
            gloss.style.position = Position.Absolute;
            gloss.style.left = ShadowRadius / 2f;
            gloss.style.right = ShadowRadius / 2f;
            gloss.style.top = 4f;
            gloss.style.height = Length.Percent(GlossHeightPercent);
            gloss.style.backgroundColor = new Color(1f, 1f, 1f, GlossAlpha);
            gloss.style.borderTopLeftRadius = ShadowRadius;
            gloss.style.borderTopRightRadius = ShadowRadius;
            gloss.style.borderBottomLeftRadius = ShadowRadius / 2f;
            gloss.style.borderBottomRightRadius = ShadowRadius / 2f;
            button.Insert(1, gloss);
        }

        /// <summary>Тінь панелі (QB-ALL-07): темна смуга під нижнім краєм, непрозорість <see cref="PanelShadowAlpha"/>.</summary>
        public static void AddPanelShadow(VisualElement panel)
        {
            if (panel == null || panel.Q(ShadowName) != null)
            {
                return;
            }

            var shadow = new VisualElement { name = ShadowName, pickingMode = PickingMode.Ignore };
            shadow.style.position = Position.Absolute;
            shadow.style.left = PanelShadowOffset;
            shadow.style.right = PanelShadowOffset;
            shadow.style.bottom = -PanelShadowOffset;
            shadow.style.height = PanelShadowOffset * 2f;
            shadow.style.backgroundColor = new Color(0f, 0f, 0f, PanelShadowAlpha);
            shadow.style.borderBottomLeftRadius = ShadowRadius;
            shadow.style.borderBottomRightRadius = ShadowRadius;
            panel.Insert(0, shadow);
        }

        private static VisualElement Fill(string name, Texture2D texture)
        {
            var element = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            element.style.position = Position.Absolute;
            element.style.left = 0;
            element.style.top = 0;
            element.style.right = 0;
            element.style.bottom = 0;
            element.style.backgroundImage = new StyleBackground(texture);
            element.style.backgroundSize = new BackgroundSize(Length.Percent(100), Length.Percent(100));
            return element;
        }

        private static Texture2D GradientTexture()
        {
            if (_gradient == null)
            {
                _gradient = new Texture2D(1, GradientHeight, TextureFormat.RGBA32, false)
                {
                    name = GradientName,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                for (int y = 0; y < GradientHeight; y++)
                {
                    // Рядок 0 текстури - низ, тому t рахується від верху.
                    float t = 1f - (y / (GradientHeight - 1f));
                    _gradient.SetPixel(0, y, GradientAt(t));
                }

                _gradient.Apply(false, true);
            }

            return _gradient;
        }

        private static Texture2D VignetteTexture()
        {
            if (_vignette == null)
            {
                _vignette = new Texture2D(VignetteSize, VignetteSize, TextureFormat.RGBA32, false)
                {
                    name = VignetteName,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave,
                };
                for (int y = 0; y < VignetteSize; y++)
                {
                    for (int x = 0; x < VignetteSize; x++)
                    {
                        float a = VignetteAlpha(x / (VignetteSize - 1f), y / (VignetteSize - 1f));
                        _vignette.SetPixel(x, y, new Color(0f, 0f, 0f, a));
                    }
                }

                _vignette.Apply(false, true);
            }

            return _vignette;
        }
    }
}

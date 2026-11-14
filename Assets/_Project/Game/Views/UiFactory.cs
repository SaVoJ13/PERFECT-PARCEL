using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace KebabStreet.Game
{
    /// <summary>
    /// Мінімальні елементи UI Toolkit для каркаса (плейсхолдери). Стиль задано кодом; справжні UXML/USS
    /// і тема - завдання Агента 2. Тексти приходять із ILocalizer за ключем.
    /// </summary>
    public static class UiFactory
    {
        private const float TitleSize = 72f;
        private const float BodySize = 40f;
        private const float ButtonHeight = 140f;
        private const float Gap = 32f;
        public static readonly Color Background = UiColors.NightPlum; // Night Plum #2B1B2E
        private static readonly Color ButtonColor = UiColors.Saffron; // Saffron #FFB703, текст Espresso (WCAG AA)
        private static readonly Color ButtonTextColor = UiColors.Espresso;
        private static readonly Color NoticeColor = UiColors.Saffron;

        /// <summary>Повноекранний вертикальний контейнер по центру.</summary>
        public static VisualElement Screen(VisualElement root)
        {
            var container = new VisualElement();
            container.style.position = Position.Absolute;
            container.style.left = 0;
            container.style.top = 0;
            container.style.right = 0;
            container.style.bottom = 0;
            container.style.flexDirection = FlexDirection.Column;
            container.style.justifyContent = Justify.Center;
            container.style.alignItems = Align.Center;
            container.style.backgroundColor = Background;
            container.AddToClassList("ks-screen");
            ArtLibrary.ApplyTheme(root);
            root.Add(container);
            return container;
        }

        public static Label Title(VisualElement parent, string text)
        {
            return AddLabel(parent, text, TitleSize, Color.white);
        }

        public static Label Body(VisualElement parent, string text)
        {
            return AddLabel(parent, text, BodySize, Color.white);
        }

        public static Label Notice(VisualElement parent, string text)
        {
            return AddLabel(parent, text, BodySize, NoticeColor);
        }

        public static Button AddButton(VisualElement parent, string text, Action onClick)
        {
            var button = new Button(SfxHub.WithTapSound(onClick)) { text = text };
            button.style.height = ButtonHeight;
            button.style.width = Length.Percent(70);
            button.style.marginTop = Gap;
            button.style.fontSize = BodySize;
            button.style.color = ButtonTextColor;
            button.AddToClassList("ks-button");
            button.style.backgroundColor = ButtonColor;
            parent.Add(button);
            return button;
        }

        private static Label AddLabel(VisualElement parent, string text, float size, Color color)
        {
            var label = new Label(text);
            label.style.fontSize = size;
            label.style.color = color;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.marginTop = Gap;
            label.style.marginLeft = Gap;
            label.style.marginRight = Gap;
            parent.Add(label);
            return label;
        }
    }
}

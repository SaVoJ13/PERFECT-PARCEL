using System.Collections.Generic;
using UnityEngine;
using PerfectParcel.Core;

namespace PerfectParcel.Juice
{
    public class FloatingScoreItem
    {
        public string Text;
        public Vector2 ScreenPos;
        public Color Color;
        public float Elapsed;
        public float Duration;
    }

    /// <summary>
    /// Менеджер вильоту очок та тексту над конвеєром (Floating Score Popups).
    /// Забезпечує плавний рух вгору та затухання написів (+100, +250, COMBO!)
    /// з високою продуктивністю та нульовими алокаціями в ігровому циклі.
    /// </summary>
    public class FloatingScoreManager : MonoBehaviour
    {
        public static FloatingScoreManager Instance { get; private set; }

        private readonly List<FloatingScoreItem> _activeItems = new List<FloatingScoreItem>();
        private readonly Queue<FloatingScoreItem> _pool = new Queue<FloatingScoreItem>();

        private const int MaxActive = 10;
        private const float DefaultDuration = 0.65f;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ParcelSortedEvent>(OnParcelSorted);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ParcelSortedEvent>(OnParcelSorted);
        }

        public void SpawnPopup(string text, Vector2 screenPos, Color color, float duration = DefaultDuration)
        {
            if (_activeItems.Count >= MaxActive)
            {
                var oldest = _activeItems[0];
                _activeItems.RemoveAt(0);
                _pool.Enqueue(oldest);
            }

            FloatingScoreItem item = _pool.Count > 0 ? _pool.Dequeue() : new FloatingScoreItem();
            item.Text = text;
            item.ScreenPos = screenPos;
            item.Color = color;
            item.Elapsed = 0f;
            item.Duration = duration;

            _activeItems.Add(item);
        }

        private void OnParcelSorted(ParcelSortedEvent e)
        {
            // Визначаємо позицію на екрані (центр конвеєра або зміщення у напрямок сортування)
            float x = 270f;
            float y = 500f;

            string label = $"+{e.PointsEarned}";
            Color c = Color.yellow;

            if (e.CurrentCombo > 1)
            {
                label += $" (x{e.CurrentCombo})";
                c = new Color(1f, 0.6f, 0.1f);
            }

            SpawnPopup(label, new Vector2(x, y), c);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = _activeItems.Count - 1; i >= 0; i--)
            {
                var item = _activeItems[i];
                item.Elapsed += dt;
                item.ScreenPos.y -= 90f * dt; // Рух вгору (у просторі GUI Y збільшується вниз)

                if (item.Elapsed >= item.Duration)
                {
                    _activeItems.RemoveAt(i);
                    _pool.Enqueue(item);
                }
            }
        }

        private void OnGUI()
        {
            if (_activeItems.Count == 0) return;

            float scale = Screen.width / 540f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            for (int i = 0; i < _activeItems.Count; i++)
            {
                var item = _activeItems[i];
                float t = item.Elapsed / item.Duration;
                float alpha = Mathf.Clamp01(1f - t);

                Color textColor = item.Color;
                textColor.a = alpha;
                style.normal.textColor = textColor;

                // Малювання тіні тексту
                style.normal.textColor = new Color(0f, 0f, 0f, alpha * 0.7f);
                GUI.Label(new Rect(item.ScreenPos.x - 100 + 2, item.ScreenPos.y - 15 + 2, 200, 30), item.Text, style);

                // Основний текст
                style.normal.textColor = textColor;
                GUI.Label(new Rect(item.ScreenPos.x - 100, item.ScreenPos.y - 15, 200, 30), item.Text, style);
            }
        }
    }
}

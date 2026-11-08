using UnityEngine;
using PerfectParcel.Conveyor;
using PerfectParcel.Core;
using PerfectParcel.Sorting;

namespace PerfectParcel.UI
{
    /// <summary>
    /// Інтерактивний HUD для тестування прототипу Phase 1.
    /// Підтримує як стандартний Canvas, так і автоматичний OnGUI fallback
    /// для миттєвої перевірки сортування без обов'язкового створення префабів Canvas.
    /// </summary>
    public class PrototypeHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SwipeInputHandler inputHandler;

        private int _score = 0;
        private int _coins = 0;
        private int _sortedCount = 0;
        private int _targetCount = 15;
        private int _lives = 3;
        private int _combo = 0;
        private GameState _gameState = GameState.Playing;

        private void OnEnable()
        {
            EventBus.Subscribe<ScoreChangedEvent>(OnScoreChanged);
            EventBus.Subscribe<LivesChangedEvent>(OnLivesChanged);
            EventBus.Subscribe<ParcelSortedEvent>(OnParcelSorted);
            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ScoreChangedEvent>(OnScoreChanged);
            EventBus.Unsubscribe<LivesChangedEvent>(OnLivesChanged);
            EventBus.Unsubscribe<ParcelSortedEvent>(OnParcelSorted);
            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
        }

        private void OnScoreChanged(ScoreChangedEvent e)
        {
            _score = e.CurrentScore;
            _coins = e.CurrentCoins;
            _sortedCount = e.SortedCount;
            _targetCount = e.TargetCount;
        }

        private void OnLivesChanged(LivesChangedEvent e)
        {
            _lives = e.CurrentLives;
        }

        private void OnParcelSorted(ParcelSortedEvent e)
        {
            _combo = e.CurrentCombo;
        }

        private void OnGameStateChanged(GameStateChangedEvent e)
        {
            _gameState = e.NewState;
        }

        private void OnGUI()
        {
            // Масштабування GUI під екран
            float scale = Screen.width / 540f;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(scale, scale, 1f));

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };

            var headerStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 16,
                normal = { textColor = Color.yellow }
            };

            // Верхня панель HUD
            GUILayout.BeginArea(new Rect(15, 15, 510, 125), headerStyle);
            GUILayout.BeginHorizontal();
            int currentLvl = GameManager.Instance != null ? GameManager.Instance.CurrentLevelIndex : 1;
            GUILayout.Label($"📦 РІВЕНЬ {currentLvl}", labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"ЖИТТЯ: {GetHeartsString(_lives)}", labelStyle);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label($"ПРОГРЕС: {_sortedCount} / {_targetCount}", labelStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"ОЧКИ: {_score} (x{_combo}) | МОНЕТИ: {_coins}", labelStyle);
            GUILayout.EndHorizontal();
            GUILayout.EndArea();

            // Нижня панель: Кнопки доступності для сортування (One-Thumb кнопки)
            if (_gameState == GameState.Playing)
            {
                GUILayout.BeginArea(new Rect(15, Screen.height / scale - 95, 510, 80));
                GUILayout.BeginHorizontal();

                GUI.backgroundColor = new Color(0.2f, 0.5f, 1f); // 🔵 Синій
                if (GUILayout.Button("⬅️ ЛІВО (Синій)", GUILayout.Height(70)))
                {
                    inputHandler?.TriggerDirectionFromUI(SortingDirection.Left);
                }

                GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f); // 🟢 Зелений
                if (GUILayout.Button("⬆️ ПРЯМО (Зелений)", GUILayout.Height(70)))
                {
                    inputHandler?.TriggerDirectionFromUI(SortingDirection.Straight);
                }

                GUI.backgroundColor = new Color(1f, 0.3f, 0.3f); // 🔴 Червоний
                if (GUILayout.Button("➡️ ПРАВО (Червоний)", GUILayout.Height(70)))
                {
                    inputHandler?.TriggerDirectionFromUI(SortingDirection.Right);
                }

                GUI.backgroundColor = Color.white;
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }

            // Модальні вікна завершення гри
            if (_gameState == GameState.GameOver)
            {
                ShowEndGamePopup("ПОРАЗКА!", "Усі життя вичерпано або посилку розбито.", Color.red, false);
            }
            else if (_gameState == GameState.LevelComplete)
            {
                int currentLvl = GameManager.Instance != null ? GameManager.Instance.CurrentLevelIndex : 1;
                ShowEndGamePopup("ПЕРЕМОГА!", $"Рівень {currentLvl} пройдено!\nРахунок: {_score} | Монети: {_coins}", Color.green, true);
            }
        }

        private void ShowEndGamePopup(string title, string message, Color color, bool isVictory)
        {
            float scale = Screen.width / 540f;
            float boxWidth = 440;
            float boxHeight = 250;
            float x = (540 - boxWidth) / 2;
            float y = (Screen.height / scale - boxHeight) / 2;

            var boxStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                normal = { textColor = color }
            };

            GUI.Box(new Rect(x, y, boxWidth, boxHeight), title, boxStyle);

            var textStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(x + 20, y + 45, boxWidth - 40, 70), message, textStyle);

            if (isVictory)
            {
                if (GUI.Button(new Rect(x + 30, y + 130, 180, 55), "СПРОБУВАТИ ЗНОВУ"))
                {
                    int currentLvl = GameManager.Instance != null ? GameManager.Instance.CurrentLevelIndex : 1;
                    GameManager.Instance?.StartLevel(currentLvl);
                }

                if (GUI.Button(new Rect(x + 230, y + 130, 180, 55), "НАСТУПНИЙ ➔"))
                {
                    int nextLvl = (GameManager.Instance != null ? GameManager.Instance.CurrentLevelIndex : 1) + 1;
                    GameManager.Instance?.StartLevel(nextLvl);
                }
            }
            else
            {
                if (GUI.Button(new Rect(x + 70, y + 140, boxWidth - 140, 55), "СПРОБУВАТИ ЗНОВУ"))
                {
                    int currentLvl = GameManager.Instance != null ? GameManager.Instance.CurrentLevelIndex : 1;
                    GameManager.Instance?.StartLevel(currentLvl);
                }
            }
        }

        private string GetHeartsString(int lives)
        {
            string s = "";
            for (int i = 0; i < lives; i++) s += "❤️ ";
            return string.IsNullOrEmpty(s) ? "💔 (0)" : s;
        }
    }
}

using UnityEngine;

namespace KebabStreet.Game
{
    /// <summary>
    /// Вібрація Android (Q-05i, QB-ALL-14): <c>View.performHapticFeedback</c> з системними константами
    /// <c>HapticFeedbackConstants</c> - коротка «клік»-вібрація, а не довга одноразова. Без SDK. Поважає системне
    /// налаштування «вібровідгук дотику» (прапорці не змінюються). Справжній клас є лише в Android-збірці на пристрої;
    /// в редакторі й тестах <see cref="Create"/> повертає заглушку і успіху не імітує.
    /// </summary>
    public static class AndroidHapticsService
    {
        /// <summary>Android: <c>HapticFeedbackConstants.VIRTUAL_KEY</c> (легкий «клік»).</summary>
        public const int ConstantVirtualKey = 1;

        /// <summary>Android: <c>HapticFeedbackConstants.LONG_PRESS</c> (сильніший, для API до 30).</summary>
        public const int ConstantLongPress = 0;

        /// <summary>Android 11+ (API 30): <c>HapticFeedbackConstants.CONFIRM</c>.</summary>
        public const int ConstantConfirm = 16;

        /// <summary>Перший API з константою CONFIRM.</summary>
        public const int ConfirmMinApi = 30;

        /// <summary>Константа для відгуку; <paramref name="apiLevel"/> - версія Android пристрою.</summary>
        public static int ConstantFor(HapticKind kind, int apiLevel)
        {
            if (kind == HapticKind.Strong)
            {
                return apiLevel >= ConfirmMinApi ? ConstantConfirm : ConstantLongPress;
            }

            return ConstantVirtualKey;
        }

        /// <summary>Справжня реалізація на Android-пристрої, інакше заглушка.</summary>
        public static IHapticsService Create()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return new Device();
#else
            return new NullHapticsService();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private sealed class Device : IHapticsService
        {
            private AndroidJavaObject _view;
            private int _api = -1;
            private bool _failed;

            public void Perform(HapticKind kind)
            {
                if (_failed || kind == HapticKind.None)
                {
                    return;
                }

                try
                {
                    if (_view == null)
                    {
                        using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                        using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        {
                            _api = version.GetStatic<int>("SDK_INT");
                            _view = activity.Call<AndroidJavaObject>("getWindow").Call<AndroidJavaObject>("getDecorView");
                        }
                    }

                    _view.Call<bool>("performHapticFeedback", ConstantFor(kind, _api));
                }
                catch (System.Exception e)
                {
                    _failed = true; // без вібрації, але без падіння гри
                    Debug.LogWarning("Haptics disabled: " + e.GetType().Name);
                }
            }
        }
#endif
    }
}

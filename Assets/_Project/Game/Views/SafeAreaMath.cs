namespace KebabStreet.Game
{
    /// <summary>
    /// Відступи безпечної зони екрана в логічних пікселях UI Toolkit (S1-17). Чиста математика без Unity:
    /// <c>Screen.safeArea</c> дає прямокутник у фізичних пікселях (початок знизу ліворуч), а панель масштабується
    /// (ScaleWithScreenSize), тож відступ ділиться на масштаб = фізична висота / логічна висота.
    /// </summary>
    public static class SafeAreaMath
    {
        /// <summary>Відступ зверху: від верху екрана до верху безпечної зони, у логічних пікселях.</summary>
        public static float Top(float screenHeight, float safeY, float safeHeight, float scale)
        {
            return NonNegative((screenHeight - (safeY + safeHeight)) / Scale(scale));
        }

        /// <summary>Відступ знизу: від низу екрана до низу безпечної зони, у логічних пікселях.</summary>
        public static float Bottom(float safeY, float scale)
        {
            return NonNegative(safeY / Scale(scale));
        }

        /// <summary>Масштаб панелі: фізична висота екрана / логічна висота кореня; 1, якщо розкладку ще не пораховано.</summary>
        public static float ScaleOf(float screenHeight, float logicalHeight)
        {
            return logicalHeight > 0f && screenHeight > 0f ? screenHeight / logicalHeight : 1f;
        }

        private static float Scale(float scale)
        {
            return scale > 0f ? scale : 1f;
        }

        private static float NonNegative(float value)
        {
            return value > 0f ? value : 0f;
        }
    }
}

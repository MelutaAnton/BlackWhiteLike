#if UNITY_EDITOR
using UnityEngine;
#endif

namespace BlackWhiteLike.Services.Haptics
{
    /// <summary>
    /// No-op реализация <see cref="IHapticsService"/> — регистрируется безусловно во всех сценах
    /// (шаг 9, §19.9), пока настоящей вибрации в проекте нет. В редакторе, при выставленном
    /// <see cref="LogCalls"/>, пишет в консоль каждый вызов — способ проверить точки вызова
    /// (захват/бросок рукой, релиз каста, победа) без физического устройства с вибромотором.
    /// </summary>
    public sealed class NullHapticsService : IHapticsService
    {
#if UNITY_EDITOR
        /// <summary>
        /// Флаг диагностики (по умолчанию выключен) — включать вручную в редакторе на время
        /// проверки точек вызова. Не сериализуется и не читается вне UNITY_EDITOR — на билде
        /// не оказывает влияния.
        /// </summary>
        public static bool LogCalls = false;
#endif

        public void LightTap() => Log(nameof(LightTap));

        public void MediumImpact() => Log(nameof(MediumImpact));

        public void Success() => Log(nameof(Success));

        private static void Log(string methodName)
        {
#if UNITY_EDITOR
            if (!LogCalls) return;
            Debug.Log($"[NullHapticsService] {methodName}()");
#endif
        }
    }
}

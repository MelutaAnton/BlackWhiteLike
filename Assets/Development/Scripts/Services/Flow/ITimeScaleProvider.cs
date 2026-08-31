namespace BlackWhiteLike.Services.Flow
{
    /// <summary>
    /// Единственная абстракция над <c>UnityEngine.Time.timeScale</c> (game-design §9, §15, шаг 5) —
    /// <c>PausedState</c> не обращается к UnityEngine.Time напрямую, тестируется без Unity.
    /// </summary>
    public interface ITimeScaleProvider
    {
        float TimeScale { get; set; }
    }
}

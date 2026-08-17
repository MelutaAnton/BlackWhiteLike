namespace BlackWhiteLike.Models
{
    /// <summary>
    /// Состояния игрового потока (Game Flow). Управляются через <see cref="Core.StateMachine.StateMachine{TKey}"/>
    /// с push-pop (см. game-design §9). Единственный источник значений — этот enum.
    /// </summary>
    public enum GameState
    {
        MainMenu,
        LevelSelect,
        LevelPlaying,
        LevelComplete,
        TempleMeta,
        Paused,
        WelcomeBack
    }
}

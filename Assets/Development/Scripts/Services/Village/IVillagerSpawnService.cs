namespace BlackWhiteLike.Services.Village
{
    /// <summary>
    /// Спавнит жителей уровня через <c>IVillagerFactory</c> (game-design §5, §6, шаг 3; Б3 ревью
    /// шага 3 — до этого сервиса <c>IVillagerFactory</c> не имел ни одного вызывающего, пуловый
    /// путь был мёртвым кодом).
    /// </summary>
    public interface IVillagerSpawnService
    {
        /// <summary>
        /// Спавнит <c>LevelConfig.VillagerCount</c> жителей по позициям
        /// <c>VillageLayoutConfig.VillagerSpawnPositions</c> (циклически, если позиций меньше, чем
        /// жителей). Повторный вызов без предварительного <see cref="DespawnAll"/> — no-op с warning.
        /// </summary>
        void SpawnInitialVillagers();

        /// <summary>Despawn'ит всех жителей, заспавненных этим сервисом, и чистит внутренний список.</summary>
        void DespawnAll();
    }
}

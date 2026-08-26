using System.Collections.Generic;
using BlackWhiteLike.Controllers.Village;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Factories.Village;
using UnityEngine;
using VContainer.Unity;

namespace BlackWhiteLike.Services.Village
{
    /// <summary>
    /// Реализация <see cref="IVillagerSpawnService"/> (Б3, ревью шага 3). Pure C#.
    /// </summary>
    /// <remarks>
    /// Регистрируется как <see cref="IStartable"/> (решение и обоснование — см. GameLifetimeScope):
    /// <c>Services/Level/LevelFlowService.cs</c> (game-design §12, шаг 5+) ещё не существует — он
    /// возьмёт на себя настоящий триггер "начался уровень" (вход в <c>GameState.LevelPlaying</c>).
    /// До тех пор, для единственной тестовой сцены проекта, старт контейнера — единственный
    /// доступный момент "уровень начался"; тот же приём уже используют GameBootstrap и
    /// PointerInputProvider (IStartable для EnhancedTouchSupport). TODO шаг 5+: заменить на явный
    /// вызов SpawnInitialVillagers() из LevelFlowService при входе в GameState.LevelPlaying, убрать
    /// IStartable отсюда.
    /// </remarks>
    public sealed class VillagerSpawnService : IVillagerSpawnService, IStartable
    {
        private readonly IVillagerFactory _villagerFactory;
        private readonly LevelConfig _levelConfig;

        private readonly List<VillagerController> _spawned = new List<VillagerController>();

        public VillagerSpawnService(IVillagerFactory villagerFactory, LevelConfig levelConfig)
        {
            _villagerFactory = villagerFactory;
            _levelConfig = levelConfig;
        }

        public void Start() => SpawnInitialVillagers();

        public void SpawnInitialVillagers()
        {
            if (_spawned.Count > 0)
            {
                Debug.LogWarning("[VillagerSpawnService] Жители уже заспавнены — повторный SpawnInitialVillagers проигнорирован (сначала DespawnAll).");
                return;
            }

            var layout = _levelConfig.Layout;
            if (layout == null)
            {
                Debug.LogWarning("[VillagerSpawnService] LevelConfig.Layout не прошит — спавн жителей пропущен.");
                return;
            }

            var positions = layout.VillagerSpawnPositions;
            if (positions == null || positions.Length == 0)
            {
                Debug.LogWarning("[VillagerSpawnService] VillageLayoutConfig.VillagerSpawnPositions пуст — спавн жителей пропущен.");
                return;
            }

            var count = _levelConfig.VillagerCount;
            for (var i = 0; i < count; i++)
            {
                // Позиций может быть меньше, чем жителей, — циклически переиспользуем (тот же
                // приём, что раньше был в editor-скрипте статичной раскладки).
                var position = positions[i % positions.Length];
                var villager = _villagerFactory.Spawn(position, Quaternion.identity);
                if (villager == null) continue; // фабрика уже залогировала причину (нет префаба/компонента)

                _spawned.Add(villager);
            }

            Debug.Log($"[VillagerSpawnService] Заспавнено {_spawned.Count} из {count} жителей.");
        }

        public void DespawnAll()
        {
            foreach (var villager in _spawned)
            {
                _villagerFactory.Despawn(villager);
            }

            _spawned.Clear();
        }
    }
}

using BlackWhiteLike.Controllers.Village;
using BlackWhiteLike.Core.Pool;
using BlackWhiteLike.Data.Configs.Villagers;
using BlackWhiteLike.Models;
using BlackWhiteLike.Services.Village;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace BlackWhiteLike.Factories.Village
{
    /// <summary>
    /// Реализация <see cref="IVillagerFactory"/>. Pure C#, единственный потребитель
    /// <see cref="IPoolManager"/> для жителей — весь остальной код спавнит через эту фабрику,
    /// не через пул напрямую (CLAUDE.md).
    /// </summary>
    public sealed class VillagerFactory : IVillagerFactory
    {
        private readonly IPoolManager _poolManager;
        private readonly IObjectResolver _resolver;
        private readonly VillagerConfig _config;
        private readonly IVillagerStateService _stateService;

        public VillagerFactory(
            IPoolManager poolManager,
            IObjectResolver resolver,
            VillagerConfig config,
            IVillagerStateService stateService)
        {
            _poolManager = poolManager;
            _resolver = resolver;
            _config = config;
            _stateService = stateService;
        }

        public VillagerController Spawn(Vector3 position, Quaternion rotation)
        {
            if (_config.Prefab == null)
            {
                Debug.LogError("[VillagerFactory] VillagerConfig.Prefab не прошит — спавн жителя невозможен.");
                return null;
            }

            var controller = _poolManager.Get<VillagerController>(_config.Prefab, position, rotation);
            if (controller == null) return null; // PoolManager уже залогировал причину (нет компонента на префабе)

            // Грабля CLAUDE.md: OnEnable уже отработал до инъекции (SetActive(true) внутри
            // pool.Get() опережает InjectGameObject). Явный ResetForReuse() ниже — уже после
            // Construct(), регистрирует жителя в IVillagerStateService и сбрасывает FSM/физику
            // (см. VillagerController.ResetForReuse — с шага 3, Б3 ревью, эта фабрика единственный
            // путь появления жителя в сцене, статичной расстановки editor-скриптом больше нет).
            _resolver.InjectGameObject(controller.gameObject);
            controller.ResetForReuse();

            return controller;
        }

        public void Despawn(VillagerController villager)
        {
            if (villager == null) return;

            // В5 (ревью шага 3): раньше Unregister шёл БЕЗУСЛОВНО перед Release — если Release
            // отвергал объект (не выдан этим пулом/уже возвращён), житель уже был снят с учёта в FSM,
            // но продолжал стоять в сцене с замороженным состоянием (все стимулы после этого — только
            // warning в лог). Теперь снимаем с учёта только при успешном возврате в пул.
            var released = _poolManager.Release(villager.gameObject);
            if (!released) return;

            var id = VillagerId.FromInstanceId(villager.gameObject.GetInstanceID());
            _stateService.Unregister(id);
        }
    }
}

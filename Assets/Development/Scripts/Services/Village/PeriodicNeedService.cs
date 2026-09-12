using System;
using System.Collections.Generic;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Levels;
using BlackWhiteLike.Models;
using VContainer;
using VContainer.Unity;

namespace BlackWhiteLike.Services.Village
{
    /// <summary>
    /// Реализация <see cref="IPeriodicNeedService"/> (game-design §5/§6, решение владельца
    /// 2026-09-12, уровни 2–5). Pure C# — снимок интервала/длительности берётся из
    /// <c>LevelConfig</c> (тот же приём, что <c>LevelEventService</c>/<c>VillagerDiseaseService</c>).
    /// </summary>
    /// <remarks>
    /// Один спавн за вызов <see cref="Tick"/> — сознательное решение: при большом deltaTime (просадка
    /// FPS, свёрнутое приложение) накопленный <c>_elapsed</c> может перекрывать несколько интервалов
    /// сразу, но пачка событий за один кадр была бы "рывком" нужд, которого игрок не ожидает и не
    /// успевает обработать. Вычитаем ровно один <c>interval</c> и спавним максимум одну нужду —
    /// остаток лага (если <c>_elapsed</c> всё ещё &gt;= interval) не сбрасывается в ноль, а
    /// разряжается следующими вызовами Tick, каждый раз по одному спавну.
    /// <para>
    /// Цели — только "свободные": для Fire/Hunger это точки <c>INeedSiteRegistry.CollectSites</c>, у
    /// которых сейчас нет Active-записи (<c>ILevelEventService.IsNeedActive</c>); для Disease
    /// (<c>NeedHostRules.IsVillagerHosted</c>) — есть хотя бы один пригодный кандидат-житель
    /// (<c>IVillagerDiseaseService.HasEligibleCandidate</c>), а siteIndex — свободный виртуальный слот
    /// (<c>NeedHostRules.VirtualSiteIndexBase</c> и выше, <see cref="FindFreeVirtualSlot"/>). Свободных
    /// целей нет — тик молча пропускается (без лога): это штатная ситуация ("деревня уже вся в
    /// нуждах"), а не ошибка конфигурации.
    /// </para>
    /// <para>
    /// Random — равновероятный выбор СРЕДИ ВСЕХ собранных свободных целей (все типы нужд и все точки
    /// смешиваются в один буфер перед выбором), а не сначала тип, потом точка — иначе тип с одной
    /// свободной точкой получал бы ту же вероятность, что тип с пятью, искажая частоту нужд.
    /// </para>
    /// </remarks>
    public sealed class PeriodicNeedService : IPeriodicNeedService, ITickable, IDisposable
    {
        // Кэшируется один раз — Enum.GetValues аллоцирует новый массив на каждый вызов.
        private static readonly NeedType[] AllNeedTypes = (NeedType[])Enum.GetValues(typeof(NeedType));

        private readonly LevelConfig _levelConfig;
        private readonly ILevelEventService _levelEventService;
        private readonly INeedSiteRegistry _needSiteRegistry;
        private readonly IVillagerDiseaseService _diseaseService;
        private readonly Random _random;

        private readonly IDisposable _levelCompletedSubscription;

        private float _elapsed;
        private bool _levelFinished;

        // Переиспользуемые буферы — без аллокаций на каждый Tick (кроме роста при первом использовании).
        private readonly List<int> _siteBuffer = new List<int>();
        private readonly List<(NeedType Type, int SiteIndex)> _candidateBuffer = new List<(NeedType, int)>();

        /// <summary>
        /// DI-конструктор (тот же блокер, что у <c>VillagerDiseaseService</c>): VContainer резолвит
        /// самый длинный конструктор и не понимает C#-дефолт <c>random = null</c> — без [Inject] на
        /// этой перегрузке контейнер упал бы на Build() с «No such registration of type: System.Random».
        /// </summary>
        [Inject]
        public PeriodicNeedService(
            IEventBus eventBus,
            LevelConfig levelConfig,
            ILevelEventService levelEventService,
            INeedSiteRegistry needSiteRegistry,
            IVillagerDiseaseService diseaseService)
            : this(eventBus, levelConfig, levelEventService, needSiteRegistry, diseaseService, new Random())
        {
        }

        /// <summary>Тестовая перегрузка — seeded <see cref="Random"/> для детерминированного выбора цели.</summary>
        public PeriodicNeedService(
            IEventBus eventBus,
            LevelConfig levelConfig,
            ILevelEventService levelEventService,
            INeedSiteRegistry needSiteRegistry,
            IVillagerDiseaseService diseaseService,
            Random random)
        {
            _levelConfig = levelConfig;
            _levelEventService = levelEventService;
            _needSiteRegistry = needSiteRegistry;
            _diseaseService = diseaseService;
            _random = random;

            _levelCompletedSubscription = eventBus.Subscribe<LevelCompletedEvent>(OnLevelCompleted);
        }

        public bool IsEnabled => _levelConfig.EventIntervalSeconds > 0f;

        public void Tick(float deltaTime)
        {
            if (!IsEnabled || _levelFinished) return;

            _elapsed += deltaTime;

            var interval = _levelConfig.EventIntervalSeconds;
            if (_elapsed < interval) return;

            // Не обнуляем — остаток лага (если deltaTime перепрыгнул несколько интервалов сразу)
            // разряжается следующими вызовами Tick, по одному спавну за раз (см. remarks класса).
            _elapsed -= interval;

            TrySpawnOne();
        }

        /// <summary>Мост для VContainer ITickable — единственное место сервиса, читающее UnityEngine.Time.</summary>
        public void Tick() => Tick(UnityEngine.Time.deltaTime);

        private void TrySpawnOne()
        {
            _candidateBuffer.Clear();

            foreach (var type in AllNeedTypes)
            {
                if (NeedHostRules.IsVillagerHosted(type))
                {
                    // Disease — нет NeedSite в сцене, "свободна" означает "есть пригодный житель";
                    // сам siteIndex — первый ещё не занятый виртуальный слот (по одному на больного).
                    if (_diseaseService.HasEligibleCandidate)
                    {
                        _candidateBuffer.Add((type, FindFreeVirtualSlot(type)));
                    }

                    continue;
                }

                _needSiteRegistry.CollectSites(type, _siteBuffer);
                foreach (var siteIndex in _siteBuffer)
                {
                    if (_levelEventService.IsNeedActive(type, siteIndex)) continue;
                    _candidateBuffer.Add((type, siteIndex));
                }
            }

            if (_candidateBuffer.Count == 0) return; // свободных целей нет — штатный молчаливый пропуск тика

            var chosen = _candidateBuffer[_random.Next(_candidateBuffer.Count)];
            // causeMiracle: default (== MiracleType.Rain) — значения не несёт: WorshipService/AlignmentService
            // читают CauseMiracle только при ByPlayer == true (см. Events.Village.cs), а здесь ByPlayer=false —
            // периодическая нужда Fear игроку не начисляет.
            _levelEventService.TriggerNeed(chosen.Type, chosen.SiteIndex, _levelConfig.PeriodicNeedDuration, byPlayer: false, causeMiracle: default);
        }

        /// <summary>
        /// Минимальный siteIndex &gt;= <see cref="NeedHostRules.VirtualSiteIndexBase"/>, для которого
        /// нет Active-записи данного типа — по одному слоту на каждого одновременно больного (обычно
        /// villager-count небольшой, цикл завершается за одну-две итерации).
        /// </summary>
        private int FindFreeVirtualSlot(NeedType type)
        {
            var slot = NeedHostRules.VirtualSiteIndexBase;
            while (_levelEventService.IsNeedActive(type, slot))
            {
                slot++;
            }

            return slot;
        }

        private void OnLevelCompleted(LevelCompletedEvent evt) => _levelFinished = true;

        public void Dispose()
        {
            _levelCompletedSubscription?.Dispose();
        }
    }
}

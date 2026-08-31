using System;
using BlackWhiteLike.Core.Events;
using BlackWhiteLike.Data.Configs.Alignment;
using BlackWhiteLike.Services.Worship;
using UnityEngine;
using VContainer;

namespace BlackWhiteLike.Controllers.Temple
{
    /// <summary>
    /// Визуал руки/храма по alignment-стейджу (game-design §7, §11.1, шаг 6). Unity API only:
    /// подписывается на <see cref="AlignmentChangedEvent"/> и переключает <c>sharedMaterial(s)</c>
    /// на прошитых рендерерах — сам стейдж (какой материал на каком score) читается из
    /// <see cref="AlignmentVisualConfig"/> (данные, не код, тот же прецедент, что MiracleConfig).
    /// Декали/VFX стейджа (<see cref="AlignmentStage.DecalPrefabsToSpawn"/>/
    /// <see cref="AlignmentStage.VfxToActivate"/>) в срезе шага 6 не обрабатываются — задел будущих
    /// стейджей (YAGNI, поля сейчас пустые).
    /// </summary>
    public sealed class AlignmentVisualController : MonoBehaviour
    {
        [SerializeField] private Renderer[] _handRenderers;
        [SerializeField] private Renderer[] _templeRenderers;

        private IEventBus _eventBus;
        private AlignmentVisualConfig _config;
        private bool _initialized;

        // Стейдж уже не struct-nullable — сравниваем по ScoreThreshold (значение стейджа для
        // данного score детерминировано конфигом, совпадение порога == тот же стейдж).
        private bool _hasAppliedStage;
        private float _appliedStageThreshold;

        private IDisposable _alignmentChangedSubscription;

        [Inject]
        public void Construct(IEventBus eventBus, AlignmentVisualConfig config, IAlignmentService alignment)
        {
            _eventBus = eventBus;
            _config = config;

            Subscribe();

            // Стартовый score читается из IAlignmentService.Score, а не хардкодится 0f (ревью шага 6,
            // ВАЖНО 4) — на момент Construct сервис уже мог получить сдвиг (например, если сцена
            // собрана так, что AlignmentService успел обработать событие до инъекции этого контроллера).
            // Событие AlignmentChangedEvent для стартового значения 0 не публикуется (Shift публикует
            // только при реальном изменении), поэтому применяем актуальный стейдж явно здесь один раз.
            ApplyStage(_config.GetStageFor(alignment.Score));

            _initialized = true;
        }

        private void OnEnable()
        {
            // CLAUDE.md: OnEnable вызывается до Construct — на самом первом Construct этот путь
            // безопасно пропускается (_initialized ещё false), на повторных enable — переподписка
            // (тот же приём, что MiracleImpactController/NeedSiteController).
            if (!_initialized) return;
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            Unsubscribe();
            _alignmentChangedSubscription = _eventBus.Subscribe<AlignmentChangedEvent>(OnAlignmentChanged);
        }

        private void Unsubscribe()
        {
            _alignmentChangedSubscription?.Dispose();
            _alignmentChangedSubscription = null;
        }

        private void OnAlignmentChanged(AlignmentChangedEvent evt)
        {
            ApplyStage(_config.GetStageFor(evt.NewScore));
        }

        private void ApplyStage(AlignmentStage stage)
        {
            // Стейдж не менялся с прошлого применения — no-op (переключение материалов не бесплатно,
            // не делаем его на каждый Fear/Awe-тик, если стейдж остаётся тем же).
            if (_hasAppliedStage && Mathf.Approximately(_appliedStageThreshold, stage.ScoreThreshold)) return;

            ApplyMaterials(_handRenderers, stage.HandMaterials);
            ApplyMaterials(_templeRenderers, stage.TempleMaterials);

            _hasAppliedStage = true;
            _appliedStageThreshold = stage.ScoreThreshold;
        }

        /// <summary>
        /// Назначает материалы стейджа рендерерам ссылкой (renderer.sharedMaterial = ...) — НЕ
        /// renderer.material (тот создаёт инстанс-копию в Play Mode, лишний оверхед и потеря связи
        /// с ассетом). Семантика — ПАРАЛЛЕЛЬНЫЕ массивы (решение оркестратора, ревью шага 6, см.
        /// xml-doc <c>AlignmentStage</c>): <paramref name="stageMaterials"/>[i] — материал для
        /// <paramref name="renderers"/>[i], рендереру за пределами длины массива материалов
        /// достаётся последний элемент (<c>Mathf.Min(i, stageMaterials.Length - 1)</c>). Пустой/null
        /// массив материалов — легальный стейдж без переопределения (пропускаем).
        /// </summary>
        private static void ApplyMaterials(Renderer[] renderers, Material[] stageMaterials)
        {
            if (stageMaterials == null || stageMaterials.Length == 0) return;
            if (renderers == null || renderers.Length == 0) return;

            if (stageMaterials.Length != renderers.Length)
            {
                Debug.LogWarning(
                    $"[AlignmentVisualController] Число материалов стейджа ({stageMaterials.Length}) не совпадает " +
                    $"с числом прошитых рендереров ({renderers.Length}) — рендереру i назначается " +
                    "stageMaterials[Min(i, stageMaterials.Length - 1)].");
            }

            // "Один раз" — не спамим предупреждением на каждый многослотовый рендерер отдельно,
            // в срезе шага 6 все рендереры однослотовые, это страховка на будущее.
            var multiSlotWarned = false;

            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null) continue;

                if (!multiSlotWarned && renderer.sharedMaterials.Length > 1)
                {
                    multiSlotWarned = true;
                    Debug.LogWarning(
                        $"[AlignmentVisualController] Рендерер '{renderer.name}' имеет {renderer.sharedMaterials.Length} " +
                        "слотов материалов — назначается только первый слот (семантика 'один материал на рендерер', " +
                        "см. xml-doc AlignmentStage).");
                }

                renderer.sharedMaterial = stageMaterials[Mathf.Min(i, stageMaterials.Length - 1)];
            }
        }
    }
}

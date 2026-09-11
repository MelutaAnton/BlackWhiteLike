using System;
using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Data.Configs.Alignment
{
    /// <summary>
    /// Один визуальный стейдж alignment-шкалы (game-design §7, §11.1, шаг 6) — набор материалов
    /// руки/храма и (задел будущих стейджей) декалей/VFX, активных начиная с <see cref="ScoreThreshold"/>.
    /// Пустые/null массивы легальны — "не менять" (§11.1): не у каждого стейджа обязаны быть все поля.
    /// </summary>
    /// <remarks>
    /// Семантика <see cref="HandMaterials"/>/<see cref="TempleMaterials"/> (решение оркестратора,
    /// ревью шага 6 — Temple/Synty Church_01_A собран из ДВУХ MeshRenderer с разными материалами):
    /// это ПАРАЛЛЕЛЬНЫЕ массивы к <c>_handRenderers</c>/<c>_templeRenderers</c>, прошитым на
    /// <c>AlignmentVisualController</c> — материал на каждый прошитый рендерер, в том же порядке.
    /// Рендереру с индексом i назначается <c>materials[Mathf.Min(i, materials.Length - 1)]</c> —
    /// то есть массив короче числа рендереров легален (последний материал используется на все
    /// "лишние" рендереры), а не является ошибкой данных. См. <c>AlignmentVisualController.ApplyMaterials</c>.
    /// </remarks>
    [Serializable]
    public struct AlignmentStage
    {
        [Tooltip("Минимальный AlignmentScore, при котором стейдж активен.")]
        [SerializeField] private float _scoreThreshold;

        [SerializeField] private Material[] _handMaterials;
        [SerializeField] private Material[] _templeMaterials;

        [Tooltip("Задел будущих стейджей — декали, спавнящиеся при активации. В срезе шага 6 не обрабатывается.")]
        [SerializeField] private GameObject[] _decalPrefabsToSpawn;

        [Tooltip("Задел будущих стейджей — VFX, активируемые при переходе на стейдж. В срезе шага 6 не обрабатывается.")]
        [SerializeField] private VfxEffectType[] _vfxToActivate;

        public float ScoreThreshold => _scoreThreshold;
        public Material[] HandMaterials => _handMaterials;
        public Material[] TempleMaterials => _templeMaterials;
        public GameObject[] DecalPrefabsToSpawn => _decalPrefabsToSpawn;
        public VfxEffectType[] VfxToActivate => _vfxToActivate;
    }

    /// <summary>
    /// Палитра UI-тинта по alignment-стейджам (game-design §7, §11.1, шаг 6; читается — шаг 10,
    /// волна 2, <c>AlignmentUiTintPresenter</c>). Дефолт (playtest-фикс волны 2: реальный ассет был
    /// сериализован всеми пятью тонами Color.white ещё с шага 6 — шкала Worship красилась в белый,
    /// текст тонул) — Saint холодный голубой, Kind тёплый бело-голубой, Neutral жёлтый (совпадает с
    /// текущим цветом шкалы Worship), Cruel оранжево-красный, Demon густой красный.
    /// </summary>
    [Serializable]
    public struct AlignmentUiPalette
    {
        [SerializeField] private Color _saintTint;
        [SerializeField] private Color _kindTint;
        [SerializeField] private Color _neutralTint;
        [SerializeField] private Color _cruelTint;
        [SerializeField] private Color _demonTint;

        public Color SaintTint => _saintTint;
        public Color KindTint => _kindTint;
        public Color NeutralTint => _neutralTint;
        public Color CruelTint => _cruelTint;
        public Color DemonTint => _demonTint;

        public static AlignmentUiPalette Default => new AlignmentUiPalette
        {
            _saintTint = new Color(0.55f, 0.8f, 1f, 1f),
            _kindTint = new Color(0.7f, 0.85f, 1f, 1f),
            _neutralTint = new Color(1f, 0.82f, 0.35f, 1f),
            _cruelTint = new Color(0.95f, 0.55f, 0.3f, 1f),
            _demonTint = new Color(0.85f, 0.25f, 0.25f, 1f)
        };
    }

    /// <summary>
    /// Визуальные стейджи руки/храма по <c>AlignmentService.Score</c> (game-design §7, §11.1, шаг 6).
    /// Читается <c>AlignmentVisualController</c>. Единственный источник маппинга "score → визуал" —
    /// данные, не код (тот же прецедент, что MiracleConfig.ResolvesNeeds).
    /// </summary>
    [CreateAssetMenu(fileName = "AlignmentVisualConfig", menuName = "BlackWhiteLike/Alignment/Alignment Visual Config")]
    public sealed class AlignmentVisualConfig : ScriptableObject
    {
        [Tooltip("Стейджи по убыванию ScoreThreshold — порядок в инспекторе значения не имеет, GetStageFor сортирует сам (см. EnsureSorted).")]
        [SerializeField] private AlignmentStage[] _stages = Array.Empty<AlignmentStage>();

        [SerializeField] private AlignmentUiPalette _uiPalette = AlignmentUiPalette.Default;

        public AlignmentUiPalette UiPalette => _uiPalette;

        // Кэш отсортированных по убыванию ScoreThreshold стейджей — сортировка выполняется один раз
        // (лениво, при первом обращении после загрузки/правки ассета), а не на каждый вызов
        // GetStageFor (может дёргаться из AlignmentChangedEvent часто).
        private AlignmentStage[] _sortedCache;
        private bool _cacheValid;

        /// <summary>
        /// Возвращает стейдж для <paramref name="score"/> — первый (по убыванию ScoreThreshold), для
        /// которого score >= ScoreThreshold; если ни один не подходит — последний (с минимальным
        /// порогом). Пустой массив стейджей — default(AlignmentStage) (все поля null/0, контроллер
        /// обязан это пережить, как и любой другой стейдж с пустыми массивами).
        /// </summary>
        public AlignmentStage GetStageFor(float score)
        {
            // GetStageIndex сам вызывает EnsureSorted и заполняет _sortedCache — повторный вызов
            // здесь был бы избыточным (второй раз — дешёвый no-op благодаря _cacheValid, но сбивал
            // с толку). Пустой массив — после GetStageIndex (вернёт 0), проверяем ПОСЛЕ вызова:
            // на пустом _sortedCache индекс 0 был бы вне границ.
            var index = GetStageIndex(score);
            if (_sortedCache.Length == 0) return default;

            return _sortedCache[index];
        }

        /// <summary>
        /// Позиция стейджа для <paramref name="score"/> в отсортированном по убыванию ScoreThreshold
        /// массиве (шаг 10, волна 2) — та же таблица переходов, что <see cref="GetStageFor"/>, но
        /// возвращает индекс вместо значения. Нужна UI-тинту (<c>AlignmentUiTintPresenter</c>): пять
        /// именованных тонов <see cref="AlignmentUiPalette"/> (Saint/Kind/Neutral/Cruel/Demon)
        /// соответствуют позиции 0..4 среди стейджей текущего ассета (Saint — наивысший порог,
        /// Demon — наинизший, см. AlignmentVisual.asset) — presenter переиспользует ЭТУ таблицу,
        /// не дублирует сортировку. Пустой массив стейджей — 0.
        /// </summary>
        public int GetStageIndex(float score)
        {
            EnsureSorted();

            if (_sortedCache.Length == 0) return 0;

            for (var i = 0; i < _sortedCache.Length; i++)
            {
                if (score >= _sortedCache[i].ScoreThreshold) return i;
            }

            return _sortedCache.Length - 1;
        }

        private void EnsureSorted()
        {
            if (_cacheValid) return;

            _sortedCache = _stages == null ? Array.Empty<AlignmentStage>() : (AlignmentStage[])_stages.Clone();

            var alreadySorted = true;
            for (var i = 1; i < _sortedCache.Length; i++)
            {
                if (_sortedCache[i].ScoreThreshold > _sortedCache[i - 1].ScoreThreshold)
                {
                    alreadySorted = false;
                    break;
                }
            }

            if (!alreadySorted)
            {
                Debug.LogWarning("[AlignmentVisualConfig] Стейджи не отсортированы по убыванию ScoreThreshold в инспекторе — отсортированы автоматически при первом обращении.");
                Array.Sort(_sortedCache, (a, b) => b.ScoreThreshold.CompareTo(a.ScoreThreshold));
            }

            _cacheValid = true;
        }

        private void OnValidate()
        {
            // Правка в инспекторе — кэш сортировки инвалидируется, пересчитается лениво при
            // следующем GetStageFor (Editor-only колбэк, в билде не вызывается — кэш и так валиден
            // с момента загрузки ассета до следующей правки, которой в билде не бывает).
            _cacheValid = false;
        }
    }
}

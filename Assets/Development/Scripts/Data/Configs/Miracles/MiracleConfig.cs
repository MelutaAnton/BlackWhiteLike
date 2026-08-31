using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Data.Configs.Miracles
{
    /// <summary>
    /// Параметры одного чуда (game-design §4, §12, §14.2) — один инстанс на значение <see cref="MiracleType"/>.
    /// Маппинг нужд (какую нужду разрешает, какую создаёт) — данные, не код (см. CLAUDE.md §14.2:
    /// "новый тип нужды — маппинг разрешающего чуда", это должно закрываться SO, не веткой в сервисе).
    /// </summary>
    [CreateAssetMenu(fileName = "MiracleConfig", menuName = "BlackWhiteLike/Miracles/Miracle Config")]
    public sealed class MiracleConfig : ScriptableObject
    {
        [SerializeField] private MiracleType _type;

        [Header("Стоимость Faith")]
        [Tooltip("Faith, списываемый один раз при BeginCast.")]
        [SerializeField] private float _baseFaithCost = 20f;

        [Tooltip("Faith/сек, списываемый во время накачки (UpdateCast) — растит радиус до MaxRadius.")]
        [SerializeField] private float _holdCostPerSecond = 10f;

        [Tooltip("Льготное окно удержания в секундах — первые N секунд hold-стоимость не списывается " +
                 "и радиус не растёт (человеческий тап ~0.1с не должен стоить дороже BaseFaithCost). " +
                 "Накачка (списание + рост радиуса) начинается после истечения этого окна.")]
        [SerializeField] private float _holdGraceSeconds = 0.15f;

        [Header("Радиус")]
        [Tooltip("Радиус каста без накачки (моментальный тап).")]
        [SerializeField] private float _baseRadius = 2f;

        [Tooltip("Радиус при полной накачке (HoldSecondsToMaxRadius).")]
        [SerializeField] private float _maxRadius = 5f;

        [Tooltip("Сколько секунд удержания нужно, чтобы радиус вырос от BaseRadius до MaxRadius.")]
        [SerializeField] private float _holdSecondsToMaxRadius = 1.5f;

        [Header("Эффект")]
        [Tooltip("Базовая сила эффекта при BaseRadius — реальная сила каста масштабируется по достигнутому радиусу (см. MiracleService.ReleaseCast).")]
        [SerializeField] private float _effectPower = 8f;

        [Tooltip("Знак определяет добро/зло (game-design §4, §7) — задел на AlignmentService, шаг 5. Положительное — Good, отрицательное — Evil.")]
        [SerializeField] private float _alignmentShift;

        [SerializeField] private VfxEffectType _castVfx;
        [SerializeField] private Sprite _icon;

        [Tooltip("Задел §4/§8 (апгрейды MiraclePower) — бонус к EffectPower по уровню апгрейда. В срезе шага 4 не используется.")]
        [SerializeField] private int[] _upgradeLevelPowerBonus = new int[0];

        [Header("Маппинг нужд (game-design §5, §14.2 — данные, не код)")]
        [Tooltip("Какие NeedType разрешает попадание этого чуда (Rain -> Fire, Hunger; Heal -> Disease).")]
        [SerializeField] private NeedType[] _resolvesNeeds = new NeedType[0];

        [Tooltip("Создаёт ли попадание этого чуда новую нужду (Fireball поджигает дом -> Fire).")]
        [SerializeField] private bool _createsNeed;

        [SerializeField] private NeedType _createdNeed;

        [Tooltip("Длительность созданной нужды до истечения, если не разрешена.")]
        [SerializeField] private float _createdNeedDuration = 15f;

        [Tooltip("Разбрасывает ли попадание этого чуда жителей взрывом (Fireball, game-design §4).")]
        [SerializeField] private bool _blastsVillagers;

        public MiracleType Type => _type;
        public float BaseFaithCost => _baseFaithCost;
        public float HoldCostPerSecond => _holdCostPerSecond;
        public float HoldGraceSeconds => _holdGraceSeconds;
        public float BaseRadius => _baseRadius;
        public float MaxRadius => _maxRadius;
        public float HoldSecondsToMaxRadius => _holdSecondsToMaxRadius;
        public float EffectPower => _effectPower;
        public float AlignmentShift => _alignmentShift;
        public VfxEffectType CastVfx => _castVfx;
        public Sprite Icon => _icon;
        public int[] UpgradeLevelPowerBonus => _upgradeLevelPowerBonus;
        public NeedType[] ResolvesNeeds => _resolvesNeeds;
        public bool CreatesNeed => _createsNeed;
        public NeedType CreatedNeed => _createdNeed;
        public float CreatedNeedDuration => _createdNeedDuration;
        public bool BlastsVillagers => _blastsVillagers;

        /// <summary>Разрешает ли попадание этого чуда нужду <paramref name="type"/> (см. ResolvesNeeds).</summary>
        public bool ResolvesNeed(NeedType type)
        {
            if (_resolvesNeeds == null) return false;

            foreach (var need in _resolvesNeeds)
            {
                if (need == type) return true;
            }

            return false;
        }

        private void OnValidate()
        {
            // Незначительное (ревью шага 4): BaseRadius участвует делителем в
            // MiracleService.ReleaseCast (power = EffectPower * radius/BaseRadius) — 0 или
            // отрицательное значение даёт деление на ноль/инверсию силы эффекта.
            _baseRadius = Mathf.Max(0.01f, _baseRadius);
            _maxRadius = Mathf.Max(_baseRadius, _maxRadius);
            _holdSecondsToMaxRadius = Mathf.Max(0.01f, _holdSecondsToMaxRadius);
            _holdGraceSeconds = Mathf.Max(0f, _holdGraceSeconds);
        }
    }
}

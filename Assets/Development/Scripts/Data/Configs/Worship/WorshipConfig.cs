using System.Collections.Generic;
using BlackWhiteLike.Models;
using UnityEngine;

namespace BlackWhiteLike.Data.Configs.Worship
{
    /// <summary>
    /// Величины Worship/Alignment (game-design §4, §7, §12, шаг 5) — единственный источник Awe за
    /// разрешённую нужду и Fear за акт террора. Отсутствующая запись — легальное состояние раннего
    /// баланса (0 + предупреждение один раз на ключ, см. <see cref="GetAwe"/>/<see cref="GetFear"/>),
    /// не ошибка конфигурации.
    /// </summary>
    [CreateAssetMenu(fileName = "WorshipConfig", menuName = "BlackWhiteLike/Worship/Worship Config")]
    public sealed class WorshipConfig : ScriptableObject
    {
        [Tooltip("Awe за разрешение нужды каждого типа (game-design §7).")]
        [SerializeField] private NeedAweEntry[] _aweByNeed = new NeedAweEntry[0];

        [Tooltip("Fear за акт террора каждого типа (game-design §7).")]
        [SerializeField] private TerrorFearEntry[] _fearByAct = new TerrorFearEntry[0];

        [Tooltip("Пассивное затухание Worship, единиц/сек. 0 по умолчанию — Open Question #2 (game-design §4).")]
        [SerializeField] private float _worshipDecayPerSecond;

        [Tooltip("Сдвиг AlignmentScore за жёсткое приземление жителя от броска рукой (game-design §7 — «малый evil»).")]
        [SerializeField] private float _handTerrorAlignmentShift = -3f;

        // Предохранитель от спама лога — предупреждение об отсутствующей записи выводится один раз
        // на ключ (тот же приём, что VfxFactory._warnedMissing). Не сериализуется — рантайм-состояние SO.
        private readonly HashSet<NeedType> _warnedMissingAwe = new HashSet<NeedType>();
        private readonly HashSet<TerrorAct> _warnedMissingFear = new HashSet<TerrorAct>();

        public float WorshipDecayPerSecond => _worshipDecayPerSecond;
        public float HandTerrorAlignmentShift => _handTerrorAlignmentShift;

        /// <summary>Awe за разрешение нужды <paramref name="type"/>. Нет записи — 0 + предупреждение один раз на тип.</summary>
        public int GetAwe(NeedType type)
        {
            if (_aweByNeed != null)
            {
                foreach (var entry in _aweByNeed)
                {
                    if (entry == null) continue;
                    if (entry.Type != type) continue;
                    return entry.Awe;
                }
            }

            if (_warnedMissingAwe.Add(type))
            {
                Debug.LogWarning($"[WorshipConfig] Нет записи Awe для {type} — используется 0.");
            }

            return 0;
        }

        /// <summary>Fear за акт террора <paramref name="act"/>. Нет записи — 0 + предупреждение один раз на тип.</summary>
        public int GetFear(TerrorAct act)
        {
            if (_fearByAct != null)
            {
                foreach (var entry in _fearByAct)
                {
                    if (entry == null) continue;
                    if (entry.Act != act) continue;
                    return entry.Fear;
                }
            }

            if (_warnedMissingFear.Add(act))
            {
                Debug.LogWarning($"[WorshipConfig] Нет записи Fear для {act} — используется 0.");
            }

            return 0;
        }
    }
}

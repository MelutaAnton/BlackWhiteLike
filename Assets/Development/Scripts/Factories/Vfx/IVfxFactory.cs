using UnityEngine;
using BlackWhiteLike.Models;

namespace BlackWhiteLike.Factories.Vfx
{
    /// <summary>
    /// Единственная точка доступа к пулу VFX-эффектов (CLAUDE.md — доступ к IPoolManager
    /// инкапсулируется в фабрике). Игровой код не хранит ссылок на партикл-префабы напрямую —
    /// только тип эффекта из <see cref="VfxEffectType"/>.
    /// </summary>
    public interface IVfxFactory
    {
        /// <summary>Проигрывает эффект <paramref name="type"/> в мировой позиции <paramref name="position"/>. Нет записи/префаба в VfxConfig — предупреждение в лог (один раз на тип), без исключения.</summary>
        void Play(VfxEffectType type, Vector3 position);
    }
}

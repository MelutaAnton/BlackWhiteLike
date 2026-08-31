namespace BlackWhiteLike.Models
{
    /// <summary>
    /// Типы VFX-эффектов проекта (game-design §4, §13, §14.2) — единственный источник ключей пула
    /// VFX (CLAUDE.md, "Запрещённые практики: дублирование строковых ключей"). Соответствие
    /// тип → префаб/пул задаётся <c>VfxConfig</c> (см. <c>Data/Configs/Vfx/VfxConfig.cs</c>).
    /// </summary>
    public enum VfxEffectType
    {
        /// <summary>Каст чуда Rain — эффект дождя/тушения (game-design §4).</summary>
        RainCast,

        /// <summary>Взрыв чуда Fireball (game-design §4 — "разбрасывает жителей взрывом").</summary>
        FireballExplosion,

        /// <summary>Каст чуда Heal — исцеляющий эффект (game-design §4, шаг 6).</summary>
        HealCast,

        /// <summary>Удар чуда Lightning (game-design §4, шаг 6).</summary>
        LightningStrike
    }
}

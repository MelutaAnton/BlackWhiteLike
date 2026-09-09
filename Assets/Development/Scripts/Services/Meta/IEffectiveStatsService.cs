namespace BlackWhiteLike.Services.Meta
{
    /// <summary>
    /// Эффективные мета-статы после апгрейдов Temple (game-design §8, §14.2, шаг 8) — единственная
    /// точка, где <c>FaithService</c>/<c>HandInteractionService</c>/<c>MiracleService</c> узнают о
    /// бонусах, не читая <see cref="IMetaProgressService"/>/<c>UpgradeCatalogConfig</c> напрямую. Без
    /// root/каталога — нейтральные значения (множители 1, бонусы 0), тот же fallback-принцип, что у
    /// остальных конфигов проекта.
    /// </summary>
    /// <remarks>
    /// Семантика <c>UpgradeLevelEntry.EffectValue</c> (зафиксировано здесь, читается только этим
    /// сервисом): для множителей — ПРОЦЕНТ прибавки (10 = +10% → ×1.10), для
    /// <see cref="FaithCapacityBonus"/> — абсолютное значение (game-design §8: "+X к BaseCapacity").
    /// Уровень 0 (апгрейд не куплен) — множители 1, бонус 0, каталог не читается.
    /// </remarks>
    public interface IEffectiveStatsService
    {
        /// <summary>Множитель предела силы броска руки (HandConfig.MaxThrowForce), апгрейд HandPower.</summary>
        float HandForceMultiplier { get; }

        /// <summary>Множитель предела массы захвата (HandConfig.MaxGrabMass), апгрейд HandPower.</summary>
        float HandMassMultiplier { get; }

        /// <summary>Абсолютный бонус к FaithConfig.BaseCapacity, апгрейд FaithCapacity.</summary>
        float FaithCapacityBonus { get; }

        /// <summary>Множитель пассивной регенерации Faith, апгрейд FaithCapacity.</summary>
        float FaithRegenMultiplier { get; }

        /// <summary>Множитель силы эффекта чуда (MiracleConfig.EffectPower), апгрейд MiraclePower.</summary>
        float MiraclePowerMultiplier { get; }

        /// <summary>Множитель радиуса чуда (BaseRadius/MaxRadius), апгрейд MiraclePower.</summary>
        float MiracleRadiusMultiplier { get; }
    }
}

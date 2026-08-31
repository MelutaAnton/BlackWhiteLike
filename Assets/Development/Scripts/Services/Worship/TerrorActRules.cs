using BlackWhiteLike.Models;

namespace BlackWhiteLike.Services.Worship
{
    /// <summary>
    /// Маппинг "какое чудо — какой акт террора" (game-design §7, §14.2, шаг 5) — единственный
    /// источник этого правила, тот же прецедент, что <c>VillagerPoseRules</c>. Lightning заведён
    /// здесь заранее (данные готовы уже сейчас), сам источник (чудо Lightning) появится на шаге 6.
    /// </summary>
    public static class TerrorActRules
    {
        /// <summary>Пытается сопоставить чудо акту террора. Rain/Heal — не акты террора, false.</summary>
        public static bool TryFromMiracle(MiracleType type, out TerrorAct act)
        {
            switch (type)
            {
                case MiracleType.Fireball:
                    act = TerrorAct.FireballOnStructure;
                    return true;

                case MiracleType.Lightning:
                    act = TerrorAct.LightningStrike;
                    return true;

                default:
                    act = default;
                    return false;
            }
        }
    }
}

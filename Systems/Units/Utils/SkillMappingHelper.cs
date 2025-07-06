// SkillMappingHelper.cs
using UnityEngine;
using DK;
using DK.Tasks;

namespace DK.Utils
{
    public static class SkillMappingHelper
    {
        /// <summary>
        /// Liefert den kombinierten Skill-Wert einer Unit
        /// basierend auf ihrem SkillSet, StatsProfile und Luck.
        /// </summary>
        public static float GetSkillValue(this UnitAI unit, SkillType skillType)
        {
            if (unit == null) return 0f;

            // 1) Basis-Skill aus dem SkillSet
            float baseVal = skillType switch
            {
                SkillType.Digging => unit.skills.digging,
                SkillType.Conquering => unit.skills.conquering,
                SkillType.Fighting => unit.skills.fighting,
                _ => 1f
            };

            // 2) Stat-Bonus pro Skill-Typ
            float statBonus = skillType switch
            {
                SkillType.Digging => 1f + unit.stats.strength * 0.05f,
                SkillType.Conquering => 1f + unit.stats.agility * 0.03f,
                SkillType.Fighting => 1f + unit.stats.defence * 0.04f,
                _ => 1f
            };

            // 3) Glücks-Multiplikator (optional für alle Skills)
            float luckBonus = 1f + unit.stats.luck * 0.01f;

            return baseVal * statBonus * luckBonus;
        }
    }
}

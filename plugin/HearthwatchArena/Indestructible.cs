using UnityEngine;

namespace HearthwatchArena
{
    // ---------- Pièces rendues indestructibles ----------
    // Les marques vivent dans le ZDO de l'instance, jamais dans le prefab : les pièces qu'un joueur
    // pose lui-même avec le même prefab restent donc parfaitement destructibles.
    //   health = -1                 -> le jeu ignore les dégâts sur une structure dont la vie est négative
    //   WearNTear.m_health = -1     -> la réparation ne peut plus rétablir une vie positive
    //   Piece.m_canBeRemoved = false -> le marteau ne peut plus la retirer
    // Les deux derniers passent par des champs ZDO : il faut donc déclarer HasFields et les drapeaux
    // de sous-classe (HasFieldsWearNTear / HasFieldsPiece) pour que le jeu les relise.
    internal static class Indestructible
    {
        private static readonly int HealthKey = ZDOVars.s_health;
        private static readonly int MaxHealthKey = "WearNTear.m_health".GetStableHashCode();
        private static readonly int HasFieldsKey = "HasFields".GetStableHashCode();
        private static readonly int HasFieldsWearNTearKey = "HasFieldsWearNTear".GetStableHashCode();
        private static readonly int HasFieldsPieceKey = "HasFieldsPiece".GetStableHashCode();
        private static readonly int CanBeRemovedKey = "Piece.m_canBeRemoved".GetStableHashCode();
        private static readonly int BuildingSkillKey = "BuildingSkillLevel".GetStableHashCode();

        public static void Mark(ZDO zdo, GameObject prefab)
        {
            if (zdo == null || prefab == null) return;
            zdo.Set(HasFieldsKey, true);
            zdo.Set(HealthKey, -1f);
            var wear = prefab.GetComponent<WearNTear>();
            if (wear != null)
            {
                zdo.Set(HasFieldsWearNTearKey, true);
                zdo.Set(MaxHealthKey, -1f);
                zdo.RemoveFloat(BuildingSkillKey);
            }
            var piece = prefab.GetComponent<Piece>();
            if (piece != null)
            {
                zdo.Set(HasFieldsPieceKey, true);
                zdo.Set(CanBeRemovedKey, false);
            }
        }
    }
}

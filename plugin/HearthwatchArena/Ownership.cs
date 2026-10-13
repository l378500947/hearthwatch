using System;
using System.Collections.Generic;
using UnityEngine;

namespace HearthwatchArena
{
    // Pièces « verrouillées » : le serveur en gardait la propriété pour que les bâtiments
    // posés par le panel ne s'effondrent jamais.
    //
    // 2026-10-12 — MÉCANISME RETIRÉ (décision de l'administrateur).
    //   Raison : le critère « CanLock » était trop large et prenait aussi les ItemDrop
    //   (objets lâchés au sol), qui devenaient alors impossibles à ramasser.
    //   Le propriétaire du serveur préfère un monde normalement simulé et reconstruira
    //   à la main en cas d'effondrement.
    //
    // Conservé uniquement comme point d'ancrage neutre : CanLock renvoie toujours false,
    // Lock / Rebuild ne font plus rien. Aucun objet n'est jamais retenu par le serveur.
    // Le patch Harmony « ReleaseNearbyZDOS » a été supprimé.
    internal static class Ownership
    {
        public static bool CanLock(GameObject prefab) => false;

        public static void Lock(ZDO zdo) { }

        public static void Rebuild(IEnumerable<ZDO> all) { }
    }
}

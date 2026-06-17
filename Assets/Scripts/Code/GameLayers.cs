using System.Collections;
using UnityEngine;
namespace Viable.VRNav {

    /// <summary>
    /// Component providing an intermediary, cleaner way to reference layers
    /// For explanation about combined layers, see : https://discussions.unity.com/t/how-do-i-use-layermasks/481/2
    /// </summary>
    public static class GameLayers {

        public static int IgnoreRaycasts => LayerMask.NameToLayer("Ignore Raycasts");
        public static int Drone => LayerMask.NameToLayer("Drone");
        public static int Buildings => LayerMask.NameToLayer("Buildings");

        public static int Combined_DefaultAndBuildings => LayerMask.GetMask("Default", "Buildings");

    }

}

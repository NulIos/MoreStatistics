using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoreStatistics
{
    public static class PerStageStats
    {
        public static string currentStageName;
        public static List<string> completedStages = new List<string>();

        public static void OnFinishSceneExit(RoR2.SceneExitController sceneExitController)
        {
            string sceneName = SceneManager.GetActiveScene().name;
            completedStages.Add(sceneName);
        }


    }
}

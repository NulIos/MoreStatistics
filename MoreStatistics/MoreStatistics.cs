using BepInEx;
using R2API;
using RoR2;
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MoreStatistics
{
    [BepInDependency(LanguageAPI.PluginGUID)]

    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]

    public class MoreStatistics : BaseUnityPlugin
    {
        public const string PluginGUID = "com.Nullos.MoreStatistics";
        public const string PluginName = "MoreStatistics";
        public const string PluginVersion = "1.1.0";

        public static PluginInfo PInfo { get; private set; }

        public string[] enemiesNames = ["AcidLarvaBody", "AffixEarthHealerBody", "ArtifactShellBody", "Assassin2Body", "AssassinBody", "BackupDroneBody", "Bandit2Body", "BanditBody", "BeetleBody", "BeetleGuardAllyBody", "BeetleGuardBody", "BeetleGuardCrystalBody", "BeetleQueen2Body", "BellBody", "BisonBody", "BomberBody", "BrotherBody", "BrotherGlassBody", "BrotherHurtBody", "CaptainBody", "ClayBody", "ClayBossBody", "ClayBruiserBody", "ClayGrenadierBody", "CommandoBody", "CommandoPerformanceTestBody", "CrocoBody", "Drone1Body", "Drone2Body", "DroneCommanderBody", "ElectricWormBody", "EmergencyDroneBody", "EnforcerBody", "EngiBeamTurretBody", "EngiBody", "EngiTurretBody", "EngiWalkerTurretBody", "EquipmentDroneBody", "FlameDroneBody", "FlyingVerminBody", "GeepBody", "GipBody", "GolemBody", "GolemBodyInvincible", "GrandParentBody", "GravekeeperBody", "GreaterWispBody", "GupBody", "HANDBody", "HaulerBody", "HereticBody", "HermitCrabBody", "HuntressBody", "ImpBody", "ImpBossBody", "JellyfishBody", "LemurianBody", "LemurianBruiserBody", "LoaderBody", "LunarExploderBody", "LunarGolemBody", "LunarWispBody", "MageBody", "MagmaWormBody", "MegaConstructBody", "MegaDroneBody", "MercBody", "MiniMushroomBody", "MinorConstructAttachableBody", "MinorConstructBody", "MinorConstructOnKillBody", "MissileDroneBody", "NullifierAllyBody", "NullifierBody", "PaladinBody", "ParentBody", "ParentPodBody", "Pot2Body", "PotMobile2Body", "PotMobileBody", "RailgunnerBody", "RoboBallBossBody", "RoboBallGreenBuddyBody", "RoboBallMiniBody", "RoboBallRedBuddyBody", "ScavBody", "ScavLunar1Body", "ScavLunar2Body", "ScavLunar3Body", "ScavLunar4Body", "ShopkeeperBody", "SniperBody", "SquidTurretBody", "SulfurPodBody", "SuperRoboBallBossBody", "TimeCrystalBody", "TitanBody", "TitanGoldBody", "ToolbotBody", "TreebotBody", "Turret1Body", "UrchinTurretBody", "VagrantBody", "VerminBody", "VoidBarnacleBody", "VoidBarnacleNoCastBody", "VoidInfestorBody", "VoidJailerAllyBody", "VoidJailerBody", "VoidMegaCrabAllyBody", "VoidMegaCrabBody", "VoidRaidCrabBody", "VoidRaidCrabJointBody", "VoidSurvivorBody", "VultureBody", "WispBody", "WispSoulBody"];
        public string[] enemiesInteractions = ["killsAgainst", "damageDealtTo", "damageTakenFrom", "killsAgainstElite"];
        public string[] statGameObjectLabels = ["KillLabel", "DealtLabel", "TakenLabel", "EliteLabel"];
        public string[] statGameObjectValueLabels = ["KillValueLabel", "DealtValueLabel", "TakenValueLabel", "EliteValueLabel"];
        public string[] statNameToken = ["STATNAME_TOTALKILLS", "STATNAME_TOTALDAMAGEDEALT", "STATNAME_TOTALDAMAGETAKEN", "STATNAME_TOTALELITEKILLS"];
        public GameObject customStripPrefab;

        private void OnEnable()
        {
            On.RoR2.UI.GameEndReportPanelController.SetPlayerInfo += PlayerInfoHook;
        }

        private void OnDisable()
        {
            On.RoR2.UI.GameEndReportPanelController.SetPlayerInfo -= PlayerInfoHook;
        }

        public void Awake()
        {
            Log.Init(Logger);

            PInfo = Info;
            Assets.Init();

            if (Assets.mainBundle == null)
            {
                Log.Error("Bundle is null");
            }
            customStripPrefab = Assets.mainBundle.LoadAsset<GameObject>("StatStripTemplate");

            SceneExitController.onFinishExit += OnFinishSceneExit;
        }

        // Hook onto SetPlayerInfo (creates the UI for the end of run screen and assign stats)
        private void PlayerInfoHook(On.RoR2.UI.GameEndReportPanelController.orig_SetPlayerInfo orig, RoR2.UI.GameEndReportPanelController self, RunReport.PlayerInfo playerInfo)
        {
            // Add basic extra stats
            String[] statsToDisplay = new String[] { "totalHealthHealed",
                                                        "totalDistanceTraveled",
                                                        "totalBloodPurchases",
                                                        "totalLunarPurchases",
                                                        "totalDronesPurchased",
                                                        "totalTurretsPurchased",
                                                        "totalEliteKills"
            };
            self.statsToDisplay = self.statsToDisplay.Concat(statsToDisplay).ToArray();

            orig(self, playerInfo);

            //Add every enemy stat
            List<string> enemiesNamesPlayerInteractedWith = GetEnemiesNamesPlayerInteractedWith(playerInfo.statSheet);

            int i = self.statStrips.Count;
            AllocateStatStrip(enemiesNamesPlayerInteractedWith.Count, self.statStrips, self.statContentArea);

            foreach(string enemyName in enemiesNamesPlayerInteractedWith)
            {
                AssignStatToStrip(playerInfo.statSheet, enemyName, self.statStrips[i]);
                i++;
            }
        }

        // Check for non null values in enemies stats, this means the player interacted with it i.e. took dmg from it or dealt dmg...
        // Without this it would print stats for every enemy even if it's only 0s
        private List<string> GetEnemiesNamesPlayerInteractedWith(RoR2.Stats.StatSheet statSheet)
        {
            List<string> enemiesNamesPlayerInteractedWith = new List<string>();

            foreach(string enemyName in enemiesNames)
            {
                foreach(string enemyInteraction in enemiesInteractions)
                {
                    string statName = $"{enemyInteraction}.{enemyName}";
                    RoR2.Stats.StatDef statDef = RoR2.Stats.StatDef.Find(statName);

                    string displayValue = statSheet.GetStatDisplayValue(statDef);

                    if(Int32.TryParse(displayValue, out int value))
                    {
                        if(value != 0)
                        {
                            enemiesNamesPlayerInteractedWith.Add(enemyName);
                            break;
                        }
                    }
                }
            }

            return enemiesNamesPlayerInteractedWith;
        }

        // Create the ui element
        private void AllocateStatStrip(int count, List<GameObject> statStrips, RectTransform statContentArea)
        {
            count += statStrips.Count;
            while (statStrips.Count > count)
            {
                int index = statStrips.Count - 1;
                UnityEngine.Object.Destroy(statStrips[index].gameObject);
                statStrips.RemoveAt(index);
            }
            while (statStrips.Count < count)
            {
                GameObject gameObject = UnityEngine.Object.Instantiate(customStripPrefab, statContentArea);
                gameObject.SetActive(value: true);
                statStrips.Add(gameObject);
            }
        }

        // Fill ui with values
        private void AssignStatToStrip(RoR2.Stats.StatSheet srcStatSheet, string enemyName, GameObject destStatStrip)
        {
            GameObject bodyPrefab = RoR2.BodyCatalog.FindBodyPrefab(enemyName);
            RoR2.CharacterBody characterBody = bodyPrefab.GetComponent<RoR2.CharacterBody>();

            string bodyToken = characterBody.baseNameToken;
            destStatStrip.transform.Find("BodyName").GetComponent<TextMeshProUGUI>().text = RoR2.Language.GetString(bodyToken);

            Texture bodyIcon = characterBody.portraitIcon;
            destStatStrip.transform.Find("PortraitIcon").GetComponent<RawImage>().texture = bodyIcon;

            for (int i = 0; i < enemiesInteractions.Length; i++)
            {
                string enemyInteraction = enemiesInteractions[i];
                string statName = $"{enemyInteraction}.{enemyName}";

                RoR2.Stats.StatDef statDef = RoR2.Stats.StatDef.Find(statName);

                string arg = "0";
                ulong value = 0uL;
                if (srcStatSheet != null)
                {
                    arg = srcStatSheet.GetStatDisplayValue(statDef);
                    value = srcStatSheet.GetStatPointValue(statDef);
                }
                string statToken = Language.GetString(statNameToken[i]);
                string text = string.Format(Language.GetString("STAT_NAME_VALUE_FORMAT"), statToken, arg);
                destStatStrip.transform.Find(statGameObjectLabels[i]).GetComponent<TextMeshProUGUI>().text = text;

                string string2 = Language.GetString("STAT_POINTS_FORMAT");
                destStatStrip.transform.Find(statGameObjectValueLabels[i]).GetComponent<TextMeshProUGUI>().text = string.Format(string2, TextSerialization.ToStringNumeric(value));
            }
        }

        private void OnFinishSceneExit(RoR2.SceneExitController sceneExitController)
        {
            Debug.Log("Finished scene exit");
            string sceneName = SceneManager.GetActiveScene().name;
            Debug.Log("Current scene" + sceneName);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                RoR2.Stats.StatSheet statSheet = PlayerCharacterMasterController.instances[0].master.GetComponent<RoR2.Stats.PlayerStatsComponent>().currentStats;

                Debug.Log("STAT SHEET\n");
                foreach(RoR2.Stats.StatField field in statSheet.fields)
                {
                    Debug.Log($"{field.name} : {field}");
                }
            }
        }
    }
}

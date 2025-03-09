using BepInEx;
using R2API;
using RoR2;
using RoR2.Stats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static RoR2.EOSStatManager;

namespace MoreStatistics
{
    [BepInDependency(LanguageAPI.PluginGUID)]

    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]

    public class MoreStatistics : BaseUnityPlugin
    {
        public const string PluginGUID = "com.Nullos.MoreStatistics";
        public const string PluginName = "MoreStatistics";
        public const string PluginVersion = "2.0.1";

        public static PluginInfo PInfo { get; private set; }

        private string[] statsToDisplay = [ "totalHealthHealed",
                                    "totalDistanceTraveled",
                                    "totalBloodPurchases",
                                    "totalLunarPurchases",
                                    "totalDronesPurchased",
                                    "totalTurretsPurchased",
                                    "totalEliteKills"
            ];

        // UI
        private Transform gameEndReportPanel;
        private GameObject moreButtonsPrefab;
        private GameObject mobStatsPanelPrefab;
        private GameObject mobStatsStripPrefab;

        private Transform parentTransform;
        private GameObject mobStatsPanel = null;

        // Stat names
        private List<string> mobNames;
        private string[] statPrefix = { "damageDealtTo", "damageTakenFrom", "killsAgainst", "killsAgainstElite" };

        private void OnEnable()
        {
            On.RoR2.UI.GameEndReportPanelController.SetPlayerInfo += PlayerInfoHook;
            On.RoR2.UI.GameEndReportPanelController.Awake += EndMenuHook;
        }

        private void OnDisable()
        {
            On.RoR2.UI.GameEndReportPanelController.SetPlayerInfo -= PlayerInfoHook;
            On.RoR2.UI.GameEndReportPanelController.Awake -= EndMenuHook;
        }

        public void Awake()
        {
            Log.Init(Logger);

            PInfo = Info;

            // Use only mobs from the txt file
            string mobNamesPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(MoreStatistics.PInfo.Location), "mobNames.txt");
            populateMobList(mobNamesPath);

            // Load assets
            Assets.Init();
            if (Assets.mainBundle == null)
            {
                Log.Error("Bundle is null");
            }

            mobStatsPanelPrefab = Assets.mainBundle.LoadAsset<GameObject>("MobStats");

            mobStatsStripPrefab = Assets.mainBundle.LoadAsset<GameObject>("StatStrip");

            moreButtonsPrefab = Assets.mainBundle.LoadAsset<GameObject>("MoreButtonsArea");
        }

        private void EndMenuHook(On.RoR2.UI.GameEndReportPanelController.orig_Awake orig, RoR2.UI.GameEndReportPanelController self)
        {
            orig(self);

            // Parent game object
            parentTransform = self.transform.Find("SafeArea (JUICED)/BodyArea");

            // Create buttons area
            GameObject buttonsArea = Instantiate(moreButtonsPrefab, parentTransform);
            // Add listener
            GameObject mobButtonGO = buttonsArea.transform.Find("MobStatsButton").gameObject;
            Button mobButton = mobButtonGO.GetComponent<Button>();
            mobButton.onClick.AddListener(openStatPanel);
        }

        // Hook onto SetPlayerInfo (creates the UI for the end of run screen and assign stats)
        private void PlayerInfoHook(On.RoR2.UI.GameEndReportPanelController.orig_SetPlayerInfo orig, RoR2.UI.GameEndReportPanelController self, RunReport.PlayerInfo playerInfo, int playerIndex)
        {
            if (!self.statsToDisplay.Contains(statsToDisplay[0]))
            {
                Log.Info("Stats added");
                self.statsToDisplay = self.statsToDisplay.Concat(statsToDisplay).ToArray();
            }

            orig(self, playerInfo, playerIndex);

            List<string> enemiesNamesPlayerInteractedWith = GetEnemiesNamesPlayerInteractedWith(playerInfo.statSheet);

            createMobStatPanel();

            Transform stripContainerTransform = mobStatsPanel.transform.Find("Panel/Scroll View/Viewport/Container");

            StatSheet statSheet = playerInfo.statSheet;
            StatDef statDef;
            ulong value;

            // Mithrix has multiple bodies depending on combat phase
            ulong killsMithrix = 0;
            ulong damageDealtToMithrix = 0;
            ulong damageTakenFromMithrix = 0;
            GameObject mithrixStatStrip = null;

            foreach(string enemyName in enemiesNamesPlayerInteractedWith)
            {
                GameObject bodyPrefab;
                CharacterBody characterBody;
                Texture bodyIcon;
                GameObject statStrip;
                if (enemyName == "BrotherHauntBody" || enemyName == "BrotherHurtBody")
                {
                    if(mithrixStatStrip == null)
                    {
                        bodyPrefab = BodyCatalog.FindBodyPrefab(enemyName);
                        characterBody = bodyPrefab.GetComponent<CharacterBody>();

                        bodyIcon = characterBody.portraitIcon;

                        statStrip = Instantiate(mobStatsStripPrefab, stripContainerTransform);
                        statStrip.transform.Find("Image/Image").GetComponent<RawImage>().texture = bodyIcon;

                        // Kills against enemies
                        statDef = StatDef.Find($"killsAgainst.{enemyName}");
                        value = statSheet.GetStatValueULong(statDef);
                        statStrip.transform.Find("Killed/Value").GetComponent<TextMeshProUGUI>().text = TextSerialization.ToStringNumeric(value);
                        killsMithrix = value;

                        // Damage dealt
                        statDef = StatDef.Find($"damageDealtTo.{enemyName}");
                        value = statSheet.GetStatValueULong(statDef);
                        statStrip.transform.Find("DamageDealt/Value").GetComponent<TextMeshProUGUI>().text = TextSerialization.ToStringNumeric(value);
                        damageDealtToMithrix = value;

                        // Damage taken
                        statDef = StatDef.Find($"damageTakenFrom.{enemyName}");
                        value = statSheet.GetStatValueULong(statDef);
                        statStrip.transform.Find("DamageTaken/Value").GetComponent<TextMeshProUGUI>().text = TextSerialization.ToStringNumeric(value);
                        damageTakenFromMithrix = value;

                        mithrixStatStrip = statStrip;
                    }
                    else
                    {
                        statStrip = mithrixStatStrip;

                        statDef = StatDef.Find($"killsAgainst.{enemyName}");
                        value = statSheet.GetStatValueULong(statDef) + killsMithrix;
                        statStrip.transform.Find("Killed/Value").GetComponent<TextMeshProUGUI>().text = TextSerialization.ToStringNumeric(value);
                        killsMithrix = value;

                        // Damage dealt
                        statDef = StatDef.Find($"damageDealtTo.{enemyName}");
                        value = statSheet.GetStatValueULong(statDef) + damageDealtToMithrix;
                        statStrip.transform.Find("DamageDealt/Value").GetComponent<TextMeshProUGUI>().text = TextSerialization.ToStringNumeric(value);
                        damageDealtToMithrix = value;

                        // Damage taken
                        statDef = StatDef.Find($"damageTakenFrom.{enemyName}");
                        value = statSheet.GetStatValueULong(statDef) + damageTakenFromMithrix;
                        statStrip.transform.Find("DamageTaken/Value").GetComponent<TextMeshProUGUI>().text = TextSerialization.ToStringNumeric(value);
                        damageTakenFromMithrix = value;
                    }

                    continue;
                }

                bodyPrefab = BodyCatalog.FindBodyPrefab(enemyName);
                characterBody = bodyPrefab.GetComponent<CharacterBody>();

                bodyIcon = characterBody.portraitIcon;

                statStrip = Instantiate(mobStatsStripPrefab, stripContainerTransform);
                statStrip.transform.Find("Image/Image").GetComponent<RawImage>().texture = bodyIcon;

                // Kills against enemies
                statDef = StatDef.Find($"killsAgainst.{enemyName}");
                value = statSheet.GetStatValueULong(statDef);
                statStrip.transform.Find("Killed/Value").GetComponent<TextMeshProUGUI>().text = TextSerialization.ToStringNumeric(value);

                // Damage dealt
                statDef = StatDef.Find($"damageDealtTo.{enemyName}");
                value = statSheet.GetStatValueULong(statDef);
                statStrip.transform.Find("DamageDealt/Value").GetComponent<TextMeshProUGUI>().text = TextSerialization.ToStringNumeric(value);

                // Damage taken
                statDef = StatDef.Find($"damageTakenFrom.{enemyName}");
                value = statSheet.GetStatValueULong(statDef);
                statStrip.transform.Find("DamageTaken/Value").GetComponent<TextMeshProUGUI>().text = TextSerialization.ToStringNumeric(value);
            }
        }

        // Check for non null values in enemies stats, this means the player interacted with it i.e. took dmg from it or dealt dmg...
        // Without this it would print stats for every enemy even if it's only 0s
        private List<string> GetEnemiesNamesPlayerInteractedWith(RoR2.Stats.StatSheet statSheet)
        {
            List<string> enemiesNamesPlayerInteractedWith = new List<string>();

            foreach(string enemyName in mobNames)
            {
                foreach(string prefix in statPrefix)
                {
                    string statName = $"{prefix}.{enemyName}";
                    StatDef statDef = StatDef.Find(statName);

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

        private void populateMobList(string path)
        {
            char[] charsToTrim = { ' ', '\n' };
            mobNames = new List<string>();
            var lines = File.ReadLines(path);
            foreach(var line in lines)
            {
                mobNames.Add(line.Trim(charsToTrim));
            }
        }

        private void createMobStatPanel()
        {
            if(mobStatsPanel != null)
            {
                Destroy(mobStatsPanel);
            }
            mobStatsPanel = Instantiate(mobStatsPanelPrefab, parentTransform);

            mobStatsPanel.transform.Find("Panel/Top/Close").GetComponent<Button>().onClick.AddListener(closeStatPanel);

            mobStatsPanel.SetActive(false);
        }
        
        private void closeStatPanel()
        {
            if (mobStatsPanel == null)
            {
                return;
            }
            mobStatsPanel.SetActive(false);
        }

        private void openStatPanel()
        {
            if(mobStatsPanel != null)
            {
                mobStatsPanel.SetActive(true);
            }
        }

        private void Update()
        {
            //if (Input.GetKeyDown(KeyCode.F2))
            //{
            //    RoR2.Stats.StatSheet statSheet = PlayerCharacterMasterController.instances[0].master.GetComponent<RoR2.Stats.PlayerStatsComponent>().currentStats;

            //    Log.Info("STAT FIELDS\n");
            //    foreach (RoR2.Stats.StatField field in statSheet.fields)
            //    {
            //        Log.Info($"{field.name}");
            //    }
            //}
            //else if (Input.GetKeyDown(KeyCode.F3))
            //{
            //    RoR2.CharacterBody body = PlayerCharacterMasterController.instances[0].master.GetBody();
            //    body.healthComponent.Die();
            //}
        }
    }
}

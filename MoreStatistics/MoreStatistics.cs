using BepInEx;
using R2API;
using RoR2;
using RoR2.Stats;
using RoR2.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[assembly: HG.Reflection.SearchableAttribute.OptInAttribute]
namespace MoreStatistics
{
    [BepInDependency(LanguageAPI.PluginGUID)]
    
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]

    public class MoreStatistics : BaseUnityPlugin
    {
        public const string PluginGUID = "com.Nullos.MoreStatistics";
        public const string PluginName = "MoreStatistics";
        public const string PluginVersion = "2.0.2";

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
        private List<string> mobBlacklist;
        private string[] statPrefix = { "damageDealtTo", "damageTakenFrom", "killsAgainst" };

        // Font
        private TMP_FontAsset font = null;

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
            string blackListPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(MoreStatistics.PInfo.Location), "mobNames.txt");
            populateMobList(blackListPath);

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

            setFont(self.transform);
        }

        // Hook onto SetPlayerInfo (creates the UI for the end of run screen and assign stats)
        private void PlayerInfoHook(On.RoR2.UI.GameEndReportPanelController.orig_SetPlayerInfo orig, RoR2.UI.GameEndReportPanelController self, RunReport.PlayerInfo playerInfo, int playerIndex)
        {
            if (!self.statsToDisplay.Contains(statsToDisplay[0]))
            {
                self.statsToDisplay = self.statsToDisplay.Concat(statsToDisplay).ToArray();
            }

            orig(self, playerInfo, playerIndex);

            List<string> enemiesNamesPlayerInteractedWith = GetEnemiesNamesPlayerInteractedWith(playerInfo.statSheet);

            createMobStatPanel();

            Transform stripContainerTransform = mobStatsPanel.transform.Find("Panel/Scroll View/Viewport/Container");

            StatSheet statSheet = playerInfo.statSheet;
            StatDef statDef;

            GameObject bodyPrefab;
            CharacterBody characterBody;
            Texture bodyIcon;
            ulong kills, damageDealt, damageTaken;

            // Mithrix has multiple bodies depending on combat phase
            ulong killsMithrix = 0;
            ulong damageDealtToMithrix = 0;
            ulong damageTakenFromMithrix = 0;
            GameObject mithrixStatStrip = null;

            foreach (string enemyName in enemiesNamesPlayerInteractedWith)
            {
                bodyPrefab = BodyCatalog.FindBodyPrefab(enemyName);
                characterBody = bodyPrefab.GetComponent<CharacterBody>();

                bodyIcon = characterBody.portraitIcon;

                // Kills
                statDef = StatDef.Find($"killsAgainst.{enemyName}");
                kills = statSheet.GetStatValueULong(statDef);

                // Damage dealt
                statDef = StatDef.Find($"damageDealtTo.{enemyName}");
                damageDealt = statSheet.GetStatValueULong(statDef);

                // Damage taken
                statDef = StatDef.Find($"damageTakenFrom.{enemyName}");
                damageTaken = statSheet.GetStatValueULong(statDef);

                if(enemyName == "BrotherBody" || enemyName == "BrotherGlassBody" || enemyName == "BrotherHauntBody" || enemyName == "BrotherHurtBody")
                {
                    if(mithrixStatStrip == null)
                    {
                        mithrixStatStrip = Instantiate(mobStatsStripPrefab, stripContainerTransform);
                    }

                    killsMithrix += kills;
                    damageDealtToMithrix += damageDealt;
                    damageTakenFromMithrix += damageTaken;
                    fillStatStrip(mithrixStatStrip, bodyIcon, killsMithrix, damageDealtToMithrix, damageTakenFromMithrix);
                }
                else
                {
                    GameObject statStrip = Instantiate(mobStatsStripPrefab, stripContainerTransform);
                    fillStatStrip(statStrip, bodyIcon, kills, damageDealt, damageTaken);
                }
            }
        }

        private void fillStatStrip(GameObject statStrip, Texture icon, ulong kills, ulong damageDealt, ulong damageTaken)
        {
            if(icon != null)
            {
                statStrip.transform.Find("Image/Image").GetComponent<RawImage>().texture = icon;
            }

            statStrip.transform.Find("Killed/Value").GetComponent<TextMeshProUGUI>().text = TextSerialization.ToStringNumeric(kills);
            statStrip.transform.Find("DamageDealt/Value").GetComponent<TextMeshProUGUI>().text = TextSerialization.ToStringNumeric(damageDealt);
            statStrip.transform.Find("DamageTaken/Value").GetComponent<TextMeshProUGUI>().text = TextSerialization.ToStringNumeric(damageTaken);
        }

        // Check for non null values in enemies stats, this means the player interacted with it i.e. took dmg from it or dealt dmg...
        // Without this it would print stats for every enemy even if it's only 0s
        private List<string> GetEnemiesNamesPlayerInteractedWith(RoR2.Stats.StatSheet statSheet)
        {
            List<string> enemiesNamesPlayerInteractedWith = new List<string>();

            foreach(string enemyName in BodyCatalog.bodyNames)
            {
                if (mobBlacklist.Contains(enemyName))
                {
                    continue;
                }

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
            mobBlacklist = new List<string>();
            var lines = File.ReadLines(path);
            foreach(var line in lines)
            {
                mobBlacklist.Add(line.Trim(charsToTrim));
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

        private void setFont(Transform gameEndReportPanel)
        {
            font = gameEndReportPanel.Find("SafeArea (JUICED)/HeaderArea/DeathFlavorText").GetComponent<HGTextMeshProUGUI>().font;

            // Panel
            mobStatsPanelPrefab.transform.Find("Panel/Header/Killed/Value").GetComponent<TextMeshProUGUI>().font = font;
            mobStatsPanelPrefab.transform.Find("Panel/Header/DamageDealt/Value").GetComponent<TextMeshProUGUI>().font = font;
            mobStatsPanelPrefab.transform.Find("Panel/Header/DamageTaken/Value").GetComponent<TextMeshProUGUI>().font = font;

            // Stat strip
            mobStatsStripPrefab.transform.Find("Killed/Value").GetComponent<TextMeshProUGUI>().font = font;
            mobStatsStripPrefab.transform.Find("DamageDealt/Value").GetComponent<TextMeshProUGUI>().font = font;
            mobStatsStripPrefab.transform.Find("DamageTaken/Value").GetComponent<TextMeshProUGUI>().font = font;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.F2))
            {
                StatSheet statSheet = PlayerCharacterMasterController.instances[0].master.GetComponent<PlayerStatsComponent>().currentStats;

                Log.Info("STAT FIELDS\n");
                foreach (StatField field in statSheet.fields)
                {
                    Log.Info($"{field.name}");
                }
            }
            else if (Input.GetKeyDown(KeyCode.F3))
            {
                CharacterBody body = PlayerCharacterMasterController.instances[0].master.GetBody();
                body.healthComponent.Die();
            }
        }

        [ConCommand(commandName = "ms_body_portraits", flags = ConVarFlags.None, helpText = "Prints a list of all character bodies with their associated portrait icon name.")]
        private static void cmd_bodyPortraits(ConCommandArgs args)
        {
            foreach(CharacterBody characterBody in BodyCatalog.allBodyPrefabBodyBodyComponents)
            {
                string bodyName = BodyCatalog.GetBodyName(BodyCatalog.FindBodyIndex(characterBody));
                string bodyIcon = characterBody.portraitIcon.name;
                Log.Info($"{bodyName} - {bodyIcon}");
            }
        }
    }
}

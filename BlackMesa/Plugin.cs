﻿using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using DunGen;
using DunGen.Graph;
using LethalLib;
using LethalLib.Modules;
using LethalLevelLoader;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Chnage anything that says Your Mod to the Name of Mods name that you are making and or the name of the interior
namespace BlackMesa
{

    [BepInPlugin("Plastered_Crab.BlackMesaInterior", "Black Mesa Interior", "0.9.0")] // Set your mod name and set your version number
    [BepInDependency("evaisa.lethallib", BepInDependency.DependencyFlags.HardDependency)]
    public class BlackMesaInterior : BaseUnityPlugin
    {

        // Awake method is called before the Menu Screen initialization
        private void Awake()
        {
            // Instantiating game objects and managing singleton instance
            bool flag = Instance == null;
            if (flag)
            {
                Instance = this;

            }
            // Retrieving types from the executing assembly
            Type[] types = Assembly.GetExecutingAssembly().GetTypes();
            foreach (Type type in types)
            {
                // Invoking methods with RuntimeInitializeOnLoadMethodAttribute
                MethodInfo[] methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic);
                foreach (MethodInfo methodInfo in methods)
                {
                    object[] customAttributes = methodInfo.GetCustomAttributes(typeof(RuntimeInitializeOnLoadMethodAttribute), false);
                    bool flag2 = customAttributes.Length != 0;
                    if (flag2)
                    {
                        methodInfo.Invoke(null, null);
                        mls.LogInfo("Invoked method with RuntimeInitializeOnLoadMethodAttribute");
                    }
                }
            }
            // Creating a logger to handle log errors and debug messages
            this.mls = BepInEx.Logging.Logger.CreateLogSource("Black Mesa Interior");

            // Loading Interior Dungeon assets from AssetBundle
            string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            BlackMesaAssets = AssetBundle.LoadFromFile(Path.Combine(directoryName, "blackmesamod")); // Loading Assetbundle name it your mod and asset name 
            bool flag3 = BlackMesaAssets == null;
            if (flag3)
            {
                this.mls.LogError("Failed to load Interior Dungeon assets.");
            }
            else
                this.mls.LogInfo("Interior Assets loaded successfully");
            {
                {
                    // Loading Dungeon Flow from AssetBundle
                    DungeonFlow BlackMesaFlow = BlackMesaAssets.LoadAsset<DungeonFlow>("Assets/LethalCompany/Mods/BlackMesaMoon/DunGen Stuff/Black Mesa.asset");// use your file path and dungenflow name
                    bool flag5 = BlackMesaFlow == null;
                    if (flag5)
                    {
                        this.mls.LogError("Failed to load Interior Dungeon Flow.");
                    }
                    AudioClip BlackMesaDungeonAudio = BlackMesaAssets.LoadAsset<AudioClip>("Assets/sound.ogg");// use your file path and sound name
                    if (BlackMesaDungeonAudio == null)
                    {
                        this.mls.LogError("Custom Ambient Coming Soon");
                    }
                    else
                        mls.LogInfo("Was loaded was called");
                    {   // Configuration setup for Mod Manager
                        configInteriorRarity = base.Config.Bind("Black Mesa Interior", "InteriorRarity", 35, new ConfigDescription("The chance that the Interior tileset will be chosen. The higher the value, the higher the chance. By default, the Interior will appear on valid moons with a roughly one in ten chance.", new AcceptableValueRange<int>(0, 99999)));
                        configInteriorMoons = base.Config.Bind("Black Mesa Interior", "InteriorMoons", "list", new ConfigDescription("Use 'list' to specify a custom list of moons for the Interior to appear on. Individual moons can be added to the list by editing the InteriorDungeonMoonsList config entry.", new AcceptableValueList<string>(configInteriorMoonsValues)));
                        configInteriorMoonsList = base.Config.Bind<string>("Black Mesa Interior", "InteriorDungeonMoonsList", "Black Mesa:99999", new ConfigDescription("Note: Requires 'InteriorMoons' to be set to 'list'. \nCan be used to specify a list of moons with individual rarities for moons to spawn on. \nRarity values will override the default rarity value provided in Bunker Rarity and will override InteriorGuaranteed. To guarantee dungeon spawning on a moon, assign arbitrarily high rarity value (e.g.  99999). \nMoons and rarities should be provided as a comma-separated list in the following format: 'Name:Rarity' Example: March:150,Offense:150 \nNote: Moon names are checked by string matching, i.e. the moon name 'dine' would enable spawning on 'dine', 'diner' and 'undine'. Be careful with modded moon names.", null));
                        configGuaranteedInterior = base.Config.Bind("Black Mesa Interior", "InteriorGuaranteed", defaultValue: false, new ConfigDescription("If enabled, the Interior will be effectively guaranteed to spawn. Only recommended for debugging/sightseeing purposes.", null)); // unused Config
                        configLengthOverride = base.Config.Bind("Black Mesa Length", "InteriorLengthOverride", -1, new ConfigDescription(string.Format("if not -1, Overrides the Interior length to what you set. 1 = 1 times big. 2 = twice as big. and so and so forth ", "2.5.0", BlackMesaFlow.Length.Min), null));
                        // Override Interior length if configured value is not -1
                        if (configLengthOverride.Value != -1)
                        {
                            mls.LogInfo($"Interior Length Override has been set to {configLengthOverride.Value}. Be careful with this value");
                            BlackMesaFlow.Length.Min = configLengthOverride.Value;
                            BlackMesaFlow.Length.Max = configLengthOverride.Value;
                        }

                        // Create an ExtendedDungeonFlow object and initialize it with dungeon flow information
                        ExtendedDungeonFlow BlackMesaExtendedDungeon = ScriptableObject.CreateInstance<ExtendedDungeonFlow>();
                        BlackMesaExtendedDungeon.contentSourceName = "Black Mesa Interior";
                        BlackMesaExtendedDungeon.dungeonFlow = BlackMesaFlow; // Dungeon flow value used for accessing data from scriptable object and dungeon flow
                        BlackMesaExtendedDungeon.dungeonFirstTimeAudio = BlackMesaDungeonAudio;
                        BlackMesaExtendedDungeon.dungeonDefaultRarity = 0;

                        // Determine the rarity value based on configuration settings
                        int newRarity = (configGuaranteedInterior.Value ? 99999 : configInteriorRarity.Value);
                        // Based on configured moon settings, register the interior on different types of moons
                        if (configInteriorMoons.Value.ToLowerInvariant() == "all")
                        {
                            // Register interior on all moons, including modded moons
                            BlackMesaExtendedDungeon.manualContentSourceNameReferenceList.Add(new StringWithRarity("Lethal Company", newRarity));
                            BlackMesaExtendedDungeon.manualContentSourceNameReferenceList.Add(new StringWithRarity("Custom", newRarity));
                            mls.LogInfo("Registered Interior on all Moons, Includes Modded Moons.");
                        }
                        else if ((configInteriorMoons.Value.ToLowerInvariant() == "list") && (configInteriorMoonsList.Value != null))
                        {
                            string[] array = configInteriorMoonsList.Value.Split(',');
                            foreach (string text in array)
                            {
                                StringWithRarity stringWithRarity = ParseMoonString(text, newRarity);
                                if (stringWithRarity != null)
                                {
                                    BlackMesaExtendedDungeon.manualPlanetNameReferenceList.Add(stringWithRarity);
                                    mls.LogInfo($"Registered Interior on moon name {stringWithRarity.Name} with rarity {stringWithRarity.Rarity}");
                                }
                                else
                                {
                                    if (stringWithRarity == null)
                                    {
                                        // Log the error, but continue processing other moons
                                        mls.LogWarning($"No moon Added to list value!");

                                    }
                                    // Add a new StringWithRarity with the default rarity
                                    BlackMesaExtendedDungeon.manualPlanetNameReferenceList.Add(new StringWithRarity(text, newRarity));
                                }
                            }
                        }
                        else
                        {
                            mls.LogError("Invalid 'InteriorDungeonMoons' config value! ");
                        }
                        // Register the Extended Dungeon Flow with LLL
                        PatchedContent.RegisterExtendedDungeonFlow(BlackMesaExtendedDungeon);
                        mls.LogInfo("Loaded Extended DungeonFlow");

                        // Configure dungeon size parameters and apply Harmony patches
                        BlackMesaExtendedDungeon.dungeonSizeMin = 2f;
                        BlackMesaExtendedDungeon.dungeonSizeMax = 3f;
                        BlackMesaExtendedDungeon.dungeonSizeLerpPercentage = 0.25f;
                        this.harmony.PatchAll(typeof(BlackMesaInterior));
                        this.harmony.PatchAll(typeof(UnityEngine.Object));
                        this.harmony.PatchAll(typeof(StartOfRound));
                    }
                }
            }
        }
        // variables that are called throughout the script

        private readonly Harmony harmony = new Harmony("Black Mesa Interior");
        // Harmony instance used for patching methods in the game

        public static BlackMesaInterior Instance;
        // Singleton instance of the MoreInteriorsDunGen class

        internal ManualLogSource mls;
        // Logger instance for logging messages and debugging information

        public static AssetBundle BlackMesaAssets;
        // AssetBundle containing Bunker Dungeon assets

        private ConfigEntry<int> configInteriorRarity;
        // Configuration entry for specifying the rarity of the Bunker dungeon

        private ConfigEntry<string> configInteriorMoons;
        // Configuration entry for specifying the moons where the Bunker dungeon can appear

        private ConfigEntry<string> configInteriorMoonsList;
        // Configuration entry for specifying a list of moons with individual rarities for spawning the Bunker dungeon

        private ConfigEntry<bool> configGuaranteedInterior;
        // Configuration entry for toggling whether the Bunker dungeon is guaranteed to spawn

        private ConfigEntry<int> configLengthOverride;
        // Configuration entry for overriding the length of the Bunker dungeon

        private string[] configInteriorMoonsValues = new string[2] { "all", "list" };
        // List of preset values for configBunkerMoons entry

        // Function to parse a string representing a moon and its rarity
        public static StringWithRarity ParseMoonString(string moonString, int newRarity)
        {
            // Check if the input string is null or empty
            if (string.IsNullOrEmpty(moonString))
            {
                return null; // Return null if the input string is empty
            }

            // Split the string into moon name and rarity using ':' as the delimiter
            string[] parts = moonString.Split(':');

            // Check if the split resulted in exactly two parts
            if (parts.Length != 2)
            {
                // If only the moon name is present without a rarity, use the default rarity
                return new StringWithRarity(parts[0], newRarity);
            }

            try
            {
                // Parse the rarity part of the string
                int rarity = int.Parse(parts[1]);

                // Create a new StringWithRarity object with the moon name and parsed rarity
                return new StringWithRarity(parts[0], rarity);
            }
            catch (FormatException)
            {
                // If parsing fails, use the default rarity value
                return new StringWithRarity(parts[0], newRarity);
            }
        }

        // Patching Different item Group Mismatch
        [HarmonyPatch(typeof(UnityEngine.Object))]
        private class ItemGroupPatch
        {
            // Patch the Equals method
            [HarmonyPatch("Equals")]
            [HarmonyPrefix]
            public static bool FixItemGroupEquals(ref bool __result, object __instance, object other)
            {
                // Cast the instance to ItemGroup if possible
                ItemGroup itemGroup = (ItemGroup)((__instance is ItemGroup) ? __instance : null);

                // If the cast was successful
                if (itemGroup != null)
                {
                    // Cast the other object to ItemGroup if possible
                    ItemGroup itemGroup2 = (ItemGroup)((other is ItemGroup) ? other : null);

                    // If the second cast was successful
                    if (itemGroup2 != null)
                    {
                        // Compare the itemSpawnTypeName properties of the two ItemGroups
                        __result = itemGroup.itemSpawnTypeName == itemGroup2.itemSpawnTypeName;

                        // Prevent the original Equals method from being executed
                        return false;
                    }
                }

                // Allow the original Equals method to be executed
                return true;
            }
        }
    }
}

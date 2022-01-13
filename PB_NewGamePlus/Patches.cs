using HarmonyLib;
using PhantomBrigade;
using PhantomBrigade.Data;
using PhantomBrigade.Mods;
using PhantomBrigade.Overworld;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

// All mods must use a unique namespace in case they contain colliding type names, like one below
namespace PBMods.NewGamePlus
{
    // All mods using libraries must include one class inheriting from ModLink
    public class NewGamePlusLink : ModLink
    {
        public override void OnLoad(Harmony harmonyInstance)
        {
            // Note that you have access to metadata, which includes directory name and full path to this loaded mod.
            // You can also access ModManager.loadedModsLookup to find other loaded mods and interact with them (e.g. if you're relying on another mod)
            Assembly executingAssembly = Assembly.GetExecutingAssembly();
            Debug.Log("Mod " + metadata.id + " is executing OnLoad | Using HarmonyInstance.PatchAll on assembly (" + executingAssembly.FullName + ") | Directory: " + metadata.directory + " | Full path: " + metadata.path);
            harmonyInstance.PatchAll(executingAssembly);
        }
    }

    // Container class for patches - useful if you prefer to keep patches organized under one umbrella type
    public class Patches
    {
        [HarmonyPatch(typeof(PhantomBrigade.Data.ScenarioFunctions))]
        [HarmonyPatch("OnEndCapitalDelayed")]
        public class PatchOnEndCapitalDelayed
        {
            // The proccess below is excuted after PhantomBrigade.Data.ScenarioFunctions.OnEndCapitalDelayed()
            public static void Postfix()
            {
                Debug.Log("<NewGamePlus> Mod executes this as suffix to ScenarioFunctions.OnEndCapitalDelayed()");

                // You can set two actions excuted according to the player's confirmation. 

                //saveInfoHelper.buttonConfirm.available = false;
                CIViewDialogConfirmation.ins.Open("Starting New Game +", "Are you sure you'd like to start New Game +? (You can not start it later.)", ConfirmStartingNewGamePlus, null);
            }


            public static string saveNameNewGamePlus = "new_game_plus";

            //public CIHelperSaveGameInfo saveInfoHelper;

            public static void ConfirmStartingNewGamePlus()
            {
                Debug.Log("<NewGamePlus> Staring initialization of new game plus");
                
                OverworldEntity baseOverworld = IDUtility.playerBaseOverworld;
                Vector3 baseResetPos = new Vector3(-872.0198f, 50.31255f, -672.7128f);   // temporary
                ResetBasePosition(baseOverworld, baseResetPos);
                InitSaveOfNewGamePlus();
                StartNewGamePlus();

                Debug.Log("<NewGamePlus> Complited initialization of new game plus");
            }

            public static void InitSaveOfNewGamePlus()
            {
                Debug.Log("<NewGamePlus> Initializing save data");
                //saveInfoHelper.buttonConfirm.available = saveAvailableLast;
                DataManagerSave.SaveFromECS();
                //DataManagerSave.SaveData(DataManagerSave.SaveLocation.PickFromBuildType);
                SaveDataForNewGamePlus(DataManagerSave.SaveLocation.PickFromBuildType);

                Debug.Log("<NewGamePlus> Finished initialization of save data");
            }

            public static void StartNewGamePlus()
            {
                Debug.Log("<NewGamePlus> Loading incompleted save data");
                //saveInfoHelper.buttonConfirm.available = false;
                DataHelperLoading.TryLoading(saveNameNewGamePlus, DataManagerSave.SaveLocation.PickFromBuildType, OnNewGamePlusKeepingScreen, true);
            }

            public static void OnNewGamePlusKeepingScreen()
            {
                CIViewBackgroundLoading.ins.SetProgressText("Initializing new game +...");

                IncrementCombatUnitLevel(20);

                Debug.Log("<NewGamePlus> Saving game after level incrementation");
                //saveInfoHelper.buttonConfirm.available = saveAvailableLast;
                DataManagerSave.DoSave(saveNameNewGamePlus, DataManagerSave.SaveLocation.PickFromBuildType);

                Co.DelayFrames(3, delegate
                {
                    OverworldContext overworld = Contexts.sharedInstance.overworld;
                    OverworldEntity baseOverworld = IDUtility.playerBaseOverworld;

                    OverworldUtility.StopMovement(baseOverworld);
                    overworld.ReplaceCameraFocusRequest(IDUtility.playerBaseOverworld.position.v);
                    overworld.ReplaceSelectedEntity(IDUtility.playerBaseOverworld.id.id);

                    CIViewBackgroundLoading.ins.TryExit();
                });
            }

            public static void SaveDataForNewGamePlus(DataManagerSave.SaveLocation saveLocation)
            {
                Debug.Log("<NewGamePlus> Making custom save data");

                DataContainerSave dataKeep = SaveSerializationHelper.data;

                DataManagerSave.SetSaveName("save_internal_newgame");
                DataManagerSave.LoadData(DataManagerSave.SaveLocation.Internal);
                DataContainerSave dataReset = SaveSerializationHelper.data;
                
                if (SaveSerializationHelper.data == null)
                {
                    Debug.LogError("<NewGamePlus> Failed to save due to missing internal data");
                    return;
                }

                string savePath = GetSavePathOfNewGamePlus(saveLocation);
                if (savePath != null)
                {
                    Debug.Log("<NewGamePlus> Writing saved game to path " + savePath);
                    KeyValuePair<string, DataContainerSave> dataPair;

                    // Save containers with reseting data
                    dataReset.OnBeforeSerialization();
                    dataPair = new KeyValuePair<string, DataContainerSave>("save_internal_newgame", dataReset);

                    if (dataPair.Value.world != null)
                    {
                        Debug.Log("<NewGamePlus> Writing world.yaml with " + dataPair.Key);
                        _SaveContainer(savePath, "world.yaml", dataPair.Value.world);
                    }

                    if (dataPair.Value.provinces != null)
                    {
                        Debug.Log("<NewGamePlus> Writing OverworldProvinces with " + dataPair.Key);
                        _SaveContainers(savePath, "OverworldProvinces", dataPair.Value.provinces);
                    }

                    if (dataPair.Value.overworldEntities != null)
                    {
                        Debug.Log("<NewGamePlus> Writing OverworldEntities with " + dataPair.Key);
                        _SaveContainers(savePath, "OverworldEntities", dataPair.Value.overworldEntities);
                    }

                    if (dataPair.Value.overworldActions != null)
                    {
                        Debug.Log("<NewGamePlus> Writing OverworldActions with " + dataPair.Key);
                        _SaveContainers(savePath, "OverworldActions", dataPair.Value.overworldActions);
                    }

                    if (dataPair.Value.combatActions != null)
                    {
                        Debug.Log("<NewGamePlus> Writing CombatActions with " + dataPair.Key);
                        _SaveContainers(savePath, "CombatActions", dataPair.Value.combatActions);
                    }

                    // Save containers with keeping data
                    dataKeep.OnBeforeSerialization();
                    dataPair = new KeyValuePair<string, DataContainerSave>("save_current", dataKeep);

                    if (dataPair.Value.metadata != null)
                    {
                        Debug.Log("<NewGamePlus> Writing metadata.yaml with " + dataPair.Key);
                        _SaveContainer(savePath, "metadata.yaml", dataPair.Value.metadata);
                    }

                    if (dataPair.Value.core != null)
                    {
                        Debug.Log("<NewGamePlus> Writing core.yaml with " + dataPair.Key);
                        _SaveContainer(savePath, "core.yaml", dataPair.Value.core);
                    }

                    if (dataPair.Value.stats != null)
                    {
                        Debug.Log("<NewGamePlus> Writing stats.yaml with " + dataPair.Key);
                        _SaveContainer(savePath, "stats.yaml", dataPair.Value.stats);
                    }

                    if (dataPair.Value.crawler != null)
                    {
                        Debug.Log("<NewGamePlus> Writing crawler.yaml with " + dataPair.Key);
                        _SaveContainer(savePath, "crawler.yaml", dataPair.Value.crawler);
                    }

                    if (dataPair.Value.combat != null)
                    {
                        Debug.Log("<NewGamePlus> Writing combat.yaml with " + dataPair.Key);
                        _SaveContainer(savePath, "combat.yaml", dataPair.Value.combat);
                    }

                    if (dataPair.Value.units != null)
                    {
                        Debug.Log("<NewGamePlus> Writing Units with " + dataPair.Key);
                        _SaveContainers(savePath, "Units", dataPair.Value.units);
                    }

                    if (dataPair.Value.pilots != null)
                    {
                        Debug.Log("<NewGamePlus> Writing Pilots with " + dataPair.Key);
                        _SaveContainers(savePath, "Pilots", dataPair.Value.pilots);
                    }

                    // Replacing saved files in subfolder with dataKeep
                    if (dataPair.Value.overworldEntities != null)
                    {
                        Debug.Log("<NewGamePlus> Replacing OverworldEntities/squad_mobilebase with " + dataPair.Key);
                        ReplaceContainers(savePath, "OverworldEntities", "squad_mobilebase", dataPair.Value.overworldEntities);
                    }

                    //DataManagerSave.unsavedChangesPossible = false;
                    //DataManagerSave.RefreshSaveHeaders();

                }

                Debug.Log("<NewGamePlus> Finished saving custom data");
            }

            // Refering from PhantomBrigade.Data.DataManagerSave.SaveContainer()
            private static void _SaveContainer<T>(string savePath, string filename, T savedObject)
            {
                UtilitiesYAML.SaveDataToFile(savePath, filename, savedObject, appendApplicationPath: false);
            }

            // Refering from PhantomBrigade.Data.DataManagerSave.SaveContainers()
            private static void _SaveContainers<T>(string savePath, string subfolder, SortedDictionary<string, T> savedDictionary) where T : DataContainer
            {
                UtilitiesYAML.SaveDecomposedDictionary(savePath + subfolder, savedDictionary, warnAboutDeletions: false, appendApplicationPath: false);
            }

            private static void ReplaceContainers<T>(string savePath, string subfolder, string key, SortedDictionary<string, T> savedDictionary) where T : DataContainer
            {
                SortedDictionary<string, T> replacedDictionary = new SortedDictionary<string, T>();
                T value;
                if (savedDictionary.TryGetValue(key, out value))
                {
                    replacedDictionary.Add(key, value);
                }
                ReplaceDecomposedDictionary(savePath + subfolder, replacedDictionary);
            }

            // Refering from UtilitiesYAML.SaveDecomposedDictionary()
            public static void ReplaceDecomposedDictionary<T>(string folderPath, IDictionary<string, T> savedDictionary, bool warnAboutDeletions = true, bool appendApplicationPath = true) where T : DataContainer
            {
                if (appendApplicationPath)
                {
                    folderPath = DataPathHelper.GetCombinedCleanPath(DataPathHelper.GetApplicationFolder(), folderPath);
                }

                if (!Directory.Exists(folderPath))
                {
                    Debug.LogWarning("<NewGamePlus> Utilities | CheckDirectorySafety | Could not find directory: " + folderPath);
                    try
                    {
                        Directory.CreateDirectory(folderPath);
                        Debug.Log("<NewGamePlus> Utilities | CheckDirectorySafety | Created directory: " + folderPath);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }

                foreach (KeyValuePair<string, T> item in savedDictionary)
                {
                    if (!string.IsNullOrEmpty(item.Key))
                    {
                        UtilitiesYAML.SaveDataToFile(folderPath, item.Key.ToLowerInvariant() + ".yaml", item.Value);
                    }
                }
            }

            // Refering from PhantomBrigade.Data.DataManagerSave.GetSavePath()
            private static string GetSavePathOfNewGamePlus(DataManagerSave.SaveLocation saveLocation)
            {
                string saveFolderPath = DataManagerSave.GetSaveFolderPath(saveLocation);
                if (string.IsNullOrEmpty(saveFolderPath))
                {
                    Debug.LogError("<NewGamePlus> Failed to process saved game due to null or empty folder path");
                    return null;
                }

                if (string.IsNullOrEmpty(saveNameNewGamePlus))
                {
                    Debug.LogError("<NewGamePlus> Failed to process saved game due to null or empty save name");
                    return null;
                }

                return saveFolderPath + saveNameNewGamePlus + "/";
            }

            // Reset Base Position
            public static void ResetBasePosition(OverworldEntity baseOverworld, Vector3 baseResetPos)
            {
                Debug.Log("<NewGamePlus> Starting reset base position to (x, y, z) = (" + baseResetPos.x + ", " + baseResetPos.y + ", " + baseResetPos.z + ")");

                // Ensure the base has all its movement cancelled
                OverworldUtility.StopMovement(baseOverworld);
                baseOverworld.isDeployed = false;

                // Move the base to one of spawn points in starting province
                baseOverworld.ReplacePosition(baseResetPos);

                // Make the base check its vertical position in case center of the province is under terrain
                baseOverworld.isPositionUnchecked = true;

                // Focus camera on the replased base
                OverworldContext overworld = Contexts.sharedInstance.overworld;
                overworld.ReplaceCameraFocusRequest(IDUtility.playerBaseOverworld.position.v);
                overworld.ReplaceSelectedEntity(IDUtility.playerBaseOverworld.id.id);

                Debug.Log("<NewGamePlus> Finished reset base position");
            }

            // Increment site level
            public static void IncrementCombatUnitLevel(int levelBoost)
            {
                Debug.Log("<NewGamePlus> Starting level incrementation");

                // Get all entities in overworld context
                OverworldContext overworld = Contexts.sharedInstance.overworld;
                var entities = overworld.GetEntities();

                foreach (var entityOverworld in entities)
                {
                    // Skip all entities that aren't actually sites on the map
                    if (!entityOverworld.hasDataKeyOverworldEntityBlueprint)
                    {
                        Debug.LogWarning("<NewGamePlus> An entity was skipped; no blueprint");
                        continue;
                    }
                    // Skip entities that don't have a persistent context counterpart or don't have a garrison
                    var entityPersistent = IDUtility.GetLinkedPersistentEntity(entityOverworld);
                    if (entityPersistent == null || !entityPersistent.hasCombatUnits || !entityPersistent.hasCombatUnitLevel)
                    {
                        Debug.LogWarning("<NewGamePlus> An entity was skipped; no combat unit");
                        continue;
                    }

                    var levelCurrent = entityPersistent.combatUnitLevel.i;
                    entityPersistent.ReplaceCombatUnitLevel(levelCurrent + levelBoost);

                    if (entityOverworld.isPlayerKnown)
                    {
                        CIViewOverworldOverlays.ins.OnEntityChange(entityOverworld);
                        Debug.Log("<NewGamePlus> entityOverworld " + entityOverworld.nameInternal + " was refleshed");
                    }
                }

                Debug.Log("<NewGamePlus> Finished level incrementation");
            }
        }
    }
}

using HarmonyLib;
using PhantomBrigade;
using PhantomBrigade.Data;
using PhantomBrigade.Mods;
using PhantomBrigade.Overworld;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

// All mods must use a unique namespace in case they contain colliding type names, like one below
namespace PBMods.NewGamePlus
{
    // All mods using libraries must include one class inheriting from ModLink
    public class NewGamePlusLink: ModLink
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
            public static void Postfix()
            {
                Debug.Log("<NewGamePlus> Mod executes this as suffix to ScenarioFunctions.OnEndCapitalDelayed()");

                // WIP: Skipping confirmation
                ConfirmSavingNewGamePlus();

                //saveInfoHelper.buttonConfirm.available = false;
                CIViewDialogConfirmation.ins.Open("Starting New Game +", "Are you sure you'd like to start New Game +? (You can not start it later.)", ConfirmLoadingNewGamePlus, null) ;
            }
        

            public static string saveNameOfNewGamePlus = "new_game_plus";

            //public CIHelperSaveGameInfo saveInfoHelper;
            public static void ConfirmSavingNewGamePlus()
            {
                //saveInfoHelper.buttonConfirm.available = saveAvailableLast;
                DataManagerSave.SaveFromECS();
                //DataManagerSave.SaveData(DataManagerSave.SaveLocation.PickFromBuildType);
                SaveDataForNewGamePlus(DataManagerSave.SaveLocation.PickFromBuildType);
            }

            //public void CancelSavingNewGamePlus()
            //{
            //    saveInfoHelper.buttonConfirm.available = saveAvailableLast;
            //}

            public static void ConfirmLoadingNewGamePlus()
            {
                //saveInfoHelper.buttonConfirm.available = false;
                DataHelperLoading.TryLoading(saveNameOfNewGamePlus, DataManagerSave.SaveLocation.PickFromBuildType);
                Co.Delay(3, delegate
                {
                    OverworldContext overworld = Contexts.sharedInstance.overworld;
                    overworld.ReplaceCameraFocusRequest(IDUtility.playerBaseOverworld.position.v);
                    IncrementCombatUnitLevel(20);
                });
            }

            //public void CancelLoadingNewGamePlus()
            //{
            //    saveInfoHelper.buttonConfirm.available = true;
            //}

            public static void SaveDataForNewGamePlus(DataManagerSave.SaveLocation saveLocation)
            {
                DataContainerSave dataCurrent = SaveSerializationHelper.data;

                DataManagerSave.SetSaveName("save_internal_newgame");
                DataManagerSave.LoadData(DataManagerSave.SaveLocation.Internal);
                DataContainerSave dataNewGame = SaveSerializationHelper.data;
                
                if (SaveSerializationHelper.data == null)
                {
                    Debug.LogError("Failed to save due to missing internal data");
                    return;
                }

                string savePath = GetSavePathOfNewGamePlus(saveLocation);
                if (savePath != null)
                {
                    Debug.Log("Writing saved game to path " + savePath);

                    // SaveContainers with dataCurrent
                    dataCurrent.OnBeforeSerialization();

                    if (dataCurrent.metadata != null)
                    {
                        SaveContainer(savePath, "metadata.yaml", dataCurrent.metadata);
                    }

                    if (dataCurrent.core != null)
                    {
                        SaveContainer(savePath, "core.yaml", dataCurrent.core);
                    }

                    if (dataCurrent.stats != null)
                    {
                        SaveContainer(savePath, "stats.yaml", dataCurrent.stats);
                    }

                    if (dataCurrent.crawler != null)
                    {
                        SaveContainer(savePath, "crawler.yaml", dataCurrent.crawler);
                    }

                    if (dataCurrent.combat != null)
                    {
                        SaveContainer(savePath, "combat.yaml", dataCurrent.combat);
                    }

                    if (dataCurrent.units != null)
                    {
                        SaveContainers(savePath, "Units", dataCurrent.units);
                    }

                    if (dataCurrent.pilots != null)
                    {
                        SaveContainers(savePath, "Pilots", dataCurrent.pilots);
                    }

                    // SaveContainers with dataNewGame
                    dataNewGame.OnBeforeSerialization();

                    if (dataNewGame.world != null)
                    {
                        SaveContainer(savePath, "world.yaml", dataNewGame.world);
                    }

                    if (dataNewGame.provinces != null)
                    {
                        SaveContainers(savePath, "OverworldProvinces", dataNewGame.provinces);
                    }

                    if (dataNewGame.overworldEntities != null)
                    {
                        SaveContainers(savePath, "OverworldEntities", dataNewGame.overworldEntities);
                    }

                    if (dataNewGame.overworldActions != null)
                    {
                        SaveContainers(savePath, "OverworldActions", dataNewGame.overworldActions);
                    }

                    if (dataNewGame.combatActions != null)
                    {
                        SaveContainers(savePath, "CombatActions", dataNewGame.combatActions);
                    }

                    //DataManagerSave.unsavedChangesPossible = false;
                    //DataManagerSave.RefreshSaveHeaders();
                }

            }

            private static void SaveContainer<T>(string savePath, string filename, T savedObject)
            {
                UtilitiesYAML.SaveDataToFile(savePath, filename, savedObject, appendApplicationPath: false);
            }

            private static void SaveContainers<T>(string savePath, string subfolder, SortedDictionary<string, T> savedDictionary) where T : DataContainer
            {
                UtilitiesYAML.SaveDecomposedDictionary(savePath + subfolder, savedDictionary, warnAboutDeletions: false, appendApplicationPath: false);
            }

            private static string GetSavePathOfNewGamePlus(DataManagerSave.SaveLocation saveLocation)
            {
                string saveFolderPath = DataManagerSave.GetSaveFolderPath(saveLocation);
                if (string.IsNullOrEmpty(saveFolderPath))
                {
                    Debug.LogError("Failed to process saved game due to null or empty folder path");
                    return null;
                }

                if (string.IsNullOrEmpty(saveNameOfNewGamePlus))
                {
                    Debug.LogError("Failed to process saved game due to null or empty save name");
                    return null;
                }

                return saveFolderPath + saveNameOfNewGamePlus + "/";
            }
            
            public static void IncrementCombatUnitLevel(int levelBoost)
            {
                Debug.Log("<NewGamePlus> Initiated level incrementation");

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
                        Debug.Log("<NewGamePlus> entityOverworld " + entityOverworld.nameInternal + "was refleshed");
                    }
                }

                Debug.Log("<NewGamePlus> Finished level incrementation");
            }
        }
    }
}

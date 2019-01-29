﻿using BeatSaberOnline.Data;
using BeatSaberOnline.Data.Steam;
using BeatSaberOnline.Utils;
using BeatSaberOnline.Views.Menus;
using CustomAvatar;
using CustomUI.Utilities;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using VRUI;
using Logger = BeatSaberOnline.Data.Logger;
using SteamAPI = BeatSaberOnline.Data.Steam.SteamAPI;

namespace BeatSaberOnline.Controllers
{
    class GameController : MonoBehaviour
    {
        public static GameController Instance;
        public static float TPS { get; } = 25f / 1000f;
        public static float Tickrate { get; } = 1000f / TPS;
        private ResultsViewController _resultsViewController;

        private string _currentScene;

        public static void Init(Scene to)
        {
            if (Instance != null)
            {
                return;
            }
            
            new GameObject("InGameOnlineController").AddComponent<GameController>();
        }

        public void Awake()
        {
            if (Instance != this)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                _resultsViewController = Resources.FindObjectsOfTypeAll<ResultsViewController>().First();
                Scoreboard.OnLoad();
                _currentScene = SceneManager.GetActiveScene().name;
            }
        }


        public void ActiveSceneChanged(Scene from, Scene to)
        {
            try
            {
                if (!SteamAPI.isLobbyConnected())
                {
                    return;
                }
                if (to.name == "GameCore" || to.name == "Menu")
                {
                   PlayerController.Instance.DestroyAvatars();
                }
            }
            catch (Exception e)
            {
                Logger.Error($"(OnlineController) Exception on {_currentScene} scene activation! Exception: {e}");
            }
        }
        private FlowCoordinator GetActiveFlowCoordinator()
        {
            FlowCoordinator[] flowCoordinators = Resources.FindObjectsOfTypeAll<FlowCoordinator>();
            foreach (FlowCoordinator f in flowCoordinators)
            {
                if (f.isActivated)
                    return f;
            }
            return null;
        }

        public void SongFinished(StandardLevelSceneSetupDataSO sender, LevelCompletionResults levelCompletionResults, IDifficultyBeatmap difficultyBeatmap, GameplayModifiers gameplayModifiers)
        {
            if (levelCompletionResults.levelEndStateType == LevelCompletionResults.LevelEndStateType.Quit || (sender == null && levelCompletionResults == null && difficultyBeatmap == null && gameplayModifiers == null))
            {
                PauseMenuManager pauseMenu = Resources.FindObjectsOfTypeAll<PauseMenuManager>().FirstOrDefault();
                pauseMenu?.MenuButtonPressed();
                SteamAPI.StopSong();
            }
            Logger.Debug("Finished song: " + levelCompletionResults.levelEndStateType + " - " + levelCompletionResults.songDuration + " - - " + levelCompletionResults.endSongTime);
            WaitingMenu.firstInit = true;
            WaitingMenu.Instance.Dismiss();
            SteamAPI.FinishSong();
           // if (_resultsViewController)
           // {
           //     _resultsViewController.Init(levelCompletionResults, difficultyBeatmap, false);
           //     FlowCoordinator activeFlowCoordinator = GetActiveFlowCoordinator();
           //     activeFlowCoordinator.InvokePrivateMethod("SetLeftScreenViewController", new object[] { _resultsViewController, false });
           // }
            PlayerDataModelSO _playerDataModel = Resources.FindObjectsOfTypeAll<PlayerDataModelSO>().First();

            _playerDataModel.currentLocalPlayer.playerAllOverallStatsData.soloFreePlayOverallStatsData.UpdateWithLevelCompletionResults(levelCompletionResults);
            _playerDataModel.Save();
            if (levelCompletionResults.levelEndStateType != LevelCompletionResults.LevelEndStateType.Failed && levelCompletionResults.levelEndStateType != LevelCompletionResults.LevelEndStateType.Cleared)
            {
                return;
            }

            PlayerDataModelSO.LocalPlayer currentLocalPlayer = _playerDataModel.currentLocalPlayer;
            bool cleared = levelCompletionResults.levelEndStateType == LevelCompletionResults.LevelEndStateType.Cleared;
            string levelID = difficultyBeatmap.level.levelID;
            BeatmapDifficulty difficulty = difficultyBeatmap.difficulty;
            PlayerLevelStatsData playerLevelStatsData = currentLocalPlayer.GetPlayerLevelStatsData(levelID, difficulty);
            bool newHighScore = playerLevelStatsData.highScore < levelCompletionResults.score;
            playerLevelStatsData.IncreaseNumberOfGameplays();
            if (cleared)
            {
                playerLevelStatsData.UpdateScoreData(levelCompletionResults.score, levelCompletionResults.maxCombo, levelCompletionResults.fullCombo, levelCompletionResults.rank);
                Resources.FindObjectsOfTypeAll<PlatformLeaderboardsModel>().First().AddScore(difficultyBeatmap, levelCompletionResults.unmodifiedScore, gameplayModifiers);
            }

        }

    }

}

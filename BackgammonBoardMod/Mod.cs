using MelonLoader;
using System.IO;
using System.Reflection;
using UnityEngine;
using Venomaus.BigAmbitionsMods.Common;

namespace BackgammonBoardMod
{
    public class Mod : MelonMod
    {
        public const string Name = "BackgammonBoardMod";
        public const string Author = "Venomaus";
        public const string Version = "1.0.0";

        private GameObject _backGammonBoardPrefab;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg($"Loading backgammon board asset bundle.");
            LoggerInstance.Msg($"Attempting to add backgammon board asset to the game.");

            InitializeAsset();
            if (_backGammonBoardPrefab == null) return;

            LoggerInstance.Msg($"Succes.");
        }

        private void InitializeAsset()
        {
            var folder = Lib.SaveData.GetSaveStoreFolderPath(Assembly.GetExecutingAssembly());
            var path = Path.Combine(folder, "..", "Assets/backgammon_board_bundle");
            var bundle = AssetBundle.LoadFromFile(path);
            if (bundle == null)
            {
                LoggerInstance.Msg($"Failed to load asset bundle.");
                return;
            }

            _backGammonBoardPrefab = bundle.LoadAsset<GameObject>("Backgammon_Board_Prefab");
            if (_backGammonBoardPrefab == null)
                LoggerInstance.Msg($"Failed to load prefab asset.");

            bundle.Unload(false);
        }
    }
}

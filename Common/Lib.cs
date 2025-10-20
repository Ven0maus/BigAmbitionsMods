using Venomaus.BigAmbitionsMods.Common.Core;

namespace Venomaus.BigAmbitionsMods.Common
{
    /// <summary>
    /// Library to access all common functionalities.
    /// </summary>
    public static class Lib
    {
        private static TimeLib _timeLib;
        private static SaveDataLib _saveDataLib;
        private static ConfigLib _configLib;

        /// <summary>
        /// Contains everything related to game time.
        /// </summary>
        public static TimeLib Time => _timeLib ?? (_timeLib = new TimeLib());

        /// <summary>
        /// Contains everything related to saving and loading gamedata.
        /// </summary>
        public static SaveDataLib SaveData => _saveDataLib ?? (_saveDataLib = new SaveDataLib());

        /// <summary>
        /// Contains everything related to building and retrieving persistent configuration data.
        /// </summary>
        public static ConfigLib Config => _configLib ?? (_configLib = new ConfigLib());
    }
}

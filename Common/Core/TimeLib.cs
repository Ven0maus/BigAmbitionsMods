using HarmonyLib;
using System;

namespace Venomaus.BigAmbitionsMods.Common.Core
{
    /// <summary>
    /// Helper to subscribe to time change events.
    /// </summary>
    public sealed class TimeLib
    {
        internal TimeLib() { }

        /// <summary>
        /// Raised when a minute is passed.
        /// </summary>
        public event EventHandler<TimeArgs> OnMinutePassed;
        /// <summary>
        /// Raised when an hour is passed.
        /// </summary>
        public event EventHandler<TimeArgs> OnHourPassed;
        /// <summary>
        /// Raised when a day is passed.
        /// </summary>
        public event EventHandler<TimeArgs> OnDayPassed;

        [HarmonyPatch(typeof(GameManager), nameof(GameManager.RunMainGameTick))]
        internal static class GameManager_RunMainGameTick
        {
            private static float _currentMinute;

            [HarmonyPrefix]
            internal static void Prefix()
            {
                _currentMinute = TimeHelper.CurrentMinute;
            }

            [HarmonyPostfix]
            internal static void Postfix(float deltaTimeWithMultiplier)
            {
                var newMinutes = _currentMinute + deltaTimeWithMultiplier;

                if ((int)_currentMinute < (int)newMinutes)
                    Lib.Time.OnMinutePassed?.Invoke(null, new TimeArgs((int)_currentMinute, ((int)newMinutes) == 60 ? 0 : (int)newMinutes));

                if (newMinutes >= 60f)
                {
                    var prevHour = SaveGameManager.Current.Hour == 0 ? 23 : SaveGameManager.Current.Hour - 1;
                    var newHour = prevHour + 1;
                    if (newHour >= 24)
                    {
                        newHour -= 24;
                        Lib.Time.OnDayPassed?.Invoke(null, new TimeArgs(SaveGameManager.Current.Day - 1, SaveGameManager.Current.Day));
                    }

                    Lib.Time.OnHourPassed?.Invoke(null, new TimeArgs(prevHour, SaveGameManager.Current.Hour));
                }
            }
        }

        public sealed class TimeArgs : EventArgs
        {
            public int PreviousValue { get; }
            public int NewValue { get; }

            internal TimeArgs(int previousValue, int newValue)
            {
                PreviousValue = previousValue;
                NewValue = newValue;
            }
        }
    }
}

using MelonLoader;
using MelonLoader.Logging;
using MelonLoader.Pastel;

namespace Venomaus.BigAmbitionsMods.Common.Helpers
{
    /// <summary>
    /// A parser to parse and log several different colors in one message.
    /// </summary>
    public class ColoredStringBuilder
    {
        private string _content;

        private ColoredStringBuilder() { }

        /// <summary>
        /// Use <see cref="Append(string, ColorARGB?)"/> method to build extra colored parts.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="color"></param>
        /// <returns></returns>
        public static ColoredStringBuilder Create(string text, ColorARGB? color = null)
        {
            return new ColoredStringBuilder()
                .Append(text, color);
        }

        /// <summary>
        /// Append a new text segment with the specified color, if color is null it will use the default color of melon logger.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="color"></param>
        /// <returns></returns>
        public ColoredStringBuilder Append(string text, ColorARGB? color = null)
        {
            if (color == null) color = MelonLogger.DefaultTextColor;
            _content += text.Pastel(color.Value);
            return this;
        }

        /// <summary>
        /// Sends the message to the MelonLogger instance console.
        /// </summary>
        public void SendMessageToConsole(MelonLogger.Instance loggerInstance)
        {
            // TODO: Figure out a way to not log ANSI codes to the logfile
            loggerInstance.Msg(_content);
        }
    }
}

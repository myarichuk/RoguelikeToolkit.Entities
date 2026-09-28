namespace RoguelikeToolkit.Entities
{
    /// <summary>
    /// Lightweight logging hooks for template parse and component-binding diagnostics.
    /// Hard failures (unparseable templates, unknown component types, cyclic references) throw;
    /// this hook covers soft, otherwise-silent drops: template properties that match no component
    /// member (likely typos) and values that convert to <see langword="null"/> and are skipped.
    /// The handler is invoked synchronously on the spawning thread; keep it fast and non-throwing
    /// (a throwing handler is swallowed so spawning never breaks because of logging).
    /// </summary>
    public static class EntityDiagnostics
    {
        private static Action<string>? _warningHandler;

        /// <summary>
        /// Gets or sets the handler for diagnostic warnings. <see langword="null"/> (the default) disables warnings.
        /// </summary>
        public static Action<string>? WarningHandler
        {
            get => _warningHandler;
            set => _warningHandler = value;
        }

        /// <summary>
        /// Invokes the warning handler when one is set. A throwing handler is swallowed.
        /// </summary>
        /// <param name="message">Warning message to report.</param>
        internal static void Warn(string message)
        {
            var handler = _warningHandler;
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(message);
            }
            catch
            {
                // Logging must never break entity creation; swallow handler faults.
            }
        }
    }
}

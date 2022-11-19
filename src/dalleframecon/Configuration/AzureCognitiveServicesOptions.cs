namespace dalleframecon.Configuration
{
    /// <summary>
    /// Configuration options for interacting with OpenAI.
    /// </summary>
    internal class OpenAiServiceOptions
    {
        /// <summary>
        /// API Key.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Validate options, throw an exception is any are invalid.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Key))
                throw new ArgumentException("Argument is invalid.", nameof(Key));
        }
    }
}

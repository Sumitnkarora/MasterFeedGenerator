namespace Indigo.Feeds.Generator.Core.Processors.Contracts
{
    public interface IDestinationProcessor<T>
    {
        /// <summary>
        /// Determines if destination is open for processing.
        /// </summary>
        bool IsOpen { get; }

        /// <summary>
        /// Opens the destination for processing.
        /// </summary>
        void Open();

        /// <summary>
        /// Processes the <paramref name="instruction"/>.
        /// </summary>
        /// <param name="instruction">Instruction for destination.</param>
        /// <returns>True if succes; false otherwise.</returns>
        bool Process(T instruction);

        /// <summary>
        /// Closes the destination.
        /// </summary>
        void Close();
    }
}

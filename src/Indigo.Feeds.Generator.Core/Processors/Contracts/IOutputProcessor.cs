using Indigo.Feeds.Generator.Core.Enums;
using Indigo.Feeds.Generator.Core.Models;
using System.Collections.Specialized;
using System.Data;

namespace Indigo.Feeds.Generator.Core.Processors.Contracts
{
    /// <summary>
    /// Output processor interface.
    /// </summary>
    public interface IOutputProcessor
    {
        /// <summary>
        /// Initializes the output processor.
        /// </summary>
        /// <returns>True if there was a done file present; False otherwise.</returns>
        bool Initialize();

        /// <summary>
        /// Creates the output from <paramref name="reader"/>.
        /// </summary>
        /// <param name="reader">The reader.</param>
        /// <param name="dict">Dictionary of column name translations.</param>
        /// <param name="catalog">Catalog name.</param>
        /// <param name="identifier">Identifier for output.</param>
        /// <param name="runType">Run type.</param>
        /// <returns>Processing counters.</returns>
        ProcessingCounters CreateOutput(IDataReader reader, StringDictionary dict, string catalog, string identifier, RunType runType);

        /// <summary>
        /// Records the output per <paramref name="instruction"/>.
        /// </summary>
        /// <param name="instruction">Instruction for output processing.</param>
        void RecordOutput(OutputInstruction instruction);
        
        /// <summary>
        /// Finalizes output processing.
        /// </summary>
        /// <returns>True if all output instructions were processed successfully; False otherwise.</returns>
        bool FinalizeProcessing(bool isSuccessfulRun);
    }
}

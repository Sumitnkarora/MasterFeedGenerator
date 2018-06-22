using Indigo.Feeds.Generator.Core.Enums;
using System;

namespace Indigo.Feeds.Generator.Core.Execution.Contracts
{
    /// <summary>
    /// Runner interface.
    /// </summary>
    public interface IRunner
    {
        /// <summary>
        /// Executes the run.
        /// </summary>
        /// <returns>Information about the execution.</returns>
        ExecutionInformation Execute();

        /// <summary>
        /// Finalizes the run.
        /// </summary>
        /// <param name="isSuccess"></param>
        /// <returns>True if successfult; false otherwise.</returns>
        bool Finalize(bool isSuccess);

        /// <summary>
        /// Initializes a run.
        /// </summary>
        /// <param name="runType">Run type.</param>
        /// <param name="effectiveFromTime">Effective start time.</param>
        /// <param name="effectiveToTime">Effective end time.</param>
        void Initialize(RunType runType, DateTime? effectiveFromTime, DateTime? effectiveToTime);

        /// <summary>
        /// Determines if runner is ready to perform another run.
        /// </summary>
        /// <returns>True if ready; false otherwise.</returns>
        bool IsReady();
    }
}

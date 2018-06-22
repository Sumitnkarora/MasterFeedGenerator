using GoogleApiPlaFeedGenerator.Json;

namespace GoogleApiPlaFeedGenerator.Execution
{
    public interface IOutputInstructionProcessor
    {
        /// <summary>
        /// Returns true if there was a done file and this files were processed during initialization. False
        /// otherwise.
        /// </summary>
        /// <returns></returns>
        bool Initialize();
        void RecordOutputInstruction(OutputInstruction content, string folderName, string fileName);
        /// <summary>
        /// Returns true if all output instructions were processed successfully (i.e. Google received all). 
        /// False otherwise. 
        /// </summary>
        /// <returns></returns>
        bool FinalizeProcessing(bool isSuccessfulRun);
    }
}

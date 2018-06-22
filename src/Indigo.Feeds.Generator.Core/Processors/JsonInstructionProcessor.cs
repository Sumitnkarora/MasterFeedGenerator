using Indigo.Feeds.Generator.Core.Models;
using Newtonsoft.Json;
using System.IO;

namespace Indigo.Feeds.Generator.Core.Processors
{
    /// <summary>
    /// Instruction processor using json format.
    /// </summary>
    public class JsonInstructionProcessor : BaseFileProcessor
    {
        /// <summary>
        /// Reads a json file and converts it to output instruction.
        /// </summary>
        /// <param name="fileInfo">File info.</param>
        /// <returns>Output instruction.</returns>
        public override OutputInstruction FileRead(FileInfo fileInfo)
        {
            OutputInstruction instruction = null;
            using (var reader = File.OpenText(fileInfo.FullName))
            {
                using (var jsonStreamer = new JsonTextReader(reader))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    instruction = serializer.Deserialize<OutputInstruction>(jsonStreamer);
                }
            }

            return instruction;
        }

        /// <summary>
        /// Writes a json file with given <paramref name="instruction"/>.
        /// </summary>
        /// <param name="instruction">Instruction with content.</param>
        /// <returns>File info.</returns>
        public override FileInfo FileWrite(OutputInstruction instruction)
        {
            var filePath = Path.Combine(instruction.OutputLocation, instruction.OutputName + ".json");

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                using (StreamWriter writer = new StreamWriter(stream))
                {
                    using (JsonWriter jsonWriter = new JsonTextWriter(writer))
                    {
                        var serializer = new JsonSerializer();
                        serializer.Serialize(jsonWriter, instruction);
                    }
                }
            }

            return new FileInfo(filePath);
        }

        /// <summary>
        /// Gets all json content files in <paramref name="folderPath"/> and subfolders.
        /// </summary>
        /// <param name="folderPath">Folder path.</param>
        /// <returns>All files.</returns>
        public override FileInfo[] GetContentFiles(string folderPath)
        {
            var workingFolder = new DirectoryInfo(Path.Combine(folderPath));
            return workingFolder.GetFiles("*.json", SearchOption.AllDirectories);
        }
    }
}

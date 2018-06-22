using Indigo.Feeds.Generator.Core.Models;
using System.IO;

namespace Indigo.Feeds.Generator.Core.Processors.Contracts
{
    /// <summary>
    /// File content processor interface.
    /// </summary>
    public interface IFileContentProcessor
    {
        /// <summary>
        /// Creates directory.
        /// </summary>
        /// <param name="folderPath">Directory path to create.</param>
        void DirectoryCreate(string folderPath);

        /// <summary>
        /// Checks for directory existence.
        /// </summary>
        /// <param name="folderPath">Folder path.</param>
        /// <returns>True if exists; false otherwise.</returns>
        bool DirectoryExists(string folderPath);

        /// <summary>
        /// Deletes directory.
        /// </summary>
        /// <param name="folderPath">Folder path.</param>
        void DirectoryDelete(string folderPath);

        /// <summary>
        /// Creates an empty file.
        /// </summary>
        /// <param name="filePath">File path.</param>
        void FileCreateEmpty(string filePath);

        /// <summary>
        /// Creates a file for writing.
        /// </summary>
        /// <param name="filePath">File path.</param>
        /// <returns>File stream.</returns>
        Stream FileCreate(string filePath);

        /// <summary>
        /// Copies file from <paramref name="sourceFileName"/> to <paramref name="destFileName"/>.
        /// </summary>
        /// <param name="sourceFileName">Source file.</param>
        /// <param name="destFileName">Destination file.</param>
        /// <param name="overwrite">Determines if overwrite permitted.</param>
        void FileCopy(string sourceFileName, string destFileName, bool overwrite);

        /// <summary>
        /// Checks for file existence.
        /// </summary>
        /// <param name="filePath">File path.</param>
        /// <returns>True if exists; false otherwise.</returns>
        bool FileExists(string filePath);

        /// <summary>
        /// Writes a file with provided <paramref name="instruction"/>.
        /// </summary>
        /// <param name="instruction">Instruction with content.</param>
        /// <returns>File info.</returns>
        FileInfo FileWrite(OutputInstruction instruction);

        /// <summary>
        /// Reads a file with provided <paramref name="instruction"/>.
        /// </summary>
        /// <param name="source">Source file info.</param>
        /// <returns>Output instruction.</returns>
        OutputInstruction FileRead(FileInfo file);

        /// <summary>
        /// Opens a <paramref name="file"/> for reading.
        /// </summary>
        /// <param name="file">Source file info.</param>
        /// <returns>Stream with file data.</returns>
        Stream FileStream(FileInfo file);

        /// <summary>
        /// Removes a file.
        /// </summary>
        /// <param name="fileInfo">File to remove.</param>
        void FileRemove(FileInfo fileInfo);

        /// <summary>
        /// Removes a file with <paramref name="filePath"/>.
        /// </summary>
        /// <param name="filePath">File path.</param>
        void FileRemove(string filePath);

        /// <summary>
        /// Gets all content files in <paramref name="folderPath"/> and subfolders.
        /// </summary>
        /// <param name="folderPath">Folder path.</param>
        /// <returns>All files.</returns>
        FileInfo[] GetContentFiles(string folderPath);
    }
}

using Indigo.Feeds.Generator.Core.Models;
using Indigo.Feeds.Generator.Core.Processors.Contracts;
using System.IO;

namespace Indigo.Feeds.Generator.Core.Processors
{
    /// <summary>
    /// Base file processor.
    /// </summary>
    public abstract class BaseFileProcessor : IFileContentProcessor
    {
        public abstract OutputInstruction FileRead(FileInfo file);
        
        public abstract FileInfo FileWrite(OutputInstruction instruction);

        public abstract FileInfo[] GetContentFiles(string folderPath);

        /// <summary>
        /// Checks for directory existence.
        /// </summary>
        /// <param name="folderPath">Folder path.</param>
        /// <returns>True if exists; false otherwise.</returns>
        public virtual bool DirectoryExists(string folderPath)
        {
            return Directory.Exists(folderPath);
        }

        /// <summary>
        /// Creates directory.
        /// </summary>
        /// <param name="folderPath">Directory path to create.</param>
        public virtual void DirectoryCreate(string folderPath)
        {
            Directory.CreateDirectory(folderPath);
        }

        /// <summary>
        /// Deletes directory.
        /// </summary>
        /// <param name="folderPath">Folder path.</param>
        public virtual void DirectoryDelete(string folderPath)
        {
            // Delete all files and folders inside folder path
            var directoryInfo = new DirectoryInfo(folderPath);
            foreach (var file in directoryInfo.GetFiles())
            {
                file.Delete();
            }

            foreach (var folder in directoryInfo.GetDirectories())
            {
                folder.Delete(true);
            }
        }

        /// <summary>
        /// Creates an empty file.
        /// </summary>
        /// <param name="filePath">File path.</param>
        public virtual void FileCreateEmpty(string filePath)
        {
            using (new FileStream(filePath, FileMode.Create))
            {
                // Nothing to do
            }
        }

        /// <summary>
        /// Creates a file for writing.
        /// </summary>
        /// <param name="filePath">File path.</param>
        /// <returns>File stream.</returns>
        public virtual Stream FileCreate(string filePath)
        {
            return File.Create(filePath);
        }

        /// <summary>
        /// Checks for file existence.
        /// </summary>
        /// <param name="filePath">File path.</param>
        /// <returns>True if exists; false otherwise.</returns>
        public virtual bool FileExists(string path)
        {
            return File.Exists(path);
        }

        /// <summary>
        /// Removes a file.
        /// </summary>
        /// <param name="fileInfo">File to remove.</param>
        public virtual void FileRemove(FileInfo fileInfo)
        {
            FileRemove(fileInfo.FullName);
        }

        public virtual void FileRemove(string filePath)
        {
            File.Delete(filePath);
        }
        
        /// <summary>
        /// Opens a <paramref name="file"/> for reading.
        /// </summary>
        /// <param name="file">Source file info.</param>
        /// <returns>Stream with file data.</returns>
        public virtual Stream FileStream(FileInfo file)
        {
            return File.OpenRead(file.FullName);
        }


        /// <summary>
        /// Copies file from <paramref name="sourceFileName"/> to <paramref name="destFileName"/>.
        /// </summary>
        /// <param name="sourceFileName">Source file.</param>
        /// <param name="destFileName">Destination file.</param>
        /// <param name="overwrite">Determines if overwrite permitted.</param>
        public virtual void FileCopy(string sourceFileName, string destFileName, bool overwrite)
        {
            File.Copy(sourceFileName, destFileName, overwrite);
        }
    }
}

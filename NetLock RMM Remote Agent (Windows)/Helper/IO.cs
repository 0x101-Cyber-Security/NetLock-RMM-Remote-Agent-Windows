using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static NetLock_RMM_Remote_Agent_Windows.Service;

namespace NetLock_RMM_Remote_Agent_Windows.Helper
{
    internal class IO
    {
        public class File_Or_Directory_Info
        {
            public string name { get; set; }
            public string path { get; set; }
            public string type { get; set; }
            public string size { get; set; }
            public DateTime last_modified { get; set; }
        }

        // Get directories from path
        public static List<File_Or_Directory_Info> Get_Directory_Index(string path)
        {
            var directoryDetails = new List<File_Or_Directory_Info>();

            try
            {
                DirectoryInfo rootDirInfo = new DirectoryInfo(path);

                // Directories
                foreach (var directory in rootDirInfo.GetDirectories())
                {
                    var dirDetail = new File_Or_Directory_Info
                    {
                        name = directory.Name,
                        path = directory.FullName,
                        last_modified = directory.LastWriteTime,
                        size = GetDirectorySizeInGB(directory),
                        type = "0", // 0 = Directory
                    };

                    directoryDetails.Add(dirDetail);
                }

                // Files
                foreach (var file in rootDirInfo.GetFiles())
                {
                    var fileDetail = new File_Or_Directory_Info
                    {
                        name = file.Name,
                        path = file.FullName,
                        last_modified = file.LastWriteTime,
                        size = Get_File_Size_In_GB(file.FullName),
                        type = file.Extension,
                    };

                    directoryDetails.Add(fileDetail);
                }

                return directoryDetails;
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("IO.GetDirectoryDetails", "General error", ex.ToString());
                return directoryDetails;
            }
        }

        // Get files from path
        /*public static List<File_Detail> GetFiles(string path)
        {
            var files = new List<File_Detail>();

            try
            {
                DirectoryInfo rootDirInfo = new DirectoryInfo(path);

                foreach (var file in rootDirInfo.GetFiles())
                {
                    var fileDetail = new File_Detail
                    {
                        Name = file.Name,
                        Path = file.FullName,
                        Type = file.Extension,
                        Size = file.Length,
                        LastModified = file.LastWriteTime,
                    };

                    files.Add(fileDetail);
                }

                return files;
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("IO.GetFiles", "General error", ex.ToString());
                return files;
            }
        }*/

        // Get drives
        public static string Get_Drives()
        {
            var driveLetters = new List<string>();

            try
            {
                DriveInfo[] allDrives = DriveInfo.GetDrives();

                foreach (DriveInfo d in allDrives)
                {
                    if (d.IsReady)
                    {
                        driveLetters.Add(d.Name);
                    }
                }

                return string.Join(",", driveLetters);
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("IO.GetDriveLetters", "General error", ex.ToString());
                return string.Empty;
            }
        }

        public static string GetDirectorySizeInGB(DirectoryInfo directory)
        {
            long size = 0;

            try
            {
                // Add file sizes.
                FileInfo[] fis = directory.GetFiles();
                foreach (FileInfo fi in fis)
                {
                    size += fi.Length;
                }

                // Convert size to GB and format to 2 decimal places.
                double sizeInGB = size / (1024.0 * 1024.0 * 1024.0);
                return sizeInGB.ToString("F2");
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("IO.GetDirectorySizeInGB", "General error", ex.ToString());
                return "0.00";
            }
        }

        public static string Get_File_Size_In_GB(string file_path)
        {
            try
            {
                FileInfo fileInfo = new FileInfo(file_path);
                long size = fileInfo.Length;

                // Convert size to GB and format to 2 decimal places.
                double sizeInGB = size / (1024.0 * 1024.0 * 1024.0);
                return sizeInGB.ToString("F2");
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("IO.Get_File_Size_In_GB", "General error", ex.ToString());
                return "0.00";
            }
        }

        // Create directory and return true if successful
        public static string Create_Directory(string path)
        {
            try
            {
                // Check if the directory exists
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                return path;
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("IO.Create_Directory", "General error", ex.ToString());
                return ex.Message;
            }
        }

        // Delete directory and files and return true if successful
        public static string Delete_Directory(string path)
        {
            StringBuilder deletedItems = new StringBuilder();

            try
            {
                // Check if the directory exists
                if (Directory.Exists(path))
                {
                    // Recursively delete all files and subdirectories
                    Delete_Directory_Recursive(new DirectoryInfo(path), deletedItems);

                    // Delete the root directory itself
                    Directory.Delete(path, true);

                    // Append the root directory to the list of deleted items
                    deletedItems.AppendLine(path);
                }

                return deletedItems.ToString();
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("IO.DeleteDirectoryAndListContents", "General error", ex.ToString());
                return ex.Message + Environment.NewLine + Environment.NewLine + deletedItems.ToString();
            }
        }

        private static void Delete_Directory_Recursive(DirectoryInfo directoryInfo, StringBuilder deletedItems)
        {
            try
            {
                // Delete all files in the directory
                foreach (FileInfo file in directoryInfo.GetFiles())
                {
                    try
                    {
                        file.Delete();
                        deletedItems.AppendLine(file.FullName);
                    }
                    catch (Exception ex)
                    {
                        Logging.Handler.Error("IO.DeleteDirectoryRecursive", "Error deleting file", ex.ToString());
                    }
                }

                // Recursively delete all subdirectories
                foreach (DirectoryInfo subdirectory in directoryInfo.GetDirectories())
                {
                    try
                    {
                        Delete_Directory_Recursive(subdirectory, deletedItems);
                        subdirectory.Delete();
                        deletedItems.AppendLine(subdirectory.FullName);
                    }
                    catch (Exception ex)
                    {
                        Logging.Handler.Error("IO.DeleteDirectoryRecursive", "Error deleting directory", ex.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("IO.Delete_Directory_Recursive", "General error", ex.ToString());
            }
        }

        // Move directory and return true if successful
        public static string Move_Directory(string source_path, string destination_path)
        {
            Logging.Handler.Debug("IO.Move_Directory", "", $"Source: {source_path}, Destination: {destination_path}");

            var movedItems = new List<string>();

            try
            {
                // Überprüfen, ob das Quellverzeichnis existiert
                if (Directory.Exists(source_path))
                {
                    // Überprüfen, ob das Zielverzeichnis existiert, und ggf. erstellen
                    if (!Directory.Exists(destination_path))
                        Directory.CreateDirectory(destination_path);

                    // Alle Dateien aus dem Quellverzeichnis in das Zielverzeichnis verschieben
                    var files = Directory.GetFiles(source_path);
                    foreach (var file in files)
                    {
                        var destFile = Path.Combine(destination_path, Path.GetFileName(file));
                        if (File.Exists(destFile))
                        {
                            // Dateinamenskonflikt behandeln
                            var newFileName = Path.GetFileNameWithoutExtension(destFile) + "_copy" + Path.GetExtension(destFile);
                            destFile = Path.Combine(destination_path, newFileName);
                        }
                        File.Move(file, destFile);
                        movedItems.Add(destFile);
                    }

                    // Alle Unterverzeichnisse aus dem Quellverzeichnis in das Zielverzeichnis verschieben
                    var directories = Directory.GetDirectories(source_path);
                    foreach (var directory in directories)
                    {
                        var destDir = Path.Combine(destination_path, Path.GetFileName(directory));
                        if (Directory.Exists(destDir))
                        {
                            // Verzeichnisnamenskonflikt behandeln
                            var newDirName = Path.GetFileName(directory) + "_copy";
                            destDir = Path.Combine(destination_path, newDirName);
                        }
                        Directory.Move(directory, destDir);
                        movedItems.Add(destDir);
                    }

                    // Am Ende das Quellverzeichnis selbst nicht verschieben, da alle Inhalte bereits verschoben wurden
                }
                else
                {
                    return $"Source path does not exist: {source_path}";
                }

                return string.Join(Environment.NewLine, movedItems);
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("IO.Move_Directory", "General error", ex.ToString());
                return ex.Message;
            }
        }

        // Rename directory or file and return true if successful
        public static string Rename_Directory(string sourceDirectoryPath, string newDirectoryName)
        {
            try
            {
                if (Directory.Exists(sourceDirectoryPath))
                {
                    string parentDirectory = Path.GetDirectoryName(sourceDirectoryPath);
                    string destDirectoryPath = Path.Combine(parentDirectory, newDirectoryName);

                    // Überprüfen, ob der neue Ordnername bereits existiert
                    if (Directory.Exists(destDirectoryPath))
                    {
                        throw new IOException("A directory with the same name already exists.");
                    }

                    Directory.Move(sourceDirectoryPath, destDirectoryPath);

                    return destDirectoryPath;
                }
                else
                {
                    return "The source directory does not exist.";
                }
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("IO.RenameDirectory", "General error", ex.ToString());
                return ex.Message;
            }
        }

        // Create file and return true if successful
        public static async Task<string> Create_File(string path, string content)
        {
            try
            {
                // Check if the file exists
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, await Base64.Handler.Decode(content));
                }

                return path;
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("IO.Create_File", "General error", ex.ToString());
                return ex.Message;
            }
        }

        // Delete file and return true if successful
        public static string Delete_File(string path)
        {
            try
            {
                // Check if the file exists
                if (File.Exists(path))
                {
                    File.Delete(path);
                    return path;
                }

                return true.ToString();
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("IO.Delete_File", "General error", ex.ToString());
                return ex.Message;
            }
        }

        // Move file and return true if successful
        public static string Move_File(string source_path, string destination_path)
        {
            try
            {
                // Check if the source file exists
                if (File.Exists(source_path))
                {
                    // Check if the destination directory exists
                    if (!Directory.Exists(Path.GetDirectoryName(destination_path)))
                        Directory.CreateDirectory(Path.GetDirectoryName(destination_path));

                    // Move the file
                    File.Move(source_path, destination_path);

                    return destination_path;
                }

                return "The source file does not exist.";
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("IO.Move_File", "General error", ex.ToString());
                return ex.Message;
            }
        }

        // Rename file and return true if successful
        public static string Rename_File(string sourceFilePath, string newFileName)
        {
            try
            {
                if (File.Exists(sourceFilePath))
                {
                    string parentDirectory = Path.GetDirectoryName(sourceFilePath);
                    string destFilePath = Path.Combine(parentDirectory, newFileName);

                    // Überprüfen, ob der neue Dateiname bereits existiert
                    if (File.Exists(destFilePath))
                    {
                        return "A file with the same name already exists.";
                    }

                    File.Move(sourceFilePath, destFilePath);

                    return destFilePath;
                }
                else
                {
                    return "The source file does not exist.";
                }
            }
            catch (Exception ex)
            {
                Logging.Handler.Error("IO.Rename_File", "General error", ex.ToString());
                return ex.Message;
            }
        }

    }
}

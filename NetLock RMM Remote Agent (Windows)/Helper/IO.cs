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

    }
}

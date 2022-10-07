namespace DoenaSoft.CalculateDvdDiscId
{
    using System;
    using System.IO;
    using System.Linq;

    /// <summary>
    /// Based on US patent 6,871,012 B1
    /// http://patentimages.storage.googleapis.com/pdfs/US6871012.pdf
    /// </summary>
    public static partial class DvdDiscIdCalculator
    {
        private const int MaxReadOutLength = 65_536;

        private const string VideoFolderName = "VIDEO_TS";

        private static readonly DateTime _baseDateUtc = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Calculates the Disc Id of a video DVD for Windows.
        /// </summary>
        public static string Calculate(string driveLetter)
        {
            var drive = GetDrive(driveLetter);

            //Step 1:
            //The filenames of the VIDEO_TS directory are collected and sorted alphabetically
            var fileNames = Directory.GetFiles(Path.Combine(drive.Name, VideoFolderName), "*.*", SearchOption.TopDirectoryOnly);

            var result = (new Patent6871012B1Calculator(drive)).Calculate(fileNames);

            return result;
        }

        /// <summary>
        /// Since Windows 10 version 1809 the calculation algorithm has changed which leads to different (i.e. faulty) disc Ids.
        /// This method exists to reproduce the faulty results.
        /// </summary>
        public static string CalculateForWin10_1809_AndHigher(string driveLetter)
        {
            var drive = GetDrive(driveLetter);

            var videoFolderName = Path.Combine(drive.Name, VideoFolderName);

            //Step 1:
            //The filenames of the VIDEO_TS directory are collected and sorted alphabetically, but only VIDEO_TS.* and VTS_01_0.*
            var fileNames = Directory.GetFiles(videoFolderName, "VIDEO_TS.*", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(videoFolderName, "VTS_01_0.*", SearchOption.TopDirectoryOnly));

            var result = (new Patent6871012B1Calculator(drive)).Calculate(fileNames);

            return result;
        }

        private static DriveInfo GetDrive(string driveLetter)
        {
            if (string.IsNullOrEmpty(driveLetter))
            {
                throw new ArgumentException(nameof(driveLetter));
            }

            var result = new DriveInfo(driveLetter);

            if (!result.IsReady)
            {
                throw new ArgumentException("Drive is not ready!", nameof(driveLetter));
            }

            var folder = new DirectoryInfo(Path.Combine(result.Name, VideoFolderName));

            if (!folder.Exists)
            {
                throw new ArgumentException("Drive does not contain a video DVD!", nameof(driveLetter));
            }

            return result;
        }
    }
}
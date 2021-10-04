using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DoenaSoft.CalculateDvdDiscId
{
    /// <summary>
    /// Based on US patent 6,871,012 B1
    /// http://patentimages.storage.googleapis.com/pdfs/US6871012.pdf
    /// </summary>
    public static partial class DvdDiscIdCalculator
    {
        private const int MaxReadOutLength = 65_536;

        private const string VideoFolderName = "VIDEO_TS";

        private static readonly DateTime _baseDateUtc = new DateTime(1601, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static string Calculate(string driveLetter)
        {
            DriveInfo drive = GetDrive(driveLetter);

            //Step 1:
            //The filenames of the VIDEO_TS directory are collected and sorted alphabetically
            IEnumerable<string> fileNames = Directory.GetFiles(Path.Combine(drive.Name, VideoFolderName), "*.*", SearchOption.TopDirectoryOnly);

            string result = Calculate(drive, fileNames);

            return result;
        }

        /// <summary>
        /// Since Windows 10 version 1809 the calculation algorithm has changed which leads to different (i.e. faulty) disc Ids.
        /// This method exists to reproduce the faulty results.
        /// </summary>
        public static string CalculateForWin10_1809_AndHigher(string driveLetter)
        {
            DriveInfo drive = GetDrive(driveLetter);

            string videoFolderName = Path.Combine(drive.Name, VideoFolderName);

            //Step 1:
            //The filenames of the VIDEO_TS directory are collected and sorted alphabetically, but only VIDEO_TS.* and VTS_01_0.*
            IEnumerable<string> fileNames = Directory.GetFiles(videoFolderName, "VIDEO_TS.*", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(videoFolderName, "VTS_01_0.*", SearchOption.TopDirectoryOnly));

            string result = Calculate(drive, fileNames);

            return result;
        }

        private static DriveInfo GetDrive(string driveLetter)
        {
            if (string.IsNullOrEmpty(driveLetter))
            {
                throw new ArgumentException(nameof(driveLetter));
            }

            DriveInfo result = new DriveInfo(driveLetter);

            if (!result.IsReady)
            {
                throw new ArgumentException("Drive is not ready!", nameof(driveLetter));
            }

            DirectoryInfo folder = new DirectoryInfo(Path.Combine(result.Name, VideoFolderName));

            if (!folder.Exists)
            {
                throw new ArgumentException("Drive does not contain a video DVD!", nameof(driveLetter));
            }

            return result;
        }

        private static string Calculate(DriveInfo drive, IEnumerable<string> fileNames)
        {
            List<FileInfo> files = fileNames.Select(fileName => new FileInfo(fileName)).ToList();

            files.Sort(CompareFiles);

            List<byte[]> hashes = new List<byte[]>();

            //Step 2:
            //The file headers from each file are computed in the CRC.
            foreach (FileInfo file in files)
            {
                AddFileMetaHash(file, hashes);
            }

            //Step 3:
            //The data from the VMGI file ("VIDEO_TS\VIDEO_TS.IFO") is computed in the CRC.
            //If present, the first 65,536 bytes of "VIDEO_TS.IFO are read and added to the CRC (if smaller then the entire file is added)
            AddFileContentHash(drive, "VIDEO_TS.IFO", hashes);

            //Note: On page 19 the patents talks about "the first VTSI file ('VIDEO_TS\VTS_xx_0.IFO')"
            //      but on page 20 it explicitly specifies "VTS_01_0.IFO".
            //Step 4:
            //The data from the first VTSI file ("VIDEO_TS\VTS_xx_0.IFO") is computed in the CRC.
            string vtsFileName = GetVtsFileName(drive);

            //If present, the first 65,536 bytes of "VTS_01_0.IFO" are read and added to the CRC (if smaller then the entire file is added)
            AddFileContentHash(drive, vtsFileName, hashes);

            byte[] hashBytes = hashes.SelectMany(bytes => bytes).ToArray();

            ulong hash = Crc64.Calculate(hashBytes);

            string result = hash.ToString("X").PadLeft(16, '0');

            return result;
        }

        private static int CompareFiles(FileInfo left, FileInfo right)
        {
            string leftName = NormalizeFileName(left);

            string rightName = NormalizeFileName(right);

            int result = leftName.CompareTo(rightName);

            return result;
        }

        /// <remarks>
        /// UDF is case sensitive. The DVD standard mandates upper case filename, hence all compliant DVDs will have all upper case file names
        /// in VIDEO_TS for the UDF filesystem.
        /// However, just to be on the save side, make sure it is upper for non-compliant DVDs.
        /// </remarks>
        private static string NormalizeFileName(FileInfo file) => file.Name.ToUpper();

        private static void AddFileMetaHash(FileInfo file, List<byte[]> hashes)
        {
            //For each filename in the list, the following structure is filled out and added to the CRC (all data fields are in LSB first): 
            //- Unsigned 64 bit integer: dateTime(the time elapsed in 100 nanosecond intervals from Jan. 1, 1601)
            //- unsigned 32 bit integer: dWFileSiZe
            //- BYTE bFilename[filename Length]
            //- BYTE bFilenameTermNull = 0

            TimeSpan diff = file.CreationTimeUtc - _baseDateUtc;

            //unsigned 64 bit Integer: dateTime (the time elapsed in 100 nanosecond intervals from Jan. 1, 1601)
            ulong hundredNanoSeconds = (ulong)(diff.TotalMilliseconds * 10_000);

            byte[] nanoSecondBytes = BitConverter.GetBytes(hundredNanoSeconds);

            //unsigned 32 bit Integer: FileSize
            uint fileSize = (uint)file.Length;

            byte[] fileSizeBytes = BitConverter.GetBytes(fileSize);

            //BYTE: Filename [filename Length]

            string fileName = NormalizeFileName(file);

            byte[] nameBytes = Encoding.UTF8.GetBytes(fileName);

            //all data fields are in LSB first
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(nanoSecondBytes);
                Array.Reverse(fileSizeBytes);
                Array.Reverse(nameBytes);      //untested if Encoding.UTF8.GetBytes() also needs to be reversed on a BigEndian system
            }

            List<byte[]> fileHashes = new List<byte[]>()
            {
                nanoSecondBytes,
                fileSizeBytes,
                nameBytes,
                new byte[] { 0 }, //BYTE: FilenameTermNull=0
            };

            byte[] fileHash = fileHashes.SelectMany(bytes => bytes).ToArray();

            hashes.Add(fileHash);
        }

        private static void AddFileContentHash(DriveInfo drive, string fileName, List<byte[]> hashes)
        {
            FileInfo file = new FileInfo(Path.Combine(Path.Combine(drive.Name, VideoFolderName), fileName));

            //If present
            if (file.Exists)
            {
                using (FileStream fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (BinaryReader br = new BinaryReader(fs))
                    {
                        //the first 65,536 bytes read and added to the CRC (if smaller then the entire file is added)
                        int bytesToRead = file.Length >= MaxReadOutLength
                            ? MaxReadOutLength
                            : (int)file.Length;

                        byte[] content = br.ReadBytes(bytesToRead);

                        hashes.Add(content);
                    }
                }
            }
        }

        private static string GetVtsFileName(DriveInfo drive)
        {
            IEnumerable<string> fileNames = Directory.GetFiles(Path.Combine(drive.Name, VideoFolderName), "VTS_*_0.IFO", SearchOption.TopDirectoryOnly);

            List<FileInfo> files = fileNames.Select(fileName => new FileInfo(fileName)).ToList();

            files.Sort(CompareFiles);

            string result = files.FirstOrDefault()?.Name ?? "VTS_01_0.IFO";

            return result;
        }
    }
}
namespace DoenaSoft.CalculateDvdDiscId
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;

    partial class DvdDiscIdCalculator
    {
        private sealed partial class Patent6871012B1Calculator
        {
            private readonly DriveInfo _drive;

            private List<byte[]> _hashes;

            public Patent6871012B1Calculator(DriveInfo drive)
            {
                _drive = drive;
            }

            public string Calculate(IEnumerable<string> fileNames)
            {
                var files = fileNames.Select(fileName => new FileInfo(fileName)).ToList();

                files.Sort(CompareFiles);

                _hashes = new List<byte[]>();

                //Step 2:
                //The file headers from each file are computed in the CRC.
                foreach (var file in files)
                {
                    this.AddFileMetaHash(file);
                }

                //Step 3:
                //The data from the VMGI file ("VIDEO_TS\VIDEO_TS.IFO") is computed in the CRC.
                //If present, the first 65,536 bytes of "VIDEO_TS.IFO are read and added to the CRC (if smaller then the entire file is added)
                this.AddFileContentHash("VIDEO_TS.IFO");

                //Note: On page 19 the patent talks about "the first VTSI file ('VIDEO_TS\VTS_xx_0.IFO')"
                //      but on page 20 it explicitly specifies "VTS_01_0.IFO".
                //Step 4:
                //The data from the first VTSI file ("VIDEO_TS\VTS_xx_0.IFO") is computed in the CRC.
                var vtsFileName = this.GetVtsFileName();

                //If present, the first 65,536 bytes of "VTS_01_0.IFO" are read and added to the CRC (if smaller then the entire file is added)
                this.AddFileContentHash(vtsFileName);

                var hashBytes = _hashes.SelectMany(bytes => bytes).ToArray();

                var hash = Crc64.Calculate(hashBytes);

                var result = hash.ToString("X").PadLeft(16, '0');

                return result;
            }

            private static int CompareFiles(FileInfo left, FileInfo right)
            {
                var leftName = NormalizeFileName(left);

                var rightName = NormalizeFileName(right);

                var result = leftName.CompareTo(rightName);

                return result;
            }

            /// <remarks>
            /// UDF is case sensitive. The DVD standard mandates upper case filename, hence all compliant DVDs will have all upper case file names
            /// in VIDEO_TS for the UDF filesystem.
            /// However, just to be on the save side, make sure it is upper for non-compliant DVDs.
            /// </remarks>
            private static string NormalizeFileName(FileInfo file) => file.Name.ToUpper();

            private void AddFileMetaHash(FileInfo file)
            {
                //For each filename in the list, the following structure is filled out and added to the CRC (all data fields are in LSB first): 
                //- Unsigned 64 bit integer: dateTime(the time elapsed in 100 nanosecond intervals from Jan. 1, 1601)
                //- unsigned 32 bit integer: dWFileSiZe
                //- BYTE bFilename[filename Length]
                //- BYTE bFilenameTermNull = 0

                var diff = file.CreationTimeUtc - _baseDateUtc;

                //unsigned 64 bit Integer: dateTime (the time elapsed in 100 nanosecond intervals from Jan. 1, 1601)
                var hundredNanoSeconds = (ulong)(diff.TotalMilliseconds * 10_000);

                var nanoSecondBytes = BitConverter.GetBytes(hundredNanoSeconds);

                //unsigned 32 bit Integer: FileSize
                var fileSize = (uint)file.Length;

                var fileSizeBytes = BitConverter.GetBytes(fileSize);

                //BYTE: Filename [filename Length]

                var fileName = NormalizeFileName(file);

                var nameBytes = Encoding.UTF8.GetBytes(fileName);

                //all data fields are in LSB first
                if (!BitConverter.IsLittleEndian)
                {
                    Array.Reverse(nanoSecondBytes);
                    Array.Reverse(fileSizeBytes);
                    Array.Reverse(nameBytes);      //untested if Encoding.UTF8.GetBytes() also needs to be reversed on a BigEndian system
                }

                var fileHashes = new List<byte[]>()
                {
                    nanoSecondBytes,
                    fileSizeBytes,
                    nameBytes,
                    new byte[] { 0 }, //BYTE: FilenameTermNull=0
                };

                var fileHash = fileHashes.SelectMany(bytes => bytes).ToArray();

                _hashes.Add(fileHash);
            }

            private void AddFileContentHash(string fileName)
            {
                var file = new FileInfo(Path.Combine(Path.Combine(_drive.Name, VideoFolderName), fileName));

                //If present
                if (file.Exists)
                {
                    using (var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        using (var br = new BinaryReader(fs))
                        {
                            //the first 65,536 bytes read and added to the CRC (if smaller then the entire file is added)
                            var bytesToRead = file.Length >= MaxReadOutLength
                                ? MaxReadOutLength
                                : (int)file.Length;

                            var content = br.ReadBytes(bytesToRead);

                            _hashes.Add(content);
                        }
                    }
                }
            }

            private string GetVtsFileName()
            {
                var fileNames = Directory.GetFiles(Path.Combine(_drive.Name, VideoFolderName), "VTS_*_0.IFO", SearchOption.TopDirectoryOnly);

                var files = fileNames.Select(fileName => new FileInfo(fileName)).ToList();

                files.Sort(CompareFiles);

                var result = files.FirstOrDefault()?.Name ?? "VTS_01_0.IFO";

                return result;
            }
        }
    }
}
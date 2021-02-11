using System;
using System.Collections.Generic;
using System.Linq;

namespace DoenaSoft.CalculateDvdDiscId
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length == 1)
            {
                Calculate(args[0]);
            }
            else
            {
                Console.Write("Please enter drive letter: ");

                string driveLetter = Console.ReadLine();

                Calculate(driveLetter);
            }
        }

        private static void Calculate(string driveLetter)
        {
            try
            {
                TryCalculate(driveLetter);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Drive '{driveLetter}' could not be calculated: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("Press <Enter> to exit.");
                Console.ReadLine();
            }
        }

        private static void TryCalculate(string driveLetter)
        {
            string goodDiscId = DvdDiscIdCalculator.Calculate(driveLetter);

            string goodFormattedDiscId = FormatDiscId(goodDiscId);

            Console.WriteLine("Good way: " + goodFormattedDiscId);

            string badDiscId = DvdDiscIdCalculator.CalculateForWin10_1809_AndHigher(driveLetter);

            string badFormattedDiscId = FormatDiscId(badDiscId);

            Console.WriteLine("Bad way:  " + badFormattedDiscId);
        }

        private static string FormatDiscId(string discId)
        {
            const int ChunkSize = 4;

            IEnumerable<string> parts = Enumerable.Range(0, discId.Length / ChunkSize).Select(chunkIndex => discId.Substring(chunkIndex * ChunkSize, ChunkSize));

            string formattedDiscId = string.Join("-", parts);

            return formattedDiscId;
        }
    }
}
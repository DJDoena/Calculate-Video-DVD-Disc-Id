using System;

namespace CalculateDvdDiscId
{
    internal static class Program
    {
        public static void Main()
        {
            Console.WriteLine("Good way:");
            string eGood = DvdDiscIdCalculator.Calculate("E:");
            Console.WriteLine(eGood);
            Console.WriteLine("Bad way:");
            string eBad = DvdDiscIdCalculator.CalculateForWin10_1809_AndHigher("E");
            Console.WriteLine(eBad);

            //Console.WriteLine("Good way:");
            //string fGood = DvdDiscIdCalculator.Calculate(@"F:\");
            //Console.WriteLine(fGood);
            //Console.WriteLine("Bad way:");
            //string fBad = DvdDiscIdCalculator.CalculateForWin10_1809_AndHigher("F:\\");
            //Console.WriteLine(fBad);

            Console.ReadLine();
        }
    }
}
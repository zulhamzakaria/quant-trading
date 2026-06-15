using QuantTrading.ML.Models;

namespace QuantTrading.ML.Engine;

public static class ConsoleReport
{
    public static void PrintSummaryLedger
        (List<ExperimentResult> ledger)
    {
        Console.WriteLine("\n====================================================================================================");
        Console.WriteLine("                               🧪 OFFICIAL GENERALIZATION SUMMARY LEDGER");
        Console.WriteLine("====================================================================================================");
        Console.WriteLine(" Ticker   | Base AUC | RSI AUC  | Delta   | Base Winner Model        | RSI Winner Model");
        Console.WriteLine("----------------------------------------------------------------------------------------------------");
  
        foreach(var result in ledger)
        {
            if(result.Delta > .001)
                Console.ForegroundColor = ConsoleColor.Green;
            else if (result.Delta < -.001)
                Console.ForegroundColor = ConsoleColor.Red;
            else
                Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($" {result.Symbol,-8} | {result.BaseAuc:F4}   | {result.RsiAuc:F4}   | {result.Delta:+0.0000;-0.0000;0.0000} | {result.BaseWinner,-24} | {result.RsiWinner}");
            Console.ResetColor();
        }
        Console.WriteLine("----------------------------------------------------------------------------------------------------");

        if (ledger.Count == 0)
            return;
        
        int totalSymbols = ledger.Count;
        int improvedCount = ledger.Count(r => r.Delta > 0);
        double avgDelta = ledger.Average(r => r.Delta);

        Console.WriteLine($"📊 SUMMARY METRICS:");
        Console.WriteLine($"   ↳ RSI Signal Improved Performance on : {improvedCount} / {totalSymbols} Symbols ({((double)improvedCount / totalSymbols):P0})");
        Console.WriteLine($"   ↳ Average Alpha Delta Across Panel   : {avgDelta:+0.0000;-0.0000;0.0000}");
        Console.WriteLine("====================================================================================================\n");

        Console.WriteLine("Pipeline execution complete. Press any key to exit.");
        Console.ReadKey();

    }
}

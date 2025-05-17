using System;
using Plastic;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: PlasticCMD run <code-or-path>");
            return;
        }

        string command = args[0];

        if (command == "run")
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Please provide a code snippet or file path.");
                return;
            }

            string input = args[1];

            // this is going to be optional
            // if not provided, it will default to false

            bool respondWithReturn = false;
            if (args.Length > 2)
            {
                bool.TryParse(args[2], out respondWithReturn);
            }

            if (System.IO.File.Exists(input))
            {
                string code = System.IO.File.ReadAllText(input);
                Interpret(code, respondWithReturn);
            }
            else
            {
                Interpret(input, respondWithReturn);
            }
        }
        else
        {
            Console.WriteLine($"Unknown command: {command}");
        }
    }

    public static void Interpret(string code, bool debugReturn = false)
    {
        EngineFeatures.Interpret(code, debugReturn);
    }
}

using System;
using System.Collections.Generic;
using System.IO;

namespace launchpad
{
    internal class VariableFiller
    {
        public Dictionary<string, string> FillVariables(IEnumerable<VariableDefinition> definitions)
        {
            var logo = File.ReadAllText("logo");
            Console.WriteLine(logo);

            var variables = new Dictionary<string, string>();
            foreach (var variableDefinition in definitions)
            {
                if(!variables.TryAdd(variableDefinition.Name, ""))
                    throw new ArgumentException($"Variable ${variableDefinition.Name} defined twice");

                Console.WriteLine($"Need {variableDefinition.Description} : ");
                variables[variableDefinition.Name] = Console.ReadLine().ToString();
            }
            return variables;
        }
    }
}

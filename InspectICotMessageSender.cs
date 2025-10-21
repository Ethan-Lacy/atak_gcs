using System;
using System.Reflection;
using System.Linq;
using System.IO;

namespace InspectICotMessageSender
{
    class Program
    {
        static void Main(string[] args)
        {
            string dllPath = @"C:\Users\ethan\.nuget\packages\wintak-dependencies\5.5.0.157\lib\WinTak.CursorOnTarget.dll";

            if (!File.Exists(dllPath))
            {
                Console.WriteLine($"DLL not found: {dllPath}");
                return;
            }

            try
            {
                Console.WriteLine("Loading assembly...");
                byte[] assemblyBytes = File.ReadAllBytes(dllPath);
                Assembly assembly = Assembly.Load(assemblyBytes);

                Console.WriteLine($"Assembly loaded: {assembly.FullName}\n");

                // Find ICotMessageSender
                var cotMessageSenderType = assembly.GetTypes().FirstOrDefault(t => t.Name == "ICotMessageSender");

                if (cotMessageSenderType != null)
                {
                    Console.WriteLine($"=== {cotMessageSenderType.FullName} ===");
                    Console.WriteLine($"Type: {(cotMessageSenderType.IsInterface ? "Interface" : "Class")}");

                    Console.WriteLine("\n--- Properties ---");
                    foreach (var prop in cotMessageSenderType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        Console.WriteLine($"  {prop.PropertyType.Name} {prop.Name}");
                    }

                    Console.WriteLine("\n--- Methods ---");
                    foreach (var method in cotMessageSenderType.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => !m.IsSpecialName))
                    {
                        var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                        Console.WriteLine($"  {method.ReturnType.Name} {method.Name}({parameters})");
                    }
                }
                else
                {
                    Console.WriteLine("ICotMessageSender not found!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine($"Stack: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner: {ex.InnerException.Message}");
                }
            }

            Console.WriteLine("\n\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}

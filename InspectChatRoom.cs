using System;
using System.Reflection;
using System.Linq;
using System.IO;

namespace InspectChatRoom
{
    class Program
    {
        static void Main(string[] args)
        {
            string dllPath = @"C:\Users\ethan\Documents\Coding\Wintak_Plugins\WinTAK Plugin (5.0)_voice\WinTAK Plugin (5.0)_voice\bin\x64\Debug\WinTak.Net.dll";

            if (!File.Exists(dllPath))
            {
                Console.WriteLine($"DLL not found: {dllPath}");
                return;
            }

            try
            {
                Console.WriteLine("Loading assembly...");
                // Load assembly with reflection-only context to avoid dependency issues
                byte[] assemblyBytes = File.ReadAllBytes(dllPath);
                Assembly assembly = Assembly.Load(assemblyBytes);

                Console.WriteLine($"Assembly loaded: {assembly.FullName}\n");

                // Find all types that contain "Chat" in their name
                var chatTypes = assembly.GetTypes().Where(t => t.FullName != null && t.FullName.Contains("Chat"));

                Console.WriteLine("=== Chat-related Types ===");
                foreach (var type in chatTypes)
                {
                    Console.WriteLine($"  {type.FullName} ({(type.IsInterface ? "Interface" : type.IsClass ? "Class" : "Other")})");
                }

                // Find IChatRoom specifically
                var chatRoomType = assembly.GetTypes().FirstOrDefault(t => t.Name == "IChatRoom");

                if (chatRoomType != null)
                {
                    Console.WriteLine($"\n\n=== {chatRoomType.FullName} ===");
                    Console.WriteLine($"Type: {(chatRoomType.IsInterface ? "Interface" : "Class")}");

                    Console.WriteLine("\n--- Properties ---");
                    foreach (var prop in chatRoomType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        Console.WriteLine($"  {prop.PropertyType.Name} {prop.Name} {{ get; }}");
                    }

                    Console.WriteLine("\n--- Methods ---");
                    foreach (var method in chatRoomType.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => !m.IsSpecialName))
                    {
                        var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                        Console.WriteLine($"  {method.ReturnType.Name} {method.Name}({parameters})");
                    }
                }
                else
                {
                    Console.WriteLine("\nIChatRoom not found!");
                }

                // Find IChatService
                var chatServiceType = assembly.GetTypes().FirstOrDefault(t => t.Name == "IChatService");

                if (chatServiceType != null)
                {
                    Console.WriteLine($"\n\n=== {chatServiceType.FullName} ===");
                    Console.WriteLine($"Type: {(chatServiceType.IsInterface ? "Interface" : "Class")}");

                    Console.WriteLine("\n--- Properties ---");
                    foreach (var prop in chatServiceType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        Console.WriteLine($"  {prop.PropertyType.Name} {prop.Name}");
                    }

                    Console.WriteLine("\n--- Methods ---");
                    foreach (var method in chatServiceType.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => !m.IsSpecialName))
                    {
                        var parameters = string.Join(", ", method.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                        Console.WriteLine($"  {method.ReturnType.Name} {method.Name}({parameters})");
                    }
                }

                // Find Message class
                var messageType = assembly.GetTypes().FirstOrDefault(t => t.Name == "Message" && t.Namespace != null && t.Namespace.Contains("Chat"));

                if (messageType != null)
                {
                    Console.WriteLine($"\n\n=== {messageType.FullName} ===");
                    Console.WriteLine($"Type: {(messageType.IsClass ? "Class" : "Other")}");

                    Console.WriteLine("\n--- Constructors ---");
                    foreach (var ctor in messageType.GetConstructors())
                    {
                        var parameters = string.Join(", ", ctor.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                        Console.WriteLine($"  {messageType.Name}({parameters})");
                    }

                    Console.WriteLine("\n--- Properties ---");
                    foreach (var prop in messageType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        Console.WriteLine($"  {prop.PropertyType.Name} {prop.Name}");
                    }
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

using System;
using System.Linq;
using System.Threading.Tasks;

namespace SQLmanager.Security
{
    public class DeviceDetectionTest
    {
        public static async Task RunDiagnosticsAsync()
        {
            Console.WriteLine("=== DIAGNÓSTICO DE DETECCIÓN DE DISPOSITIVOS ===");
            
            var deviceDetectionService = new DeviceDetectionService();
            
            try
            {
                // 1. Verificar dispositivos conectados
                Console.WriteLine("\n1. Verificando dispositivos conectados...");
                var devices = await deviceDetectionService.GetConnectedIOSDevicesAsync();
                Console.WriteLine($"Dispositivos encontrados: {string.Join(", ", devices)}");
                
                // 2. Verificar iCloud Keychain
                Console.WriteLine("\n2. Verificando iCloud Keychain...");
                var iCloudEnabled = await deviceDetectionService.IsICloudKeychainEnabledAsync();
                Console.WriteLine($"iCloud Keychain habilitado: {iCloudEnabled}");
                
                // 3. Verificar cuenta de iCloud
                Console.WriteLine("\n3. Verificando cuenta de iCloud...");
                var account = await deviceDetectionService.GetCurrentICloudAccountAsync();
                Console.WriteLine($"Cuenta de iCloud: {account}");
                
                // 4. Verificar coincidencia de cuenta
                Console.WriteLine("\n4. Verificando coincidencia de cuenta...");
                var accountMatch = await deviceDetectionService.VerifyICloudAccountMatchAsync();
                Console.WriteLine($"Coincidencia de cuenta: {accountMatch}");
                
                // 5. Ejecutar comandos de diagnóstico
                Console.WriteLine("\n5. Ejecutando comandos de diagnóstico...");
                await RunDiagnosticCommandsAsync();
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error durante el diagnóstico: {ex.Message}");
            }
        }
        
        private static async Task RunDiagnosticCommandsAsync()
        {
            var commands = new[]
            {
                ("system_profiler SPBluetoothDataType", "Dispositivos Bluetooth (inalámbricos)"),
                ("defaults read MobileMeAccounts", "Cuentas MobileMe (iCloud)"),
                ("defaults read com.apple.AppleMultitouchTrackpad", "Configuración Continuity"),
                ("defaults read com.apple.AppleMultitouchTrackpad", "Configuración Handoff"),
                ("system_profiler SPiBridgeDataType", "Dispositivos iBridge"),
                ("security list-keychains", "Keychains del sistema")
            };
            
            foreach (var (command, description) in commands)
            {
                try
                {
                    Console.WriteLine($"\n--- {description} ---");
                    var result = await RunCommandAsync(command.Split(' ')[0], string.Join(" ", command.Split(' ').Skip(1)));
                    if (!string.IsNullOrEmpty(result))
                    {
                        var lines = result.Split('\n').Take(10); // Mostrar solo las primeras 10 líneas
                        foreach (var line in lines)
                        {
                            if (!string.IsNullOrWhiteSpace(line))
                            {
                                Console.WriteLine($"  {line}");
                            }
                        }
                        if (result.Split('\n').Length > 10)
                        {
                            Console.WriteLine("  ... (más líneas disponibles)");
                        }
                    }
                    else
                    {
                        Console.WriteLine("  (sin resultado)");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  Error: {ex.Message}");
                }
            }
        }
        
        private static async Task<string> RunCommandAsync(string command, string arguments)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new System.Diagnostics.Process { StartInfo = startInfo };
                process.Start();
                
                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();
                
                await process.WaitForExitAsync();
                
                if (process.ExitCode != 0)
                {
                    return $"Error (exit code {process.ExitCode}): {error}";
                }
                
                return output;
            }
            catch (Exception ex)
            {
                return $"Error ejecutando {command}: {ex.Message}";
            }
        }
    }
} 
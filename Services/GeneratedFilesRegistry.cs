using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace FacturacionAlemana.Services
{
    /// <summary>
    /// Servicio para mantener un registro de los archivos generados
    /// </summary>
    public static class GeneratedFilesRegistry
    {
        private static readonly string RegistryPath;
        private const int MaxRecentFiles = 20;

        static GeneratedFilesRegistry()
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FacturacionAlemana"
            );
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);

            RegistryPath = Path.Combine(appDataPath, "generated_files.json");
        }        /// <summary>
        /// Registra un archivo XML generado
        /// </summary>
        public static void Register(string filePath)
        {
            try
            {
                var registry = LoadRegistry();

                // Crear entrada
                var entry = new Dictionary<string, object>
                { 
                    { "path", filePath },
                    { "timestamp", DateTime.Now.ToString("O") },
                    { "fileName", Path.GetFileName(filePath) }
                };

                // Agregar al inicio
                registry.Insert(0, entry);

                // Limitar a los últimos N archivos
                registry = registry.Take(MaxRecentFiles).ToList();

                // Guardar
                SaveRegistry(registry);
            }
            catch
            {
                // No bloquear la aplicación si falla el registro
            }
        }        /// <summary>
        /// Obtiene la lista de archivos generados recientemente (validando que existan)
        /// </summary>
        public static List<string> GetRecentFiles()
        {
            try
            {
                var registry = LoadRegistry();
                var validFiles = registry
                    .Where(e => !string.IsNullOrEmpty((string)e["path"]) && File.Exists((string)e["path"]))
                    .Select(e => (string)e["path"])
                    .ToList();

                // Si hay archivos que ya no existen, actualizar el registro
                if (validFiles.Count < registry.Count)
                {
                    // Reconstruir el registro solo con archivos válidos
                    var updatedRegistry = registry
                        .Where(e => !string.IsNullOrEmpty((string)e["path"]) && File.Exists((string)e["path"]))
                        .ToList();
                    SaveRegistry(updatedRegistry);
                }

                return validFiles;
            }
            catch
            {
                return new List<string>();
            }
        }

        /// <summary>
        /// Elimina un archivo específico del registro
        /// </summary>
        public static void RemoveFile(string filePath)
        {
            try
            {
                var registry = LoadRegistry();

                // Remover el archivo del registro
                registry = registry
                    .Where(e => !string.Equals((string)e["path"], filePath, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                // Guardar el registro actualizado
                SaveRegistry(registry);
            }
            catch
            {
                // No bloquear la aplicación si falla la eliminación
            }
        }

        private static List<Dictionary<string, object>> LoadRegistry()
        {
            if (!File.Exists(RegistryPath))
                return new List<Dictionary<string, object>>();

            try
            {
                var json = File.ReadAllText(RegistryPath);
                var doc = JsonDocument.Parse(json);
                var result = new List<Dictionary<string, object>>();

                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var dict = new Dictionary<string, object>();
                    foreach (var prop in element.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.String)
                            dict[prop.Name] = prop.Value.GetString() ?? "";
                        else
                            dict[prop.Name] = prop.Value.ToString();
                    }
                    result.Add(dict);
                }

                return result;
            }
            catch
            {
                return new List<Dictionary<string, object>>();
            }
        }

        private static void SaveRegistry(List<Dictionary<string, object>> registry)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(registry, options);
                File.WriteAllText(RegistryPath, json);
            }
            catch
            {
                // Ignorar errores de guardado
            }
        }
    }
}

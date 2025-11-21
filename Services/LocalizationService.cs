using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;

namespace FacturacionAlemana.Services
{
    /// <summary>
    /// Evento que se dispara cuando cambia el idioma
    /// </summary>
    public delegate void LanguageChangedEventHandler(string languageCode);

    public class LocalizationService    {
        private static LocalizationService? _instance;
        private static readonly object _lockObject = new object();

        private Dictionary<string, object> _translations = new();
        private string _currentLanguage = "es"; // Idioma por defecto
        private const string LanguageKey = "CurrentLanguage";
        private readonly string _languageDirectory;

        public event LanguageChangedEventHandler? LanguageChanged;

        /// <summary>
        /// Eventos para notificar cambios dinámicos a la UI
        /// </summary>
        public event EventHandler? LanguageChangedUI;

        // Propiedades públicas para idiomas disponibles
        public List<(string Code, string DisplayName)> AvailableLanguages { get; private set; }

        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value && AvailableLanguages.Any(l => l.Code == value))
                {
                    SetLanguage(value);
                }
            }
        }

        private LocalizationService()
        {
            // Determinar la ruta de los archivos de idioma
            _languageDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Languages");

            // Crear directorio si no existe
            if (!Directory.Exists(_languageDirectory))
            {
                Directory.CreateDirectory(_languageDirectory);
            }

            AvailableLanguages = new List<(string, string)>();
            LoadAvailableLanguages();
            LoadLanguagePreference();
            LoadLanguage(_currentLanguage);
        }

        /// <summary>
        /// Obtiene la instancia singleton del servicio
        /// </summary>
        public static LocalizationService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        _instance ??= new LocalizationService();
                    }
                }
                return _instance;
            }
        }

        /// <summary>
        /// Carga los idiomas disponibles desde los archivos JSON
        /// </summary>
        private void LoadAvailableLanguages()
        {
            AvailableLanguages.Clear();

            if (!Directory.Exists(_languageDirectory))
                return;

            var languageFiles = Directory.GetFiles(_languageDirectory, "*.json");

            foreach (var file in languageFiles)
            {
                var fileName = Path.GetFileNameWithoutExtension(file);
                var displayName = GetLanguageDisplayName(fileName);
                AvailableLanguages.Add((fileName, displayName));
            }

            // Ordenar por código de idioma
            AvailableLanguages = AvailableLanguages.OrderBy(l => l.Code).ToList();
        }

        /// <summary>
        /// Obtiene el nombre amigable del idioma
        /// </summary>
        private string GetLanguageDisplayName(string languageCode)
        {
            return languageCode.ToUpper() switch
            {
                "ES" => "🇪🇸 Español",
                "EN" => "🇬🇧 English",
                "DE" => "🇩🇪 Deutsch",
                _ => languageCode.ToUpper()
            };
        }

        /// <summary>
        /// Carga el idioma guardado en las preferencias
        /// </summary>
        private void LoadLanguagePreference()
        {
            try
            {
                var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                    "FacturacionAlemana");
                
                if (!Directory.Exists(appDataPath))
                    Directory.CreateDirectory(appDataPath);

                var preferencesFile = Path.Combine(appDataPath, "preferences.json");

                if (File.Exists(preferencesFile))
                {
                    var json = File.ReadAllText(preferencesFile);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var prefs = JsonSerializer.Deserialize<Dictionary<string, string>>(json, options);

                    if (prefs != null && prefs.ContainsKey(LanguageKey))
                    {
                        var savedLanguage = prefs[LanguageKey];
                        if (AvailableLanguages.Any(l => l.Code == savedLanguage))
                        {
                            _currentLanguage = savedLanguage;
                        }
                    }
                }
            }
            catch
            {
                // Si hay error, usar idioma por defecto
                _currentLanguage = "es";
            }
        }

        /// <summary>
        /// Guarda la preferencia de idioma
        /// </summary>
        private void SaveLanguagePreference()
        {
            try
            {
                var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                    "FacturacionAlemana");
                
                if (!Directory.Exists(appDataPath))
                    Directory.CreateDirectory(appDataPath);

                var preferencesFile = Path.Combine(appDataPath, "preferences.json");
                var prefs = new Dictionary<string, string> { { LanguageKey, _currentLanguage } };
                var json = JsonSerializer.Serialize(prefs, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(preferencesFile, json);
            }
            catch
            {
                // Silenciosamente ignorar errores de escritura
            }
        }        /// <summary>
        /// Carga el archivo JSON del idioma especificado
        /// </summary>
        private void LoadLanguage(string languageCode)
        {
            try
            {
                var filePath = Path.Combine(_languageDirectory, $"{languageCode}.json");

                if (!File.Exists(filePath))
                {
                    MessageBox.Show($"No se encontró el archivo de idioma: {languageCode}.json", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var json = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                _translations = JsonElementToDictionary(root);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el idioma '{languageCode}': {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Convierte un JsonElement a Dictionary
        /// </summary>
        private Dictionary<string, object> JsonElementToDictionary(JsonElement element)
        {
            var result = new Dictionary<string, object>();

            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Object)
                    {
                        result[property.Name] = JsonElementToDictionary(property.Value);
                    }
                    else if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        result[property.Name] = property.Value.GetRawText();
                    }
                    else
                    {
                        result[property.Name] = property.Value.GetString() ?? "";
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Cambia el idioma actual y notifica a los observadores
        /// </summary>
        public void SetLanguage(string languageCode)
        {
            if (_currentLanguage == languageCode)
                return;

            _currentLanguage = languageCode;
            LoadLanguage(languageCode);
            SaveLanguagePreference();

            // Disparar eventos
            LanguageChanged?.Invoke(languageCode);
            LanguageChangedUI?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Obtiene una cadena de traducción usando una clave jerárquica
        /// Ej: "HomePage.Title", "CreateInvoicePage.SellerSection"
        /// </summary>
        public string Get(string key)
        {
            try
            {
                var keys = key.Split('.');
                object? value = _translations;

                foreach (var k in keys)
                {
                    if (value is Dictionary<string, object> dict)
                    {
                        if (dict.TryGetValue(k, out var nextValue))
                        {
                            value = nextValue;
                        }
                        else
                        {
                            // Retornar versión legible del segmento final en vez de la clave entre corchetes
                            return PrettifyKey(keys.Last());
                        }
                    }
                    else
                    {
                        return PrettifyKey(keys.Last());
                    }
                }

                return value?.ToString() ?? PrettifyKey(keys.Last());
            }
            catch
            {
                return PrettifyKey(key.Split('.').Last());
            }
        }

        private string PrettifyKey(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
            // Reemplazar guiones/underscores por espacios
            string s = raw.Replace('_', ' ').Replace('-', ' ');
            // Insertar espacios entre camelCase/PascalCase
            s = Regex.Replace(s, "([a-z])([A-Z])", "$1 $2");
            // Si aún contiene segmentos concatenados con mayúsculas, separar grupos de mayúsculas seguidas
            s = Regex.Replace(s, "([A-Z]+)([A-Z][a-z])", "$1 $2");
            return s.Trim();
        }

        /// <summary>
        /// Obtiene una cadena de traducción con formato (reemplazo de placeholders)
        /// Ej: Get("Messages.SuccessPdfGenerated", "archivo.pdf") 
        ///     reemplaza {0} con "archivo.pdf"
        /// </summary>
        public string Get(string key, params object[] args)
        {
            var template = Get(key);
            try
            {
                return string.Format(template, args);
            }
            catch
            {
                return template;
            }
        }

        /// <summary>
        /// Verifica si existe una clave de traducción
        /// </summary>
        public bool Exists(string key)
        {
            try
            {
                var keys = key.Split('.');
                object? value = _translations;

                foreach (var k in keys)
                {
                    if (value is Dictionary<string, object> dict)
                    {
                        if (dict.TryGetValue(k, out var nextValue))
                        {
                            value = nextValue;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return false;
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }        /// <summary>
        /// Obtiene todas las traducciones de una sección (ej: "HomePage")
        /// </summary>
        public Dictionary<string, string> GetSection(string sectionKey)
        {
            var result = new Dictionary<string, string>();

            try
            {
                if (_translations.TryGetValue(sectionKey, out var section))
                {
                    if (section is Dictionary<string, object> dict)
                    {
                        foreach (var kvp in dict)
                        {
                            result[kvp.Key] = kvp.Value?.ToString() ?? "";
                        }
                    }
                }
            }
            catch
            {
                // Retornar diccionario vacío si hay error
            }

            return result;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace Aurora.App.Services
{
    public sealed class LocalizationResources : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public string this[string key] => Localizer.Get(key);
        internal void NotifyLanguageChanged() => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs("Item[]"));
    }

    public sealed record AppLanguage(string Code, string NativeName);

    /// <summary>
    /// Application UI localization, independent of the language used by AI models.
    /// Missing languages or keys fall back to English, then to the key itself.
    /// </summary>
    public static class Localizer
    {
        public const string DefaultLanguage = "en";
        public static LocalizationResources Resources { get; } = new();

        public static IReadOnlyList<AppLanguage> SupportedLanguages { get; } = new[]
        {
            new AppLanguage("en", "English"),
            new AppLanguage("es", "Español"),
            new AppLanguage("fr", "Français"),
            new AppLanguage("de", "Deutsch")
        };

        private static string _language = DefaultLanguage;
        private sealed class OriginalValues
        {
            public Dictionary<DependencyProperty, string> Values { get; } = new();
        }
        private static readonly ConditionalWeakTable<DependencyObject, OriginalValues> OriginalText = new();

        public static string Language => _language;
        public static event EventHandler? LanguageChanged;

        public static string GetResponseLanguageInstruction(string? language = null)
        {
            var name = NormalizeLanguage(language ?? _language) switch
            {
                "es" => "Spanish",
                "fr" => "French",
                "de" => "German",
                _ => "English"
            };
            return $"RESPONSE LANGUAGE REQUIREMENT: Write all user-facing responses, including chat text that may be read aloud, in {name}, regardless of the language of the user's message. Translate your response when needed. Keep code, tool identifiers, and proper nouns unchanged where appropriate.";
        }

        public static string NormalizeLanguage(string? code)
        {
            if (string.IsNullOrWhiteSpace(code)) return DefaultLanguage;
            var neutral = code.Trim().Split('-', '_')[0];
            var match = SupportedLanguages.FirstOrDefault(l => string.Equals(l.Code, neutral, StringComparison.OrdinalIgnoreCase));
            return match?.Code ?? DefaultLanguage;
        }

        public static void SetLanguage(string? code)
        {
            _language = NormalizeLanguage(code);
            try
            {
                var culture = CultureInfo.GetCultureInfo(_language);
                CultureInfo.DefaultThreadCurrentUICulture = culture;
                CultureInfo.CurrentUICulture = culture;
            }
            catch (CultureNotFoundException)
            {
            }
            Resources.NotifyLanguageChanged();
            ApplyToOpenWindows();
            LanguageChanged?.Invoke(null, EventArgs.Empty);
        }

        public static string Get(string key) => Get(key, _language);

        public static string Get(string key, string? language)
        {
            var lang = NormalizeLanguage(language);
            if (Strings.TryGetValue(lang, out var table) && table.TryGetValue(key, out var value)) return value;
            var uiText = GetUiText(key, lang);
            if (!string.Equals(uiText, key, StringComparison.Ordinal)) return uiText;
            if (Strings[DefaultLanguage].TryGetValue(key, out var fallback)) return fallback;
            return key;
        }

        public static string GetUiText(string text, string? language = null)
        {
            var lang = NormalizeLanguage(language ?? _language);
            if (string.Equals(lang, DefaultLanguage, StringComparison.Ordinal)) return GetEnglishText(text);
            if (UiTranslations.TryGetValue(text, out var translations) && translations.TryGetValue(lang, out var translated))
                return translated;
            var normalizedText = NormalizeUiText(text);
            if (UiTranslations.TryGetValue(normalizedText, out translations) && translations.TryGetValue(lang, out translated))
                return translated;

            foreach (var entry in UiTranslations)
            {
                if (entry.Value.TryGetValue(lang, out translated) &&
                    string.Equals(NormalizeUiText(translated), normalizedText, StringComparison.Ordinal))
                    return translated;
            }

            return GetEnglishText(text);
        }

        public static string FormatUiText(string template, params object?[] args) =>
            string.Format(CultureInfo.CurrentUICulture, GetUiText(template), args);

        private static string GetEnglishText(string text)
        {
            var normalizedText = NormalizeUiText(text);
            foreach (var entry in UiTranslations)
            {
                if (entry.Value.Values.Any(value => string.Equals(NormalizeUiText(value), normalizedText, StringComparison.Ordinal)))
                    return entry.Key;
            }
            return text;
        }

        private static string NormalizeUiText(string text) =>
            string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        public static void ApplyToOpenWindows()
        {
            if (Application.Current is not { } app) return;
            foreach (Window window in app.Windows) ApplyToWindow(window);
        }

        public static void ApplyToWindow(Window window)
            => ApplyToElement(window);

        public static void ApplyToElement(DependencyObject root)
        {
            var seen = new HashSet<DependencyObject>();
            var pending = new Stack<DependencyObject>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var element = pending.Pop();
                if (!seen.Add(element)) continue;
                TranslateLocalStringValues(element);

                foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>())
                    pending.Push(child);

                if (element is Visual or Visual3D)
                {
                    var childCount = VisualTreeHelper.GetChildrenCount(element);
                    for (var i = 0; i < childCount; i++) pending.Push(VisualTreeHelper.GetChild(element, i));
                }
            }
        }

        private static void TranslateLocalStringValues(DependencyObject element)
        {
            var originals = OriginalText.GetValue(element, static _ => new OriginalValues());
            var values = element.GetLocalValueEnumerator();
            while (values.MoveNext())
            {
                var property = values.Current.Property;
                if (property != System.Windows.Controls.TextBlock.TextProperty &&
                    property != System.Windows.Documents.Run.TextProperty &&
                    property != System.Windows.Controls.ContentControl.ContentProperty &&
                    property != System.Windows.Controls.HeaderedContentControl.HeaderProperty &&
                    property != System.Windows.Controls.HeaderedItemsControl.HeaderProperty &&
                    property != Window.TitleProperty &&
                    property != FrameworkElement.ToolTipProperty &&
                    property != System.Windows.Automation.AutomationProperties.NameProperty)
                    continue;
                if (property.ReadOnly) continue;
                if (!originals.Values.TryGetValue(property, out var original))
                {
                    if (values.Current.Value is not string initial || GetUiText(initial, DefaultLanguage) == initial)
                        continue;
                    original = GetEnglishText(initial);
                    originals.Values[property] = original;
                }

                var localized = GetUiText(original);
                if (!Equals(element.GetValue(property), localized))
                    element.SetValue(property, localized);
            }
        }

        private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> UiTranslations = CreateUiTranslations();

        private static Dictionary<string, IReadOnlyDictionary<string, string>> CreateUiTranslations()
        {
            var result = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
            void Add(string english, string spanish, string french, string german)
                => result[english] = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["es"] = spanish, ["fr"] = french, ["de"] = german
                };

            Add("Aurora Chat", "Chat de Aurora", "Discussion Aurora", "Aurora-Chat");
            Add("Chat with Aurora", "Chatea con Aurora", "Discuter avec Aurora", "Mit Aurora chatten");
            Add("Your full conversation window", "Tu ventana de conversación completa", "Votre fenêtre de conversation complète", "Dein vollständiges Gesprächsfenster");
            Add("Clear this chat", "Borrar este chat", "Effacer cette conversation", "Diesen Chat löschen");
            Add("Delete this conversation only", "Eliminar solo esta conversación", "Supprimer uniquement cette conversation", "Nur diese Unterhaltung löschen");
            Add("Favorite", "Favorito", "Favori", "Favorit");
            Add("Folder", "Carpeta", "Dossier", "Ordner");
            Add("Tags", "Etiquetas", "Étiquettes", "Tags");
            Add("Comma-separated tags", "Etiquetas separadas por comas", "Étiquettes séparées par des virgules", "Kommagetrennte Tags");
            Add("Save tags", "Guardar etiquetas", "Enregistrer les étiquettes", "Tags speichern");
            Add("Provider", "Proveedor", "Fournisseur", "Anbieter");
            Add("Model", "Modelo", "Modèle", "Modell");
            Add("Blank = provider default model", "Vacío = modelo predeterminado del proveedor", "Vide = modèle par défaut du fournisseur", "Leer = Standardmodell des Anbieters");
            Add("Apply", "Aplicar", "Appliquer", "Anwenden");
            Add("Send", "Enviar", "Envoyer", "Senden");
            Add("Aurora Setup", "Configuración de Aurora", "Configuration d'Aurora", "Aurora-Einrichtung");
            Add("Interface language", "Idioma de la interfaz", "Langue de l'interface", "Oberflächensprache");
            Add("Welcome to Aurora", "Te damos la bienvenida a Aurora", "Bienvenue dans Aurora", "Willkommen bei Aurora");
            Add("Let's get Aurora installed and ready to use.", "Vamos a instalar Aurora y dejarlo listo para usar.", "Installons Aurora et préparons-le à l'emploi.", "Richten wir Aurora ein und machen es einsatzbereit.");
            Add("The setup wizard will help you choose an AI provider and model. After that, Aurora will ask which device permissions and Windows capabilities you want to enable.", "El asistente te ayudará a elegir un proveedor y un modelo de IA. Después, Aurora te preguntará qué permisos del dispositivo y capacidades de Windows quieres activar.", "L'assistant vous aidera à choisir un fournisseur et un modèle d'IA. Aurora vous demandera ensuite quelles autorisations et fonctionnalités Windows activer.", "Der Einrichtungsassistent hilft dir bei der Wahl eines KI-Anbieters und -Modells. Danach fragt Aurora, welche Geräteberechtigungen und Windows-Funktionen aktiviert werden sollen.");
            Add("Language saved. Interface text updates immediately.", "Idioma guardado. El texto de la interfaz se actualiza inmediatamente.", "Langue enregistrée. Le texte de l'interface est mis à jour immédiatement.", "Sprache gespeichert. Die Oberflächentexte werden sofort aktualisiert.");
            Add("Aurora AI Provider Setup", "Configuración del proveedor de IA de Aurora", "Configuration du fournisseur d'IA Aurora", "Aurora-KI-Anbietereinrichtung");
            Add("Aurora - AI Provider Setup", "Aurora: configuración del proveedor de IA", "Aurora - Configuration du fournisseur d'IA", "Aurora – KI-Anbietereinrichtung");
            Add("Aurora - Initial Setup", "Aurora: configuración inicial", "Aurora - Configuration initiale", "Aurora – Ersteinrichtung");
            Add("Aurora AI Provider Setup", "Configuración del proveedor de IA de Aurora", "Configuration du fournisseur d'IA Aurora", "Aurora-KI-Anbietereinrichtung");
            Add("Pick an engine and paste your key - no need to touch settings.json by hand.", "Elige un motor y pega tu clave; no hace falta editar settings.json.", "Choisissez un moteur et collez votre clé, sans modifier settings.json.", "Wähle eine Engine und füge deinen Schlüssel ein – settings.json muss nicht bearbeitet werden.");
            Add("Your profile", "Tu perfil", "Votre profil", "Dein Profil");
            Add("New User", "Nuevo usuario", "Nouvel utilisateur", "Neuer Benutzer");
            Add("Profile password", "Contraseña del perfil", "Mot de passe du profil", "Profilpasswort");
            Add("Generate password", "Generar contraseña", "Générer un mot de passe", "Passwort generieren");
            Add("Enter your own password or generate a strong one. Aurora stores only a verifier, not the password itself.", "Escribe tu contraseña o genera una segura. Aurora solo guarda un verificador, no la contraseña.", "Saisissez votre mot de passe ou générez-en un robuste. Aurora ne conserve qu'un vérificateur, pas le mot de passe.", "Gib dein Passwort ein oder generiere ein sicheres. Aurora speichert nur einen Prüfwert, nicht das Passwort.");
            Add("Generate recovery code", "Generar código de recuperación", "Générer un code de récupération", "Wiederherstellungscode generieren");
            Add("Chat engine", "Motor de chat", "Moteur de conversation", "Chat-Engine");
            Add("API key (required)", "Clave de API (obligatoria)", "Clé API (obligatoire)", "API-Schlüssel (erforderlich)");
            Add("Get a key", "Obtener una clave", "Obtenir une clé", "Schlüssel abrufen");
            Add("Model", "Modelo", "Modèle", "Modell");
            Add("API docs", "Documentación de API", "Documentation API", "API-Dokumentation");
            Add("Rate limits", "Límites de uso", "Limites de débit", "Ratenlimits");
            Add("Models", "Modelos", "Modèles", "Modelle");
            Add("Pricing", "Precios", "Tarifs", "Preise");
            Add("Pasted from clipboard - double-check it, then hit Save and Continue.", "Pegado desde el portapapeles: revísalo y pulsa Guardar y continuar.", "Collé depuis le presse-papiers : vérifiez-le, puis cliquez sur Enregistrer et continuer.", "Aus Zwischenablage eingefügt – überprüfe den Inhalt und klicke auf Speichern und fortfahren.");
            Add("✓ Image generation is included with this same key", "✓ La generación de imágenes está incluida con esta misma clave", "✓ La génération d'images est incluse avec cette même clé", "✓ Bildgenerierung ist mit diesem Schlüssel enthalten");
            Add("Gemini's image generation is bundled in - no separate image provider key is required.", "La generación de imágenes de Gemini está incluida; no se necesita otra clave.", "La génération d'images Gemini est incluse : aucune autre clé n'est nécessaire.", "Die Gemini-Bildgenerierung ist enthalten – kein separater Bildanbieter-Schlüssel nötig.");
            Add("Skip for now", "Omitir por ahora", "Ignorer pour l'instant", "Vorerst überspringen");
            Add("Save and Continue", "Guardar y continuar", "Enregistrer et continuer", "Speichern und fortfahren");
            Add("Cancel", "Cancelar", "Annuler", "Abbrechen");
            Add("Your key is stored locally in %AppData%\\Aurora\\settings.json and sent only to the provider you pick above.", "Tu clave se guarda localmente en %AppData%\\Aurora\\settings.json y solo se envía al proveedor elegido.", "Votre clé est enregistrée localement dans %AppData%\\Aurora\\settings.json et envoyée uniquement au fournisseur choisi.", "Dein Schlüssel wird lokal unter %AppData%\\Aurora\\settings.json gespeichert und nur an den gewählten Anbieter gesendet.");
            Add("Windows capabilities", "Capacidades de Windows", "Fonctionnalités Windows", "Windows-Funktionen");
            Add("Every capability is visible here. Turn permissions on explicitly, then use the matching controls.", "Aquí puedes ver todas las capacidades. Activa los permisos explícitamente y usa los controles correspondientes.", "Toutes les fonctionnalités sont répertoriées ici. Activez explicitement les autorisations, puis utilisez les commandes correspondantes.", "Hier sind alle Funktionen aufgeführt. Aktiviere Berechtigungen ausdrücklich und verwende dann die passenden Steuerelemente.");
            Add("Permissions", "Permisos", "Autorisations", "Berechtigungen");
            Add("Files & folders", "Archivos y carpetas", "Fichiers et dossiers", "Dateien und Ordner");
            Add("Screen capture", "Captura de pantalla", "Capture d'écran", "Bildschirmaufnahme");
            Add("Clipboard", "Portapapeles", "Presse-papiers", "Zwischenablage");
            Add("Application launch & process access", "Inicio de aplicaciones y acceso a procesos", "Lancement d'applications et accès aux processus", "Anwendungsstart und Prozesszugriff");
            Add("Approved terminal commands", "Comandos de terminal aprobados", "Commandes de terminal approuvées", "Genehmigte Terminalbefehle");
            Add("UI automation", "Automatización de interfaz", "Automatisation de l'interface", "UI-Automatisierung");
            Add("Network access", "Acceso a la red", "Accès réseau", "Netzwerkzugriff");
            Add("Power controls", "Controles de energía", "Commandes d'alimentation", "Energieverwaltung");
            Add("Camera access", "Acceso a la cámara", "Accès à la caméra", "Kamerazugriff");
            Add("Microphone access", "Acceso al micrófono", "Accès au microphone", "Mikrofonzugriff");
            Add("MCP servers & tools", "Servidores y herramientas MCP", "Serveurs et outils MCP", "MCP-Server und -Tools");
            Add("Save permissions", "Guardar permisos", "Enregistrer les autorisations", "Berechtigungen speichern");
            Add("Camera", "Cámara", "Caméra", "Kamera");
            Add("Select a detected Windows camera, check permission, initialize it, or capture a photo.", "Selecciona una cámara detectada por Windows, comprueba el permiso, inicialízala o toma una foto.", "Sélectionnez une caméra Windows détectée, vérifiez l'autorisation, initialisez-la ou prenez une photo.", "Wähle eine erkannte Windows-Kamera aus, prüfe die Berechtigung, initialisiere sie oder nimm ein Foto auf.");
            Add("Refresh cameras", "Actualizar cámaras", "Actualiser les caméras", "Kameras aktualisieren");
            Add("Check permission", "Comprobar permiso", "Vérifier l'autorisation", "Berechtigung prüfen");
            Add("Initialize", "Inicializar", "Initialiser", "Initialisieren");
            Add("Capture photo", "Tomar foto", "Prendre une photo", "Foto aufnehmen");
            Add("MCP & tools", "MCP y herramientas", "MCP et outils", "MCP und Tools");
            Add("Connect a stdio MCP server and discover the tools it exposes.", "Conecta un servidor MCP stdio y descubre las herramientas que ofrece.", "Connectez un serveur MCP stdio et découvrez les outils qu'il expose.", "Verbinde einen stdio-MCP-Server und entdecke seine Tools.");
            Add("Server name", "Nombre del servidor", "Nom du serveur", "Servername");
            Add("Command", "Comando", "Commande", "Befehl");
            Add("Arguments", "Argumentos", "Arguments", "Argumente");
            Add("Connect & discover", "Conectar y descubrir", "Connecter et découvrir", "Verbinden und entdecken");
            Add("Disconnect", "Desconectar", "Déconnecter", "Trennen");
            Add("Discovered tools", "Herramientas encontradas", "Outils découverts", "Entdeckte Tools");
            Add("Image generation", "Generación de imágenes", "Génération d'images", "Bildgenerierung");
            Add("Refresh integration clients", "Actualizar clientes de integración", "Actualiser les clients d'intégration", "Integrationsclients aktualisieren");
            Add("Status", "Estado", "État", "Status");
            Add("Ready.", "Listo.", "Prêt.", "Bereit.");
            Add("Aurora Memory & Data", "Memoria y datos de Aurora", "Mémoire et données Aurora", "Aurora-Speicher und -Daten");
            Add("Memory & Data", "Memoria y datos", "Mémoire et données", "Speicher und Daten");
            Add("Saved companions and conversation memory stored locally by Aurora.", "Compañeros guardados y memoria de conversaciones almacenados localmente por Aurora.", "Compagnons enregistrés et mémoire des conversations stockés localement par Aurora.", "Gespeicherte Begleiter und Gesprächsspeicher, lokal von Aurora gespeichert.");
            Add("Find conversations", "Buscar conversaciones", "Rechercher des conversations", "Unterhaltungen suchen");
            Add("Search titles, tags and message text", "Buscar títulos, etiquetas y mensajes", "Rechercher dans les titres, étiquettes et messages", "Titel, Tags und Nachrichtentext durchsuchen");
            Add("Filter by tag", "Filtrar por etiqueta", "Filtrer par étiquette", "Nach Tag filtern");
            Add("★ Favorites", "★ Favoritos", "★ Favoris", "★ Favoriten");
            Add("New folder name", "Nombre de la nueva carpeta", "Nom du nouveau dossier", "Name des neuen Ordners");
            Add("Add folder", "Añadir carpeta", "Ajouter un dossier", "Ordner hinzufügen");
            Add("Delete selected folder", "Eliminar carpeta seleccionada", "Supprimer le dossier sélectionné", "Ausgewählten Ordner löschen");
            Add("Conversations in the folder are kept", "Se conservarán las conversaciones de la carpeta", "Les conversations du dossier seront conservées", "Unterhaltungen im Ordner bleiben erhalten");
            Add("Toggle favorite", "Alternar favorito", "Activer/désactiver le favori", "Favorit umschalten");
            Add("Messages", "Mensajes", "Messages", "Nachrichten");
            Add("Clear chat", "Borrar chat", "Effacer la conversation", "Chat löschen");
            Add("Delete only this companion's conversation", "Eliminar solo la conversación de este compañero", "Supprimer uniquement la conversation de ce compagnon", "Nur die Unterhaltung dieses Begleiters löschen");
            Add("Install Aurora", "Instalar Aurora", "Installer Aurora", "Aurora installieren");
            Add("Preparing Aurora setup... 60 seconds", "Preparando la configuración de Aurora... 60 segundos", "Préparation de l'installation d'Aurora... 60 secondes", "Aurora-Einrichtung wird vorbereitet... 60 Sekunden");
            Add("Preparation complete. Opening Aurora setup...", "Preparación completa. Abriendo la configuración de Aurora...", "Préparation terminée. Ouverture de la configuration d'Aurora...", "Vorbereitung abgeschlossen. Aurora-Einrichtung wird geöffnet...");
            Add("Aurora", "Aurora", "Aurora", "Aurora");
            Add("Aurora — Windows Capabilities", "Aurora — Capacidades de Windows", "Aurora — Fonctionnalités Windows", "Aurora — Windows-Funktionen");
            Add("Aurora Security Lockdown", "Bloqueo de seguridad de Aurora", "Verrouillage de sécurité Aurora", "Aurora-Sicherheitssperre");
            Add("AURORA SECURITY LOCKDOWN", "BLOQUEO DE SEGURIDAD DE AURORA", "VERROUILLAGE DE SÉCURITÉ AURORA", "AURORA-SICHERHEITSSPERRE");
            Add("Aurora has stopped interactive access until the installation and user recovery checks pass.", "Aurora ha detenido el acceso interactivo hasta que se completen las comprobaciones de instalación y recuperación del usuario.", "Aurora a suspendu l'accès interactif jusqu'à la réussite des vérifications d'installation et de récupération.", "Aurora hat den interaktiven Zugriff angehalten, bis die Installations- und Benutzerwiederherstellungsprüfungen bestanden sind.");
            Add("Recovery", "Recuperación", "Récupération", "Wiederherstellung");
            Add("Enter the recovery code stored for the active Aurora profile.", "Introduce el código de recuperación guardado para el perfil activo de Aurora.", "Saisissez le code de récupération enregistré pour le profil Aurora actif.", "Gib den für das aktive Aurora-Profil gespeicherten Wiederherstellungscode ein.");
            Add("Verify and unlock", "Verificar y desbloquear", "Vérifier et déverrouiller", "Überprüfen und entsperren");
            Add("Emergency stop is fail-closed. Closing this screen does not bypass lockdown.", "La parada de emergencia bloquea el acceso de forma segura. Cerrar esta ventana no evita el bloqueo.", "L'arrêt d'urgence verrouille l'accès par défaut. Fermer cette fenêtre ne contourne pas le verrouillage.", "Der Not-Aus sperrt standardmäßig. Das Schließen dieses Fensters umgeht die Sperre nicht.");
            Add("Aurora Profiles", "Perfiles de Aurora", "Profils Aurora", "Aurora-Profile");
            Add("User profiles", "Perfiles de usuario", "Profils utilisateur", "Benutzerprofile");
            Add("Each profile has separate settings and companion memory. Voice matching is optional and only suggests a profile.", "Cada perfil tiene ajustes y memoria de compañeros independientes. La identificación de voz es opcional y solo sugiere un perfil.", "Chaque profil possède ses propres paramètres et sa mémoire de compagnons. La reconnaissance vocale est facultative et ne fait que suggérer un profil.", "Jedes Profil hat eigene Einstellungen und Begleiterspeicher. Die Stimmerkennung ist optional und schlägt nur ein Profil vor.");
            Add("Delete", "Eliminar", "Supprimer", "Löschen");
            Add("Add profile", "Añadir perfil", "Ajouter un profil", "Profil hinzufügen");
            Add("Default", "Predeterminado", "Par défaut", "Standard");
            Add("Compact", "Compacto", "Compact", "Kompakt");
            Add("Chat focused", "Enfocado en el chat", "Centré sur la discussion", "Chatfokussiert");
            Add("Companion focused", "Enfocado en el compañero", "Centré sur le compagnon", "Begleiterfokussiert");
            Add("Reset to default", "Restablecer valores predeterminados", "Rétablir les valeurs par défaut", "Auf Standard zurücksetzen");
            Add("👥  Profiles", "👥  Perfiles", "👥  Profils", "👥  Profile");
            Add("✎  Change name", "✎  Cambiar nombre", "✎  Changer de nom", "✎  Namen ändern");
            Add("⚒  Tools & Capabilities", "⚒  Herramientas y capacidades", "⚒  Outils et fonctionnalités", "⚒  Tools und Funktionen");
            Add("Save name", "Guardar nombre", "Enregistrer le nom", "Namen speichern");
            Add("Your name", "Tu nombre", "Votre nom", "Dein Name");
            Add("What should Aurora call you?", "¿Cómo debería llamarte Aurora?", "Comment Aurora doit-elle vous appeler ?", "Wie soll Aurora dich nennen?");
            Add("Please enter a name.", "Introduce un nombre.", "Saisissez un nom.", "Bitte gib einen Namen ein.");
            Add("Help me start a task: ", "Ayúdame a empezar una tarea: ", "Aidez-moi à commencer une tâche : ", "Hilf mir, eine Aufgabe zu beginnen: ");
            Add("Show me today's schedule, tasks, and reminders.", "Muéstrame la agenda, las tareas y los recordatorios de hoy.", "Montre-moi le programme, les tâches et les rappels du jour.", "Zeig mir den heutigen Zeitplan, die Aufgaben und Erinnerungen.");
            Add("Create a reminder: ", "Crear un recordatorio: ", "Créer un rappel : ", "Erinnerung erstellen: ");
            Add("Attach a file", "Adjuntar un archivo", "Joindre un fichier", "Datei anhängen");
            Add("Photo saved to:", "Foto guardada en:", "Photo enregistrée dans :", "Foto gespeichert unter:");
            Add("Aurora Camera", "Cámara de Aurora", "Caméra Aurora", "Aurora-Kamera");
            Add("Restore", "Restaurar", "Restaurer", "Wiederherstellen");
            Add("Search titles, tags and message text", "Buscar títulos, etiquetas y mensajes", "Rechercher dans les titres, étiquettes et messages", "Titel, Tags und Nachrichtentext durchsuchen");
            Add("Jamendo", "Jamendo", "Jamendo", "Jamendo");
            Add("Refresh", "Actualizar", "Actualiser", "Aktualisieren");
            Add("Microphone", "Micrófono", "Microphone", "Mikrofon");
            Add("System default", "Predeterminado del sistema", "Par défaut du système", "Systemstandard");
            Add("{0} (not connected)", "{0} (no conectado)", "{0} (non connecté)", "{0} (nicht verbunden)");
            Add("Choose which microphone Aurora listens to for voice input and the wake word. System default follows whatever Windows uses. Press Save to apply.", "Elige qué micrófono escucha Aurora para la entrada de voz y la palabra de activación. El predeterminado del sistema sigue el de Windows. Pulsa Guardar para aplicar.", "Choisissez le microphone qu'Aurora écoute pour la saisie vocale et le mot d'activation. Par défaut du système suit le choix de Windows. Cliquez sur Enregistrer pour appliquer.", "Wähle das Mikrofon, auf das Aurora für Spracheingabe und Aktivierungswort hört. Systemstandard folgt der Windows-Auswahl. Zum Übernehmen auf Speichern klicken.");
            Add("Use selected", "Usar el seleccionado", "Utiliser la sélection", "Ausgewähltes verwenden");
            Add("Enroll voice", "Registrar voz", "Enregistrer la voix", "Stimme registrieren");
            Add("🔐 Recovery code", "🔐 Código de recuperación", "🔐 Code de récupération", "🔐 Wiederherstellungscode");
            Add("📁 Recovery storage", "📁 Almacenamiento de recuperación", "📁 Stockage de récupération", "📁 Wiederherstellungsspeicher");
            Add("Open recovery folder", "Abrir carpeta de recuperación", "Ouvrir le dossier de récupération", "Wiederherstellungsordner öffnen");
            Add("Recovery storage:", "Almacenamiento de recuperación:", "Stockage de récupération :", "Wiederherstellungsspeicher:");
            Add(" Enable speaker suggestions", " Activar sugerencias por voz", " Activer les suggestions vocales", " Stimmvorschläge aktivieren");
            Add("Aurora Recovery Code", "Código de recuperación de Aurora", "Code de récupération Aurora", "Aurora-Wiederherstellungscode");
            Add("Profile recovery", "Recuperación del perfil", "Récupération du profil", "Profilwiederherstellung");
            Add("Create a recovery code for this profile. Aurora stores only a secure verifier; the code itself is shown to you once.", "Crea un código de recuperación para este perfil. Aurora solo guarda un verificador seguro; el código se muestra una sola vez.", "Créez un code de récupération pour ce profil. Aurora ne conserve qu'un vérificateur sécurisé ; le code lui-même ne s'affiche qu'une seule fois.", "Erstelle einen Wiederherstellungscode für dieses Profil. Aurora speichert nur einen sicheren Prüfwert; der Code wird dir nur einmal angezeigt.");
            Add("Your recovery code", "Tu código de recuperación", "Votre code de récupération", "Dein Wiederherstellungscode");
            Add("Generate", "Generar", "Générer", "Generieren");
            Add("Copy", "Copiar", "Copier", "Kopieren");
            Add("Save to file", "Guardar en un archivo", "Enregistrer dans un fichier", "In Datei speichern");
            Add("Print", "Imprimir", "Imprimer", "Drucken");
            Add("Save or print this code before confirming. Do not put it in GitHub, logs, screenshots, or connector messages.", "Guarda o imprime este código antes de confirmar. No lo incluyas en GitHub, registros, capturas de pantalla ni mensajes de conectores.", "Enregistrez ou imprimez ce code avant de confirmer. Ne le placez pas dans GitHub, les journaux, les captures d'écran ou les messages de connecteur.", "Speichere oder drucke diesen Code, bevor du bestätigst. Gib ihn nicht in GitHub, Protokolle, Screenshots oder Connector-Nachrichten ein.");
            Add("I have saved my recovery code somewhere safe.", "He guardado mi código de recuperación en un lugar seguro.", "J'ai enregistré mon code de récupération dans un endroit sûr.", "Ich habe meinen Wiederherstellungscode sicher gespeichert.");
            Add("Cancel", "Cancelar", "Annuler", "Abbrechen");
            Add("Save recovery code", "Guardar código de recuperación", "Enregistrer le code de récupération", "Wiederherstellungscode speichern");
            Add("Settings", "Configuración", "Paramètres", "Einstellungen");
            Add("Engine, voice, integrations, and your local data.", "Motor, voz, integraciones y datos locales.", "Moteur, voix, intégrations et données locales.", "Engine, Sprache, Integrationen und lokale Daten.");
            Add("🧠 AI engine", "🧠 Motor de IA", "🧠 Moteur d'IA", "🧠 KI-Engine");
            Add("Provider, API key, reset", "Proveedor, clave de API, restablecer", "Fournisseur, clé API, réinitialisation", "Anbieter, API-Schlüssel, Zurücksetzen");
            Add("🧩 Models", "🧩 Modelos", "🧩 Modèles", "🧩 Modelle");
            Add("🎨 Companion colors", "🎨 Colores de compañeros", "🎨 Couleurs des compagnons", "🎨 Begleiterfarben");
            Add("Orb color per companion", "Color del orbe por compañero", "Couleur de l'orbe par compagnon", "Orb-Farbe pro Begleiter");
            Add("🔊 Voice", "🔊 Voz", "🔊 Voix", "🔊 Sprache");
            Add("OpenAI, ElevenLabs, Azure, or offline", "OpenAI, ElevenLabs, Azure o sin conexión", "OpenAI, ElevenLabs, Azure ou hors ligne", "OpenAI, ElevenLabs, Azure oder offline");
            Add("📄 Documents", "📄 Documentos", "📄 Documents", "📄 Dokumente");
            Add("Sift's trusted folder", "Carpeta de confianza de Sift", "Dossier approuvé de Sift", "Vertrauenswürdiger Ordner von Sift");
            Add("⚙️ System", "⚙️ Sistema", "⚙️ Système", "⚙️ System");
            Add("Volume, weather & web search", "Volumen, clima y búsqueda web", "Volume, météo et recherche Web", "Lautstärke, Wetter und Websuche");
            Add("🎵 Music", "🎵 Música", "🎵 Musique", "🎵 Musik");
            Add("🏠 Smart home", "🏠 Hogar inteligente", "🏠 Maison connectée", "🏠 Smart Home");
            Add("SmartThings, Home Assistant, Hubitat, Alexa", "SmartThings, Home Assistant, Hubitat y Alexa", "SmartThings, Home Assistant, Hubitat, Alexa", "SmartThings, Home Assistant, Hubitat, Alexa");
            Add("📧 Google", "📧 Google", "📧 Google", "📧 Google");
            Add("🛠️ Developer", "🛠️ Desarrollador", "🛠️ Développeur", "🛠️ Entwickler");
            Add("Override-code-gated", "Protegido por código de autorización", "Protégé par un code d'accès", "Durch Freigabecode geschützt");
            Add("💾 Memory & data", "💾 Memoria y datos", "💾 Mémoire et données", "💾 Speicher und Daten");
            Add("Where everything is stored", "Dónde se almacena todo", "Où tout est stocké", "Speicherort aller Daten");
            Add("Documents", "Documentos", "Documents", "Dokumente");
            Add("Developer", "Desarrollador", "Développeur", "Entwickler");
            Add("Star a Settings entry to pin it to Home.", "Marca una entrada de Configuración con una estrella para fijarla en Inicio.", "Marquez une entrée des paramètres d'une étoile pour l'épingler à l'accueil.", "Markiere einen Eintrag in den Einstellungen mit einem Stern, um ihn an der Startseite anzuheften.");
            Add("Pin {0} to Home", "Fijar {0} en Inicio", "Épingler {0} à l'accueil", "{0} an der Startseite anheften");
            Add("Unpin {0} from Home", "Desfijar {0} de Inicio", "Détacher {0} de l'accueil", "{0} von der Startseite lösen");
            Add("Could not save sidebar shortcuts: {0}", "No se pudieron guardar los accesos directos: {0}", "Impossible d'enregistrer les raccourcis : {0}", "Verknüpfungen konnten nicht gespeichert werden: {0}");
            Add("Conversations and local storage", "Conversaciones y almacenamiento local", "Conversations et stockage local", "Gespräche und lokaler Speicher");
            Add("Permissions, camera, images & MCP", "Permisos, cámara, imágenes y MCP", "Autorisations, caméra, images et MCP", "Berechtigungen, Kamera, Bilder und MCP");
            Add("Browse and search your saved conversations, favorites, folders, and tags.", "Explora y busca tus conversaciones guardadas, favoritos, carpetas y etiquetas.", "Parcourez et recherchez vos conversations, favoris, dossiers et étiquettes enregistrés.", "Durchsuche gespeicherte Gespräche, Favoriten, Ordner und Tags.");
            Add("Open Memory", "Abrir Memoria", "Ouvrir la mémoire", "Speicher öffnen");
            Add("Manage Windows permissions, camera, image generation, and MCP tools.", "Administra los permisos de Windows, la cámara, la generación de imágenes y las herramientas MCP.", "Gérez les autorisations Windows, la caméra, la génération d'images et les outils MCP.", "Verwalte Windows-Berechtigungen, Kamera, Bilderzeugung und MCP-Werkzeuge.");
            Add("Open Tools & Capabilities", "Abrir Herramientas y capacidades", "Ouvrir les outils et fonctionnalités", "Werkzeuge und Funktionen öffnen");
            Add("← Settings", "← Configuración", "← Paramètres", "← Einstellungen");
            Add("AI engine", "Motor de IA", "Moteur d'IA", "KI-Engine");
            Add("Change API key / provider", "Cambiar clave de API o proveedor", "Changer la clé API ou le fournisseur", "API-Schlüssel/Anbieter ändern");
            Add("Reset Aurora completely", "Restablecer Aurora por completo", "Réinitialiser complètement Aurora", "Aurora vollständig zurücksetzen");
            Add("Resetting wipes every key, token, and companion's chat history and restarts Aurora as if freshly installed. This can't be undone.", "El restablecimiento elimina todas las claves, tokens y conversaciones de compañeros, y reinicia Aurora como recién instalada. Esta acción no se puede deshacer.", "La réinitialisation efface toutes les clés, tous les jetons et l'historique des compagnons, puis redémarre Aurora comme après une nouvelle installation. Cette action est irréversible.", "Beim Zurücksetzen werden alle Schlüssel, Token und Begleiter-Chats gelöscht und Aurora wird wie nach einer Neuinstallation gestartet. Dies kann nicht rückgängig gemacht werden.");
            Add("Save model", "Guardar modelo", "Enregistrer le modèle", "Modell speichern");
            Add("Companion colors", "Colores de compañeros", "Couleurs des compagnons", "Begleiterfarben");
            Add("Voice", "Voz", "Voix", "Sprache");
            Add("Companions speak their replies out loud by default", "Los compañeros leen sus respuestas en voz alta de forma predeterminada", "Les compagnons lisent leurs réponses à voix haute par défaut", "Begleiter sprechen ihre Antworten standardmäßig laut aus");
            Add("Emotion cues (subtle orb glow tint; reply text is never changed)", "Indicadores de emoción (brillo sutil del orbe; el texto de respuesta no cambia)", "Indices émotionnels (lueur subtile de l'orbe ; le texte des réponses reste inchangé)", "Emotionshinweise (dezentes Leuchten des Orbs; Antworttext bleibt unverändert)");
            Add("Voice input sensitivity (confidence threshold)", "Sensibilidad de entrada de voz (umbral de confianza)", "Sensibilité de la saisie vocale (seuil de confiance)", "Spracheingabe-Empfindlichkeit (Konfidenzschwelle)");
            Add("Speaking pace", "Velocidad al hablar", "Débit de parole", "Sprechtempo");
            Add("Aurora voice volume", "Volumen de voz de Aurora", "Volume vocal d'Aurora", "Aurora-Sprechlautstärke");
            Add("Voice provider", "Proveedor de voz", "Fournisseur vocal", "Sprachanbieter");
            Add("API key", "Clave de API", "Clé API", "API-Schlüssel");
            Add("Get API key", "Obtener clave de API", "Obtenir une clé API", "API-Schlüssel abrufen");
            Add("Save key", "Guardar clave", "Enregistrer la clé", "Schlüssel speichern");
            Add("Save", "Guardar", "Enregistrer", "Speichern");
            Add("Test voice", "Probar voz", "Tester la voix", "Stimme testen");
            Add("Sift's documents folder", "Carpeta de documentos de Sift", "Dossier de documents de Sift", "Dokumentenordner von Sift");
            Add("Choose folder...", "Elegir carpeta...", "Choisir un dossier...", "Ordner auswählen...");
            Add("Clear", "Borrar", "Effacer", "Löschen");
            Add("System", "Sistema", "Système", "System");
            Add("Weather & web search", "Clima y búsqueda web", "Météo et recherche Web", "Wetter und Websuche");
            Add("App updates", "Actualizaciones de la aplicación", "Mises à jour de l'application", "App-Updates");
            Add("Check for updates", "Buscar actualizaciones", "Rechercher des mises à jour", "Nach Updates suchen");
            Add("Pull latest", "Descargar la última versión", "Télécharger la dernière version", "Neueste Version abrufen");
            Add("System volume", "Volumen del sistema", "Volume système", "Systemlautstärke");
            Add("How to get a Client ID", "Cómo obtener un ID de cliente", "Comment obtenir un ID client", "Client-ID abrufen");
            Add("Client ID", "ID de cliente", "ID client", "Client-ID");
            Add("Connect with Spotify", "Conectar con Spotify", "Se connecter à Spotify", "Mit Spotify verbinden");
            Add("Connect with Google", "Conectar con Google", "Se connecter à Google", "Mit Google verbinden");
            Add("SmartThings", "SmartThings", "SmartThings", "SmartThings");
            Add("Home Assistant", "Home Assistant", "Home Assistant", "Home Assistant");
            Add("Hubitat", "Hubitat", "Hubitat", "Hubitat");
            Add("Amazon Alexa", "Amazon Alexa", "Amazon Alexa", "Amazon Alexa");
            Add("Unified device catalog", "Catálogo unificado de dispositivos", "Catalogue unifié des appareils", "Einheitlicher Gerätekatalog");
            Add("Get devices", "Obtener dispositivos", "Récupérer les appareils", "Geräte abrufen");
            Add("Include notes / raw metadata in catalog", "Incluir notas y metadatos sin procesar en el catálogo", "Inclure les notes et métadonnées brutes dans le catalogue", "Notizen/Rohmetadaten im Katalog anzeigen");
            Add("Google", "Google", "Google", "Google");
            Add("Gmail, Drive & Docs", "Gmail, Drive y Documentos", "Gmail, Drive et Docs", "Gmail, Drive und Docs");
            Add("Client Secret", "Secreto de cliente", "Secret client", "Clientschlüssel");
            Add("Developer mode", "Modo de desarrollador", "Mode développeur", "Entwicklermodus");
            Add("Show", "Mostrar", "Afficher", "Anzeigen");
            Add("Create developer password", "Crear contraseña de desarrollador", "Créer un mot de passe développeur", "Entwicklerpasswort erstellen");
            Add("Unlock developer mode", "Desbloquear modo de desarrollador", "Déverrouiller le mode développeur", "Entwicklermodus entsperren");
            Add("Lock developer mode", "Bloquear modo de desarrollador", "Verrouiller le mode développeur", "Entwicklermodus sperren");
            Add("Role label", "Etiqueta de rol", "Libellé du rôle", "Rollenbezeichnung");
            Add("Tool access", "Acceso a herramientas", "Accès aux outils", "Toolzugriff");
            Add("System prompt", "Instrucción del sistema", "Instruction système", "Systemprompt");
            Add("Save overrides", "Guardar cambios", "Enregistrer les modifications", "Änderungen speichern");
            Add("Every companion's chat history, renamed name, and all the keys/tokens above live only on this PC - nothing is synced anywhere else.", "El historial de chat, el nombre personalizado y las claves y tokens de cada compañero solo se guardan en este PC; no se sincroniza nada.", "L'historique, le nom personnalisé et les clés/jetons de chaque compagnon restent sur ce PC : rien n'est synchronisé.", "Chatverlauf, geänderter Name und alle Schlüssel/Token jedes Begleiters bleiben auf diesem PC; nichts wird synchronisiert.");
            Add("Home", "Inicio", "Accueil", "Startseite");
            Add("Chat", "Chat", "Discussion", "Chat");
            Add("Memory", "Memoria", "Mémoire", "Speicher");
            Add("Music", "Música", "Musique", "Musik");
            Add("Smart Home", "Hogar inteligente", "Maison connectée", "Smart Home");
            Add("Tools", "Herramientas", "Outils", "Tools");
            Add("Your AI Companion", "Tu compañero de IA", "Votre compagnon IA", "Dein KI-Begleiter");
            Add("Local profile", "Perfil local", "Profil local", "Lokales Profil");
            Add("AI Provider Status", "Estado del proveedor de IA", "État du fournisseur d'IA", "KI-Anbieterstatus");
            Add("Today's Overview", "Resumen de hoy", "Aperçu du jour", "Tagesübersicht");
            Add("Open today's schedule", "Abrir la agenda de hoy", "Ouvrir le programme du jour", "Heutigen Zeitplan öffnen");
            Add("Now Playing", "Reproduciendo ahora", "En cours de lecture", "Läuft gerade");
            Add("Disk", "Disco", "Disque", "Festplatte");
            Add("Live Windows performance", "Rendimiento de Windows en tiempo real", "Performances Windows en direct", "Windows-Leistung in Echtzeit");
            Add("Voice input", "Entrada de voz", "Entrée vocale", "Spracheingabe");
            Add("Add files, tools, and actions", "Añadir archivos, herramientas y acciones", "Ajouter des fichiers, outils et actions", "Dateien, Tools und Aktionen hinzufügen");
            Add("Ask Aurora anything", "Pregúntale lo que quieras a Aurora", "Posez toutes vos questions à Aurora", "Frage Aurora, was du möchtest");
            Add("Attach file", "Adjuntar archivo", "Joindre un fichier", "Datei anhängen");
            Add("Stop speaking", "Dejar de hablar", "Arrêter la lecture vocale", "Sprechen stoppen");
            Add("Add file", "Añadir archivo", "Ajouter un fichier", "Datei hinzufügen");
            Add("Take photo", "Tomar foto", "Prendre une photo", "Foto aufnehmen");
            Add("Record video", "Grabar video", "Enregistrer une vidéo", "Video aufnehmen");
            Add("Add reminder", "Añadir recordatorio", "Ajouter un rappel", "Erinnerung hinzufügen");
            Add("Dashboard layout", "Diseño del panel", "Disposition du tableau de bord", "Dashboard-Layout");
            Add("Hide navigation", "Ocultar navegación", "Masquer la navigation", "Navigation ausblenden");
            Add("Show navigation", "Mostrar navegación", "Afficher la navigation", "Navigation einblenden");
            Add("Hide information panel", "Ocultar panel de información", "Masquer le panneau d'informations", "Informationsbereich ausblenden");
            Add("Show information panel", "Mostrar panel de información", "Afficher le panneau d'informations", "Informationsbereich einblenden");
            Add("Mini Aurora", "Aurora mini", "Mini Aurora", "Mini-Aurora");
            Add("Minimize", "Minimizar", "Réduire", "Minimieren");
            Add("Maximize", "Maximizar", "Agrandir", "Maximieren");
            Add("Close", "Cerrar", "Fermer", "Schließen");
            Add("Account menu", "Menú de cuenta", "Menu du compte", "Kontomenü");
            Add("Aurora Memory & Data", "Memoria y datos de Aurora", "Mémoire et données Aurora", "Aurora-Speicher und -Daten");
            Add("Profile recovery", "Recuperación del perfil", "Récupération du profil", "Profilwiederherstellung");
            Add("Delete profile", "Eliminar perfil", "Supprimer le profil", "Profil löschen");
            Add("Add profile", "Añadir perfil", "Ajouter un profil", "Profil hinzufügen");
            Add("Connected", "Conectado", "Connecté", "Verbunden");
            Add("Not connected", "No conectado", "Non connecté", "Nicht verbunden");
            Add("Hi, this is Aurora. This is what I sound like.", "Hola, soy Aurora. Así es como sueno.", "Bonjour, c'est Aurora. Voici ma voix.", "Hallo, ich bin Aurora. So klinge ich.");
            Add("Provider: {0}. Image generation uses Aurora's configured provider and API key.", "Proveedor: {0}. La generación de imágenes usa el proveedor y la clave de API configurados en Aurora.", "Fournisseur : {0}. La génération d'images utilise le fournisseur et la clé API configurés dans Aurora.", "Anbieter: {0}. Die Bildgenerierung verwendet den in Aurora konfigurierten Anbieter und API-Schlüssel.");
            Add("Provider: {0}. Integration clients refreshed.", "Proveedor: {0}. Clientes de integración actualizados.", "Fournisseur : {0}. Clients d'intégration actualisés.", "Anbieter: {0}. Integrationsclients aktualisiert.");
            Add("Permissions saved. Aurora services will respect these settings.", "Permisos guardados. Los servicios de Aurora respetarán esta configuración.", "Autorisations enregistrées. Les services Aurora respecteront ces paramètres.", "Berechtigungen gespeichert. Aurora-Dienste berücksichtigen diese Einstellungen.");
            Add("No Windows camera devices were found.", "No se encontraron cámaras de Windows.", "Aucune caméra Windows n'a été trouvée.", "Keine Windows-Kameras gefunden.");
            Add("Found {0} camera device(s).", "Se encontraron {0} cámara(s).", "{0} caméra(s) détectée(s).", "{0} Kameragerät(e) gefunden.");
            Add("Camera discovery failed: {0}", "Error al buscar cámaras: {0}", "Échec de la recherche de caméras : {0}", "Kamerasuche fehlgeschlagen: {0}");
            Add("Windows camera permission is allowed.", "El permiso de cámara de Windows está concedido.", "L'autorisation de caméra Windows est accordée.", "Die Windows-Kameraberechtigung ist erteilt.");
            Add("Camera permission is blocked. Enable it in Windows Settings > Privacy & security > Camera.", "El permiso de cámara está bloqueado. Actívalo en Configuración de Windows > Privacidad y seguridad > Cámara.", "L'accès à la caméra est bloqué. Activez-le dans Paramètres Windows > Confidentialité et sécurité > Caméra.", "Der Kamerazugriff ist gesperrt. Aktiviere ihn unter Windows-Einstellungen > Datenschutz und Sicherheit > Kamera.");
            Add("Permission check failed: {0}", "Error al comprobar el permiso: {0}", "Échec de la vérification de l'autorisation : {0}", "Berechtigungsprüfung fehlgeschlagen: {0}");
            Add("Enable Camera access above first.", "Primero activa el acceso a la cámara.", "Activez d'abord l'accès à la caméra ci-dessus.", "Aktiviere zuerst oben den Kamerazugriff.");
            Add("Camera initialized.", "Cámara inicializada.", "Caméra initialisée.", "Kamera initialisiert.");
            Add("Camera initialized: {0}", "Cámara inicializada: {0}", "Caméra initialisée : {0}", "Kamera initialisiert: {0}");
            Add("Camera initialization failed: {0}", "Error al inicializar la cámara: {0}", "Échec de l'initialisation de la caméra : {0}", "Kamerainitialisierung fehlgeschlagen: {0}");
            Add("Photo captured: {0}", "Foto capturada: {0}", "Photo capturée : {0}", "Foto aufgenommen: {0}");
            Add("Capture failed: {0}", "Error al capturar: {0}", "Échec de la capture : {0}", "Aufnahme fehlgeschlagen: {0}");
            Add("Enable MCP access above first.", "Primero activa el acceso a MCP.", "Activez d'abord l'accès MCP ci-dessus.", "Aktiviere zuerst oben den MCP-Zugriff.");
            Add("Enter an MCP server name and command.", "Introduce el nombre y el comando del servidor MCP.", "Saisissez le nom et la commande du serveur MCP.", "Gib einen MCP-Servernamen und -Befehl ein.");
            Add("Connected to {0}; discovered {1} tool(s).", "Conectado a {0}; se encontraron {1} herramienta(s).", "Connecté à {0} ; {1} outil(s) découvert(s).", "Mit {0} verbunden; {1} Tool(s) gefunden.");
            Add("MCP connection failed: {0}", "Error al conectar con MCP: {0}", "Échec de la connexion MCP : {0}", "MCP-Verbindung fehlgeschlagen: {0}");
            Add("No MCP server is connected in this window.", "No hay ningún servidor MCP conectado en esta ventana.", "Aucun serveur MCP n'est connecté dans cette fenêtre.", "In diesem Fenster ist kein MCP-Server verbunden.");
            Add("MCP server disconnected.", "Servidor MCP desconectado.", "Serveur MCP déconnecté.", "MCP-Server getrennt.");
            Add("Integration clients refreshed.", "Clientes de integración actualizados.", "Clients d'intégration actualisés.", "Integrationsclients aktualisiert.");
            Add("location", "ubicación", "localisation", "Standort");
            Add("microphone", "micrófono", "microphone", "Mikrofon");
            Add("For each capability, choose Allow or Deny. Device permissions are also confirmed by Windows when required.", "Para cada capacidad, elige Permitir o Denegar. Windows también confirma los permisos de dispositivos cuando es necesario.", "Pour chaque fonctionnalité, choisissez Autoriser ou Refuser. Windows confirme également les autorisations des appareils si nécessaire.", "Wähle für jede Funktion Zulassen oder Ablehnen. Geräteberechtigungen werden bei Bedarf auch von Windows bestätigt.");
            Add("Aurora will ask for permission to use location, microphone, camera, and Windows capabilities.", "Aurora solicitará permiso para usar la ubicación, el micrófono, la cámara y las funciones de Windows.", "Aurora demandera l'autorisation d'utiliser la localisation, le microphone, la caméra et les fonctionnalités Windows.", "Aurora bittet um Berechtigungen für Standort, Mikrofon, Kamera und Windows-Funktionen.");
            Add("Aurora needs your permission", "Aurora necesita tu permiso", "Aurora a besoin de votre autorisation", "Aurora benötigt deine Berechtigung");
            Add("Aurora would like to use your {0}.", "Aurora quiere usar tu {0}.", "Aurora souhaite utiliser votre {0}.", "Aurora möchte deinen {0} verwenden.");
            Add("Allow Aurora to access your {0}?", "¿Permitir que Aurora acceda a tu {0}?", "Autoriser Aurora à accéder à votre {0} ?", "Aurora den Zugriff auf {0} erlauben?");
            Add("Aurora would like to use {0}.", "Aurora quiere usar {0}.", "Aurora souhaite utiliser {0}.", "Aurora möchte {0} verwenden.");
            Add("Allow Aurora to access {0}?", "¿Permitir que Aurora acceda a {0}?", "Autoriser Aurora à accéder à {0} ?", "Aurora den Zugriff auf {0} erlauben?");
            Add("{0} access denied. Continuing...", "Acceso a {0} denegado. Continuando...", "Accès à {0} refusé. Suite...", "Zugriff auf {0} verweigert. Wird fortgesetzt...");
            Add("Allow selected. Waiting for Windows to finish the {0} permission request...", "Permiso concedido. Esperando a que Windows complete la solicitud de acceso a {0}...", "Autorisation accordée. En attente de la fin de la demande d'accès à {0} par Windows...", "Zulassen ausgewählt. Warte, bis Windows die Berechtigungsanfrage für {0} abgeschlossen hat...");
            Add("{0} access allowed. Continuing...", "Acceso a {0} permitido. Continuando...", "Accès à {0} autorisé. Suite...", "Zugriff auf {0} erlaubt. Wird fortgesetzt...");
            Add("Windows denied {0} access. Continuing...", "Windows denegó el acceso a {0}. Continuando...", "Windows a refusé l'accès à {0}. Suite...", "Windows hat den Zugriff auf {0} verweigert. Wird fortgesetzt...");
            Add("Windows could not grant {0} access. Continuing...", "Windows no pudo conceder acceso a {0}. Continuando...", "Windows n'a pas pu autoriser l'accès à {0}. Suite...", "Windows konnte den Zugriff auf {0} nicht erlauben. Wird fortgesetzt...");
            Add("{0} access could not be granted. Continuing...", "No se pudo conceder acceso a {0}. Continuando...", "L'accès à {0} n'a pas pu être accordé. Suite...", "Zugriff auf {0} konnte nicht gewährt werden. Wird fortgesetzt...");
            Add("Aurora uses this capability for features that depend on it. Your choice can also be controlled later in Windows privacy settings.", "Aurora usa esta capacidad para las funciones que la necesitan. También puedes cambiar tu decisión más tarde en la configuración de privacidad de Windows.", "Aurora utilise cette fonctionnalité pour les fonctions qui en dépendent. Vous pourrez également modifier votre choix dans les paramètres de confidentialité Windows.", "Aurora benötigt diese Funktion für entsprechende Features. Deine Auswahl kannst du später in den Windows-Datenschutzeinstellungen ändern.");
            Add("You can change this permission later in Windows capabilities settings.", "Puedes cambiar este permiso más tarde en la configuración de capacidades de Windows.", "Vous pourrez modifier cette autorisation ultérieurement dans les paramètres des fonctionnalités Windows.", "Diese Berechtigung kannst du später in den Windows-Funktionseinstellungen ändern.");
            Add("Aurora — {0} permission", "Aurora — permiso de {0}", "Aurora — autorisation {0}", "Aurora – {0}-Berechtigung");
            Add("Allow", "Permitir", "Autoriser", "Zulassen");
            Add("Deny", "Denegar", "Refuser", "Ablehnen");
            Add("Permissions complete — Aurora is starting soon.", "Permisos completados: Aurora se iniciará en breve.", "Autorisations terminées — Aurora démarrera bientôt.", "Berechtigungen abgeschlossen – Aurora startet in Kürze.");
            Add("Startup delay remaining: {0} seconds", "Tiempo restante para iniciar: {0} segundos", "Délai avant démarrage : {0} secondes", "Verbleibende Startverzögerung: {0} Sekunden");
            Add("Good morning, {0}", "Buenos días, {0}", "Bonjour, {0}", "Guten Morgen, {0}");
            Add("Good afternoon, {0}", "Buenas tardes, {0}", "Bon après-midi, {0}", "Guten Tag, {0}");
            Add("Good evening, {0}", "Buenas noches, {0}", "Bonsoir, {0}", "Guten Abend, {0}");
            Add("AI provider", "Proveedor de IA", "Fournisseur d'IA", "KI-Anbieter");
            Add("{0} configured", "{0} configurado", "{0} configuré", "{0} konfiguriert");
            Add("{0} disconnected", "{0} desconectado", "{0} déconnecté", "{0} getrennt");
            Add("Listening for “Hey Aurora”", "Escuchando «Hey Aurora»", "À l'écoute de « Hey Aurora »", "Höre auf „Hey Aurora“");
            Add("What can I do for you?", "¿En qué puedo ayudarte?", "Que puis-je faire pour vous ?", "Was kann ich für dich tun?");
            Add("Waiting for location permission…", "Esperando el permiso de ubicación…", "En attente de l'autorisation de localisation…", "Warte auf Standortberechtigung…");
            Add("Finding your location…", "Buscando tu ubicación…", "Recherche de votre position…", "Dein Standort wird ermittelt…");
            Add("Location unavailable", "Ubicación no disponible", "Localisation indisponible", "Standort nicht verfügbar");
            Add("Weather", "Clima", "Météo", "Wetter");
            Add("Weather unavailable", "Clima no disponible", "Météo indisponible", "Wetter nicht verfügbar");
            Add("Weather service is not available.", "El servicio meteorológico no está disponible.", "Le service météo n'est pas disponible.", "Der Wetterdienst ist nicht verfügbar.");
            Add("Location permission required", "Se requiere permiso de ubicación", "Autorisation de localisation requise", "Standortberechtigung erforderlich");
            Add("Allow Aurora to use Windows location services to show local NWS weather.", "Permite que Aurora use los servicios de ubicación de Windows para mostrar el clima local del NWS.", "Autorisez Aurora à utiliser les services de localisation Windows pour afficher la météo locale du NWS.", "Erlaube Aurora, Windows-Standortdienste für das lokale NWS-Wetter zu verwenden.");
            Add("Unable to load National Weather Service data: {0}", "No se pudieron cargar los datos del Servicio Meteorológico Nacional: {0}", "Impossible de charger les données du National Weather Service : {0}", "Daten des National Weather Service konnten nicht geladen werden: {0}");
            Add("NWS observation", "Observación del NWS", "Observation du NWS", "NWS-Beobachtung");
            Add("NWS forecast", "Pronóstico del NWS", "Prévision du NWS", "NWS-Vorhersage");
            Add("Thinking...", "Pensando...", "Réflexion en cours...", "Denke nach...");
            Add("Something went wrong on the last request.", "Se produjo un error en la última solicitud.", "Une erreur s'est produite lors de la dernière demande.", "Bei der letzten Anfrage ist ein Fehler aufgetreten.");
            Add("New conversation ready.", "Nueva conversación lista.", "Nouvelle conversation prête.", "Neue Unterhaltung bereit.");
            Add("Chat history cleared.", "Historial del chat borrado.", "Historique de la conversation effacé.", "Chatverlauf gelöscht.");
            Add("Ready to help with anything.", "Listo para ayudarte con lo que necesites.", "Prêt à vous aider pour tout.", "Bereit, bei allem zu helfen.");
            Add("Ready to dig into a topic.", "Listo para investigar un tema.", "Prêt à approfondir un sujet.", "Bereit, ein Thema zu recherchieren.");
            Add("Ready to plan something.", "Listo para planificar.", "Prêt à planifier.", "Bereit zum Planen.");
            Add("Ready to handle messages and documents.", "Listo para gestionar mensajes y documentos.", "Prêt à gérer les messages et les documents.", "Bereit, Nachrichten und Dokumente zu bearbeiten.");
            Add("Ready to control the smart home.", "Listo para controlar el hogar inteligente.", "Prêt à contrôler la maison connectée.", "Bereit, das Smart Home zu steuern.");
            Add("Default (global)", "Predeterminado (global)", "Par défaut (global)", "Standard (global)");
            Add("Folder created.", "Carpeta creada.", "Dossier créé.", "Ordner erstellt.");
            Add("Folder deleted. Its conversations were kept.", "Carpeta eliminada. Se conservaron sus conversaciones.", "Dossier supprimé. Ses conversations ont été conservées.", "Ordner gelöscht. Die Unterhaltungen darin blieben erhalten.");
            Add("Select a folder to delete.", "Selecciona una carpeta para eliminar.", "Sélectionnez un dossier à supprimer.", "Wähle einen Ordner zum Löschen aus.");
            Add("Checking what's playing...", "Comprobando qué se está reproduciendo...", "Vérification du titre en cours...", "Wiedergabe wird überprüft...");
            Add("Spotify couldn't be reached right now.", "No se pudo conectar con Spotify.", "Spotify est inaccessible pour le moment.", "Spotify ist derzeit nicht erreichbar.");
            Add("Nothing is currently playing.", "No hay nada reproduciéndose.", "Aucun titre n'est en cours de lecture.", "Es wird gerade nichts abgespielt.");
            Add("Music controls unavailable.", "Controles de música no disponibles.", "Commandes musicales indisponibles.", "Musiksteuerung nicht verfügbar.");
            Add("Windows media controls aren't available on this PC.", "Los controles multimedia de Windows no están disponibles en este PC.", "Les commandes multimédias Windows ne sont pas disponibles sur ce PC.", "Windows-Mediensteuerung ist auf diesem PC nicht verfügbar.");
            Add("Windows media", "Multimedia de Windows", "Médias Windows", "Windows-Medien");
            Add("No folder", "Sin carpeta", "Aucun dossier", "Kein Ordner");
            Add("All folders", "Todas las carpetas", "Tous les dossiers", "Alle Ordner");
            Add("Enter the recovery code saved for the active profile.", "Introduce el código de recuperación guardado para el perfil activo.", "Saisissez le code de récupération enregistré pour le profil actif.", "Gib den für das aktive Profil gespeicherten Wiederherstellungscode ein.");
            Add("No recovery code is saved for the active profile. Aurora remains locked until one is created and confirmed.", "No hay ningún código de recuperación guardado para el perfil activo. Aurora permanecerá bloqueada hasta que se cree y confirme uno.", "Aucun code de récupération n'est enregistré pour le profil actif. Aurora restera verrouillée jusqu'à la création et la confirmation d'un code.", "Für das aktive Profil ist kein Wiederherstellungscode gespeichert. Aurora bleibt gesperrt, bis einer erstellt und bestätigt wurde.");
            Add("Enter a recovery code.", "Introduce un código de recuperación.", "Saisissez un code de récupération.", "Gib einen Wiederherstellungscode ein.");
            Add("Recovery code rejected. Aurora remains locked.", "Código de recuperación rechazado. Aurora permanece bloqueada.", "Code de récupération refusé. Aurora reste verrouillée.", "Wiederherstellungscode abgelehnt. Aurora bleibt gesperrt.");
            Add("Recovery failed: {0}", "Error en la recuperación: {0}", "Échec de la récupération : {0}", "Wiederherstellung fehlgeschlagen: {0}");
            Add("Profile: {0}", "Perfil: {0}", "Profil : {0}", "Profil: {0}");
            Add("New profile", "Nuevo perfil", "Nouveau profil", "Neues Profil");
            Add("Profile name", "Nombre del perfil", "Nom du profil", "Profilname");
            Add("Create", "Crear", "Créer", "Erstellen");
            Add("Delete profile \"{0}\"? This permanently removes the profile and its profile-scoped Aurora data.", "¿Eliminar el perfil «{0}»? Se eliminarán permanentemente el perfil y sus datos de Aurora.", "Supprimer le profil « {0} » ? Le profil et ses données Aurora seront définitivement supprimés.", "Profil „{0}“ löschen? Das Profil und seine Aurora-Daten werden dauerhaft entfernt.");
            Add("Type {0} exactly to enable deletion.", "Escribe exactamente {0} para activar la eliminación.", "Saisissez exactement {0} pour autoriser la suppression.", "Gib exakt {0} ein, um das Löschen zu aktivieren.");
            Add("Aurora must keep at least one profile, and the selected profile could not be deleted.", "Aurora debe conservar al menos un perfil y no se pudo eliminar el perfil seleccionado.", "Aurora doit conserver au moins un profil ; le profil sélectionné n'a pas pu être supprimé.", "Aurora muss mindestens ein Profil behalten. Das ausgewählte Profil konnte nicht gelöscht werden.");
            Add("Profile \"{0}\" was deleted.", "Se eliminó el perfil «{0}».", "Le profil « {0} » a été supprimé.", "Profil „{0}“ wurde gelöscht.");
            Add("Aurora could not delete profile \"{0}\". No partial deletion was intended.\n\n{1}", "Aurora no pudo eliminar el perfil «{0}». No se pretendía realizar una eliminación parcial.\n\n{1}", "Aurora n'a pas pu supprimer le profil « {0} ». Aucune suppression partielle n'était prévue.\n\n{1}", "Aurora konnte Profil „{0}“ nicht löschen. Es wurden keine Teillöschungen beabsichtigt.\n\n{1}");
            Add("Enter the password for {0} to switch profiles.", "Introduce la contraseña de {0} para cambiar de perfil.", "Saisissez le mot de passe de {0} pour changer de profil.", "Gib das Passwort für {0} ein, um das Profil zu wechseln.");
            Add("That password is not valid for this profile.", "La contraseña no es válida para este perfil.", "Ce mot de passe n'est pas valide pour ce profil.", "Dieses Passwort ist für das Profil ungültig.");
            Add("Select the active profile to manage its recovery code.", "Selecciona el perfil activo para administrar su código de recuperación.", "Sélectionnez le profil actif pour gérer son code de récupération.", "Wähle das aktive Profil aus, um seinen Wiederherstellungscode zu verwalten.");
            Add("Choose where Aurora stores recovery-code verifiers", "Elige dónde almacena Aurora los verificadores de códigos de recuperación", "Choisissez l'emplacement de stockage des vérificateurs de codes de récupération Aurora", "Speicherort für Aurora-Wiederherstellungscode-Prüfwerte auswählen");
            Add("Recovery-code verifier files were moved to the selected folder. The recovery codes themselves are never stored there in plaintext.", "Los archivos de verificación se movieron a la carpeta seleccionada. Los códigos de recuperación nunca se guardan allí en texto sin cifrar.", "Les fichiers de vérification ont été déplacés dans le dossier choisi. Les codes de récupération ne sont jamais stockés en clair.", "Die Prüfwertdateien wurden in den ausgewählten Ordner verschoben. Wiederherstellungscodes selbst werden dort nie im Klartext gespeichert.");
            Add("Aurora could not change the recovery storage folder. No recovery code was changed.\n\n{0}", "Aurora no pudo cambiar la carpeta de recuperación. No se modificó ningún código.\n\n{0}", "Aurora n'a pas pu modifier le dossier de récupération. Aucun code n'a été modifié.\n\n{0}", "Aurora konnte den Wiederherstellungsordner nicht ändern. Kein Wiederherstellungscode wurde verändert.\n\n{0}");
            Add("Aurora could not open the recovery storage folder.\n\n{0}", "Aurora no pudo abrir la carpeta de recuperación.\n\n{0}", "Aurora n'a pas pu ouvrir le dossier de récupération.\n\n{0}", "Aurora konnte den Wiederherstellungsordner nicht öffnen.\n\n{0}");
            Add("Speak a normal sentence using Aurora's microphone after closing this window. Aurora will store only a local voice signature for this profile. Voice matching is not authentication.", "Cuando cierres esta ventana, di una frase normal usando el micrófono de Aurora. Aurora solo guardará una firma de voz local para este perfil. La identificación por voz no es autenticación.", "Après avoir fermé cette fenêtre, prononcez une phrase normale avec le microphone d'Aurora. Aurora ne conservera qu'une signature vocale locale pour ce profil. La reconnaissance vocale n'est pas une méthode d'authentification.", "Sprich nach dem Schließen dieses Fensters einen normalen Satz ins Aurora-Mikrofon. Aurora speichert nur eine lokale Stimm-Signatur für dieses Profil. Stimmerkennung ist keine Authentifizierung.");
            Add("Your recovery code is now protected for this profile. Aurora does not store the plaintext code.", "Tu código de recuperación ya está protegido para este perfil. Aurora no almacena el código en texto sin cifrar.", "Votre code de récupération est protégé pour ce profil. Aurora ne stocke pas le code en clair.", "Dein Wiederherstellungscode ist jetzt für dieses Profil geschützt. Aurora speichert den Code nicht im Klartext.");
            Add("Recovery code saved", "Código de recuperación guardado", "Code de récupération enregistré", "Wiederherstellungscode gespeichert");
            Add("Recovery storage: {0}", "Almacenamiento de recuperación: {0}", "Stockage de récupération : {0}", "Wiederherstellungsspeicher: {0}");
            Add("Save Aurora recovery code", "Guardar código de recuperación de Aurora", "Enregistrer le code de récupération Aurora", "Aurora-Wiederherstellungscode speichern");
            Add("Text file (*.txt)|*.txt", "Archivo de texto (*.txt)|*.txt", "Fichier texte (*.txt)|*.txt", "Textdatei (*.txt)|*.txt");
            Add("Aurora profile recovery code", "Código de recuperación del perfil de Aurora", "Code de récupération du profil Aurora", "Aurora-Profilwiederherstellungscode");
            Add("Get a free key", "Obtener una clave gratis", "Obtenir une clé gratuite", "Kostenlosen Schlüssel abrufen");
            Add("Get an API key", "Obtener una clave de API", "Obtenir une clé API", "API-Schlüssel abrufen");
            Add("Get a token", "Obtener un token", "Obtenir un jeton", "Token abrufen");
            Add("Free, no card needed - sign in with any Google account and copy the key back here.", "Gratis, no se necesita tarjeta: inicia sesión con cualquier cuenta de Google y copia aquí la clave.", "Gratuit, sans carte bancaire : connectez-vous avec un compte Google et copiez la clé ici.", "Kostenlos, keine Karte nötig – mit einem Google-Konto anmelden und den Schlüssel hier einfügen.");
            Add("The free tier is rate-limited (chat: roughly 10-15 requests/minute; images: up to ~500/day) - plenty for personal use, but heavy back-to-back use across all five companions could occasionally hit a limit.", "El nivel gratuito tiene límites (chat: unas 10–15 solicitudes por minuto; imágenes: hasta unas 500 al día). Suele bastar para uso personal, aunque un uso intensivo de los cinco compañeros puede alcanzar el límite.", "L'offre gratuite est limitée (chat : environ 10 à 15 requêtes/minute ; images : jusqu'à environ 500/jour). Elle convient à un usage personnel, mais une utilisation intensive des cinq compagnons peut atteindre la limite.", "Der kostenlose Tarif ist begrenzt (Chat: etwa 10–15 Anfragen/Minute; Bilder: bis zu ca. 500/Tag). Für den persönlichen Gebrauch reicht das meist, intensive Nutzung aller fünf Begleiter kann das Limit jedoch erreichen.");
            Add("Free forever, no card needed - sign in with any email and copy the key back here.", "Gratis para siempre, no se necesita tarjeta: inicia sesión con cualquier correo y copia aquí la clave.", "Gratuit à vie, sans carte bancaire : connectez-vous avec une adresse e-mail et copiez la clé ici.", "Dauerhaft kostenlos, keine Karte nötig – mit einer beliebigen E-Mail anmelden und den Schlüssel hier einfügen.");
            Add("The free tier is rate-limited (roughly 30 requests/minute, up to ~14,400/day depending on the model) - plenty for personal use. Doesn't include image generation - Home/Sift image requests would need a separate Gemini key added too.", "El nivel gratuito tiene límites (unas 30 solicitudes por minuto y hasta unas 14 400 al día, según el modelo), suficiente para uso personal. No incluye generación de imágenes; para solicitudes de Home o Sift necesitarás añadir una clave de Gemini.", "L'offre gratuite est limitée (environ 30 requêtes/minute, jusqu'à 14 400/jour selon le modèle), ce qui suffit à un usage personnel. La génération d'images n'est pas incluse ; les demandes de Home ou Sift nécessitent aussi une clé Gemini.", "Der kostenlose Tarif ist begrenzt (etwa 30 Anfragen/Minute, je nach Modell bis zu 14.400/Tag) und reicht meist privat aus. Bildgenerierung ist nicht enthalten; für Bildanfragen von Home/Sift wird zusätzlich ein Gemini-Schlüssel benötigt.");
            Add("Requires an OpenAI account with billing set up - paste a key from your API dashboard.", "Requiere una cuenta de OpenAI con facturación configurada. Pega una clave de tu panel de API.", "Un compte OpenAI avec facturation configurée est nécessaire. Collez une clé depuis votre tableau de bord API.", "Ein OpenAI-Konto mit eingerichteter Abrechnung ist erforderlich. Füge einen Schlüssel aus dem API-Dashboard ein.");
            Add("This is a paid, metered API - OpenAI doesn't offer a permanent free tier (new accounts sometimes get a small trial credit that expires). Lightweight models are inexpensive for personal chat use, but you'll need a card on file eventually.", "Esta API es de pago por uso; OpenAI no ofrece un nivel gratuito permanente (las cuentas nuevas a veces reciben crédito de prueba temporal). Los modelos ligeros son económicos para chats personales, pero necesitarás una tarjeta registrada.", "Cette API est payante à l'usage ; OpenAI ne propose pas d'offre gratuite permanente (les nouveaux comptes reçoivent parfois un petit crédit d'essai temporaire). Les modèles légers sont abordables, mais une carte bancaire sera nécessaire.", "Diese API wird nutzungsabhängig berechnet; OpenAI bietet keinen dauerhaft kostenlosen Tarif (neue Konten erhalten manchmal ein befristetes Testguthaben). Leichte Modelle sind günstig, aber eine hinterlegte Karte ist erforderlich.");
            Add("Requires an Anthropic account with billing set up - paste a key from your API console.", "Requiere una cuenta de Anthropic con facturación configurada. Pega una clave de tu consola de API.", "Un compte Anthropic avec facturation configurée est nécessaire. Collez une clé depuis votre console API.", "Ein Anthropic-Konto mit eingerichteter Abrechnung ist erforderlich. Füge einen Schlüssel aus der API-Konsole ein.");
            Add("This is a paid, metered API - Anthropic doesn't offer a permanent free tier (new accounts sometimes get a small trial credit that expires). You'll need a card on file for ongoing use.", "Esta API es de pago por uso; Anthropic no ofrece un nivel gratuito permanente (las cuentas nuevas a veces reciben crédito de prueba temporal). Para seguir usándola necesitarás una tarjeta registrada.", "Cette API est payante à l'usage ; Anthropic ne propose pas d'offre gratuite permanente (les nouveaux comptes reçoivent parfois un petit crédit d'essai temporaire). Une carte bancaire sera nécessaire pour continuer.", "Diese API wird nutzungsabhängig berechnet; Anthropic bietet keinen dauerhaft kostenlosen Tarif (neue Konten erhalten manchmal ein befristetes Testguthaben). Für die weitere Nutzung ist eine Karte erforderlich.");
            Add("Paste a GitHub personal access token for an account with an active Copilot subscription. This uses GitHub's internal, undocumented Copilot API (the same one editor extensions use) - it is unofficial, unsupported, and may break or be blocked without notice.", "Pega un token de acceso personal de GitHub de una cuenta con una suscripción activa a Copilot. Esta integración usa la API interna y no documentada de Copilot; no es oficial ni compatible y podría dejar de funcionar o bloquearse sin aviso.", "Collez un jeton d'accès personnel GitHub associé à un abonnement Copilot actif. Cette intégration utilise l'API Copilot interne et non documentée de GitHub ; elle n'est pas officielle, n'est pas prise en charge et peut cesser de fonctionner ou être bloquée sans préavis.", "Füge ein persönliches GitHub-Zugriffstoken eines Kontos mit aktivem Copilot-Abo ein. Diese Integration nutzt GitHubs interne, undokumentierte Copilot-API; sie ist inoffiziell, nicht unterstützt und kann ohne Vorwarnung ausfallen oder gesperrt werden.");
            Add("GitHub Copilot is a paid subscription service. This integration is unofficial - it works by exchanging your token through GitHub's internal Copilot endpoint, which is not a publicly documented or supported API and may violate GitHub's Copilot terms of use. Use at your own risk.", "GitHub Copilot es un servicio de suscripción de pago. Esta integración es extraoficial; intercambia el token mediante una API interna no documentada ni compatible y podría infringir las condiciones de Copilot. Úsala bajo tu responsabilidad.", "GitHub Copilot est un service payant par abonnement. Cette intégration non officielle utilise un point de terminaison interne non documenté ni pris en charge, et peut enfreindre les conditions Copilot. Utilisez-la à vos risques.", "GitHub Copilot ist ein kostenpflichtiger Abonnementdienst. Diese inoffizielle Integration nutzt einen internen, nicht dokumentierten und nicht unterstützten Endpunkt und verstößt möglicherweise gegen Copilot-Nutzungsbedingungen. Nutzung auf eigenes Risiko.");
            Add("Unlock {0}", "Desbloquear {0}", "Déverrouiller {0}", "{0} entsperren");
            Add("Profile deleted", "Perfil eliminado", "Profil supprimé", "Profil gelöscht");
            Add("Profile password", "Contraseña del perfil", "Mot de passe du profil", "Profilpasswort");
            Add("Recovery code", "Código de recuperación", "Code de récupération", "Wiederherstellungscode");
            Add("Recovery storage", "Almacenamiento de recuperación", "Stockage de récupération", "Wiederherstellungsspeicher");
            Add("Voice profile", "Perfil de voz", "Profil vocal", "Stimmprofil");
            Add("New profile", "Nuevo perfil", "Nouveau profil", "Neues Profil");
            Add("Recovery storage: {0}", "Almacenamiento de recuperación: {0}", "Stockage de récupération : {0}", "Wiederherstellungsspeicher: {0}");
            Add("Memory & data", "Memoria y datos", "Mémoire et données", "Speicher und Daten");
            Add("Smart home", "Hogar inteligente", "Maison connectée", "Smart Home");
            Add("🎙  Voice input", "🎙  Entrada de voz", "🎙  Entrée vocale", "🎙  Spracheingabe");
            Add("♫  Music", "♫  Música", "♫  Musique", "♫  Musik");
            Add("⌂  Smart Home", "⌂  Hogar inteligente", "⌂  Maison connectée", "⌂  Smart Home");
            Add("☑  Add reminder", "☑  Añadir recordatorio", "☑  Ajouter un rappel", "☑  Erinnerung hinzufügen");
            Add("⚙  Settings", "⚙  Configuración", "⚙  Paramètres", "⚙  Einstellungen");
            Add("📎  Add file", "📎  Añadir archivo", "📎  Ajouter un fichier", "📎  Datei hinzufügen");
            Add("📷  Take photo", "📷  Tomar foto", "📷  Prendre une photo", "📷  Foto aufnehmen");
            Add("🎥  Record video", "🎥  Grabar video", "🎥  Enregistrer une vidéo", "🎥  Video aufnehmen");
            Add("▶ / pause", "▶ / pausa", "▶ / pause", "▶ / Pause");
            Add("Play / pause", "Reproducir / pausar", "Lecture / pause", "Wiedergabe / Pause");
            Add("Previous", "Anterior", "Précédent", "Zurück");
            Add("Next", "Siguiente", "Suivant", "Weiter");
            Add("CPU", "CPU", "CPU", "CPU");
            Add("RAM", "RAM", "RAM", "RAM");
            Add("GPU", "GPU", "GPU", "GPU");
            Add("How to get a Client ID / Secret", "Cómo obtener un ID y secreto de cliente", "Comment obtenir un ID client et un secret", "Client-ID und Secret abrufen");
            Add("1. Go to developer.spotify.com/dashboard and log in with any Spotify account.\n2. Create an app (any name/description works).\n3. In the app's settings, add this exact Redirect URI: http://127.0.0.1:8888/callback\n4. Copy the Client ID shown on the app's page into the box below.", "1. Ve a developer.spotify.com/dashboard e inicia sesión con cualquier cuenta de Spotify.\n2. Crea una aplicación (el nombre y la descripción pueden ser cualquiera).\n3. En los ajustes de la aplicación, añade esta URI de redirección exacta: http://127.0.0.1:8888/callback\n4. Copia el ID de cliente que aparece en la página de la aplicación en el campo siguiente.", "1. Accédez à developer.spotify.com/dashboard et connectez-vous avec un compte Spotify.\n2. Créez une application (le nom et la description sont libres).\n3. Dans ses paramètres, ajoutez exactement cette URI de redirection : http://127.0.0.1:8888/callback\n4. Copiez l'ID client affiché sur la page de l'application dans le champ ci-dessous.", "1. Öffne developer.spotify.com/dashboard und melde dich mit einem Spotify-Konto an.\n2. Erstelle eine App (Name und Beschreibung sind frei wählbar).\n3. Füge in den App-Einstellungen genau diese Redirect-URI hinzu: http://127.0.0.1:8888/callback\n4. Kopiere die angezeigte Client-ID in das Feld unten.");
            Add("1. Go to console.cloud.google.com and create a project (or use an existing one).\n2. APIs & Services → OAuth consent screen → set it up for your own use (External + add yourself as a test user is fine).\n3. APIs & Services → Credentials → Create Credentials → OAuth client ID → Application type: Desktop app.\n4. Copy the Client ID and Client Secret it gives you into the two boxes below.\n5. APIs & Services → Library → enable the Gmail API and Google Drive API.", "1. Ve a console.cloud.google.com y crea un proyecto o selecciona uno existente.\n2. APIs y servicios → Pantalla de consentimiento de OAuth → configúrala para uso propio (Externo y añádete como usuario de prueba).\n3. APIs y servicios → Credenciales → Crear credenciales → ID de cliente de OAuth → Aplicación de escritorio.\n4. Copia el ID de cliente y el secreto de cliente en los campos siguientes.\n5. APIs y servicios → Biblioteca → activa las API de Gmail y Google Drive.", "1. Accédez à console.cloud.google.com et créez un projet ou choisissez-en un existant.\n2. API et services → Écran de consentement OAuth → configurez-le pour votre usage (Externe et ajoutez-vous comme utilisateur test).\n3. API et services → Identifiants → Créer des identifiants → ID client OAuth → Application de bureau.\n4. Copiez l'ID client et le secret client dans les deux champs ci-dessous.\n5. API et services → Bibliothèque → activez les API Gmail et Google Drive.", "1. Öffne console.cloud.google.com und erstelle ein Projekt oder wähle ein vorhandenes aus.\n2. APIs & Dienste → OAuth-Zustimmungsbildschirm → für eigene Nutzung einrichten (Extern; dich als Testnutzer hinzufügen).\n3. APIs & Dienste → Anmeldedaten → Anmeldedaten erstellen → OAuth-Client-ID → Desktop-App.\n4. Kopiere Client-ID und Client-Secret in die beiden Felder unten.\n5. APIs & Dienste → Bibliothek → Gmail API und Google Drive API aktivieren.");
            Add("Real sign-in via Spotify's own OAuth flow - needs a free Client ID from Spotify's developer dashboard, no client secret required. Reading what's playing and searching works on any account; play/pause/skip needs Spotify Premium and an active device (Spotify open somewhere).", "Inicio de sesión real mediante OAuth de Spotify. Necesitas un ID de cliente gratuito del panel de desarrolladores de Spotify; no hace falta secreto de cliente. Consultar lo que suena y buscar funciona con cualquier cuenta; reproducir, pausar y saltar requiere Spotify Premium y un dispositivo activo.", "Connexion réelle via OAuth de Spotify : un ID client gratuit du tableau de bord développeur Spotify est nécessaire, sans secret client. La lecture en cours et la recherche fonctionnent avec tous les comptes ; lecture/pause/suivant nécessitent Spotify Premium et un appareil actif.", "Echte Anmeldung über den Spotify-OAuth-Ablauf. Eine kostenlose Client-ID aus dem Spotify-Entwicklerdashboard ist erforderlich, kein Client-Secret. Wiedergabeanzeige und Suche funktionieren mit jedem Konto; Wiedergabe/Pause/Überspringen erfordern Spotify Premium und ein aktives Gerät.");
            Add("Personal Access Token from account.smartthings.com/tokens", "Token de acceso personal de account.smartthings.com/tokens", "Jeton d'accès personnel depuis account.smartthings.com/tokens", "Persönliches Zugriffstoken von account.smartthings.com/tokens");
            Add("Base URL and a long-lived access token from your HA profile page", "URL base y token de acceso de larga duración de tu perfil de Home Assistant", "URL de base et jeton d'accès longue durée depuis votre profil Home Assistant", "Basis-URL und langlebiges Zugriffstoken aus deinem Home-Assistant-Profil");
            Add("Maker API base URL (for one Hubitat app) and its access token", "URL base de Maker API (para una aplicación de Hubitat) y su token de acceso", "URL de base de Maker API (pour une application Hubitat) et son jeton d'accès", "Maker-API-Basis-URL (für eine Hubitat-App) und zugehöriges Zugriffstoken");
            Add("Save & Test", "Guardar y probar", "Enregistrer et tester", "Speichern und testen");
            Add("Changes the color of each companion's orb, both in the sidebar and the header - takes effect immediately, no restart needed.", "Cambia el color del orbe de cada compañero en la barra lateral y el encabezado. Se aplica de inmediato, sin reiniciar.", "Modifie la couleur de l'orbe de chaque compagnon dans la barre latérale et l'en-tête. Le changement est immédiat, sans redémarrage.", "Ändert die Orb-Farbe jedes Begleiters in Seitenleiste und Kopfzeile – sofort und ohne Neustart.");
            Add("Sift can only read files inside one folder you choose here - never anything else on your PC, and it can't browse into subfolders or escape this one. Works for plain-text-style files (txt, md, csv, json, code files); PDFs, Word docs, spreadsheets, and images aren't readable yet.", "Sift solo puede leer archivos dentro de la carpeta que elijas aquí: nada más del PC, sin subcarpetas ni acceso fuera de ella. Admite archivos de texto (txt, md, csv, json y código); todavía no puede leer PDF, Word, hojas de cálculo ni imágenes.", "Sift ne peut lire que les fichiers du dossier choisi ici, sans accéder au reste du PC ni aux sous-dossiers. Formats pris en charge : texte (txt, md, csv, json, code) ; PDF, Word, tableurs et images ne sont pas encore lisibles.", "Sift kann nur Dateien im hier ausgewählten Ordner lesen – nichts anderes auf dem PC und keine Unterordner. Unterstützt werden Textdateien (txt, md, csv, json, Quellcode); PDFs, Word-Dokumente, Tabellen und Bilder noch nicht.");
            Add("This list is fetched live from your provider's own API every time you open this page - not a fixed list baked into Aurora. New models your provider adds show up here as soon as they're live on your account; nothing to update in the app.", "Diese Liste wird bei jedem Öffnen live über die API deines Anbieters abgerufen und ist nicht fest in Aurora hinterlegt. Neue Modelle erscheinen, sobald sie für dein Konto verfügbar sind; ein App-Update ist nicht nötig.", "Cette liste est récupérée en direct depuis l'API de votre fournisseur à chaque ouverture ; elle n'est pas intégrée à Aurora. Les nouveaux modèles apparaissent dès qu'ils sont disponibles sur votre compte, sans mise à jour de l'application.", "Diese Liste wird beim Öffnen live über die API deines Anbieters geladen und ist nicht fest in Aurora eingebaut. Neue Modelle erscheinen, sobald sie für dein Konto verfügbar sind; ein App-Update ist nicht nötig.");
            Add("Recognized speech below this confidence is treated as noise and never sent to your companion - raise it in a noisy room, lower it in a quiet one. Every recognized phrase (accepted or not) stays available for debugging voice-recognition quality.", "El habla reconocida por debajo de este nivel de confianza se considera ruido y nunca se envía al compañero. Súbelo en un lugar ruidoso y bájalo en uno silencioso. Todas las frases reconocidas se conservan para depurar la calidad del reconocimiento.", "La parole reconnue sous ce seuil de confiance est considérée comme du bruit et n'est jamais envoyée au compagnon. Augmentez-le dans un lieu bruyant, diminuez-le dans un lieu calme. Toutes les phrases reconnues restent disponibles pour diagnostiquer la reconnaissance vocale.", "Spracherkennung unterhalb dieser Konfidenz wird als Rauschen behandelt und nie an den Begleiter gesendet. In lauter Umgebung erhöhen, in ruhiger senken. Alle erkannten Sätze bleiben zur Fehleranalyse verfügbar.");
            Add("How fast replies are spoken, in words per minute - a natural conversational pace is around 150-170. Applies to whichever voice provider is selected below without changing what's said, only how quickly.", "Velocidad de lectura en palabras por minuto (un ritmo natural suele ser de 150 a 170). Se aplica al proveedor de voz seleccionado y solo cambia la velocidad, no el contenido.", "Vitesse de lecture en mots par minute (un rythme naturel est d'environ 150 à 170). S'applique au fournisseur vocal choisi et ne change que le débit, pas le contenu.", "Sprechgeschwindigkeit in Wörtern pro Minute (natürlich sind etwa 150–170). Gilt für den ausgewählten Sprachanbieter und ändert nur das Tempo, nicht den Inhalt.");
            Add("Leave blank to reuse your OpenAI chat API key from the AI engine section, if you've already set one. TTS is billed per character (a small fraction of a cent per reply) - a card on your OpenAI account is required, there's no free tier for this specific API.", "Déjalo vacío para reutilizar la clave de API de chat de OpenAI si ya la configuraste. La conversión de texto a voz se cobra por carácter y requiere una tarjeta asociada a tu cuenta de OpenAI; esta API no tiene nivel gratuito.", "Laissez ce champ vide pour réutiliser votre clé API de chat OpenAI si elle est déjà configurée. La synthèse vocale est facturée par caractère et nécessite une carte associée à votre compte OpenAI ; cette API n'a pas d'offre gratuite.", "Leer lassen, um einen bereits eingerichteten OpenAI-Chat-API-Schlüssel wiederzuverwenden. Text-to-Speech wird pro Zeichen berechnet und erfordert eine Karte im OpenAI-Konto; für diese API gibt es keinen kostenlosen Tarif.");
            Add("How loud Aurora's spoken replies are, independent of your PC's system volume. Click Save to apply.", "Volumen de las respuestas habladas de Aurora, independiente del volumen del sistema. Pulsa Guardar para aplicar.", "Volume des réponses vocales d'Aurora, indépendant du volume système du PC. Cliquez sur Enregistrer pour appliquer.", "Lautstärke von Auroras gesprochenen Antworten, unabhängig von der Systemlautstärke. Zum Übernehmen auf Speichern klicken.");
            Add("If the picked provider has no key saved yet, or a request to it fails, replies automatically fall back to the offline Windows voice instead of staying silent.", "Si no hay una clave guardada para el proveedor elegido o falla una solicitud, las respuestas usarán automáticamente la voz de Windows sin conexión.", "Si aucune clé n'est enregistrée pour le fournisseur choisi ou qu'une requête échoue, les réponses basculent automatiquement vers la voix Windows hors ligne.", "Falls für den Anbieter kein Schlüssel gespeichert ist oder eine Anfrage fehlschlägt, werden Antworten automatisch mit der Offline-Windows-Stimme gesprochen.");
            Add("OpenAI doesn't publish a voice-listing API, so unlike the Models page this list is a starting point, not a live fetch - type any other voice name OpenAI adds and it'll work the same way.", "OpenAI no ofrece una API para listar voces, así que esta lista es orientativa, no se carga en directo. Puedes escribir cualquier nombre de voz que añada OpenAI.", "OpenAI ne propose pas d'API de liste des voix : cette liste est indicative et non récupérée en direct. Vous pouvez saisir le nom de toute nouvelle voix ajoutée par OpenAI.", "OpenAI bietet keine API zum Auflisten von Stimmen. Diese Liste ist daher eine Auswahl und wird nicht live geladen. Weitere von OpenAI hinzugefügte Stimmen können eingegeben werden.");
            Add("Free tier available (around 10,000 characters/month, no card required to start) - found under your account's Profile + API Key settings once signed in.", "Hay un nivel gratuito (unos 10 000 caracteres al mes, sin tarjeta para empezar) en los ajustes Perfil y clave de API de tu cuenta.", "Une offre gratuite est disponible (environ 10 000 caractères/mois, sans carte bancaire pour commencer) dans les paramètres Profil et clé API de votre compte.", "Ein kostenloser Tarif (etwa 10.000 Zeichen/Monat, keine Karte zum Start erforderlich) ist nach der Anmeldung unter Profil und API-Schlüssel verfügbar.");
            Add("Includes any custom or cloned voices already in your ElevenLabs account, not just their stock ones. Save the key above first if \"Load voices\" comes back empty.", "Incluye las voces personalizadas o clonadas de tu cuenta de ElevenLabs, no solo las predeterminadas. Si «Cargar voces» no devuelve nada, guarda primero la clave.", "Inclut les voix personnalisées ou clonées de votre compte ElevenLabs, pas seulement celles d'origine. Si « Charger les voix » ne renvoie rien, enregistrez d'abord la clé.", "Enthält benutzerdefinierte oder geklonte Stimmen deines ElevenLabs-Kontos. Wenn „Stimmen laden“ leer bleibt, speichere zuerst den Schlüssel.");
            Add("Needs an Azure account (free tier includes 0.5M characters/month). \"Create resource\" opens the Azure Portal's Speech service page - after creating one, its key and region are under Keys and Endpoint in the resource's menu.", "Requiere una cuenta de Azure (el nivel gratuito incluye 500 000 caracteres al mes). «Crear recurso» abre el servicio Speech de Azure Portal; después encontrarás la clave y la región en Claves y punto de conexión.", "Un compte Azure est requis (l'offre gratuite inclut 500 000 caractères/mois). « Créer une ressource » ouvre le service Speech du portail Azure ; la clé et la région se trouvent ensuite sous Clés et point de terminaison.", "Ein Azure-Konto ist erforderlich (kostenlos: 500.000 Zeichen/Monat). „Ressource erstellen“ öffnet den Speech-Dienst im Azure-Portal. Schlüssel und Region findest du anschließend unter Schlüssel und Endpunkt.");
            Add("The short region code from your Azure resource, e.g. eastus or westeurope - it's part of the API endpoint, so it has to match exactly.", "Código corto de región de tu recurso de Azure, por ejemplo eastus o westeurope. Debe coincidir exactamente con el punto de conexión de la API.", "Code court de la région de votre ressource Azure (par ex. eastus ou westeurope). Il fait partie du point de terminaison API et doit correspondre exactement.", "Kurzer Regionscode deiner Azure-Ressource, z. B. eastus oder westeurope. Er ist Teil des API-Endpunkts und muss exakt stimmen.");
            Add("Azure's catalog runs to hundreds of neural voices across languages - loading it live means new ones Microsoft adds show up here automatically.", "El catálogo de Azure incluye cientos de voces neuronales en distintos idiomas. Al cargarlo en directo aparecerán automáticamente las nuevas voces de Microsoft.", "Le catalogue Azure compte des centaines de voix neuronales dans de nombreuses langues. Le chargement en direct affiche automatiquement celles ajoutées par Microsoft.", "Der Azure-Katalog umfasst Hunderte neuronale Stimmen in vielen Sprachen. Durch Live-Laden erscheinen neue Microsoft-Stimmen automatisch.");
            Add("Runs fully offline through Windows' built-in voices - pick whichever's installed on this PC. Smaller selection than the cloud providers above, but free and needs no API key.", "Funciona sin conexión con las voces integradas de Windows. Elige una instalada en este PC. Hay menos opciones que en los servicios en la nube, pero es gratis y no necesita clave.", "Fonctionne entièrement hors ligne avec les voix intégrées de Windows. Choisissez-en une installée sur ce PC. Le choix est plus limité, mais c'est gratuit et sans clé API.", "Funktioniert vollständig offline mit den integrierten Windows-Stimmen. Wähle eine auf diesem PC installierte Stimme. Weniger Auswahl als bei Cloud-Anbietern, dafür kostenlos und ohne API-Schlüssel.");
            Add("No voices showing up? Windows Settings → Time & Language → Speech lets you add more.", "No aparecen voces? Puedes añadir más en Configuración de Windows → Hora e idioma → Voz.", "Aucune voix affichée ? Vous pouvez en ajouter dans Paramètres Windows → Heure et langue → Voix.", "Keine Stimmen angezeigt? Weitere kannst du unter Windows-Einstellungen → Zeit und Sprache → Sprache hinzufügen.");
            Add("Nothing to set up here - Aurora and Scout can already check real-time weather (free, no key) and search the web (a quick free instant-answer lookup, falling back to opening full results in your browser) right now.", "No hay nada que configurar: Aurora y Scout ya pueden consultar el clima en tiempo real y buscar en la web sin clave. Las búsquedas muestran respuestas rápidas y, si hace falta, abren los resultados completos en el navegador.", "Aucune configuration requise : Aurora et Scout peuvent déjà consulter gratuitement la météo en temps réel et rechercher sur le Web. Une réponse rapide est affichée, avec ouverture des résultats complets dans le navigateur si nécessaire.", "Hier ist keine Einrichtung nötig: Aurora und Scout können kostenlos das aktuelle Wetter abrufen und im Web suchen. Die Suche liefert schnelle Antworten und öffnet bei Bedarf vollständige Ergebnisse im Browser.");
            Add("Checks the latest GitHub release, downloads the matching Windows package, and prepares a safe restart once the update is staged.", "Busca la versión más reciente de GitHub, descarga el paquete de Windows correspondiente y prepara un reinicio seguro cuando la actualización esté lista.", "Vérifie la dernière version GitHub, télécharge le package Windows correspondant et prépare un redémarrage sûr une fois la mise à jour prête.", "Sucht nach dem neuesten GitHub-Release, lädt das passende Windows-Paket herunter und bereitet einen sicheren Neustart vor.");
            Add("Controls your PC's master output volume directly - the same one the taskbar slider controls. Companions can also adjust this when you ask them to.", "Controla directamente el volumen de salida principal del PC, igual que el control de la barra de tareas. También puedes pedir a los compañeros que lo ajusten.", "Contrôle directement le volume de sortie principal du PC, comme le curseur de la barre des tâches. Vous pouvez aussi demander aux compagnons de le régler.", "Steuert direkt die Hauptausgabelautstärke des PCs wie der Schieberegler in der Taskleiste. Begleiter können sie auf Anfrage ebenfalls ändern.");
            Add("This one can't be a real one-click connection: Alexa only talks to apps through a published Smart Home Skill, which means its own Amazon Developer account, a hosted Lambda function, and Amazon's account-linking flow - not something a token or key can shortcut. This toggle just tracks that you've set that up elsewhere; it doesn't verify anything.", "No se puede conectar Alexa con un solo clic: requiere una Skill de hogar inteligente publicada, una cuenta de Amazon Developer, una función Lambda alojada y el proceso de vinculación de Amazon. Este interruptor solo indica que lo configuraste por separado; no verifica la conexión.", "Alexa ne permet pas de connexion en un clic : il faut publier une compétence Smart Home, disposer d'un compte Amazon Developer, héberger une fonction Lambda et utiliser le processus de liaison Amazon. Ce bouton indique seulement que la configuration a été faite ailleurs ; il ne la vérifie pas.", "Alexa lässt sich nicht mit einem Klick verbinden: Erforderlich sind ein veröffentlichter Smart-Home-Skill, ein Amazon-Developer-Konto, eine gehostete Lambda-Funktion und Amazons Kontoverknüpfung. Dieser Schalter merkt nur vor, dass du es extern eingerichtet hast; er überprüft nichts.");
            Add("Turn this on to show each device's raw hub metadata alongside the normalized catalog fields.", "Actívalo para mostrar los metadatos originales del concentrador junto a los campos normalizados.", "Activez cette option pour afficher les métadonnées brutes du concentrateur avec les champs normalisés.", "Aktiviere diese Option, um rohe Hub-Metadaten neben den normalisierten Katalogfeldern anzuzeigen.");
            Add("Powers Sift's access to your inbox and documents. This does a real Google sign-in - you'll need your own free OAuth client from Google Cloud Console (Aurora can't ship one for you, since every installed app registers its own).", "Permite que Sift acceda a tu bandeja de entrada y documentos. Se inicia sesión realmente con Google; necesitas tu propio cliente OAuth gratuito de Google Cloud Console.", "Autorise Sift à accéder à votre boîte de réception et à vos documents. La connexion Google est réelle ; vous devez créer votre propre client OAuth gratuit dans Google Cloud Console.", "Ermöglicht Sift den Zugriff auf Posteingang und Dokumente. Die Google-Anmeldung ist echt; du benötigst einen eigenen kostenlosen OAuth-Client aus der Google Cloud Console.");
            Add("Create your own developer password. Aurora stores only a secure verifier; the same password unlocks developer mode and authorizes a complete Aurora reset.", "Crea una contraseña de desarrollador. Aurora solo almacena un verificador seguro; la contraseña desbloquea el modo de desarrollador y autoriza el restablecimiento completo.", "Créez un mot de passe développeur. Aurora ne conserve qu'un vérificateur sécurisé ; ce mot de passe déverrouille le mode développeur et autorise la réinitialisation complète.", "Erstelle ein Entwicklerpasswort. Aurora speichert nur einen sicheren Prüfwert; dasselbe Passwort entsperrt den Entwicklermodus und autorisiert das vollständige Zurücksetzen.");
            Add("🎙  Voice input", "🎙  Entrada de voz", "🎙  Entrée vocale", "🎙  Spracheingabe");
            Add("♫  Music", "♫  Música", "♫  Musique", "♫  Musik");
            Add("⌂  Smart Home", "⌂  Hogar inteligente", "⌂  Maison connectée", "⌂  Smart Home");
            Add("☑  Add reminder", "☑  Añadir recordatorio", "☑  Ajouter un rappel", "☑  Erinnerung hinzufügen");
            Add("⚙  Settings", "⚙  Configuración", "⚙  Paramètres", "⚙  Einstellungen");
            Add("📷  Take photo", "📷  Tomar foto", "📷  Prendre une photo", "📷  Foto aufnehmen");
            Add("🎥  Record video", "🎥  Grabar video", "🎥  Enregistrer une vidéo", "🎥  Video aufnehmen");
            Add("How to get a Client ID / Secret", "Cómo obtener un ID y secreto de cliente", "Comment obtenir un ID client et un secret", "Client-ID und Secret abrufen");
            Add("OpenAI (default - biggest natural-sounding selection)", "OpenAI (predeterminado: mayor selección de voces naturales)", "OpenAI (par défaut - plus grand choix de voix naturelles)", "OpenAI (Standard – größte Auswahl natürlich klingender Stimmen)");
            Add("ElevenLabs", "ElevenLabs", "ElevenLabs", "ElevenLabs");
            Add("Azure Speech", "Azure Speech", "Azure Speech", "Azure Speech");
            Add("Windows built-in (offline, free, no key)", "Integrada de Windows (sin conexión, gratis, sin clave)", "Windows intégrée (hors ligne, gratuite, sans clé)", "Windows integriert (offline, kostenlos, ohne Schlüssel)");
            Add("Emotion cues", "Indicadores de emoción", "Indices émotionnels", "Emotionshinweise");
            Add("Load voices", "Cargar voces", "Charger les voix", "Stimmen laden");
            Add("Create resource", "Crear recurso", "Créer une ressource", "Ressource erstellen");
            Add("Speech key", "Clave de Speech", "Clé Speech", "Speech-Schlüssel");
            Add("Region", "Región", "Région", "Region");
            Add("To clear everything, use \"Reset Aurora completely\" in the AI engine section.", "Para borrar todo, usa «Restablecer Aurora por completo» en la sección Motor de IA.", "Pour tout effacer, utilisez « Réinitialiser complètement Aurora » dans la section Moteur d'IA.", "Zum Löschen aller Daten „Aurora vollständig zurücksetzen“ im Bereich KI-Engine verwenden.");
            Add("Custom...", "Personalizado...", "Personnalisé...", "Benutzerdefiniert...");
            Add("Cyan", "Cian", "Cyan", "Cyan");
            Add("Sky blue", "Azul cielo", "Bleu ciel", "Himmelblau");
            Add("Indigo", "Índigo", "Indigo", "Indigo");
            Add("Violet", "Violeta", "Violet", "Violett");
            Add("Pink", "Rosa", "Rose", "Rosa");
            Add("Coral", "Coral", "Corail", "Koralle");
            Add("Amber", "Ámbar", "Ambre", "Bernstein");
            Add("Gold", "Dorado", "Or", "Gold");
            Add("Green", "Verde", "Vert", "Grün");
            Add("Teal", "Verde azulado", "Bleu sarcelle", "Blaugrün");
            Add("★ Favorite", "★ Favorito", "★ Favori", "★ Favorit");

            return result;
        }

        public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> AllStrings => Strings;

        private static readonly Dictionary<string, IReadOnlyDictionary<string, string>> Strings = new(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = new Dictionary<string, string>
            {
                ["Settings.Title"] = "Settings",
                ["Settings.Language.Title"] = "Language",
                ["Settings.Language.Description"] = "Choose the language Aurora uses for its interface. This does not change the language the AI model replies in.",
                ["Settings.Language.Label"] = "Interface language",
                ["Settings.Language.RestartNote"] = "Language saved. Interface text updates immediately.",
                ["Settings.Privacy.Title"] = "Privacy and local-only mode",
                ["Settings.Privacy.Description"] = "Keep Aurora on your device by disabling cloud AI, external integrations, and internet-backed tools while leaving local capabilities available.",
                ["Settings.Privacy.Toggle"] = "Enable local-only mode",
                ["Settings.Privacy.StatusOff"] = "Cloud AI and external integrations are available.",
                ["Settings.Privacy.StatusOn"] = "Enabled: Aurora will only use local capabilities and block cloud AI and external integrations.",
                ["Settings.Approval.Title"] = "High-impact action approval",
                ["Settings.Approval.Description"] = "Control when Aurora asks for confirmation before risky side effects such as terminal commands, file writes, clipboard changes, and MCP tool calls.",
                ["Settings.Approval.Policy"] = "Approval policy",
                ["Settings.Approval.AskForRisky"] = "Ask for risky actions",
                ["Settings.Approval.AlwaysAsk"] = "Always ask before any side effect",
                ["Settings.Approval.AllowTrusted"] = "Allow trusted tools; ask for everything else",
                ["Settings.Approval.TrustedTools"] = "Trusted tools (comma-separated)",
                ["Settings.Approval.Save"] = "Save approval policy",
                ["Settings.Audit.Title"] = "Action audit log",
                ["Settings.Audit.Description"] = "Review recent side-effecting actions, approval outcomes, and tool calls with search and redaction applied to sensitive values.",
                ["Settings.Audit.Clear"] = "Clear log",
                ["Settings.Audit.Empty"] = "No recent actions recorded yet.",
                ["Settings.Audit.Status"] = "Status: ",
                ["Settings.Audit.Approved"] = "Approved",
                ["Settings.Audit.Denied"] = "Denied",
                ["Settings.Music.Title"] = "Music",
                ["Settings.Music.Description"] = "Search and play music directly in Aurora using the Jamendo catalog.",
                ["Settings.Music.ClientId"] = "Jamendo Client ID",
                ["Settings.Music.GetClientId"] = "Get Client ID",
                ["Settings.Music.SaveConnect"] = "Save & Connect",
                ["Settings.Music.Connected"] = "Connected",
                ["Settings.Music.NotConnected"] = "Not connected",
                ["Settings.Music.Notice"] = "Jamendo is intended for supported non-commercial use. Track licensing and attribution requirements apply.",
                ["Setup.KeyRequired"] = "{0} API key (required)",
                ["Setup.GetKeyTooltip"] = "Opens {0}'s key page in your browser so you can sign in and create a key",
                ["Setup.Examples"] = "Examples: {0}",
                ["Setup.CheckingKey"] = "Checking your {0} key...",
                ["Setup.ModelsLoaded"] = "{0} models loaded live from {1}.",
                ["Setup.OpenBrowserError"] = "Couldn't open the browser automatically ({0}). Go to {1} manually.",
                ["Setup.ProfileNameRequired"] = "Enter a profile name before continuing.",
                ["Setup.ProfilePasswordRequired"] = "Create or generate a profile password before continuing.",
                ["Setup.ApiKeyRequired"] = "A {0} API key is required to bring your companions online. Use \"Cancel\" if you'd rather add it later.",
                ["Setup.RecoveryTitle"] = "Aurora Recovery Code",
                ["Setup.RecoveryLead"] = "Save this recovery code somewhere safe.",
                ["Setup.RecoveryBody"] = "It can be used to recover this profile. Aurora will store only a verifier.",
                ["Setup.RecoveryError"] = "Couldn't generate a recovery code: {0}"
            },
            ["es"] = new Dictionary<string, string>
            {
                ["Settings.Title"] = "Configuración",
                ["Settings.Language.Title"] = "Idioma",
                ["Settings.Language.Description"] = "Elige el idioma de la interfaz de Aurora. Esto no cambia el idioma en el que responde el modelo de IA.",
                ["Settings.Language.Label"] = "Idioma de la interfaz",
                ["Settings.Language.RestartNote"] = "Idioma guardado. El texto de la interfaz se actualiza inmediatamente.",
                ["Settings.Privacy.Title"] = "Privacidad y modo solo local",
                ["Settings.Privacy.Description"] = "Mantén Aurora en tu dispositivo desactivando la IA en la nube, las integraciones externas y las herramientas con Internet, sin afectar las funciones locales.",
                ["Settings.Privacy.Toggle"] = "Activar modo solo local",
                ["Settings.Privacy.StatusOff"] = "La IA en la nube y las integraciones externas están disponibles.",
                ["Settings.Privacy.StatusOn"] = "Activado: Aurora solo usará funciones locales y bloqueará la IA en la nube y las integraciones externas.",
                ["Settings.Approval.Title"] = "Aprobación de acciones de alto impacto",
                ["Settings.Approval.Policy"] = "Política de aprobación",
                ["Settings.Approval.AskForRisky"] = "Preguntar en acciones de riesgo",
                ["Settings.Approval.AlwaysAsk"] = "Preguntar siempre antes de cualquier efecto secundario",
                ["Settings.Approval.AllowTrusted"] = "Permitir herramientas de confianza; preguntar por lo demás",
                ["Settings.Approval.TrustedTools"] = "Herramientas de confianza (separadas por comas)",
                ["Settings.Approval.Save"] = "Guardar política de aprobación",
                ["Settings.Audit.Title"] = "Registro de auditoría de acciones",
                ["Settings.Audit.Clear"] = "Borrar registro",
                ["Settings.Audit.Empty"] = "Aún no hay acciones registradas.",
                ["Settings.Audit.Status"] = "Estado: ",
                ["Settings.Audit.Approved"] = "Aprobada",
                ["Settings.Audit.Denied"] = "Denegada",
                ["Settings.Music.Title"] = "Música",
                ["Settings.Music.ClientId"] = "ID de cliente de Jamendo",
                ["Settings.Music.GetClientId"] = "Obtener ID de cliente",
                ["Settings.Music.SaveConnect"] = "Guardar y conectar",
                ["Settings.Music.Connected"] = "Conectado",
                ["Settings.Music.NotConnected"] = "No conectado",
                ["Setup.KeyRequired"] = "Clave de API de {0} (obligatoria)",
                ["Setup.GetKeyTooltip"] = "Abre la página de claves de {0} en el navegador para iniciar sesión y crear una clave",
                ["Setup.Examples"] = "Ejemplos: {0}",
                ["Setup.CheckingKey"] = "Comprobando tu clave de {0}...",
                ["Setup.ModelsLoaded"] = "{0} modelos cargados en directo desde {1}.",
                ["Setup.OpenBrowserError"] = "No se pudo abrir el navegador automáticamente ({0}). Ve manualmente a {1}.",
                ["Setup.ProfileNameRequired"] = "Introduce un nombre de perfil antes de continuar.",
                ["Setup.ProfilePasswordRequired"] = "Crea o genera una contraseña de perfil antes de continuar.",
                ["Setup.ApiKeyRequired"] = "Se necesita una clave de API de {0} para conectar tus compañeros. Usa «Cancelar» si prefieres añadirla más tarde.",
                ["Setup.RecoveryTitle"] = "Código de recuperación de Aurora",
                ["Setup.RecoveryLead"] = "Guarda este código de recuperación en un lugar seguro.",
                ["Setup.RecoveryBody"] = "Se puede usar para recuperar este perfil. Aurora solo guardará un verificador.",
                ["Setup.RecoveryError"] = "No se pudo generar un código de recuperación: {0}"
            },
            ["fr"] = new Dictionary<string, string>
            {
                ["Settings.Title"] = "Paramètres",
                ["Settings.Language.Title"] = "Langue",
                ["Settings.Language.Description"] = "Choisissez la langue de l'interface d'Aurora. Cela ne change pas la langue des réponses du modèle d'IA.",
                ["Settings.Language.Label"] = "Langue de l'interface",
                ["Settings.Language.RestartNote"] = "Langue enregistrée. Le texte de l'interface est mis à jour immédiatement.",
                ["Settings.Privacy.Title"] = "Confidentialité et mode local uniquement",
                ["Settings.Privacy.Toggle"] = "Activer le mode local uniquement",
                ["Settings.Privacy.StatusOff"] = "L'IA dans le cloud et les intégrations externes sont disponibles.",
                ["Settings.Privacy.StatusOn"] = "Activé : Aurora n'utilisera que les fonctions locales et bloquera l'IA dans le cloud et les intégrations externes.",
                ["Settings.Approval.Title"] = "Approbation des actions à fort impact",
                ["Settings.Approval.Policy"] = "Politique d'approbation",
                ["Settings.Approval.AskForRisky"] = "Demander pour les actions risquées",
                ["Settings.Approval.AlwaysAsk"] = "Toujours demander avant tout effet de bord",
                ["Settings.Approval.AllowTrusted"] = "Autoriser les outils de confiance ; demander pour le reste",
                ["Settings.Approval.TrustedTools"] = "Outils de confiance (séparés par des virgules)",
                ["Settings.Approval.Save"] = "Enregistrer la politique d'approbation",
                ["Settings.Audit.Title"] = "Journal d'audit des actions",
                ["Settings.Audit.Clear"] = "Effacer le journal",
                ["Settings.Audit.Empty"] = "Aucune action enregistrée pour le moment.",
                ["Settings.Audit.Status"] = "Statut : ",
                ["Settings.Audit.Approved"] = "Approuvée",
                ["Settings.Audit.Denied"] = "Refusée",
                ["Settings.Music.Title"] = "Musique",
                ["Settings.Music.ClientId"] = "ID client Jamendo",
                ["Settings.Music.GetClientId"] = "Obtenir l'ID client",
                ["Settings.Music.SaveConnect"] = "Enregistrer et connecter",
                ["Settings.Music.Connected"] = "Connecté",
                ["Settings.Music.NotConnected"] = "Non connecté",
                ["Setup.KeyRequired"] = "Clé API {0} (obligatoire)",
                ["Setup.GetKeyTooltip"] = "Ouvre la page de clé de {0} dans votre navigateur pour vous connecter et créer une clé",
                ["Setup.Examples"] = "Exemples : {0}",
                ["Setup.CheckingKey"] = "Vérification de votre clé {0}...",
                ["Setup.ModelsLoaded"] = "{0} modèles chargés en direct depuis {1}.",
                ["Setup.OpenBrowserError"] = "Impossible d'ouvrir automatiquement le navigateur ({0}). Accédez manuellement à {1}.",
                ["Setup.ProfileNameRequired"] = "Saisissez un nom de profil avant de continuer.",
                ["Setup.ProfilePasswordRequired"] = "Créez ou générez un mot de passe de profil avant de continuer.",
                ["Setup.ApiKeyRequired"] = "Une clé API {0} est nécessaire pour connecter vos compagnons. Cliquez sur « Annuler » pour l'ajouter plus tard.",
                ["Setup.RecoveryTitle"] = "Code de récupération Aurora",
                ["Setup.RecoveryLead"] = "Conservez ce code de récupération dans un endroit sûr.",
                ["Setup.RecoveryBody"] = "Il peut servir à récupérer ce profil. Aurora ne stockera qu'un vérificateur.",
                ["Setup.RecoveryError"] = "Impossible de générer un code de récupération : {0}"
            },
            ["de"] = new Dictionary<string, string>
            {
                ["Settings.Title"] = "Einstellungen",
                ["Settings.Language.Title"] = "Sprache",
                ["Settings.Language.Description"] = "Wähle die Sprache der Aurora-Oberfläche. Die Antwortsprache des KI-Modells ändert sich dadurch nicht.",
                ["Settings.Language.Label"] = "Oberflächensprache",
                ["Settings.Language.RestartNote"] = "Sprache gespeichert. Die Oberflächentexte werden sofort aktualisiert.",
                ["Settings.Privacy.Title"] = "Datenschutz und reiner Lokalmodus",
                ["Settings.Privacy.Toggle"] = "Reinen Lokalmodus aktivieren",
                ["Settings.Privacy.StatusOff"] = "Cloud-KI und externe Integrationen sind verfügbar.",
                ["Settings.Privacy.StatusOn"] = "Aktiviert: Aurora nutzt nur lokale Funktionen und blockiert Cloud-KI und externe Integrationen.",
                ["Settings.Approval.Title"] = "Genehmigung folgenreicher Aktionen",
                ["Settings.Approval.Policy"] = "Genehmigungsrichtlinie",
                ["Settings.Approval.AskForRisky"] = "Bei riskanten Aktionen fragen",
                ["Settings.Approval.AlwaysAsk"] = "Vor jedem Seiteneffekt immer fragen",
                ["Settings.Approval.AllowTrusted"] = "Vertrauenswürdige Tools erlauben; sonst fragen",
                ["Settings.Approval.TrustedTools"] = "Vertrauenswürdige Tools (durch Kommas getrennt)",
                ["Settings.Approval.Save"] = "Genehmigungsrichtlinie speichern",
                ["Settings.Audit.Title"] = "Aktionsprotokoll",
                ["Settings.Audit.Clear"] = "Protokoll löschen",
                ["Settings.Audit.Empty"] = "Noch keine Aktionen aufgezeichnet.",
                ["Settings.Audit.Status"] = "Status: ",
                ["Settings.Audit.Approved"] = "Genehmigt",
                ["Settings.Audit.Denied"] = "Abgelehnt",
                ["Settings.Music.Title"] = "Musik",
                ["Settings.Music.ClientId"] = "Jamendo-Client-ID",
                ["Settings.Music.GetClientId"] = "Client-ID abrufen",
                ["Settings.Music.SaveConnect"] = "Speichern und verbinden",
                ["Settings.Music.Connected"] = "Verbunden",
                ["Settings.Music.NotConnected"] = "Nicht verbunden",
                ["Setup.KeyRequired"] = "{0}-API-Schlüssel (erforderlich)",
                ["Setup.GetKeyTooltip"] = "Öffnet die Schlüsselseite von {0} im Browser, damit du dich anmelden und einen Schlüssel erstellen kannst",
                ["Setup.Examples"] = "Beispiele: {0}",
                ["Setup.CheckingKey"] = "Schlüssel von {0} wird überprüft...",
                ["Setup.ModelsLoaded"] = "{0} Modelle live von {1} geladen.",
                ["Setup.OpenBrowserError"] = "Browser konnte nicht automatisch geöffnet werden ({0}). Öffne {1} manuell.",
                ["Setup.ProfileNameRequired"] = "Gib vor dem Fortfahren einen Profilnamen ein.",
                ["Setup.ProfilePasswordRequired"] = "Erstelle oder generiere vor dem Fortfahren ein Profilpasswort.",
                ["Setup.ApiKeyRequired"] = "Ein {0}-API-Schlüssel ist erforderlich, um deine Begleiter zu verbinden. Wähle „Abbrechen“, wenn du ihn später hinzufügen möchtest.",
                ["Setup.RecoveryTitle"] = "Aurora-Wiederherstellungscode",
                ["Setup.RecoveryLead"] = "Bewahre diesen Wiederherstellungscode an einem sicheren Ort auf.",
                ["Setup.RecoveryBody"] = "Mit ihm kann dieses Profil wiederhergestellt werden. Aurora speichert nur einen Prüfwert.",
                ["Setup.RecoveryError"] = "Wiederherstellungscode konnte nicht generiert werden: {0}"
            }
        };
    }
}

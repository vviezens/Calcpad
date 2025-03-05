using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;




namespace Calcpad.Wpf
{
    public partial class TemplateManager : Window
    {

        private readonly string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Calcpad");
        private readonly string publicKeyFileName = "public_key.pem";
        private readonly string PublicKeyPath = "";

        private const string PrivateKeyPath = "private_key.pem";
        public event Action<string> CodeLoaded;
        private ObservableCollection<ServerPath> serverPaths = new ObservableCollection<ServerPath>();
        //private Dictionary<string, TreeViewItem> serverNodes = new Dictionary<string, TreeViewItem>();
        private Dictionary<string, Dictionary<string, TreeViewItem>> serverNodes = new();
        private Dictionary<string, string> templateContents = new();

        private ObservableCollection<TreeViewItem> TemplateTreeItems { get; set; } = new ObservableCollection<TreeViewItem>();


        public TemplateManager()
        {
            InitializeComponent();
            LoadServerPaths();
            ServerListView.ItemsSource = serverPaths;
            TemplateTree.ItemsSource = TemplateTreeItems; // Setzt ItemsSource für TreeView
            RefreshAllServers(); // Lädt Server-Status + Templates direkt beim Start
            PublicKeyPath = Path.Combine(folderPath, publicKeyFileName);
            LoadPublicKeyIfExists();


        }

private List<ServerPath> LoadServersFromConfig()
{
    List<ServerPath> serverPaths = new List<ServerPath>();
    string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TemplateServerConfig.csv");

    if (!File.Exists(filePath))
    {
        Console.WriteLine("⚠️ Server config file not found - skip loading.");
        return serverPaths; // return empty list
    }

    try
    {
        foreach (var line in File.ReadAllLines(filePath).Skip(1)) // skip first row (Header)
        {
            var parts = line.Split(',');

                serverPaths.Add(new ServerPath
                {
                    ServerName = parts[0].Trim(),
                    ServerUrl = parts[1].Trim(),
                    Comment = parts[2].Trim()
                });
            
        }
        Console.WriteLine($"✅ {serverPaths.Count} server loaded sucessfully from config file.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"⚠️ Error loading the server config file: {ex.Message}");
    }

    return serverPaths;
}



        private void LoadServerPaths()
        {
            serverPaths.Clear();
            //serverPaths.Add(new ServerPath { ServerName = "server1", ServerUrl = "https://localhost:7085", Comment = "Lokaler Blazor Server" });
            //serverPaths.Add(new ServerPath { ServerName = "server2", ServerUrl = "https://localhost:5001", Comment = "Backup-Server" });
            //serverPaths.Add(new ServerPath { ServerName = "server3", ServerUrl = "https://tmplsvr3.io", Comment = "Testserver" });

            // load server from config file
            foreach (var server in LoadServersFromConfig())
            {
                serverPaths.Add(server);
            }


            // show server in ui (e.g. ListView oder TreeView)
            ServerListView.ItemsSource = serverPaths;

            Console.WriteLine($"Overall count of loaded servers: {serverPaths.Count}");

        }

        /// <summary>
        /// updates server status and reloads templates
        /// </summary>
        private async void RefreshTemplates_Click(object sender, RoutedEventArgs e)
        {
            Console.WriteLine("update all servers...");
            await RefreshAllServers();
        }

        /// <summary>
        /// updates all server status values and reloads all templates
        /// </summary>
        private async Task RefreshAllServers()
        {
            foreach (var server in serverPaths)
            {
                await server.CheckServerStatus();
                if (server.ServerStatus == "ON")
                {
                    await LoadTemplatesFromServer(server);
                }
            }
        }

        /*
        private async Task LoadTemplatesFromServer(ServerPath server)
        {
            try
            {
                if (server.ServerStatus != "ON")
                {
                    Console.WriteLine($"Skip server {server.ServerUrl}, because it is offline.");
                    return;
                }

                Console.WriteLine($"Load Templates from {server.ServerUrl}...");
                var client = new TemplateClient();
                List<Template> templates = await client.GetTemplatesAsync($"{server.ServerUrl}/api/templates");

                if (templates.Count == 0) return;

                var existingNode = TemplateTreeItems.FirstOrDefault(node => node.Header.ToString() == server.ServerUrl);
                if (existingNode != null)
                {
                    TemplateTreeItems.Remove(existingNode);
                }

                var serverNode = new TreeViewItem { Header = server.ServerUrl, IsExpanded = true };
                TemplateTreeItems.Add(serverNode);
                serverNodes[server.ServerUrl] = new Dictionary<string, TreeViewItem> { { server.ServerUrl, serverNode } };

                foreach (var template in templates)
                {
                    //AddTemplateToTree(server.ServerUrl, template.Path, template.Title, template.Content);
                    //AddTemplateToTree(server.ServerUrl, template.Path, template.Title, template.Content, template);
                    //AddTemplateToTree(server.ServerUrl, template.Path, template.Title, template.Content, template);
                    AddTemplateToTree(server.ServerUrl, template.Path, template);

                }

                // expand the tree view
                ExpandAllNodes(serverNode);

                Console.WriteLine($"templates updated for {server.ServerUrl}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"error while loading template form {server.ServerUrl}: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        */
        
        private async Task LoadTemplatesFromServer(ServerPath server)
        {
            try
            {
                if (server.ServerStatus != "ON")
                {
                    Console.WriteLine($"Skip server {server.ServerUrl}, because it is offline.");
                    return;
                }

                Console.WriteLine($"Load Templates from {server.ServerUrl}...");

                var client = new TemplateClient();
                List<Template> templates = new List<Template>();

                // 1️⃣ Zuerst versuchen, YAML zu laden
                //var yamlTemplates = await client.GetTemplatesAsync($"{server.ServerUrl}/api/templates?format=yaml", "yaml");
                var yamlTemplates = await client.GetTemplatesAsync($"{server.ServerUrl}/api/templates?format=yaml");
                Console.WriteLine($"📂 YAML Templates geladen: {yamlTemplates.Count}");
                if (yamlTemplates.Any())
                {
                    templates = yamlTemplates;
                    Console.WriteLine($"✅ Loaded {templates.Count} YAML templates from {server.ServerUrl}");
                }
                else
                {
                    // 2️⃣ Falls YAML nicht geht, JSON als Fallback laden
                    //var jsonTemplates = await client.GetTemplatesAsync($"{server.ServerUrl}/api/templates?format=json", "json");
                    var jsonTemplates = await client.GetTemplatesAsync($"{server.ServerUrl}/api/templates?format=json");
                    if (jsonTemplates.Any())
                    {
                        templates = jsonTemplates;
                        Console.WriteLine($"🔄 Using JSON fallback: Loaded {templates.Count} templates from {server.ServerUrl}");
                    }
                    else
                    {
                        Console.WriteLine("⚠️ No templates found in either YAML or JSON format.");
                        return;
                    }
                }

                // 🔽 Bestehenden Serverknoten im TreeView ersetzen
                var existingNode = TemplateTreeItems.FirstOrDefault(node => node.Header.ToString() == server.ServerUrl);
                if (existingNode != null)
                {
                    TemplateTreeItems.Remove(existingNode);
                }

                var serverNode = new TreeViewItem { Header = server.ServerUrl, IsExpanded = true };
                TemplateTreeItems.Add(serverNode);
                serverNodes[server.ServerUrl] = new Dictionary<string, TreeViewItem> { { server.ServerUrl, serverNode } };

                foreach (var template in templates)
                {
                    Console.WriteLine($"🌳 Füge hinzu: {template.Path}");
                    AddTemplateToTree(server.ServerUrl, template.Path, template);
                }

                ExpandAllNodes(serverNode);
                Console.WriteLine($"✅ Templates updated for {server.ServerUrl}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error while loading templates from {server.ServerUrl}: {ex.Message}");
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            Console.WriteLine($"🌲 TreeView aktualisiert: {TemplateTreeItems.Count} Knoten");

        }




        private void ExpandAllNodes(TreeViewItem item)
        {
            item.IsExpanded = true;
            foreach (var subItem in item.Items.OfType<TreeViewItem>())
            {
                ExpandAllNodes(subItem);
            }
        }

        /*
        private void AddTemplateToTree(string serverUrl, List<string> pathList, Template template)
        {
            if (!serverNodes.ContainsKey(serverUrl))
            {
                var serverNode = new TreeViewItem { Header = serverUrl, Tag = null };
                TemplateTree.Items.Add(serverNode);
                serverNodes[serverUrl] = new Dictionary<string, TreeViewItem> { { serverUrl, serverNode } };
            }

            var currentNode = serverNodes[serverUrl][serverUrl];
            string currentPath = serverUrl;

            foreach (var pathPart in pathList)
            {
                currentPath += $"/{pathPart}";
                if (!serverNodes[serverUrl].ContainsKey(currentPath))
                {
                    var newNode = new TreeViewItem { Header = pathPart, Tag = null };
                    currentNode.Items.Add(newNode);
                    serverNodes[serverUrl][currentPath] = newNode;
                }
                currentNode = serverNodes[serverUrl][currentPath];
            }

            // set whole template as tag!
            var titleNode = new TreeViewItem { Header = template.Title, Tag = template };
            currentNode.Items.Add(titleNode);
        }
        */

        private void AddTemplateToTree(string serverUrl, List<string> pathList, Template template)
        {
            if (!serverNodes.ContainsKey(serverUrl))
            {
                var serverNode = new TreeViewItem { Header = serverUrl, Tag = null };
                TemplateTree.Items.Add(serverNode);
                serverNodes[serverUrl] = new Dictionary<string, TreeViewItem> { { serverUrl, serverNode } };
            }

            var currentNode = serverNodes[serverUrl][serverUrl];
            string currentPath = serverUrl;

            foreach (var pathPart in pathList)
            {
                currentPath += $"/{pathPart}";
                if (!serverNodes[serverUrl].ContainsKey(currentPath))
                {
                    var newNode = new TreeViewItem { Header = pathPart, Tag = null };
                    currentNode.Items.Add(newNode);
                    serverNodes[serverUrl][currentPath] = newNode;
                }
                currentNode = serverNodes[serverUrl][currentPath];
            }

            // set whole template as tag!
            var titleNode = new TreeViewItem { Header = template.Title, Tag = template };
            currentNode.Items.Add(titleNode);
        }


        private void DisplayMetadata(Template template)
        {
            if (template == null) return;

            MetadataList.Items.Clear();

            // Debugging: proof if template has been received correctly
            Debug.WriteLine($"show metadata for: {template.Title}");

            // Dictionary with all metadata
            var properties = new Dictionary<string, string>
    {
        //{ "Path", string.Join(" > ", template.Path) }, // Array zu String umwandeln
        { "Title", template.Title },
        //{ "Content", template.Content },
        { "Likes", template.Likes.ToString() },
        { "Validations", template.Validations.ToString() },
        { "Creator", template.Creator },
        //{ "Authors", string.Join(", ", template.Authors) }, // Liste zu String
        { "CreatedDate", template.CreatedDate },
        { "Description", template.Description },
        { "Version", template.Version },
        { "Url", template.Url },
        //{ "ValidatorHashes", string.Join(", ", template.ValidatorHashes) },
        //{ "ValidatorLogos", string.Join(", ", template.ValidatorLogos) },
        //{ "ValidatorContact", template.ValidatorContact }
    };

            foreach (var property in properties)
            {
                MetadataList.Items.Add(new { Key = property.Key, Value = property.Value });
            }
        }

        private void TemplateTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            Debug.WriteLine("TreeView selection has changed!");

            if (e.NewValue is TreeViewItem selectedItem)
            {
                Debug.WriteLine($"Selected Item: {selectedItem.Header}");

                if (selectedItem.Tag is Template template)
                {
                    Debug.WriteLine($"Template selected: {template.Title}");

                    // Metadaten anzeigen
                    DisplayMetadata(template);

                    // Vorschau laden (falls relevant)
                    TemplateWebViewer.NavigateToString(template.Content);
                    //TemplateWebViewer.NavigateToString(Encoding.UTF8.GetString(Convert.FromBase64String(template.Content)));

                }
                else
                {
                    Debug.WriteLine("No template-object in tag!");
                }
            }
            else
            {
                Debug.WriteLine("Selection ist not a TreeView item!");
            }
        }

        



        private void AddServer_Click(object sender, RoutedEventArgs e)
        {
            int newServerNumber = serverPaths.Count + 1;
            serverPaths.Add(new ServerPath { ServerName = $"server{newServerNumber}", ServerUrl = "https://newserver.io", Comment = "Neuer Server" });
        }

        private void RemoveServer_Click(object sender, RoutedEventArgs e)
        {
            if (ServerListView.SelectedItem is ServerPath selectedServer)
            {
                serverPaths.Remove(selectedServer);
            }
            else
            {
                MessageBox.Show("Please select the server you want to delete!");
            }
        }


        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (TemplateTree.SelectedItem is TreeViewItem selectedNode && selectedNode.Tag is Template template)
            {
                Console.WriteLine($"Template loaded: {template.Title}");

                //string code = Encoding.UTF8.GetString(Convert.FromBase64String(template.Content)); // Code aus "Content"-Feld des Templates holen
                string code = template.Content;
                CodeLoaded?.Invoke(code); // Event feuern und Code übermitteln

                this.Close(); // Fenster schließen, falls gewünscht
            }
            else
            {
                MessageBox.Show("Please select valid template that contains code", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }


        public class ServerPath : INotifyPropertyChanged
        {
            public string ServerName { get; set; }
            public string ServerUrl { get; set; }
            public string Comment { get; set; }

            private string _serverStatus = "OFF";
            private Brush _statusColor = Brushes.Gray;

            public string ServerStatus
            {
                get { return _serverStatus; }
                set
                {
                    _serverStatus = value;
                    OnPropertyChanged(nameof(ServerStatus));
                }
            }

            public Brush StatusColor
            {
                get { return _statusColor; }
                set
                {
                    _statusColor = value;
                    OnPropertyChanged(nameof(StatusColor));
                }
            }

            public async Task CheckServerStatus()
            {
                try
                {
                    using (HttpClient client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(2);
                        var response = await client.GetAsync(ServerUrl);
                        if (response.IsSuccessStatusCode)
                        {
                            ServerStatus = "ON";
                            StatusColor = Brushes.Green;
                        }
                        else
                        {
                            ServerStatus = "OFF";
                            StatusColor = Brushes.Gray;
                        }
                    }
                }
                catch
                {
                    ServerStatus = "OFF";
                    StatusColor = Brushes.Gray;
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
            protected void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }


        }

        private void UploadLogo_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Hier Code für den Datei-Upload des Logos hinzufügen
            MessageBox.Show("Logo-Upload noch nicht implementiert.");
        }

        private void UploadTemplate_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Hier Code für das Hochladen des Templates auf den Server hinzufügen
            MessageBox.Show("Template-Upload noch nicht implementiert.");
        }

        private void LoadPublicKey()
        {
            if (File.Exists(PublicKeyPath))
            {
                string publicKey = File.ReadAllText(PublicKeyPath);
                ValidatorHashTextBox.Text = publicKey;
            }
            else
            {
                ValidatorHashTextBox.Text = "Please generate public key in Tab 'My Identity'";
            }

            // Immer grau und nicht editierbar
            ValidatorHashTextBox.Foreground = Brushes.Black;
            ValidatorHashTextBox.Background = Brushes.LightGray;
        }

        private void GenerateKeyPair_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (RSA rsa = RSA.Create(2048))
                {
                    string privateKey = Convert.ToBase64String(rsa.ExportRSAPrivateKey());
                    string publicKey = Convert.ToBase64String(rsa.ExportRSAPublicKey());

                    // Speichere den öffentlichen Schlüssel
                    SavePublicKey(publicKey);

                    // Speichere den privaten Schlüssel
                    File.WriteAllText(PrivateKeyPath, privateKey);

                    // Speichere den öffentlichen Schlüssel
                    File.WriteAllText(PublicKeyPath, publicKey);

                    // Update die Textfelder mit dem öffentlichen Schlüssel
                    PublicKeyTextBox.Text = publicKey;
                    ValidatorHashTextBox.Text = publicKey;

                    // Update die Styles der Textfelder
                    ValidatorHashTextBox.Foreground = Brushes.Black;
                    ValidatorHashTextBox.Background = Brushes.LightGray;

                    // Deaktiviere den Button "Generate Key pair"
                    GenerateKeyPairButton.IsEnabled = false;

                    // Rufe die Methode auf, um den privaten Schlüssel zu speichern und die Nachricht zu zeigen
                    //SavePrivateKeyAndShowMessage(privateKey);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fehler beim Generieren des Schlüssels: " + ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private void DisableUI()
        {
            // Deaktiviere alle Tabs
            TabControl.IsEnabled = false;

            // Deaktiviere den Schließen-Button
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;

            // Deaktiviere den Button "Save private Key"
            SavePrivateKeyButton.IsEnabled = false;
        }

        private void EnableUI()
        {
            // Aktiviert alle Tabs und Buttons
            TabControl.IsEnabled = true;

            // Erlaube das Schließen des Fensters
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;

            // Deaktiviere den Button "Save private Key" nach dem Speichern
            SavePrivateKeyButton.IsEnabled = false;
        }


        private void SavePrivateKey_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!File.Exists(PrivateKeyPath))
                {
                    MessageBox.Show("Es wurde noch kein Private Key generiert.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                string privateKey = File.ReadAllText(PrivateKeyPath);

                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    FileName = "private_key.pem",
                    Filter = "PEM files (*.pem)|*.pem|All files (*.*)|*.*"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    // Speichern des privaten Schlüssels in der gewählten Datei
                    File.WriteAllText(saveFileDialog.FileName, privateKey);

                    // Setze die UI zurück
                    EnableUI();

                    // Erfolgsnachricht anzeigen
                    MessageBox.Show("Private Key wurde erfolgreich gespeichert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Fehler beim Speichern des Private Keys: " + ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Überprüfe, ob der private Schlüssel gespeichert wurde
            if (SavePrivateKeyButton.IsEnabled == false)
            {
                // Wenn der Button deaktiviert ist, verhindere das Schließen des Fensters
                e.Cancel = true;
                MessageBox.Show("Bitte speichern Sie den Private Key, bevor Sie das Fenster schließen.", "Warnung", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Überprüfe, ob der private Schlüssel existiert
            if (File.Exists(PrivateKeyPath))
            {
                EnableUI();
            }
            else
            {
                DisableUI();
            }
        }



        public void SavePublicKey(string publicKeyContent)
        {
            // Der Ordnerpfad, der das Verzeichnis enthält, aber nicht die Datei (public_key.pem)
            string folderPath = Path.GetDirectoryName(PublicKeyPath);

            try
            {
                // Zeige den Ordnerpfad in einer MessageBox an
                MessageBox.Show("Überprüfe Ordnerpfad: " + folderPath, "Ordnerpfad", MessageBoxButton.OK, MessageBoxImage.Information);

                // Wenn der Ordner noch nicht existiert, erstelle ihn
                if (!Directory.Exists(folderPath))
                {
                    MessageBox.Show("Ordner existiert nicht, er wird jetzt erstellt...", "Ordner erstellen", MessageBoxButton.OK, MessageBoxImage.Information);
                    Directory.CreateDirectory(folderPath); // Erstellt den Ordner und alle notwendigen übergeordneten Ordner
                    MessageBox.Show("Ordner wurde erfolgreich erstellt: " + folderPath, "Ordner erstellt", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Ordner existiert bereits: " + folderPath, "Ordner vorhanden", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Schreibe den öffentlichen Schlüssel in die Datei
                File.WriteAllText(PublicKeyPath, publicKeyContent);
                MessageBox.Show("Die Datei wurde gespeichert: " + PublicKeyPath, "Datei gespeichert", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                // Fange alle Fehler ab und zeige sie in einer MessageBox an
                MessageBox.Show("Fehler beim Erstellen des Ordners oder beim Schreiben der Datei: " + ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadPublicKeyIfExists()
        {
            // Prüfen, ob die Datei existiert
            if (File.Exists(PublicKeyPath))
            {
                try
                {
                    // Lade den Inhalt der Datei
                    string publicKey = File.ReadAllText(PublicKeyPath);

                    // Fülle das Textfeld "ValidatorHash" im Tab "Template Upload"
                    ValidatorHashTextBox.Text = publicKey;

                    // Fülle das Textfeld "Public Key" im Tab "My Identity"
                    PublicKeyTextBox.Text = publicKey;

                    // Deaktiviere die Buttons im Tab "My Identity"
                    GenerateKeyPairButton.IsEnabled = false;
                    SavePrivateKeyButton.IsEnabled = false;

                    MessageBox.Show("Public Key wurde erfolgreich geladen.", "Public Key geladen", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Fehler beim Laden des Public Keys: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                // Falls die Datei nicht existiert
                ValidatorHashTextBox.Text = "Kein Public Key gefunden. Generieren Sie einen neuen Key.";
                PublicKeyTextBox.Text = string.Empty;
                GenerateKeyPairButton.IsEnabled = true;
                SavePrivateKeyButton.IsEnabled = true;
            }
        }





    }
}

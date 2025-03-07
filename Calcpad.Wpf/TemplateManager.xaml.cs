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
using Calcpad.Shared;



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
            string directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Calcpad");
            string filePath = Path.Combine(directoryPath, "TemplateServerConfig.csv");

            // Falls das Verzeichnis nicht existiert, erstelle es
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            // Falls die Datei nicht existiert, erstelle sie mit Standardwerten
            if (!File.Exists(filePath))
            {
                Console.WriteLine("⚠️ Server config file not found - creating default config.");
                try
                {
                    string[] defaultConfig = {
                "Server,Url,Comment,User,Password",
                "server1,https://localhost:7085,comment1,admin,1234",
                "server2,https://localhost:5001,comment2,admin,1234"
            };
                    File.WriteAllLines(filePath, defaultConfig);
                    Console.WriteLine("✅ Default server config file created.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Error creating the server config file: {ex.Message}");
                    return serverPaths; // Rückgabe der leeren Liste, falls die Datei nicht erstellt werden kann
                }
            }

            // Jetzt existiert die Datei sicher -> Server-Daten einlesen
            try
            {
                foreach (var line in File.ReadAllLines(filePath).Skip(1)) // Erste Zeile überspringen (Header)
                {
                    var parts = line.Split(',');

                    if (parts.Length >= 5) // Sicherstellen, dass genug Spalten vorhanden sind
                    {
                        serverPaths.Add(new ServerPath
                        {
                            ServerName = parts[0].Trim(),
                            ServerUrl = parts[1].Trim(),
                            Comment = parts[2].Trim(),
                            User = parts[3].Trim(),
                            Password = parts[4].Trim()
                        });
                    }
                }
                Console.WriteLine($"✅ {serverPaths.Count} server loaded successfully from config file.");
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
            var titleNode = new TreeViewItem { Header = template.Title+"Hier", Tag = template };
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

            // Likes und Validierungen holen
            int likes = template.Likes_total;
            int validations = template.Validation_total;

            // Rang bestimmen
            string rank = "🥉 Bronze";
            SolidColorBrush rankColor = Brushes.Gray;

            if (likes >= 11 && validations >= 6)
            {
                rank = "🥈 Silber";
                rankColor = Brushes.Blue;
            }
            if (likes >= 51 && validations >= 21)
            {
                rank = "🥇 Gold";
                rankColor = Brushes.Orange;
            }
            if (likes >= 101 && validations >= 51)
            {
                rank = "💎 Platin";
                rankColor = Brushes.Purple;
            }
            if (likes >= 501 && validations >= 101)
            {
                rank = "🔥 Legendär";
                rankColor = Brushes.Red;
            }

            // TreeViewItem mit Farben & Rang setzen
            var titleNode = new TreeViewItem
            {
                Tag = template
            };

            // StackPanel für farbigen Header
            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };

            // Rang-Label
            var rankLabel = new Label
            {
                Content = rank,
                Foreground = rankColor,
                FontWeight = FontWeights.Bold
            };

            // Template-Name
            var titleLabel = new Label
            {
                Content = $"{template.Title}  👍 {likes}  ✅ {validations}",
                Foreground = Brushes.Black
            };

            stackPanel.Children.Add(rankLabel);
            stackPanel.Children.Add(titleLabel);

            titleNode.Header = stackPanel;
            currentNode.Items.Add(titleNode);
        }


        private void DisplayMetadata(Template template)
        {
            if (template == null) return;

            MetadataList.Items.Clear();
            Debug.WriteLine($"show metadata for: {template.Template_name}");

            var properties = new Dictionary<string, string>
    {
        { "id", template.Id },
        { "Template_name", template.Template_name },
        { "Version", template.Version },
        { "Description", template.Description },
        { "Author_name", template.Author_name },
        { "Author_public_key", template.Author_public_key },
        { "Created_at", template.Created_at },
        { "Updated_at", template.Updated_at },
        { "Tags", Convert.ToString(template.Tags) },
        { "Deprecated", template.Deprecated },
        { "Deprecation_total_votes", Convert.ToString(template.Deprecation_total_votes) },
        { "Deprecation_total_votes_needed", Convert.ToString(template.Deprecation_votes_needed) },
        { "User_votes_user_ids", Convert.ToString(template.User_votes_user_ids) },
        { "User_votes_user_ids_weight", Convert.ToString(template.User_votes_user_ids_weight) },
        { "Likes_total", Convert.ToString(template.Likes_total) },
        { "Likes_users", Convert.ToString(template.Likes_users) },
        { "Validation_total", Convert.ToString(template.Validation_total) },
        { "Validation_user_ids", Convert.ToString(template.Validation_user_ids) },
        { "Validation_signatures", Convert.ToString(template.Validation_signatures) },
        { "Hash", template.Hash },
    };

            // Definierte Reihenfolge der priorisierten Einträge
            var prioritizedKeys = new List<string> { "Likes_total", "Validation_total", "Deprecation_total_votes" };

            var sortedProperties = properties
                .OrderBy(p => prioritizedKeys.Contains(p.Key) ? prioritizedKeys.IndexOf(p.Key) : int.MaxValue) // Feste Reihenfolge für priorisierte Keys
                .ThenBy(p => p.Key) // Danach alphabetisch sortieren
                .ToList();

            foreach (var property in sortedProperties)
            {
                object valueDisplay = property.Value;
                object actionDisplay = null; // Standardmäßig keine Aktion

                if (prioritizedKeys.Contains(property.Key))
                {
                    var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };

                    if (property.Key == "Likes_total")
                    {
                        var likeButton = new Button
                        {
                            Content = "👍",
                            Width = 30,
                            Margin = new Thickness(5, 0, 5, 0),
                            Visibility = Visibility.Visible,
                            ToolTip = "Like the template! \n (templates with many likes will be listed on top \n ultra high rated templates can reach the hall of fame \n likes will be validated with your public key)"
                        };
                        likeButton.Click += LikeTemplate_Click;
                        stackPanel.Children.Add(likeButton);
                    }

                    if (property.Key == "Validation_total")
                    {
                        var validateButton = new Button
                        {
                            Content = "✅",
                            Width = 30,
                            Margin = new Thickness(5, 0, 5, 0),
                            Visibility = Visibility.Visible,
                            ToolTip = "Validate correctness of template! \n (the correctness will be signed with your private key - optionally with your logo)"
                        };
                        validateButton.Click += ValidateTemplate_Click;
                        stackPanel.Children.Add(validateButton);
                    }

                    if (property.Key == "Deprecation_total_votes")
                    {
                        var deprecatedButton = new Button
                        {
                            Content = "⚠️",
                            Width = 30,
                            Margin = new Thickness(5, 0, 5, 0),
                            Visibility = Visibility.Visible,
                            ToolTip = "Mark as deprecated! \n (if template reaches 5 deprecated marks \n it will not be listed anymore)"
                        };
                        deprecatedButton.Click += MarkAsDeprecated_Click;
                        stackPanel.Children.Add(deprecatedButton);
                    }

                    actionDisplay = stackPanel;
                }

                MetadataList.Items.Add(new { Key = property.Key, Value = valueDisplay, Action = actionDisplay });
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

        /*
        private void SaveServersToConfig()
        {
            string directoryPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Calcpad");
            string filePath = Path.Combine(directoryPath, "TemplateServerConfig.csv");

            try
            {
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                using (StreamWriter writer = new StreamWriter(filePath, false))
                {
                    writer.WriteLine("Server,Url,Comment"); // Header schreiben

                    foreach (var server in serverPaths)
                    {
                        writer.WriteLine($"{server.ServerName},{server.ServerUrl},{server.Comment},{server.User},{server.Password}");
                    }
                }

                Console.WriteLine("✅ Server config file updated successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error saving the server config file: {ex.Message}");
            }
        }
        */

        private void SaveServersToConfig()
        {
            string filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Calcpad\TemplateServerConfig.csv");

            try
            {
                using (StreamWriter writer = new StreamWriter(filePath, false))
                {
                    writer.WriteLine("Server,Url,Comment,Password"); // Header mit Passwort-Spalte

                    foreach (var server in serverPaths)
                    {
                        writer.WriteLine($"{server.ServerName},{server.ServerUrl},{server.Comment},{server.User},{server.Password}");
                    }
                }

                Console.WriteLine("✅ Server config file updated successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Error saving the server config file: {ex.Message}");
            }
        }



        private void AddServer_Click(object sender, RoutedEventArgs e)
        {
            int newServerNumber = serverPaths.Count + 1;
            var newServer = new ServerPath
            {
                ServerName = $"server{newServerNumber}",
                ServerUrl = "https://yoururl.io",
                Comment = "Neuer Server",
                User = "admin",
                Password = "1234"
            };

            serverPaths.Add(newServer);
        }

        private void RemoveServer_Click(object sender, RoutedEventArgs e)
        {
            if (ServerListView.SelectedItem is ServerPath selectedServer)
            {
                serverPaths.Remove(selectedServer);
                SaveServersToConfig(); // Speichert die aktualisierte Liste nach dem Entfernen in die CSV
            }
            else
            {
                MessageBox.Show("Please select the server you want to delete!");
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                if (passwordBox.DataContext is ServerPath serverPath)
                {
                    serverPath.Password = passwordBox.Password;
                }
            }
        }

        private void SaveServersToCsv_Click(object sender, RoutedEventArgs e)
        {
            SaveServersToConfig(); // Speichert alle Server inklusive Passwörter in die CSV
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

            public string User { get; set; }

            public string Password { get; set; }

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
            MessageBox.Show("Logo-Upload not implemented right now");
        }

        private void UploadTemplate_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Hier Code für das Hochladen des Templates auf den Server hinzufügen
            MessageBox.Show("Template-Upload not implemented right now.");
        }

        private async void LikeTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (TemplateTree.SelectedItem is TreeViewItem selectedNode && selectedNode.Tag is Template template)
            {
                string userPublicKey = File.ReadAllText(PublicKeyPath);
                if (string.IsNullOrEmpty(userPublicKey))
                {
                    MessageBox.Show("Public key not found. Please generate your key first!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Prüfen, ob der Benutzer bereits geliked hat
                if (template.Likes_users.Contains(userPublicKey))
                {
                    MessageBox.Show("You have already liked this template!", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // Like hinzufügen
                template.Likes_total++;
                template.Likes_users.Add(userPublicKey);

                // Server-Update mit TemplateServerUpdate
                var serverUpdater = new TemplateServerUpdate("https://your-template-server.com"); // URL anpassen
                bool success = await serverUpdater.LikeTemplate(template.Id, userPublicKey);

                if (success)
                {
                    MessageBox.Show("Template liked successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    UpdateLikeButtonUI(selectedNode, userPublicKey);
                }
                else
                {
                    MessageBox.Show("Failed to update like on server.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("Please select a valid template!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void UpdateLikeButtonUI(TreeViewItem selectedNode, string userPublicKey)
        {
            if (selectedNode.Header is StackPanel stackPanel)
            {
                foreach (var child in stackPanel.Children)
                {
                    if (child is Button likeButton && likeButton.Content.ToString() == "👍")
                    {
                        likeButton.Background = Brushes.LightGreen;
                        likeButton.ToolTip = "You already liked this template.";
                        break;
                    }
                }
            }
        }




        private void ValidateTemplate_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Hier Code für das Hochladen des Templates auf den Server hinzufügen
            MessageBox.Show("Validate_Click not implemented right now.");
        }

        private void MarkAsDeprecated_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Hier Code für das Hochladen des Templates auf den Server hinzufügen
            MessageBox.Show("MarkAsDeprecated_Click not implemented right now.");
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
                MessageBox.Show("Error while generating the key: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    MessageBox.Show("A private Key has not been generated", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    MessageBox.Show("Private key has been stored sucessfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while storing privae key: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        /*
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Überprüfe, ob der private Schlüssel gespeichert wurde
            if (SavePrivateKeyButton.IsEnabled == false)
            {
                // Wenn der Button deaktiviert ist, verhindere das Schließen des Fensters
                e.Cancel = true;
                MessageBox.Show("Please store private key before closing this window!", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        */
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
                MessageBox.Show("Check folderpath: " + folderPath, "Folderpath", MessageBoxButton.OK, MessageBoxImage.Information);

                // Wenn der Ordner noch nicht existiert, erstelle ihn
                if (!Directory.Exists(folderPath))
                {
                    MessageBox.Show("folder does not exist, so it will be created...", "create folder", MessageBoxButton.OK, MessageBoxImage.Information);
                    Directory.CreateDirectory(folderPath); // Erstellt den Ordner und alle notwendigen übergeordneten Ordner
                    MessageBox.Show("folder was creaded successfully: " + folderPath, "folder created", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("folder already exists: " + folderPath, "folder exists", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                // Schreibe den öffentlichen Schlüssel in die Datei
                File.WriteAllText(PublicKeyPath, publicKeyContent);
                MessageBox.Show("the file was stored: " + PublicKeyPath, "files stored", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                // Fange alle Fehler ab und zeige sie in einer MessageBox an
                MessageBox.Show("Error while creating folders or writing the file: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

                    //MessageBox.Show("public key has been loaded succsssfully.", "public key loaded", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"error while loading public key: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                // Falls die Datei nicht existiert
                ValidatorHashTextBox.Text = "public key not found pleas generate new one.";
                PublicKeyTextBox.Text = string.Empty;
                GenerateKeyPairButton.IsEnabled = true;
                SavePrivateKeyButton.IsEnabled = true;
            }
        }

        private void OpenExplorer_Click(object sender, RoutedEventArgs e)
        {
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Calcpad");

            try
            {
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
                System.Diagnostics.Process.Start("explorer.exe", folderPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error while opening public key folder: " + ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void EscapeTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (TemplateInput == null || string.IsNullOrWhiteSpace(TemplateInput.Text))
            {
                MessageBox.Show("Please enter a template to escape.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string escapedTemplate = EscapeForJson(TemplateInput.Text);
            EscapedTemplateOutput.Text = escapedTemplate;
        }

        private void CopyEscapedTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(EscapedTemplateOutput.Text))
            {
                MessageBox.Show("There is no escaped template to copy.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Clipboard.SetText(EscapedTemplateOutput.Text);
            MessageBox.Show("Escaped template copied to clipboard!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private string EscapeForJson(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            StringBuilder escaped = new StringBuilder();
            foreach (char c in input)
            {
                switch (c)
                {
                    case '\\': escaped.Append("\\\\"); break;
                    case '\"': escaped.Append("\\\""); break;
                    case '\b': escaped.Append("\\b"); break;
                    case '\f': escaped.Append("\\f"); break;
                    case '\n': escaped.Append("\\n"); break;
                    case '\r': escaped.Append("\\r"); break;
                    case '\t': escaped.Append("\\t"); break;
                    default:
                        if (char.IsControl(c))
                        {
                            escaped.AppendFormat("\\u{0:X4}", (int)c);
                        }
                        else
                        {
                            escaped.Append(c);
                        }
                        break;
                }
            }
            return escaped.ToString();
        }






    }
}

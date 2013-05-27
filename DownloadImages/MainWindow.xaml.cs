using System;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Net;
using System.ComponentModel;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace DownloadImages
{
    public partial class MainWindow : Window
    {
        private FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
        private Queue<string> downloadUrls = new Queue<string>();
        private string directoryFolder = "";
        private int fileNameIndex = 1;
        private int numberOfFiles = 0;
        private StringBuilder log = new StringBuilder();
        private string logFileFolder = "";
        private int numberOfFilesOk = 0;
        private int numberOfFilesFailed = 0;
        private string fileName = "";
        private Stopwatch stopWatch;
        private Timer timer;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void buttonChooseFolder_Click(object sender, RoutedEventArgs e)
        {
            SetFolderBrowserDialogAttributes();

            if( this.folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                textBoxChosenFolder.Text = this.folderBrowserDialog.SelectedPath;
                SetDirectoryFolder();
                SetLogFileFolder();
            }
        }

        private void SetDirectoryFolder()
        {
            directoryFolder = this.folderBrowserDialog.SelectedPath;
        }

        private void SetLogFileFolder()
        {
            logFileFolder = string.Format("{0}\\{1}", directoryFolder, "LOG.txt");
        }

        private void SetFolderBrowserDialogAttributes()
        {
            this.folderBrowserDialog.Description = "Selecione uma pasta para armazenar as imagens";
            this.folderBrowserDialog.RootFolder = Environment.SpecialFolder.MyComputer;
            this.folderBrowserDialog.ShowNewFolderButton = true;
        }

        private void LogHyperlink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(logFileFolder);
        }

        private void buttonStartDownload_Click(object sender, RoutedEventArgs e)
        {
            var hasErrors = ValidateDirectoryFolder();

            var urlsBlock = textBoxLinks.Text;

            hasErrors = hasErrors && ValidateUrlsBlock(urlsBlock);
            
            var urls = urlsBlock.Split(new char[]{';'}, StringSplitOptions.RemoveEmptyEntries);

            hasErrors = hasErrors && ValidateUrls(urls);

            DownloadFile(urls);
        }

        private bool ValidateUrls(string[] urls)
        {
            if (urls.Count() <= 0)
            {
                ShowErrorMessage("Você deve adicionar pelo menos uma URL para download!");
                return false;
            }
            return true;
        }

        private bool ValidateUrlsBlock(string urlsBlock)
        {
            if (string.IsNullOrEmpty(urlsBlock))
            {
                ShowErrorMessage("Você deve adicionar pelo menos uma URL para download!");
                return false;
            }
            return true;
        }

        private bool ValidateDirectoryFolder()
        {
            if (string.IsNullOrEmpty(directoryFolder))
            {
                ShowErrorMessage("Você deve selecionar o diretório para salvar as imagens antes de iniciar o download!");
                return false;
            }
            return true;
        }

        private void ShowErrorMessage(string message)
        {
            System.Windows.MessageBox.Show(message, "Atenção", MessageBoxButton.OK, MessageBoxImage.Exclamation);
        }

        private string GetFileName(int index, string extension)
        {
            if (index < 10)
            {
                return string.Format("000{0}{1}", index.ToString(), extension);
            }

            if (index < 100)
            {
                return "00" + index.ToString() + extension;
            }

            if (index < 1000)
            {
                return "0" + index.ToString() + extension;
            }

            return index.ToString() + extension;
        }

        private void DownloadFile(IEnumerable<string> urls)
        {
            StartDownloadTimeCounter();
            
            foreach (var url in urls)
            {
                downloadUrls.Enqueue(url);
            }

            numberOfFiles = urls.Count();

            buttonStartDownload.Content = "Downloading...";
            buttonStartDownload.IsEnabled = false;
            
            DownloadFileAsync();
        }

        private void StartDownloadTimeCounter()
        {
            timer = new Timer();
            stopWatch = new Stopwatch();
            timer.Interval = 1000;
            timer.Enabled = true;
            timer.Start();
            stopWatch.Start();
            timer.Tick += new EventHandler(timer_Tick);
        }

        private void timer_Tick(object sender, EventArgs e)
        {
            var ts = stopWatch.Elapsed;
            labelTimers.Content = string.Format("Tempo decorrido: {0:00}:{1:00}:{2:00}", ts.Hours, ts.Minutes, ts.Seconds);
        }

        private void DownloadFileAsync()
        {            
            if (downloadUrls.Any())
            {
                WebClient client = new WebClient();
                client.DownloadProgressChanged += client_DownloadProgressChanged;
                client.DownloadFileCompleted += client_DownloadFileCompleted;

                var url = downloadUrls.Dequeue();
                url = url.Replace(Environment.NewLine, string.Empty);

                var fileExtension = GetFileExtension(url);
                fileName = GetFileName(fileNameIndex, fileExtension);

                log.AppendLine(string.Format("URL: {0}", url));
                log.AppendLine(string.Format("Imagem: {0}", fileName));

                client.DownloadFileAsync(new Uri(url), string.Format("{0}\\{1}", directoryFolder, fileName));
                
                labelProgress.Content = string.Format("Imagem {0} de {1}...", fileNameIndex, numberOfFiles);
                
                fileNameIndex++;
                
                return;
            }

            buttonStartDownload.Content = "Download completo!";
            
            SaveLog(log);

            LogHyperlink_TextBlock.Visibility = System.Windows.Visibility.Visible;

            SetStatusLabelContents();

            stopWatch.Stop();
        }

        private void SetStatusLabelContents()
        {
            labelProgress.Content = string.Format("Finalizado. {0}", labelProgress.Content);
            labelFilesFailed.Content = string.Format("{0} imagens falharam.", numberOfFilesFailed);
            labelFilesOk.Content = string.Format("{0} imagens Ok.", numberOfFilesOk);
        }

        private static string GetFileExtension(string url)
        {
            var fileExtensionIndex = url.LastIndexOf(".");
            var fileExtension = url.Substring(fileExtensionIndex, url.Length - fileExtensionIndex);
            return fileExtension;
        }

        private void client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                log.AppendLine("Status: FALHA");
                numberOfFilesFailed++;
                DeleteFailedFile();
            }
            else if (e.Cancelled)
            {
                log.AppendLine("Status: CANCELADA");
                numberOfFilesFailed++;
            }
            else
            {
                log.AppendLine("Status: OK");
                numberOfFilesOk++;
            }

            log.AppendLine();

            DownloadFileAsync();
        }

        private void DeleteFailedFile()
        {
            FileInfo fileToDelete = new FileInfo(directoryFolder + "\\" + fileName);
            fileToDelete.Delete();
        }

        private void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            double bytesIn = double.Parse(e.BytesReceived.ToString());
            double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
            double percentage = bytesIn / totalBytes * 100;
            progressBarDownload.Value = int.Parse(Math.Truncate(percentage).ToString());
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void SaveLog(StringBuilder logMessages)
        {
            using (StreamWriter file = new StreamWriter(logFileFolder))
            {
                file.Write(logMessages.ToString());
            }
        }
    }
}

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
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
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
            this.folderBrowserDialog.Description = "Selecione uma pasta para armazenar as imagens";
            this.folderBrowserDialog.RootFolder = Environment.SpecialFolder.MyComputer;
            this.folderBrowserDialog.ShowNewFolderButton = true;

            if( this.folderBrowserDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                textBoxChosenFolder.Text = this.folderBrowserDialog.SelectedPath;
                directoryFolder = this.folderBrowserDialog.SelectedPath;
                logFileFolder = directoryFolder + "\\LOG.txt";
            }
        }

        private void LogHyperlink_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(logFileFolder);
        }

        private void buttonStartDownload_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(directoryFolder))
            {
                System.Windows.MessageBox.Show("Você deve selecionar o diretório para salvar as imagens antes de iniciar o download!", "Atenção", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }
            var urlsBlock = textBoxLinks.Text;

            if (string.IsNullOrEmpty(urlsBlock))
            {
                System.Windows.MessageBox.Show("Você deve adicionar pelo menos uma URL para download!", "Atenção", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            var urls = urlsBlock.Split(new char[]{';'}, StringSplitOptions.RemoveEmptyEntries);

            if (urls.Count() <= 0)
            {
                System.Windows.MessageBox.Show("Você deve adicionar pelo menos uma URL para download!", "Atenção", MessageBoxButton.OK, MessageBoxImage.Exclamation);
                return;
            }

            downloadFile(urls);
        }

        private string GetFileName(int index, string extension)
        {
            if (index < 1000)
            {
                if (index < 100)
                {
                    if (index < 10)
                    {
                        return "000" + index.ToString() + extension;
                    }
                    return "00" + index.ToString() + extension;
                }
                return "0" + index.ToString() + extension;
            }
            return index.ToString() + extension;
        }

        private void downloadFile(IEnumerable<string> urls)
        {
            timer = new Timer();
            stopWatch = new Stopwatch();
            timer.Interval = 1000;
            timer.Enabled = true;
            timer.Start();
            stopWatch.Start();
            timer.Tick += new EventHandler(timer_Tick);
            log.AppendLine("==================================");
            log.AppendLine(string.Format("Início: {0}.", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")));
            log.AppendLine("==================================");
            foreach (var url in urls)
            {
                downloadUrls.Enqueue(url);
            }

            numberOfFiles = urls.Count();

            buttonStartDownload.Content = "Downloading...";
            buttonStartDownload.IsEnabled = false;
            
            DownloadFile();
        }

        void timer_Tick(object sender, EventArgs e)
        {
            var ts = stopWatch.Elapsed;
            labelTimers.Content = string.Format("Tempo decorrido: {0:00}:{1:00}:{2:00}", ts.Hours, ts.Minutes, ts.Seconds);
        }

        private void DownloadFile()
        {            
            if (downloadUrls.Any())
            {
                WebClient client = new WebClient();
                client.DownloadProgressChanged += client_DownloadProgressChanged;
                client.DownloadFileCompleted += client_DownloadFileCompleted;

                var url = downloadUrls.Dequeue();
                var fileExtensionIndex = url.LastIndexOf(".");
                var fileExtension = url.Substring(fileExtensionIndex, url.Length - fileExtensionIndex);
                fileName = GetFileName(fileNameIndex, fileExtension);
                url = url.Replace(Environment.NewLine, string.Empty);

                log.AppendLine(string.Format("URL: {0}", url));
                log.AppendLine(string.Format("Imagem: {0}", fileName));

                client.DownloadFileAsync(new Uri(url), directoryFolder + "\\" + fileName);
                
                labelProgress.Content = string.Format("Imagem {0} de {1}...", fileNameIndex, numberOfFiles);
                
                fileNameIndex++;
                
                return;
            }
            buttonStartDownload.Content = "Download completo!";
            
            log.AppendLine("==================================");
            log.AppendLine(string.Format("Fim: {0}.", DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss")));
            log.AppendLine("==================================");
            SaveLog(log);

            LogHyperlink_TextBlock.Visibility = System.Windows.Visibility.Visible;
            
            labelProgress.Content = string.Format("Finalizado. {0}", labelProgress.Content);
            labelFilesFailed.Content = string.Format("{0} imagens falharam.",numberOfFilesFailed);
            labelFilesOk.Content = string.Format("{0} imagens Ok.", numberOfFilesOk);
            stopWatch.Stop();
        }

        private void client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                log.AppendLine("Status: FALHA");
                numberOfFilesFailed++;
                FileInfo fileToDelete = new FileInfo(directoryFolder + "\\" + fileName);
                fileToDelete.Delete();
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

            DownloadFile();
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

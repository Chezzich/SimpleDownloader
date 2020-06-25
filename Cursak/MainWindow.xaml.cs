using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml.Serialization;

namespace Cursak
{
    public partial class MainWindow : Window
    {
        FileDownload file;
        List<FileDownload> threads = new List<FileDownload>();
        public ObservableCollection<ItemElement> Items { get; set; }
        public ObservableCollection<ItemElement> History { get; set; }
        public ObservableCollection<ItemElement> Active { get; set; }
        static int id;
        public MainWindow()
        {
            InitializeComponent();
            try
            {
                threads = Deserialize().ToList<FileDownload>();
            }
            catch (ArgumentNullException ex)
            {
                threads = new List<FileDownload>();
            }
            try
            {
                Items = new ObservableCollection<ItemElement>(DeserializeItems());
            }
            catch (ArgumentNullException ex)
            {
                Items = new ObservableCollection<ItemElement>();
            }
            Active = new ObservableCollection<ItemElement>(Items.Where((e) => e.Status != Statuses.Completed));
            History = new ObservableCollection<ItemElement>(Items.Where((e) => e.Status == Statuses.Completed));
            list.ItemsSource = Active;
            id = list.Items.Count;
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            string path = tbRef.Text;
            string name = tbName.Text;
            if (path != String.Empty && name != String.Empty)
            {
                file = new FileDownload(path, name, 100);
                file._id = id;
                ProgressBar bar = new ProgressBar();
                bar.Maximum = file.ContentLength;
                bar.Value = file.BytesWritten;
                ItemElement it = new ItemElement { Id = id, Title = path, Bar = (int)bar.Value };
                if (CheckItems(it.Title))
                {
                    try
                    {
                        file.Start(it, History, Active);
                    }
                    catch (Exception ex)
                    {
                        tbEx.Text = ex.Message;
                        Delete(file, it);
                    }
                    Items.Add(it);
                    Active.Add(it);
                    id++;
                    threads.Add(file);
                    Serialize(threads.ToArray(), "threads.xml");
                    Serialize(Items, "items.xml");
                }
            }
        }

        private bool CheckItems(string path)
        {
            foreach (var item in Items)
                if (item.Title.Equals(path))
                    return false;
            return true;
        }

        private void Delete(FileDownload file, ItemElement it)
        {
            Items.Remove(it);
            threads.Remove(file);
            Active.Remove(it);
        }

        private void btnResume_Click(object sender, RoutedEventArgs e)
        {
            if (list.SelectedIndex != -1)
            {
                try
                {
                    threads[Items.IndexOf(list.SelectedItem as ItemElement)].Start(list.SelectedItem as ItemElement, History, Active);
                    (list.SelectedItem as ItemElement).Status = Statuses.Active;
                }
                catch (ArgumentOutOfRangeException)
                { }
            }
        }

        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            if (list.SelectedIndex != -1)
            {
                try
                {
                    threads[Items.IndexOf(list.SelectedItem as ItemElement)].Pause(list.SelectedItem as ItemElement);
                    (list.SelectedItem as ItemElement).Status = Statuses.Paused;
                }
                catch (ArgumentOutOfRangeException)
                { }
            }
        }

        public void Serialize(FileDownload[] threads, string path)
        {
            XmlSerializer formatter = new XmlSerializer(typeof(FileDownload[]));
            File.WriteAllText(path, "");
            using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate))
            {
                formatter.Serialize(fs, threads);
            }
        }

        public void Serialize(ObservableCollection<ItemElement> items, string path)
        {
            XmlSerializer formatter = new XmlSerializer(typeof(ObservableCollection<ItemElement>));
            File.WriteAllText(path, "");
            using (FileStream fs = new FileStream(path, FileMode.OpenOrCreate))
            {
                formatter.Serialize(fs, items);
            }
        }

        public FileDownload[] Deserialize()
        {
            XmlSerializer formatter = new XmlSerializer(typeof(FileDownload[]));
            if (File.Exists("threads.xml"))
            {
                using (FileStream fs = new FileStream("threads.xml", FileMode.Open))
                {
                    FileDownload[] threads = (FileDownload[])formatter.Deserialize(fs);
                    return threads;
                }
            }
            else
            {
                using (FileStream fs = new FileStream("threads.xml", FileMode.Create))
                {
                    formatter.Serialize(fs, new FileDownload[] { });
                    return null;
                }
            }
        }

        public ItemElement[] DeserializeItems()
        {
            XmlSerializer formatter = new XmlSerializer(typeof(ItemElement[]));
            if (File.Exists("items.xml"))
            {
                using (FileStream fs = new FileStream("items.xml", FileMode.Open))
                {
                    ItemElement[] items = (ItemElement[])formatter.Deserialize(fs);
                    return items;
                }
            }
            else
            {
                using (FileStream fs = new FileStream("items.xml", FileMode.Create))
                {
                    formatter.Serialize(fs, new ItemElement[] { });
                    return null;
                }
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Serialize(threads.ToArray(), "threads.xml");
            Serialize(Items, "items.xml");
        }

        private void btnHistory_Click(object sender, RoutedEventArgs e)
        {
            list.ItemsSource = History;
            Dispatcher.Invoke(() => { });
        }

        private void btnActive_Click(object sender, RoutedEventArgs e)
        {
            list.ItemsSource = Active;
            Dispatcher.Invoke(() => { });
        }

        private void btnClear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var item in History)
                {
                    threads.Remove(threads[History.IndexOf(item as ItemElement)]);
                }
            }
            catch (Exception ex)
            { }
            History.Clear();
            Items = Active;
        }
    }

    public enum Statuses
    {
        Paused = 0,
        Active = 1,
        Completed = 2
    }

    public class ItemElement
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public int Bar { get; set; }
        public Statuses Status { get; set; }
    }

    public class FileDownload
    {
        public bool _allowedToRun;
        public string _source;
        public string _destination;
        public int _chunkSize;
        public int _id;

        private int _contentLength;

        public int BytesWritten { get; set; }
        public int ContentLength { get { return _contentLength; } }

        public bool Done { get { return ContentLength == BytesWritten; } }

        public FileDownload()
        {
            _allowedToRun = true;
            BytesWritten = 0;
        }

        public FileDownload(string source, string destination, int chunkSize)
        {
            _allowedToRun = true;

            _source = source;
            _destination = destination;
            _chunkSize = chunkSize;
            _contentLength = Convert.ToInt32(GetContentLength());

            BytesWritten = 0;
        }

        public long GetContentLength()
        {
            var request = (HttpWebRequest)WebRequest.Create(_source);
            request.Method = "HEAD";

            using (var response = request.GetResponse())
                return response.ContentLength;
        }

        private async Task Start(ItemElement item, ObservableCollection<ItemElement> history, ObservableCollection<ItemElement> active, int range)
        {
            if (!_allowedToRun)
                throw new InvalidOperationException();

            var request = (HttpWebRequest)WebRequest.Create(_source);
            request.Method = "GET";
            request.UserAgent = "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR 1.0.3705;)";
            request.AddRange(range);

            using (var response = await request.GetResponseAsync())
            {
                using (var responseStream = response.GetResponseStream())
                {
                    using (var fs = new FileStream(_destination, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                    {
                        item.Status = Statuses.Active;
                        while (_allowedToRun)
                        {
                            var buffer = new byte[_chunkSize];
                            var bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length);

                            if (bytesRead == 0) break;

                            await fs.WriteAsync(buffer, 0, bytesRead);
                            BytesWritten += bytesRead;
                            item.Bar = BytesWritten;
                            Application.Current.Dispatcher.Invoke(() => { });
                        }
                        if ((int)item.Bar >= _contentLength)
                        {
                            item.Status = Statuses.Completed;
                            active.Remove(item);
                            history.Add(item);
                            Application.Current.Dispatcher.Invoke(() => { });
                        }

                        await fs.FlushAsync();
                    }
                }
            }
        }

        public Task Start(ItemElement item, ObservableCollection<ItemElement> history, ObservableCollection<ItemElement> active)
        {
            _allowedToRun = true;
            return Start(item, history, active, BytesWritten);
        }

        public void Pause(ItemElement item)
        {
            _allowedToRun = false;
            item.Status = Statuses.Paused;
        }
    }
}
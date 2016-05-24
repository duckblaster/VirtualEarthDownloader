using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace Downloader {

    static class Program {

        #region Private Fields

        static readonly string filePrefix = "D:\\Projects\\MapData\\virtual-earth-birdseye\\";
        static readonly HashSet<int> imageDownloadIds = new HashSet<int>();
        static readonly List<string> imageDownloads = new List<string>();
        static readonly Dictionary<string, ImageState> imagesDownloading = new Dictionary<string, ImageState>();
        static readonly object processLock = new object();
        static readonly string urlPrefix = "http://ak.t0.tiles.virtualearth.net/tiles/o31311100030-";
        static readonly string urlSuffix = "?g=5197";
        static readonly WebClient webClient = new WebClient();
        static int downloadsCount;
        static int metadataDownloadsCount;
        static StreamWriter metadataListFile;

        #endregion Private Fields

        #region Private Methods

        static void Combine(string dir, int zoom) {
            var imageTime = new DateTime(2000, 1, 1);
            foreach (var image in Directory.EnumerateFiles(dir)) {
                var tmpTime = File.GetLastWriteTime(image);
                if (tmpTime > imageTime) {
                    imageTime = tmpTime;
                }
            }
            if (imageTime <= File.GetLastWriteTime($"{dir}.jpg")) {
                return;
            }
            var count = 1;
            switch (zoom) {
                case 2:
                    count = 2;
                    break;

                case 3:
                    count = 4;
                    break;

                case 19:
                    count = 8;
                    break;

                case 20:
                    count = 16;
                    break;

                default:
                    return;
            }
            Console.WriteLine(dir);
            lock (processLock) {
                using (var process = new System.Diagnostics.Process()) {
                    process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                    process.StartInfo.FileName = "stitch.bat";
                    process.StartInfo.Arguments = $"{dir.Replace(filePrefix, "")} {count}";
                    process.StartInfo.WorkingDirectory = filePrefix;
                    process.Start();
                    process.WaitForExit();
                }
            }
        }

        private static void Combine(int image, int zoom) {
            Combine($"{filePrefix}{zoom}\\{image}", zoom);
        }

        static void DownloadImage(int image, int zoom = 20) {
            var id = $"{image}{zoom}";
            lock (imageDownloads) {
                if (imageDownloads.Contains(id)) {
                    return;
                } else {
                    imageDownloads.Add(id);
                }
            }
            var numTiles = 1;
            switch (zoom) {
                case 1:
                    numTiles = 1;
                    break;

                case 2:
                    numTiles = 4;
                    break;

                case 3:
                    numTiles = 16;
                    break;

                case 19:
                    numTiles = 47;
                    break;

                case 20:
                    numTiles = 192;
                    break;

                default:
                    return;
            }
            var tileDownloads = new SortedSet<int>();
            for (var i = 0; i < numTiles; i++) {
                if (DownloadTile(image, zoom, i)) {
                    tileDownloads.Add(i);
                }
            }
            if (zoom > 1) {
                imagesDownloading.Add($"{image}-{zoom}", new ImageState(image, zoom, tileDownloads));
            }
        }

        static void DownloadImage(string image) {
            int imageNum;
            var zoom = 20;
            var parts = image.Split('-');
            int tmp;
            if (parts.Length == 2 && int.TryParse(parts[1], out tmp) && tmp > 0) {
                zoom = tmp;
            }
            if (int.TryParse(parts[0], out imageNum) && imageNum > 0) {
                DownloadImage(imageNum, zoom);
            }
        }

        static bool DownloadTile(int image, int zoom, int tile) {
            var seperator = zoom > 1 ? "\\tile-" : "-";
            var fileName = $"{filePrefix}{zoom}\\{image}{seperator}{tile:000}.jpg";
            var fileDir2 = zoom > 1 ? $"\\{image}" : "";
            var fileDir = $"{filePrefix}{zoom}{fileDir2}";
            if (!File.Exists(fileName) || (new FileInfo(fileName)).Length < 10) {
                var url = $"{urlPrefix}{image}-{zoom}-{tile}{urlSuffix}";
                if (!Directory.Exists(fileDir)) {
                    Directory.CreateDirectory(fileDir);
                }

                WaitForDownloads();
                downloadsCount++;
                var state = new TileState(image, zoom, tile);
                webClient.DownloadFileAsync(new Uri(url), fileName, state);
                return true;
            }
            return false;
        }

        static void GetViews() {
            /*
            minX = -41.30
            minY = 174.75
            maxX = -41.25
            maxY = 174.81
            */
            const double minX = -41.285;
            const double minY = 174.77;
            const double maxX = -41.26;
            const double maxY = 174.8;
            const double stepX = 0.005;
            const double stepY = 0.005;
            var metadataListFileName = $"{filePrefix}metadata\\list.txt";
            File.Delete(metadataListFileName);
            metadataListFile = File.AppendText(metadataListFileName);
            for (var x = minX; x <= maxX; x += stepX) {
                for (var y = minY; y <= maxY; y += stepY) {
                    for (var d = 90; d < 360; d += 45) {
                        var coords = $"{x},{y}";
                        var dir = d;
                        var url = $"http://dev.virtualearth.net/REST/V1/Imagery/Metadata/Birdseye/{coords}?dir={dir}&key=Anqg-XzYo-sBPlzOWFHIcjC3F8s17P_O7L4RrevsHVg4fJk6g_eEmUBphtSn4ySg&zl=20&dl=2";
                        var state = new MetadataState(coords, dir, url);
                        WaitForMetadataDownloads();
                        webClient.DownloadStringAsync(new Uri(url), state);
                        metadataDownloadsCount++;
                    }
                }
            }
            WaitForMetadataDownloads(true);
            metadataListFile.Dispose();
        }

        static void Main(string[] args) {
            ServicePointManager.DefaultConnectionLimit = 1000;

            webClient.DownloadStringCompleted += MetadataDownloaded;
            webClient.DownloadFileCompleted += TileDownloaded;

            GetViews();
            if (args.Length == 1 && args[0] == "getviews") {
                GetViews();
                return;
            }

            if (args.Length == 1 && File.Exists(args[0])) {
                args = File.ReadAllLines(args[0]);
            }
            foreach (var arg in args) {
                DownloadImage(arg);
            }

            WaitForDownloads(true);

            for (var i = 2; i <= 20; i++) {
                var zoomDir = $"{filePrefix}{i}";
                if (Directory.Exists(zoomDir)) {
                    foreach (var dir in Directory.EnumerateDirectories(zoomDir)) {
                        Combine(dir, i);
                    }
                }
            }
            Console.WriteLine("Done");
            Console.ReadKey();
        }

        private static void MetadataDownloaded(object sender, DownloadStringCompletedEventArgs e) {
            metadataDownloadsCount--;
            if (e.Cancelled || e.Error != null) {
                return;
            }
            var state = (MetadataState)e.UserState;
            var jsonText = e.Result;
            var json = JObject.Parse(jsonText);
            var resource = json["resourceSets"][0]["resources"][0];
            var imageUrl = (string)resource["imageUrl"];
            var match = Regex.Match(imageUrl, @"-([0-9]{1,6})-20-");
            var imageNum = int.Parse(match.Groups[1].Value);
            var save = false;
            lock (imageDownloadIds) {
                if (!imageDownloadIds.Contains(imageNum)) {
                    imageDownloadIds.Add(imageNum);
                    save = true;
                }
            }
            if (save) {
                var metadataFile = $"{filePrefix}metadata\\{state.dir},{imageNum},{state.coords}.json";
                if (!File.Exists(metadataFile)) {
                    File.WriteAllText(metadataFile, json.ToString());
                }
            }
            lock (metadataListFile) {
                metadataListFile.WriteLine($"{state.dir},{imageNum},{state.coords}");
            }
            Console.WriteLine($"{state.coords},{imageNum},{state.dir}");
            DownloadImage(imageNum, 20);
        }

        private static void TileDownloaded(object sender, System.ComponentModel.AsyncCompletedEventArgs e) {
            downloadsCount--;
            if (e.Cancelled || e.Error != null) {
                return;
            }
            var state = (TileState)e.UserState;
            var imageState = imagesDownloading[$"{state.image}-{state.zoom}"];
            imageState.tiles.Remove(state.tile);
            if (imageState.tiles.Count == 0) {
                Combine(imageState.image, imageState.zoom);
            }
        }

        static void WaitForDownloads(bool all = false) {
            while ((all && downloadsCount > 0) || downloadsCount > 100) {
                Thread.Sleep(10);
            }
        }

        static void WaitForMetadataDownloads(bool all = false) {
            while ((all && metadataDownloadsCount > 0) || metadataDownloadsCount > 100) {
                Thread.Sleep(10);
            }
        }

        #endregion Private Methods

        #region Public Classes

        public class ImageState {

            #region Public Fields

            public readonly int image;
            public readonly SortedSet<int> tiles;
            public readonly int zoom;

            #endregion Public Fields

            #region Public Constructors

            public ImageState(int image, int zoom, SortedSet<int> tiles) {
                this.image = image;
                this.zoom = zoom;
                this.tiles = tiles;
            }

            #endregion Public Constructors
        }

        public class MetadataState {

            #region Public Fields

            public readonly string coords;
            public readonly int dir;
            public readonly string url;

            #endregion Public Fields

            #region Public Constructors

            public MetadataState(string coords, int dir, string url) {
                this.coords = coords;
                this.dir = dir;
                this.url = url;
            }

            #endregion Public Constructors
        }

        public class TileState {

            #region Public Fields

            public readonly int image;
            public readonly int tile;
            public readonly int zoom;

            #endregion Public Fields

            #region Public Constructors

            public TileState(int image, int zoom, int tile) {
                this.image = image;
                this.zoom = zoom;
                this.tile = tile;
            }

            #endregion Public Constructors
        }

        #endregion Public Classes
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Linq;
using Newtonsoft.Json.Linq;
using DotSpatial.Projections;

namespace Downloader {

    static class Program {

        #region Private Fields

        static readonly string filePrefix = "D:\\Projects\\MapData\\virtual-earth-birdseye\\";
        static readonly HashSet<int> imageDownloadIds = new HashSet<int>();
        static readonly List<string> imageDownloads = new List<string>();
        static readonly ConcurrentQueue<string> imageQueue = new ConcurrentQueue<string>();
        static readonly Dictionary<string, ImageState> imagesDownloading = new Dictionary<string, ImageState>();
        static readonly ConcurrentQueue<string> stitchQueue = new ConcurrentQueue<string>();
        static readonly string urlPrefix = "http://ak.t0.tiles.virtualearth.net/tiles/o31311100030-";
        static readonly string urlSuffix = "?g=5197";
        static bool allDownloadsAdded;
        static bool allStitchingAdded;
        static bool closing;
        static int metadataDownloadsCount;
        static StreamWriter metadataListFile;
        static StreamWriter geolocationMetadataFile;
        static StreamWriter missingTilesList;
#pragma warning disable CC0052 // Make field readonly
        static int downloadQueueSleepTime = 10;
        static int downloadsCount;
        static int downloadSleepTime = 100;
        static int maxConcurrentDownloads = 100;
        static int maxConcurrentMetadataDownloads = 50;
        static int metadataSleepTime = 100;
        static int stitchQueueSleepTime = 10;
#pragma warning restore CC0052 // Make field readonly

        #endregion Private Fields

        #region Private Methods

        static bool CheckDir(string dir, int zoom) {
            var imageTime = new DateTime(2000, 1, 1);
            var imageCount = 0;
            var numTiles = NumTilesForZoom(zoom);
            if (!Directory.Exists(dir)) {
                return false;
            }
            foreach (var image in Directory.EnumerateFiles(dir)) {
                var fileInfo = new FileInfo(image);
                var tmpTime = fileInfo.LastWriteTime;
                var size = fileInfo.Length;
                if (size > 100) {
                    imageCount++;
                }
                if (tmpTime > imageTime) {
                    imageTime = tmpTime;
                }
            }
            if (numTiles == imageCount && (imageTime > File.GetLastWriteTime($"{dir}.jpg"))) {
                return true;
            }
            if (numTiles > imageCount) {
                missingTilesList.WriteLine($"{dir}");
            }
            return false;
        }

        static void Combine(string dir, int zoom) {
            if (!CheckDir(dir, zoom)) {
                return;
            }
            int count;
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
            stitchQueue.Enqueue($"{dir.Replace(filePrefix, "")} {count}");
        }

        static void DoClose(object sender, ConsoleCancelEventArgs e) {
            closing = true;
            e.Cancel = true;
            WaitForMetadataDownloads(true);
            WaitForDownloads(true);
        }

        static void DownloadImage(int image, int zoom = 20) {
            var numTiles = NumTilesForZoom(zoom);
            var tileDownloads = new bool[numTiles];
            var state = new ImageState(image, zoom, tileDownloads);
            if (zoom > 1) {
                imagesDownloading.Add($"{image}-{zoom}", state);
            }
            for (var i = 0; i < numTiles; i++) {
                if (DownloadTile(image, zoom, i)) {
                    tileDownloads[i] = true;
                    Interlocked.Increment(ref state.downloadCount);
                }
            }
            state.doneAdding = true;
            if (state.downloadCount > 0) {
                Combine($"{filePrefix}{zoom}\\{image}", zoom);
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
            var tmpFileName = $"{filePrefix}tmp\\{zoom}-{image}-{tile:000}.jpg";
            var fileDir2 = zoom > 1 ? $"\\{image}" : "";
            var fileDir = $"{filePrefix}{zoom}{fileDir2}";
            if (!File.Exists(fileName) || (new FileInfo(fileName)).Length < 10) {
                var url = $"{urlPrefix}{image}-{zoom}-{tile}{urlSuffix}";
                if (!Directory.Exists(fileDir)) {
                    Directory.CreateDirectory(fileDir);
                }

                WaitForDownloads();
                if (closing) { return false; }
                Interlocked.Increment(ref downloadsCount);
                var state = new TileState(image, zoom, tile, tmpFileName, fileName);
                using (var wc = new WebClient()) {
                    wc.DownloadFileCompleted += TileDownloaded;
                    wc.DownloadFileAsync(new Uri(url), tmpFileName, state);
                }
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
            var minX = -41.30;
            var minY = 174.76;
            var maxX = -41.245;
            var maxY = 174.81;

            const double stepX = 0.001;
            const double stepY = 0.001;

            if (minX > maxX) {
                var tmp = minX;
                minX = maxX;
                maxX = tmp;
            }
            if (minY > maxY) {
                var tmp = minY;
                minY = maxY;
                maxY = tmp;
            }
            var midX = (minX + maxX) / 2;
            var midY = (minY + maxY) / 2;

            var countX = (int)Math.Ceiling((maxX - minX) / stepX);
            var countY = (int)Math.Ceiling((maxY - minY) / stepY);

            var metadataListFileName = $"{filePrefix}metadata\\list.txt";
            if (File.Exists(metadataListFileName)) {
                var fileWriteTime = File.GetLastWriteTime(metadataListFileName);
                var oldFileName = metadataListFileName.Replace("list.txt", $"old\\List\\{fileWriteTime.ToString("yyyy-MM-dd HH-mm")}.txt");
                File.Move(metadataListFileName, oldFileName);
            }
            metadataListFile = File.AppendText(metadataListFileName);

            var x = 0;
            var y = 0;
            var dx = 0;
            var dy = -1;
            var t = Math.Max(countX, countY);
            var maxI = t * t;
            for (var i = 0; i < maxI; i++) {
                if ((-countX / 2 <= x) && (x <= countX / 2) && (-countY / 2 <= y) && (y <= countY / 2)) {
                    var xCoord = midX + stepX * x;
                    var yCoord = midY + stepY * y;
                    for (var d = 0; d < 360; d += 45) {
                        if (closing) { return; }
                        var coords = $"{xCoord},{yCoord}";
                        var dir = d;
                        var url = $"http://dev.virtualearth.net/REST/V1/Imagery/Metadata/Birdseye/{coords}?dir={dir}&key=Anqg-XzYo-sBPlzOWFHIcjC3F8s17P_O7L4RrevsHVg4fJk6g_eEmUBphtSn4ySg&zl=20&dl=2";
                        var state = new MetadataState(coords, dir, url);
                        WaitForMetadataDownloads();
                        metadataDownloadsCount++;
                        using (var wc = new WebClient()) {
                            wc.DownloadStringCompleted += MetadataDownloaded;
                            wc.DownloadStringAsync(new Uri(url), state);
                        }
                    }
                }
                if ((x == y) || ((x < 0) && (x == -y)) || ((x > 0) && (x == 1 - y))) {
                    t = dx;
                    dx = -dy;
                    dy = t;
                }
                x += dx;
                y += dy;
            }

            WaitForMetadataDownloads(true);
            metadataListFile.Dispose();
        }

        static void MakeGeolocation() {
            var metadata = Directory.EnumerateFiles($"{filePrefix}metadata\\old\\json", "*.json").Select(fileName => {
                var imageNum =  fileName.Split(',')[1];
                return new { image = imageNum, file = fileName };
            }).ToLookup(x => x.image, x => x.file);
            var geolocationFileName = $"{filePrefix}20.geo.txt";
            File.Delete(geolocationFileName);
            var geolocationFile = File.AppendText(geolocationFileName);
            foreach (var file in Directory.EnumerateFiles($"{filePrefix}20")) {
                var fileInfo = new FileInfo(file);
                var image = fileInfo.Name.Replace(".jpg", "");
                var metadataFileList = metadata[image];
                var metadataFile = $"{filePrefix}metadata\\json\\{image}.json";
                if (!File.Exists(metadataFile)) {
                    if(metadataFileList.Any()) {
                        metadataFile = metadataFileList.First();
                    } else {
                        continue;
                    }
                }
                try {
                    var contents = File.ReadAllText(metadataFile);
                    var json = JObject.Parse(contents);
                    var resource = json["resourceSets"][0]["resources"][0];
                    var bec = resource["bes"]["bec"];
                    var time = (DateTime)resource["bes"]["bei"]["pcd"];
                    Console.WriteLine(image);
                    fileInfo.CreationTime = time;
                    var x = (double)bec["qcx"];
                    var y = (double)bec["qcy"];
                    var z = (double)bec["qcz"];

                    var coords = new Coordinate(new Point3D {
                        X = x,
                        Y = y,
                        Z = z
                    });
                    var destDir = $"M:\\Projects\\Pix4D\\Wellington\\images\\";
                    var destName = $"{resource["orientation"]}_{time.ToString("yyyy-MM-dd_HH-mm")}_{image}.jpg";
                    var destFile = $"{destDir}{destName}";
                    if (!File.Exists(destFile)) {
                        Directory.CreateDirectory(destDir);
                        fileInfo.CopyTo(destFile);
                    }
                    new FileInfo(destFile).CreationTime = time;
                    geolocationFile.WriteLine($"{destName},{coords.Latitude},{coords.Longitude},{coords.Altitude}");
                } catch (Exception e) {
                    Console.WriteLine($"{image} Error: {e.Message}");
                    File.AppendAllText($"{filePrefix}20.errors.txt", $"{image} Error: {e.Message}\r\n{e}\r\n");
                }
            }
            geolocationFile.Dispose();
        }

        static void Main(string[] args) {
            Console.CancelKeyPress += DoClose;
            ServicePointManager.DefaultConnectionLimit = 10000;

            var geoMetadataFileName = $"{filePrefix}metadataGeo.txt";
            if(File.Exists(geoMetadataFileName)) {
                var fileWriteTime = File.GetLastWriteTime(geoMetadataFileName);
                var oldFileName = geoMetadataFileName.Replace("metadataGeo.txt", $"metadata\\old\\metadataGeo\\{fileWriteTime.ToString("yyyy-MM-dd HH-mm")}.txt");
                File.Move(geoMetadataFileName, oldFileName);
            }
            geolocationMetadataFile = File.AppendText(geoMetadataFileName);
            geolocationMetadataFile.WriteLine("image,lat,long,alt,time");

            try {
                File.Delete($"{filePrefix}missingTiles.txt");
            } catch (FileNotFoundException) {
                Console.WriteLine("missingTiles list already deleted");
            }
            missingTilesList = File.AppendText($"{filePrefix}missingTiles.txt");

            try {
                Directory.Delete($"{filePrefix}tmp\\", true);
            } catch(DirectoryNotFoundException) {
                Console.WriteLine("tmp already deleted");
            }
            Directory.CreateDirectory($"{filePrefix}tmp\\");

            var downloaderQueuer = new Thread(QueueDownloads);
            downloaderQueuer.Start();

            var stitchers = new List<Thread>();
            for (var i = 0; i < 4; i++) {
                var stitcher = new Thread(Stitch);
                stitcher.Start();
                stitchers.Add(stitcher);
            }

            new Thread(MergeAllChanged).Start();

            if (args.Length == 1 && args[0] == "getviews") {
                GetViews();
                return;
            }

            if (args.Length == 1 && File.Exists(args[0])) {
                args = File.ReadAllLines(args[0]);
            }
            foreach (var arg in args) {
                QueueImage(arg);
            }

            GetViews();

            Thread.Sleep(1000);

            allDownloadsAdded = true;

            WaitForDownloads(true);

            downloaderQueuer.Join();

            MergeAllChanged();

            allStitchingAdded = true;

            Thread.Sleep(1000);

            while (stitchers.Count > 0) {
                foreach (var stitcher in stitchers) {
                    if (stitcher.Join(100)) {
                        stitchers.Remove(stitcher);
                        break;
                    }
                }
            }

            MakeGeolocation();

            metadataListFile?.Dispose();
            missingTilesList.Dispose();

            Console.WriteLine("Done");
            Console.ReadKey();
        }

        static void MergeAllChanged() {
            for (var i = 2; i <= 20; i++) {
                var zoomDir = $"{filePrefix}{i}";
                if (Directory.Exists(zoomDir)) {
                    foreach (var dir in Directory.EnumerateDirectories(zoomDir)) {
                        Combine(dir, i);
                    }
                }
            }
        }

        static void MetadataDownloaded(object sender, DownloadStringCompletedEventArgs e) {
            Interlocked.Decrement(ref metadataDownloadsCount);
            if (e.Cancelled || e.Error != null) {
                return;
            }
            var state = (MetadataState)e.UserState;
            var jsonText = e.Result;
            var imageNum = 0;
            try {
                var json = JObject.Parse(jsonText);
                var resource = json["resourceSets"][0]["resources"][0];
                var imageUrl = (string)resource["imageUrl"];
                var match = Regex.Match(imageUrl, @"-([0-9]{1,6})-20-");
                imageNum = int.Parse(match.Groups[1].Value);
                var save = false;
                lock (imageDownloadIds) {
                    if (!imageDownloadIds.Contains(imageNum)) {
                        imageDownloadIds.Add(imageNum);
                        save = true;
                    }
                }
                if (save) {
                    var metadataFile = $"{filePrefix}metadata\\json\\{imageNum}.json";
                    if (!File.Exists(metadataFile)) {
                        File.WriteAllText(metadataFile, json.ToString());
                    }
                    var bec = resource["bes"]["bec"];
                    var time = (DateTime)resource["bes"]["bei"]["pcd"];
                    var alt = bec["ol"];
                    var lat = bec["olt"];
                    var lon = bec["olg"];
                    geolocationMetadataFile.WriteLine($"{imageNum}.jpg,{lat},{lon},{alt},{time}");
                    geolocationMetadataFile.Flush();
                }
            } catch (Exception ex) {
                Console.Write(ex);
            }
            lock (metadataListFile) {
                metadataListFile.WriteLine($"{imageNum},{state.dir},{state.coords}");
            }
            Console.WriteLine($"{state.coords},{imageNum},{state.dir}");
            QueueImage(imageNum, 20);
        }

        static int NumTilesForZoom(int zoom) {
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
            }
            return numTiles;
        }

        static void QueueDownloads() {
            string image = null;
            while (!closing && (imageQueue.TryPeek(out image) || downloadsCount > 0 || metadataDownloadsCount > 0 || !allDownloadsAdded)) {
                while (!closing && downloadsCount < maxConcurrentDownloads && stitchQueue.Count < 1 && imageQueue.TryDequeue(out image)) {
                    DownloadImage(image);
                }
                Thread.Sleep(downloadQueueSleepTime);
            }
        }

        static void QueueImage(string image) {
            if (!image.Contains("-")) {
                image = $"{image}-20";
            }
            lock (imageDownloads) {
                if (imageDownloads.Contains(image)) {
                    return;
                }
                imageDownloads.Add(image);
            }
            imageQueue.Enqueue(image);
        }

        static void QueueImage(int image, int zoom) {
            QueueImage($"{image}-{zoom}");
        }

        static void Stitch() {
            string command = null;
            while (!closing && (stitchQueue.TryPeek(out command) || downloadsCount > 0 || metadataDownloadsCount > 0 || !allDownloadsAdded || !allStitchingAdded)) {
                while (!closing && stitchQueue.TryDequeue(out command)) {
                    Console.WriteLine(command);
                    using (var process = new System.Diagnostics.Process()) {
                        process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                        process.StartInfo.FileName = "stitch.bat";
                        process.StartInfo.Arguments = command;
                        process.StartInfo.WorkingDirectory = filePrefix;
                        process.Start();
                        process.WaitForExit();
                    }
                }
                Thread.Sleep(stitchQueueSleepTime);
            }
        }

        static void TileDownloaded(object sender, System.ComponentModel.AsyncCompletedEventArgs e) {
            Interlocked.Decrement(ref downloadsCount);
            if (e.Cancelled) {
                return;
            }
            if (e.Error != null) {
                Console.Write("ERROR");
            }
            var state = (TileState)e.UserState;
            File.Move(state.tmpFileName, state.fileName);
            var imageState = imagesDownloading[$"{state.image}-{state.zoom}"];
            imageState.tiles[state.tile] = false;
            Interlocked.Decrement(ref imageState.downloadCount);
            if (imageState.doneAdding && imageState.downloadCount == 0) {
                Combine($"{filePrefix}{imageState.zoom}\\{imageState.image}", imageState.zoom);
            }
        }

        static void WaitForDownloads(bool all = false) {
            string tmp;
            while ((all && (downloadsCount > 0 || (!closing && imageQueue.TryPeek(out tmp)))) || downloadsCount > maxConcurrentDownloads) {
                Thread.Sleep(downloadSleepTime);
            }
        }

        static void WaitForMetadataDownloads(bool all = false) {
            while ((all && metadataDownloadsCount > 0) || metadataDownloadsCount > maxConcurrentMetadataDownloads || (imageQueue.Count > maxConcurrentDownloads && !closing)) {
                Thread.Sleep(metadataSleepTime);
            }
        }

        #endregion Private Methods

        #region Public Classes

        public class ImageState {

            #region Public Fields

            public readonly int image;
            public readonly bool[] tiles;
            public readonly int zoom;
            public int downloadCount;
            public bool doneAdding;

            #endregion Public Fields

            #region Public Constructors

            public ImageState(int image, int zoom, bool[] tiles) {
                this.image = image;
                this.zoom = zoom;
                this.tiles = tiles;
                downloadCount = 0;
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
            public readonly string tmpFileName;
            public readonly string fileName;

            #endregion Public Fields

            #region Public Constructors

            public TileState(int image, int zoom, int tile, string tmpFileName, string fileName) {
                this.image = image;
                this.zoom = zoom;
                this.tile = tile;
                this.tmpFileName = tmpFileName;
                this.fileName = fileName;
            }

            #endregion Public Constructors
        }

        #endregion Public Classes
    }
}

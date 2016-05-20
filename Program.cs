using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Downloader {
    class Program {
        static List<Task> downloads = new List<Task>();
        static string urlPrefix = "http://ak.t0.tiles.virtualearth.net/tiles/o31311100030-";
        static string urlSuffix = "?g=5197";
        static string filePrefix = "D:\\Projects\\MapData\\virtual-earth-birdseye\\";
        static Dictionary<int, List<string>> changedDirs = new Dictionary<int, List<string>>();

        static void DownloadTile(int image, int zoom, int tile) {
            var seperator = zoom > 1 ? "\\tile-" : "-";
            var fileName = $"{filePrefix}{zoom}\\{image}{seperator}{tile:000}.jpg";
            var fileDir2 = zoom>1?$"\\{image}" : "";
            var fileDir = $"{filePrefix}{zoom}{fileDir2}";
            if (!File.Exists(fileName) || (new FileInfo(fileName)).Length < 10) {
                var url = $"{urlPrefix}{image}-{zoom}-{tile}{urlSuffix}";
                //Console.WriteLine(url);
                if(!Directory.Exists(fileDir)) {
                    Directory.CreateDirectory(fileDir);
                }
                if(!changedDirs[zoom].Contains(fileDir)) {
                    changedDirs[zoom].Add(fileDir);
                }
                var wc = new WebClient();
                //wc.DownloadFile(url, fileName);
                downloads.Add(wc.DownloadFileTaskAsync(url, fileName).ContinueWith((x) => { Console.WriteLine(url);}));
            }
        }

        static void DownloadImage(int image, int zoom = 20) {
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
            for (var i = 0; i < numTiles; i++) {
                DownloadTile(image, zoom, i);
            }
        }

        static void DownloadImage(string image) {
            int imageNum;
            int zoom = 20;
            var parts = image.Split('-');
            int tmp;
            if (parts.Length == 2 && int.TryParse(parts[1], out tmp) && tmp > 0) {
                zoom = tmp;
            }
            if (int.TryParse(parts[0], out imageNum) && imageNum > 0) {
                DownloadImage(imageNum, zoom);
            }
        }

        static void Combine(string dir, int count) {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            process.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            process.StartInfo.FileName = "stitch.bat";
            process.StartInfo.Arguments = $"{dir.Replace(filePrefix, "")} {count}";
            process.StartInfo.WorkingDirectory = filePrefix;
            process.Start();
        }

        static void Main(string[] args) {
            for (var i = 1; i <= 20; i++) {
                changedDirs[i] = new List<string>();
            }

            var images = File.ReadAllLines($"{filePrefix}images.txt");
            if (args.Length == 1 && File.Exists(args[0])) {
                args = File.ReadAllLines(args[0]);
            }
            foreach (var arg in images) {
                DownloadImage(arg);
            }
            Task.WaitAll(downloads.ToArray());
            for(var i = 2; i <= 20; i++) {
                var count = 1;
                var zoomDir = $"{filePrefix}{i}";
                if (Directory.Exists(zoomDir)) {
                    switch (i) {
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
                    foreach (var dir in changedDirs[i]) {
                        Console.WriteLine(dir);
                        Combine(dir, count);
                    }
                }
            }
            Console.WriteLine("Done");
            //Console.ReadKey();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MediaServices.Client;

namespace AMS.BulkUpload
{
    internal class Program
    {
        private static readonly CloudMediaContext MediaContext = new CloudMediaContext(new MediaServicesCredentials(ConfigurationManager.AppSettings["MediaServiceName"], ConfigurationManager.AppSettings["MediaServiceKey"]));

        private static void Main(string[] args)
        {
            var watchPath = ConfigurationManager.AppSettings["WatchPath"];
            var log = new StringBuilder();

            var filePaths = Directory.EnumerateFiles(watchPath).ToList();

            if (!filePaths.Any())
            {
                throw new FileNotFoundException($"No files in {watchPath}");
            }

            Console.WriteLine($"Processing {filePaths.Count} file(s) in {watchPath}");

            var uploadTasks = new List<Task>();

            var blobTransferClient = new BlobTransferClient();

            blobTransferClient.TransferProgressChanged += BlobTransferClient_TransferProgressChanged;

            foreach (var filePath in filePaths)
            {
                var fileName = Path.GetFileName(filePath);

                var assets = MediaContext.Assets.ToList();

                if (assets.Any(a => a.Name == fileName))
                {
                    log.AppendLine($"Skipped {fileName} - Asset already exists");
                }
                else
                {
                    var asset = MediaContext.Assets.Create(fileName, AssetCreationOptions.None);
                    var assetFile = asset.AssetFiles.Create(fileName);
                    var accessPolicy = MediaContext.AccessPolicies.Create(fileName, TimeSpan.FromDays(3), AccessPermissions.List | AccessPermissions.Read | AccessPermissions.Write | AccessPermissions.Delete);
                    var locator = MediaContext.Locators.CreateLocator(LocatorType.Sas, asset, accessPolicy);
                    uploadTasks.Add(assetFile.UploadAsync(filePath, blobTransferClient, locator, CancellationToken.None));
                    log.AppendLine($"Created asset {asset.Name} (Id: {asset.Id})");
                }
            }

            if (uploadTasks.Count > 0)
            {
                Task.WaitAll(uploadTasks.ToArray());
                Console.WriteLine();
            }

            WriteLog(log.ToString());

            Console.WriteLine("Press any key to continue...");

            Console.ReadLine();
        }

        private static void BlobTransferClient_TransferProgressChanged(object sender, BlobTransferProgressChangedEventArgs e)
        {
            var percentComplete = Math.Round((decimal)e.BytesTransferred / e.TotalBytesToTransfer * 100, 0);
            var message = $"\rUploading - {percentComplete}% complete";
            Console.Write(message.PadRight(25, ' '));
        }

        private static void WriteLog(string content)
        {
            var fileName = $"{DateTime.Now:yyyy-MM-dd-HH-mm-ss}.log";
            var textWriter = new StreamWriter(fileName);
            textWriter.Write(content);
            textWriter.Close();
            Console.WriteLine($"Processing Complete - See {fileName} for details");
        }
    }
}
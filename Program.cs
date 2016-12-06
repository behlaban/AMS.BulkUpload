using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
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

            var filePaths = Directory.EnumerateFiles(watchPath).ToList();

            if (!filePaths.Any())
            {
                throw new FileNotFoundException($"No files in {watchPath}");
            }

            Console.WriteLine($"Start uploading {filePaths.Count} file(s) in {watchPath}");

            var uploadTasks = new List<Task>();

            var blobTransferClient = new BlobTransferClient();

            blobTransferClient.TransferProgressChanged += BlobTransferClient_TransferProgressChanged;

            foreach (var filePath in filePaths)
            {
                var fileName = Path.GetFileName(filePath);
                var asset = MediaContext.Assets.Create(fileName, AssetCreationOptions.None);
                var accessPolicy = MediaContext.AccessPolicies.Create(fileName, TimeSpan.FromDays(3), AccessPermissions.List | AccessPermissions.Read | AccessPermissions.Write | AccessPermissions.Delete);
                var locator = MediaContext.Locators.CreateLocator(LocatorType.Sas, asset, accessPolicy);
                var assetFile = asset.AssetFiles.Create(fileName);
                uploadTasks.Add(assetFile.UploadAsync(filePath, blobTransferClient, locator, CancellationToken.None));
            }

            Task.WaitAll(uploadTasks.ToArray());

            Console.WriteLine();

            Console.WriteLine($"Done uploading {filePaths.Count} file(s)");

            Console.ReadLine();
        }

        private static void BlobTransferClient_TransferProgressChanged(object sender, BlobTransferProgressChangedEventArgs e)
        {
            var percentComplete = Math.Round((decimal)e.BytesTransferred / e.TotalBytesToTransfer * 100, 0);
            var message = $"\r{percentComplete}% complete";
            Console.Write(message.PadRight(25, ' '));
        }
    }
}
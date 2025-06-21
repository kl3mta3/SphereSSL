using SphereSSLv2.Data;
using System.Diagnostics;

namespace SphereSSLv2.Model
{
    public class CertRecordServiceManager
    {
        internal DatabaseManager dbRecord;


        public async Task LoadCertRecordServiceBat(string orderId)
        {
            string batContent = dbRecord.GetRestartScriptById(orderId); // Get from DB
            string filePath = Path.Combine(AppContext.BaseDirectory, "Temp", "restart_script.bat");

            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!); // Ensure Temp exists
            await File.WriteAllTextAsync(filePath, batContent); // Async write
        }

        public async Task ExecuteCertRecordServiceBat(string batFilePath)
        {
            if (string.IsNullOrWhiteSpace(batFilePath))
                throw new ArgumentException("The path to the .bat file cannot be null or empty.", nameof(batFilePath));

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{batFilePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (!string.IsNullOrWhiteSpace(error))
                throw new Exception($"Error executing .bat file:\n{error}");


        }


        public async Task AutoRenewCertRecord(string orderId)
        {

            //get Order from cache if its there,  if not get from DB
            // CertRecord order = dbRecord.GetCertRecordById(orderId);







        }




        public async Task ManualRenewCertRecord(string orderId)
        {








        }
    }
}

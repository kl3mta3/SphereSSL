using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using Org.BouncyCastle.Asn1.Ocsp;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using SphereSSLv2.Data.Helpers;
using SphereSSLv2.Models.Dtos;
using SphereSSLv2.Models.UserModels;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;


namespace SphereSSLv2.Pages
{
    public class ExchangeModel : PageModel
    {
        private readonly ILogger<ExchangeModel> _logger;
        public UserSession CurrentUser = new();

        public ExchangeModel(ILogger<ExchangeModel> logger)
        {
            _logger = logger;

        }

        public async Task<IActionResult> OnGet()
        {
            var random = new Random();
            ViewData["TitleTag"] = SphereSSLTaglines.TaglineArray[random.Next(SphereSSLTaglines.TaglineArray.Length)];


            var sessionData = HttpContext.Session.GetString("UserSession");

            //if not logged in return
            if (sessionData == null)
            {
                return RedirectToPage("/Index");
            }

            CurrentUser = JsonConvert.DeserializeObject<UserSession>(sessionData);

            if (CurrentUser == null)
            {

                return RedirectToPage("/Index");
            }

            bool _isSuperAdmin = string.Equals(CurrentUser.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);


            return Page();
        }



        public async Task<IActionResult> OnPostPemConversionAsync([FromBody] KeyExchangeRequest key)
        {
            if (key == null || string.IsNullOrWhiteSpace(key.CertPem) || string.IsNullOrWhiteSpace(key.KeyFile))
                return BadRequest("Both certificate PEM and private key PEM are required.");

            try
            {
                // Load Certificate from PEM
                var certBytes = GetDerFromPem(key.CertPem, "CERTIFICATE");
                var cert = new X509Certificate2(certBytes);

                // Load Private Key from PEM
                var rsa = RSA.Create();
                rsa.ImportFromPem(key.KeyFile.ToCharArray());

                // If they want a .pfx (PKCS#12)
                if (key.OutputType == "pfx")
                {
                    var certWithKey = cert.CopyWithPrivateKey(rsa);
                    var password = key.Password ?? "";
                    var pfxBytes = string.IsNullOrWhiteSpace(password)
                        ? certWithKey.Export(X509ContentType.Pfx)
                        : certWithKey.Export(X509ContentType.Pfx, password);

                    return File(pfxBytes, "application/x-pkcs12", "certificate.pfx");
                }
                // If they want CRT/KEY (zip)
                else if (key.OutputType == "crtkey")
                {
                    var certCrt = ExportToPem(cert); // Helper for PEM format
                    var keyPem = ExportPrivateKeyToPem(rsa);

                    using var ms = new MemoryStream();
                    using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
                    {
                        var crtEntry = zip.CreateEntry("certificate.crt");
                        using (var crtStream = crtEntry.Open())
                            crtStream.Write(Encoding.UTF8.GetBytes(certCrt));

                        var keyEntry = zip.CreateEntry("private.key");
                        using (var keyStream = keyEntry.Open())
                            keyStream.Write(Encoding.UTF8.GetBytes(keyPem));
                    }

                    return File(ms.ToArray(), "application/zip", "cert_and_key.zip");
                }
                else
                {
                    return BadRequest("Invalid key exchange request.");
                }
            }
            catch (Exception ex)
            {
                return BadRequest("Failed to convert PEM: " + ex.Message);
            }
        }

        public async Task<IActionResult> OnPostPfxConversionAsync([FromBody] KeyExchangeRequest key)
        {
            // Validate input
            if (key == null || string.IsNullOrWhiteSpace(key.KeyFile))
                return BadRequest("PFX data is required.");

            try
            {
                // Parse PFX bytes
                byte[] pfxBytes;
                // If already bytes, just get them. Otherwise, if user pasted base64, decode:
                if (key.KeyFile.Trim().StartsWith("MII")) // DER base64 check
                    pfxBytes = Convert.FromBase64String(key.KeyFile.Trim());
                else
                    pfxBytes = Encoding.UTF8.GetBytes(key.KeyFile.Trim());

                var password = key.Password ?? "";

                // Load the certificate + private key from PFX
                var cert = new X509Certificate2(
                    pfxBytes, password,
                    X509KeyStorageFlags.Exportable
                );

                // Export CERT
                var certPem = ExportToPem(cert);

                // Export PRIVATE KEY
                var rsa = cert.GetRSAPrivateKey();
                var keyPem = rsa != null ? ExportPrivateKeyToPem(rsa) : "";

                if (key.OutputType == "pem")
                {
                    // Combined PEM output
                    var combinedPem = certPem + "\n" + keyPem;
                    return File(Encoding.UTF8.GetBytes(combinedPem), "application/x-pem-file", "certificate.pem");
                }
                else if (key.OutputType == "crtkey")
                {
                    // CRT (cert only) and KEY (private key) as ZIP
                    using (var ms = new MemoryStream())
                    {
                        using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
                        {
                            var crtEntry = zip.CreateEntry("certificate.crt");
                            using (var crtStream = crtEntry.Open())
                                crtStream.Write(Encoding.UTF8.GetBytes(certPem));

                            var keyEntry = zip.CreateEntry("private.key");
                            using (var keyStream = keyEntry.Open())
                                keyStream.Write(Encoding.UTF8.GetBytes(keyPem));
                        }
                        return File(ms.ToArray(), "application/zip", "cert_and_key.zip");
                    }
                }
                else
                {
                    return BadRequest("Invalid key exchange request.");
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Failed to convert PFX: {ex.Message}");
            }
        }

        public async Task<IActionResult> OnPostCrtKeyConversionAsync([FromBody] KeyExchangeRequest key)
        {
            // Validate input
            if (key == null)
                return BadRequest("Request is missing.");

            // Prioritize single-file mode (KeyFile), otherwise use CertPem/KeyPem
            string certPem = key.CertPem?.Trim() ?? "";
            string keyPem = key.KeyPem?.Trim() ?? "";
            string singleFile = key.KeyFile?.Trim() ?? "";
            string password = key.Password ?? "";

            try
            {
                if (!string.IsNullOrEmpty(singleFile))
                {
                    // The user pasted a full PEM (maybe PFX or PEM bundle)
                    if (key.OutputType == "pfx")
                    {
                        // Convert PEM to PFX
                        var reader = new StringReader(singleFile);
                        var pemReader = new PemReader(reader);
                        object obj = pemReader.ReadObject();

                        AsymmetricKeyParameter privKey = null;
                        X509Certificate cert = null;

                        // Scan for both
                        reader = new StringReader(singleFile);
                        pemReader = new PemReader(reader);
                        while ((obj = pemReader.ReadObject()) != null)
                        {
                            if (obj is AsymmetricCipherKeyPair keyPair)
                                privKey = keyPair.Private;
                            else if (obj is AsymmetricKeyParameter keyParam)
                                privKey = keyParam;
                            else if (obj is X509Certificate c)
                                cert = c;
                        }

                        if (privKey == null || cert == null)
                            return BadRequest("Could not parse both certificate and key from provided PEM.");

                        var store = new Pkcs12Store();
                        var certEntry = new X509CertificateEntry(cert);
                        store.SetKeyEntry("mykey", new AsymmetricKeyEntry(privKey), new[] { certEntry });

                        using var ms = new MemoryStream();
                        store.Save(ms, password.ToCharArray(), new SecureRandom());
                        return File(ms.ToArray(), "application/x-pkcs12", "certificate.pfx");
                    }
                    else if (key.OutputType == "pem")
                    {
                        // Return the pasted PEM as-is
                        return File(Encoding.UTF8.GetBytes(singleFile), "application/x-pem-file", "certificate.pem");
                    }
                    else
                    {
                        return BadRequest("Unsupported output type.");
                    }
                }
                else if (!string.IsNullOrEmpty(certPem) && !string.IsNullOrEmpty(keyPem))
                {
                    // Combine CRT and KEY PEM and convert as requested
                    if (key.OutputType == "pfx")
                    {
                        // Convert CRT/KEY PEM to PFX
                        X509Certificate cert;
                        AsymmetricKeyParameter privKey;

                        using (var certReader = new StringReader(certPem))
                        using (var keyReader = new StringReader(keyPem))
                        {
                            var pemCertReader = new PemReader(certReader);
                            cert = (X509Certificate)pemCertReader.ReadObject();

                            var pemKeyReader = new PemReader(keyReader);
                            object keyObj = pemKeyReader.ReadObject();
                            privKey = keyObj is AsymmetricCipherKeyPair pair ? pair.Private : (AsymmetricKeyParameter)keyObj;
                        }

                        var store = new Pkcs12Store();
                        var certEntry = new X509CertificateEntry(cert);
                        store.SetKeyEntry("mykey", new AsymmetricKeyEntry(privKey), new[] { certEntry });

                        using var ms = new MemoryStream();
                        store.Save(ms, password.ToCharArray(), new SecureRandom());
                        return File(ms.ToArray(), "application/x-pkcs12", "certificate.pfx");
                    }
                    else if (key.OutputType == "pem")
                    {
                        // Return combined PEM (cert + key)
                        var sb = new StringBuilder();
                        sb.AppendLine(certPem.Trim());
                        sb.AppendLine();
                        sb.AppendLine(keyPem.Trim());
                        return File(Encoding.UTF8.GetBytes(sb.ToString()), "application/x-pem-file", "certificate.pem");
                    }
                    else
                    {
                        return BadRequest("Unsupported output type.");
                    }
                }
                else
                {
                    return BadRequest("No valid certificate/key input provided.");
                }
            }
            catch (Exception ex)
            {
                return BadRequest("Conversion failed: " + ex.Message);
            }
        }

        private static string ExportToPem(X509Certificate2 cert)
        {
            var builder = new StringBuilder();
            builder.AppendLine("-----BEGIN CERTIFICATE-----");
            builder.AppendLine(Convert.ToBase64String(cert.RawData, Base64FormattingOptions.InsertLineBreaks));
            builder.AppendLine("-----END CERTIFICATE-----");
            return builder.ToString();
        }
        private static string ExportPrivateKeyToPem(RSA rsa)
        {
            var builder = new StringBuilder();
            var keyParams = rsa.ExportPkcs8PrivateKey();
            builder.AppendLine("-----BEGIN PRIVATE KEY-----");
            builder.AppendLine(Convert.ToBase64String(keyParams, Base64FormattingOptions.InsertLineBreaks));
            builder.AppendLine("-----END PRIVATE KEY-----");
            return builder.ToString();
        }
        private static byte[] GetDerFromPem(string pem, string type)
        {
            var header = $"-----BEGIN {type}-----";
            var footer = $"-----END {type}-----";
            var start = pem.IndexOf(header, StringComparison.Ordinal);
            var end = pem.IndexOf(footer, StringComparison.Ordinal);
            if (start < 0 || end < 0) throw new InvalidOperationException("PEM section not found.");
            var base64 = pem.Substring(start + header.Length, end - (start + header.Length)).Replace("\r", "").Replace("\n", "");
            return Convert.FromBase64String(base64);
        }
    }
}

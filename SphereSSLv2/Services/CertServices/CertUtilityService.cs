using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;
using System.Text;

namespace SphereSSLv2.Services.CertServices
{
    public class CertUtilityService
    {

        /// <summary>
        /// Creates a local self-signed certificate and saves it as a .pfx file.
        /// </summary>
        /// <param name="subjectName">CN for the cert, e.g. "CN=yourdomain.com"</param>
        /// <param name="outputPath">Full file path for output, e.g. "C:\certs\mycert.pfx"</param>
        /// <param name="password">Password for the PFX file (can be null or empty for no password)</param>
        /// <param name="validDays">How many days until the cert expires</param>
        public static byte[] CreateSelfSignedCert( string subjectName, IEnumerable<string> sanNames, string password = "", int validDays = 365)
        {
            using var rsa = RSA.Create(2048);
            var certReq = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            string outputPath= Path.Combine(AppContext.BaseDirectory, "temp", $"{subjectName}.pfx");
            // Add extensions (basic constraints, key usage, etc)
            certReq.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, false));
            certReq.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
            certReq.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(certReq.PublicKey, false));

            // Subject Alternative Name (SAN)
            certReq.CertificateExtensions.Add(
                new X509Extension(
                    new AsnEncodedData(
                        new Oid("2.5.29.17"),
                        BuildSubjectAltName(sanNames)
                    ),
                    false
                )
            );

            DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddMinutes(-5);
            DateTimeOffset notAfter = notBefore.AddDays(validDays);

            using var cert = certReq.CreateSelfSigned(notBefore, notAfter);

            // Export as PFX (with private key)
            byte[] pfxBytes = string.IsNullOrEmpty(password)
                ? cert.Export(X509ContentType.Pfx)
                : cert.Export(X509ContentType.Pfx, password);

            File.WriteAllBytes(outputPath, pfxBytes);

            return pfxBytes;
        }

        private static byte[] BuildSubjectAltName(IEnumerable<string> sanNames)
        {
            // Builds a SAN extension with all DNS names
            // ASN.1 encoding: SEQUENCE of [2] (dNSName)
            List<byte> raw = new List<byte>();
            foreach (var name in sanNames)
            {
                var dns = name.Trim();
                var dnsBytes = Encoding.ASCII.GetBytes(dns);
                raw.Add(0x82); // dNSName tag
                raw.Add((byte)dnsBytes.Length);
                raw.AddRange(dnsBytes);
            }

            var san = new List<byte>();
            san.Add(0x30); // SEQUENCE
            san.Add((byte)raw.Count);
            san.AddRange(raw);

            return san.ToArray();
        }

    }
}

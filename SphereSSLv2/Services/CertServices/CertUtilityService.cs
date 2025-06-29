using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography;

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
        public static byte[] CreateSelfSignedCert(string subjectName, string outputPath, string password = "", int validDays = 365)
        {
            using var rsa = RSA.Create(2048);
            var certReq = new CertificateRequest(subjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            // Add extensions (basic constraints, key usage, etc)
            certReq.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(false, false, 0, false));
            certReq.CertificateExtensions.Add(
                new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
            certReq.CertificateExtensions.Add(
                new X509SubjectKeyIdentifierExtension(certReq.PublicKey, false));

            // Subject Alternative Name (SAN) (for browsers to trust the cert in modern times)
            certReq.CertificateExtensions.Add(
                new X509Extension(
                    new AsnEncodedData(
                        new Oid("2.5.29.17"),
                        BuildSubjectAltName(subjectName)
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


        private static byte[] BuildSubjectAltName(string subjectName)
        {
            // Extract DNS name from "CN=domain.com"
            string dns = subjectName.Replace("CN=", "").Trim();
            // SAN format: 0x30 (SEQUENCE), length, 0x82 (dNSName), length, bytes of name
            byte[] dnsBytes = System.Text.Encoding.ASCII.GetBytes(dns);
            byte[] san = new byte[4 + dnsBytes.Length];
            san[0] = 0x30; // SEQUENCE
            san[1] = (byte)(2 + dnsBytes.Length);
            san[2] = 0x82; // dNSName
            san[3] = (byte)dnsBytes.Length;
            Array.Copy(dnsBytes, 0, san, 4, dnsBytes.Length);
            return san;
        }

    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DaveMusicAlexa
{
    public static class PemHelper
    {
        static string CertHeader = "-----BEGIN CERTIFICATE-----";
        static string CertFooter = "-----END CERTIFICATE-----";

        static HttpClient _client = new HttpClient();


        public static IEnumerable<string> ParseSujectAlternativeNames(X509Certificate2 cert)
        {
            Regex sanRex = new Regex(@"^DNS Name=(.*)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

            var sanList = from X509Extension ext in cert.Extensions
                          where ext.Oid.FriendlyName.Equals("Subject Alternative Name", StringComparison.Ordinal)
                          let data = new AsnEncodedData(ext.Oid, ext.RawData)
                          let text = data.Format(true)
                          from line in text.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                          let match = sanRex.Match(line)
                          where match.Success && match.Groups.Count > 0 && !string.IsNullOrEmpty(match.Groups[1].Value)
                          select match.Groups[1].Value;

            return sanList;
        }


        public static bool ValidateCertificateChain(X509Certificate2 certificate, IEnumerable<X509Certificate2> chain)
        {
            using (var verifier = new X509Chain())
            {
                verifier.ChainPolicy.ExtraStore.AddRange(chain.ToArray());
                var result = verifier.Build(certificate);
                return result;
            }
        }

        public static X509Certificate2 ParseCertificate(string base64CertificateText)
        {
            var bytes = Convert.FromBase64String(base64CertificateText);
            X509Certificate2 cert = new X509Certificate2(bytes);
            return cert;
        }

        public static async Task<X509Certificate2[]> DownloadPemCertificatesAsync(string pemUri)
        {
            var pemText = await _client.GetStringAsync(pemUri);
            if (string.IsNullOrEmpty(pemText)) return null;
            return ReadPemCertificates(pemText);
        }


        public static X509Certificate2[] ReadPemCertificates(string pemString)
        {
            var lines = pemString.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> certList = new List<string>();
            StringBuilder grouper = null;
            for (int i = 0; i < lines.Length; i++)
            {
                var curLine = lines[i];
                if (curLine.Equals(CertHeader, StringComparison.Ordinal))
                {
                    grouper = new StringBuilder();
                }
                else if (curLine.Equals(CertFooter, StringComparison.Ordinal))
                {
                    certList.Add(grouper.ToString());
                    grouper = null;
                }
                else
                {
                    if (grouper != null)
                    {
                        grouper.Append(curLine);
                    }
                }
            }

            List<X509Certificate2> collection = new List<X509Certificate2>();

            foreach (var certText in certList)
            {
                var cert = ParseCertificate(certText);
                collection.Add(cert);
            }

            return collection.ToArray();
        }
    }

    
}

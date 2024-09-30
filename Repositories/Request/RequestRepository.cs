using System;
using System.Security.Cryptography;
using System.Text;

namespace Expense.API.Repositories.Request
{
	public class RequestRepository:IRequestRepository
	{
		public RequestRepository()
		{
		}
        private readonly string secretKey = "CLNWdt98ejGk7H5Wkxue2q/cI3b41e+rxVtPhCoVP90="; //same as in frontend

        public string DecryptData(string data)
        {
            // Ensure that the input data is properly formatted
            if (string.IsNullOrEmpty(data) || !data.Contains(":"))
            {
                throw new ArgumentException("Invalid input data for decryption.");
            }

            // Split the input data into IV and ciphertext
            var parts = data.Split(':');
            if (parts.Length != 2)
            {
                throw new ArgumentException("Invalid input format for decryption.");
            }

            var iv = Convert.FromBase64String(parts[0]); // Extract and decode the IV
            var ciphertext = parts[1]; // Get the actual ciphertext

            using (var aes = Aes.Create())
            {
                aes.Key = Convert.FromBase64String(secretKey); // Decode the Base64 key
                aes.IV = iv; // Set the extracted IV
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
                using (var msDecrypt = new MemoryStream(Convert.FromBase64String(ciphertext)))
                {
                    using (var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    using (var reader = new StreamReader(csDecrypt))
                    {
                        return reader.ReadToEnd(); // Return the decrypted data
                    }
                }
            }
        }

    }
}


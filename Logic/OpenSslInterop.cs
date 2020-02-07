using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using System;
using System.Text;

namespace XiboClient.Logic
{
    public class OpenSslInterop
    {
        public static string decrypt(string cypherText, string cypherKey, AsymmetricKeyParameter privateKey)
        {
            // Create a RSA cypher
            IBufferedCipher rsaCipher = CipherUtilities.GetCipher("RSA//PKCS1PADDING");
            rsaCipher.Init(false, privateKey);

            // Get the decrypted key
            byte[] key = rsaCipher.DoFinal(Convert.FromBase64String(cypherKey));

            // Use this Key to decrypt the main message
            IBufferedCipher rsaKeyCipher = CipherUtilities.GetCipher("RC4");
            KeyParameter parameter = new KeyParameter(key);
            rsaKeyCipher.Init(false, parameter);

            byte[] opened = rsaKeyCipher.DoFinal(Convert.FromBase64String(cypherText));

            return Encoding.ASCII.GetString(opened);
        }
    }
}

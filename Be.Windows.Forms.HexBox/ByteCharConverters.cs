using System.Text;

namespace Be.Windows.Forms
{
    /// <summary>
    /// The interface for objects that can translate between characters and bytes.
    /// </summary>
    public interface IByteCharConverter
    {
        /// <summary>
        /// Returns the character to display for the byte passed across.
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        char ToChar(byte b);

        /// <summary>
        /// Returns the character to display for the byte array passed across.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        string ToString(byte[] data);

        /// <summary>
        /// Returns the byte to use when the character passed across is entered during editing.
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        byte ToByte(char c);
    }

    /// <summary>
    /// The default <see cref="IByteCharConverter"/> implementation.
    /// </summary>
    public class DefaultByteCharConverter : IByteCharConverter
    {
        /// <summary>
        /// Returns the character to display for the byte passed across.
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public virtual char ToChar(byte b) => b > 0x1F && !(b > 0x7E && b < 0xA0) ? (char)b : '.';

        /// <summary>
        /// See <see cref="IByteCharConverter.ToString" /> for more information.
        /// </summary>
        public virtual string ToString(byte[] data)
        {
            string result = "";
            for (int idx = 0; idx < data.Length; idx++) result += ToChar(data[idx]);
            return result;
        }

        /// <summary>
        /// Returns the byte to use for the character passed across.
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public virtual byte ToByte(char c) => (byte)c;

        /// <summary>
        /// Returns a description of the byte char provider.
        /// </summary>
        /// <returns></returns>
        public override string ToString() => "ANSI (Default)";
    }

    /// <summary>
    /// A byte char provider that can translate bytes encoded in codepage 500 EBCDIC
    /// </summary>
    public class EbcdicByteCharProvider : IByteCharConverter
    {
        /// <summary>
        /// The IBM EBCDIC code page 500 encoding. Note that this is not always supported by .NET,
        /// the underlying platform has to provide support for it.
        /// </summary>
        private Encoding _ebcdicEncoding = Encoding.GetEncoding(500);

        /// <summary>
        /// Returns the EBCDIC character corresponding to the byte passed across.
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public virtual char ToChar(byte b)
        {
            string encoded = _ebcdicEncoding.GetString(new byte[] { b });
            return encoded.Length > 0 ? encoded[0] : '.';
        }

        /// <summary>
        /// See <see cref="IByteCharConverter.ToString" /> for more information.
        /// </summary>
        public virtual string ToString(byte[] data)
        {
            string encoded = _ebcdicEncoding.GetString(data);
            if (encoded.Length == 0) for (int i = 0; i < data.Length; i++) encoded += ".";
            return encoded;
        }

        /// <summary>
        /// Returns the byte corresponding to the EBCDIC character passed across.
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public virtual byte ToByte(char c)
        {
            byte[] decoded = _ebcdicEncoding.GetBytes(new char[] { c });
            return decoded.Length > 0 ? decoded[0] : (byte)0;
        }

        /// <summary>
        /// Returns a description of the byte char provider.
        /// </summary>
        /// <returns></returns>
        public override string ToString() => "EBCDIC (Code Page 500)";
    }
}

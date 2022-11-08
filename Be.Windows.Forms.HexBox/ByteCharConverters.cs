using System.Text;

namespace Be.Windows.Forms
{
    /// <summary>
    /// The interface for objects that can translate between characters and bytes.
    /// </summary>
    public interface IByteCharConverter
    {
        /// <summary>
        /// Returns a character encoding.
        /// </summary>
        /// <returns>Encoding</returns>
        Encoding getEncoding();

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
        /// <param name="align">Determines whether to format the string to generate blanks for EncodingByteCharProvider</param>
        /// <returns></returns>
        string ToString(byte[] data, bool align = false);

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
        public virtual string ToString(byte[] data, bool align = false)
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
        /// See <see cref="IByteCharConverter.getEncoding" /> for more information.
        /// </summary>
        public Encoding getEncoding() => null;

        /// <summary>
        /// Returns a description of the byte char provider.
        /// </summary>
        /// <returns></returns>
        public override string ToString() => "ANSI (Default)";
    }

    /// <summary>
    /// A byte char provider that can translate bytes encoded by codepage(default codepage 500 EBCDIC)
    /// </summary>
    public class EncodingByteCharProvider : IByteCharConverter
    {
        /// <summary>
        /// Default code page is IBM EBCDIC 500 encoding. Note that this is not always supported by .NET,
        /// the underlying platform has to provide support for it.
        /// </summary>
        private Encoding _encoding;

        /// <summary>
        /// The encoding of EncodingByteCharProvider is determined by codepage
        /// </summary>
        /// <param name="codepage">default code page is 500 encoding.</param>
        public EncodingByteCharProvider(int codepage = 500) => _encoding = _encoding = Encoding.GetEncoding(codepage);

        /// <summary>
        /// Returns the Encoding character corresponding to the byte passed across.
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public virtual char ToChar(byte b)
        {
            string encoded = _encoding.GetString(new byte[] { b });
            return encoded.Length > 0 ? encoded[0] : '.';
        }

        /// <summary>
        /// See <see cref="IByteCharConverter.ToString" /> for more information.
        /// </summary>
        public virtual string ToString(byte[] data, bool align = false)
        {
            string encoded = "";
            var chars = _encoding.GetChars(data);
            foreach (char c in chars)
            {
                if (c.ToString().Length == 0) encoded += ".";
                else if (c == '\0') encoded += " ";
                else
                {
                    encoded += c;
                    if (!align || c == '�') continue;
                    var byteCount = _encoding.GetByteCount(c.ToString());
                    if (byteCount > 1) for (int i = 1; i < byteCount; i++) encoded += " ";
                }
            }
            if (align && encoded.Length < data.Length) for (int i = encoded.Length; i <= data.Length; i++) encoded += " ";

            return encoded;
        }

        /// <summary>
        /// Returns the byte corresponding to the Encoding character passed across.
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        public virtual byte ToByte(char c)
        {
            byte[] decoded = _encoding.GetBytes(new char[] { c });
            return decoded.Length > 0 ? decoded[0] : (byte)0;
        }

        /// <summary>
        /// See <see cref="IByteCharConverter.getEncoding" /> for more information.
        /// </summary>
        public Encoding getEncoding() => _encoding;

        /// <summary>
        /// Returns a description of the byte char provider.
        /// </summary>
        /// <returns></returns>
        public override string ToString() => string.Format("{0} (Code Page {1})", _encoding.EncodingName, _encoding.CodePage);
    }
}

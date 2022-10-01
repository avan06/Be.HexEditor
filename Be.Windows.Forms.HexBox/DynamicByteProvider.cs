using System;
using System.Collections.Generic;

namespace Be.Windows.Forms
{
    /// <summary>
    /// Byte provider for a small amount of data.
    /// </summary>
    public class DynamicByteProvider : IByteProvider
    {
        /// <summary>
        /// Contains information about changes.
        /// </summary>
        bool _hasChanges;

        /// <summary>
        /// Contains a byte collection.
        /// </summary>
        List<byte> _bytes;

        /// <summary>
        /// HashSet containing the position of the changed byte value.
        /// </summary>
        HashSet<long> _changedPosSet;

        /// <summary>
        /// Initializes a new instance of the DynamicByteProvider class.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="changedPosSet">HashSet containing the position of the changed byte value</param>
        public DynamicByteProvider(byte[] data, HashSet<long> changedPosSet = null) : this(new List<Byte>(data), changedPosSet) { }

        /// <summary>
        /// Initializes a new instance of the DynamicByteProvider class.
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="changedPosSet">HashSet containing the position of the changed byte value</param>
        public DynamicByteProvider(List<Byte> bytes, HashSet<long> changedPosSet = null)
        {
            _bytes = bytes;
            _changedPosSet = changedPosSet;
        }

        /// <summary>
        /// Raises the Changed event.
        /// </summary>
        void OnChanged(EventArgs e)
        {
            _hasChanges = true;

            if(Changed != null) Changed(this, e);
        }

        /// <summary>
        /// Raises the LengthChanged event.
        /// </summary>
        void OnLengthChanged(EventArgs e)
        {
            if(LengthChanged != null) LengthChanged(this, e);
        }

        /// <summary>
        /// Gets the byte collection.
        /// </summary>
        public List<Byte> Bytes => _bytes;

        /// <summary>
        /// Gets the position of the changed byte HashSet collection.
        /// </summary>
        public HashSet<long> ChangedPosSet => _changedPosSet;
        

        #region IByteProvider Members
        /// <summary>
        /// True, when changes are done.
        /// </summary>
        public bool HasChanges() => _hasChanges;

        /// <summary>
        /// Applies changes.
        /// </summary>
        public void ApplyChanges() => _hasChanges = false;

        /// <summary>
        /// Occurs, when the write buffer contains new changes.
        /// </summary>
        public event EventHandler Changed;

        /// <summary>
        /// Occurs, when InsertBytes or DeleteBytes method is called.
        /// </summary>
        public event EventHandler LengthChanged;

        /// <summary>
        /// Reads a byte from the byte collection.
        /// </summary>
        /// <param name="index">the index of the byte to read</param>
        /// <returns>the byte</returns>
        public byte ReadByte(long index) => _bytes[(int)index];

        /// <summary>
        /// See <see cref="IByteProvider.ReadBytes" /> for more information.
        /// </summary>
        public byte[] ReadBytes(long index, int length)
        {
            byte[] buffer = new byte[length];
            for (int idx = 0; idx < length; idx++)
            {
                if (index + idx > this.Length - 1)
                {
                    Array.Resize(ref buffer, idx);
                    break;
                }
                buffer[idx] = _bytes[(int)index + idx];
            }

            return buffer;
        }

        /// <summary>
        /// Write a byte into the byte collection.
        /// </summary>
        /// <param name="index">the index of the byte to write.</param>
        /// <param name="value">the byte</param>
        public void WriteByte(long index, byte value)
        {
            _bytes[(int)index] = value;
            OnChanged(EventArgs.Empty);
        }

        /// <summary>
        /// See <see cref="IByteProvider.InsertBytes" /> for more information.
        /// </summary>
        public void WriteBytes(long index, byte[] values)
        {
            for (int idx = 0; idx < values.Length; idx++) _bytes[(int)index + idx] = values[idx];
            OnChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Deletes bytes from the byte collection.
        /// </summary>
        /// <param name="index">the start index of the bytes to delete.</param>
        /// <param name="length">the length of bytes to delete.</param>
        public void DeleteBytes(long index, long length)
        { 
            int internal_index = (int)Math.Max(0, index);
            int internal_length = (int)Math.Min((int)Length, length);
            _bytes.RemoveRange(internal_index, internal_length); 

            OnLengthChanged(EventArgs.Empty);
            OnChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Inserts byte into the byte collection.
        /// </summary>
        /// <param name="index">the start index of the bytes in the byte collection</param>
        /// <param name="bs">the byte array to insert</param>
        public void InsertBytes(long index, byte[] bs)
        { 
            _bytes.InsertRange((int)index, bs); 

            OnLengthChanged(EventArgs.Empty);
            OnChanged(EventArgs.Empty);
        }

        /// <summary>
        /// Gets the length of the bytes in the byte collection.
        /// </summary>
        public long Length => _bytes.Count;

        /// <summary>
        /// Returns true
        /// </summary>
        public bool SupportsWriteByte() => true;

        /// <summary>
        /// Returns true
        /// </summary>
        public bool SupportsInsertBytes() => true;

        /// <summary>
        /// Returns true
        /// </summary>
        public bool SupportsDeleteBytes() => true;
        #endregion
    }
}
